using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ValinorInterApiProxy.Core.Ports;

namespace ValinorInterApiProxy.Api.Tests.Infrastructure;

/// <summary>
/// A <see cref="WebApplicationFactory{TEntryPoint}"/> for <c>Program</c> that swaps real JWT
/// bearer authentication for <see cref="TestAuthHandler"/> and replaces the Infrastructure-layer
/// Inter ports with in-memory fakes, so Api-level tests exercise the real endpoint/authorization
/// pipeline without MTLS, a real identity provider, or a live Banco Inter dependency.
/// </summary>
public sealed class ProxyWebApplicationFactory : WebApplicationFactory<Program>
{
    public FakeInterAccessTokenProvider AccessTokenProvider { get; } = new();

    public FakeInterStatementClient StatementClient { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services
                .AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            services.RemoveAll<IInterAccessTokenProvider>();
            services.AddSingleton<IInterAccessTokenProvider>(AccessTokenProvider);

            services.RemoveAll<IInterStatementClient>();
            services.AddSingleton<IInterStatementClient>(StatementClient);
        });
    }
}
