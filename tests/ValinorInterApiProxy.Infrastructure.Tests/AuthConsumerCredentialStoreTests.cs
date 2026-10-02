using Microsoft.Extensions.Options;
using ValinorInterApiProxy.Infrastructure.Auth;

namespace ValinorInterApiProxy.Infrastructure.Tests;

/// <summary>
/// Unit tests for <see cref="AuthConsumerCredentialStore"/>'s config-backed lookup.
/// </summary>
public sealed class AuthConsumerCredentialStoreTests
{
    [Fact]
    public void FindByClientId_KnownClient_ReturnsMatchingConsumer()
    {
        var options = new AuthConsumersOptions
        {
            Consumers =
            [
                new AuthConsumerEntry { ClientId = "consumer-app", ClientSecret = "s3cr3t", AllowedScopes = ["token-issue"] },
            ],
        };
        var store = new AuthConsumerCredentialStore(Options.Create(options));

        var consumer = store.FindByClientId("consumer-app");

        Assert.NotNull(consumer);
        Assert.Equal("consumer-app", consumer.ClientId);
        Assert.Equal("s3cr3t", consumer.ClientSecret);
        Assert.Equal(["token-issue"], consumer.AllowedScopes);
    }

    [Fact]
    public void FindByClientId_UnknownClient_ReturnsNull()
    {
        var options = new AuthConsumersOptions { Consumers = [] };
        var store = new AuthConsumerCredentialStore(Options.Create(options));

        Assert.Null(store.FindByClientId("unknown-client"));
    }
}