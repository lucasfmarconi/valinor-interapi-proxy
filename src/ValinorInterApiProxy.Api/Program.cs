using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ValinorInterApiProxy.Api.Authorization;
using ValinorInterApiProxy.Api.Contracts;
using ValinorInterApiProxy.Api.Endpoints;
using ValinorInterApiProxy.Api.Middleware;
using ValinorInterApiProxy.Core.Ports;
using ValinorInterApiProxy.Core.UseCases;
using ValinorInterApiProxy.Infrastructure.Caching;
using ValinorInterApiProxy.Infrastructure.InterApi;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Inter MTLS client (constitution Principle I: only this typed client holds the Inter cert).
builder.Services.Configure<InterOptions>(builder.Configuration.GetSection(InterOptions.SectionName));
builder.Services.AddInterHttpClient();
builder.Services.AddTransient<IInterTokenClient>(sp => sp.GetRequiredService<InterHttpClient>());
builder.Services.AddTransient<IInterStatementClient>(sp => sp.GetRequiredService<InterHttpClient>());

// Extrato capability (US1): internally-managed, cached Inter access token (FR-005) and the use
// case that validates the requested period before calling Inter.
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IInterAccessTokenProvider, MemoryCacheInterAccessTokenProvider>();
var extratoMaxPeriodDays = builder.Configuration.GetValue("Extrato:MaxPeriodDays", 90);
builder.Services.AddTransient(sp => new GetStatementUseCase(
    sp.GetRequiredService<IInterAccessTokenProvider>(),
    sp.GetRequiredService<IInterStatementClient>(),
    extratoMaxPeriodDays));

// Token capability (US2): always issues a fresh Inter token (FR-004), no caching.
builder.Services.AddTransient<IssueTokenUseCase>();

// JWT bearer authentication (constitution Principle II; FR-001): validates signature, issuer,
// audience, and expiry using the configured identity provider's metadata.
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Jwt:Authority"];
        options.Audience = builder.Configuration["Jwt:Audience"];
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
        };
        options.Events = new JwtBearerEvents
        {
            OnChallenge = context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                var body = ErrorResponseFactory.Create(context.HttpContext, "Missing, invalid, or expired token.");
                return context.Response.WriteAsJsonAsync(body);
            },
            OnForbidden = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                var body = ErrorResponseFactory.Create(
                    context.HttpContext,
                    "Token is missing the required scope for this operation.");
                return context.Response.WriteAsJsonAsync(body);
            },
        };
    });

// Two distinct, least-privilege authorization policies (constitution Principle II; FR-002/FR-003).
builder.Services.AddScopePolicies();
builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCorrelationLogging();

app.UseAuthentication();
app.UseAuthorization();

app.MapExtratoEndpoint();
app.MapTokenEndpoint();

app.Run();

public partial class Program;
