using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ValinorInterApiProxy.Api.Authorization;
using ValinorInterApiProxy.Api.Contracts;
using ValinorInterApiProxy.Api.Endpoints;
using ValinorInterApiProxy.Api.Middleware;
using ValinorInterApiProxy.Core.Ports;
using ValinorInterApiProxy.Core.UseCases;
using ValinorInterApiProxy.Infrastructure.Auth;
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

// Self-issued JWT (constitution Principle II, amended 1.1.0): the proxy is its own identity
// provider for registered consumers — no external OIDC Authority dependency.
builder.Services.AddOptions<JwtIssuerOptions>()
    .Bind(builder.Configuration.GetSection(JwtIssuerOptions.SectionName))
    .Validate(o => !string.IsNullOrWhiteSpace(o.Issuer), "Jwt:Issuer is required.")
    .Validate(o => !string.IsNullOrWhiteSpace(o.Audience), "Jwt:Audience is required.")
    .Validate(
        o => TryDecodeSigningKey(o.SigningKey, out var bytes) && bytes.Length >= 32,
        "Jwt:SigningKey must be base64-encoded and decode to at least 32 bytes (256 bits).")
    .ValidateOnStart();
builder.Services.AddOptions<AuthConsumersOptions>()
    .Bind(builder.Configuration.GetSection(JwtIssuerOptions.SectionName))
    .Validate(o => o.Consumers.Count > 0, "At least one Jwt:Consumers entry must be configured.")
    .ValidateOnStart();

builder.Services.AddSingleton<IClientCredentialStore, AuthConsumerCredentialStore>();
builder.Services.AddSingleton<IJwtIssuer, SymmetricJwtIssuer>();
builder.Services.AddTransient<IssueAuthTokenUseCase>();

// JWT bearer authentication (constitution Principle II; FR-001): validates signature, issuer,
// audience, and expiry against the self-issued token's own signing key.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtIssuerOptions>>((bearerOptions, jwtOptions) =>
    {
        var settings = jwtOptions.Value;

        // Preserve "sub"/"scope" claim names exactly as issued — JwtSecurityTokenHandler's
        // default inbound claim mapping would otherwise silently remap "sub" to a long legacy
        // URI, breaking ScopeAuthorizationHandler and CorrelationLoggingMiddleware.
        bearerOptions.MapInboundClaims = false;
        bearerOptions.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = settings.Issuer,
            ValidateAudience = true,
            ValidAudience = settings.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(settings.SigningKey)),
        };
        bearerOptions.Events = new JwtBearerEvents
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
app.MapAuthTokenEndpoint();

app.Run();

static bool TryDecodeSigningKey(string? signingKey, out byte[] bytes)
{
    bytes = [];
    if (string.IsNullOrWhiteSpace(signingKey))
    {
        return false;
    }

    try
    {
        bytes = Convert.FromBase64String(signingKey);
        return true;
    }
    catch (FormatException)
    {
        return false;
    }
}

public partial class Program;
