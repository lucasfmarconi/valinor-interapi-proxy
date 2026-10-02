using ValinorInterApiProxy.Core.Exceptions;
using ValinorInterApiProxy.Core.Models;
using ValinorInterApiProxy.Core.Ports;
using ValinorInterApiProxy.Core.UseCases;

namespace ValinorInterApiProxy.Core.Tests;

/// <summary>
/// Unit tests for <see cref="IssueAuthTokenUseCase"/>'s credential/scope validation (constitution
/// Principle II). Each failure case asserts the use case never calls <see cref="IJwtIssuer"/>.
/// </summary>
public sealed class IssueAuthTokenUseCaseTests
{
    private static readonly AuthConsumer Consumer = new("consumer-app", "correct-secret", ["token-issue", "extrato-read"]);

    [Fact]
    public void Execute_ValidCredentials_IssuesTokenWithRequestedScope()
    {
        var jwtIssuer = new RecordingJwtIssuer();
        var useCase = new IssueAuthTokenUseCase(new FakeCredentialStore(Consumer), jwtIssuer);

        useCase.Execute(Consumer.ClientId, Consumer.ClientSecret, "extrato-read");

        Assert.True(jwtIssuer.WasCalled);
        Assert.Equal(Consumer.ClientId, jwtIssuer.Subject);
        Assert.Equal(["extrato-read"], jwtIssuer.Scopes);
    }

    [Fact]
    public void Execute_OmittedScope_DefaultsToConsumersFullAllowedScopes()
    {
        var jwtIssuer = new RecordingJwtIssuer();
        var useCase = new IssueAuthTokenUseCase(new FakeCredentialStore(Consumer), jwtIssuer);

        useCase.Execute(Consumer.ClientId, Consumer.ClientSecret, requestedScope: null);

        Assert.Equal(Consumer.AllowedScopes, jwtIssuer.Scopes);
    }

    [Fact]
    public void Execute_UnknownClientId_ThrowsInvalidClientCredentialsWithoutCallingIssuer()
    {
        var jwtIssuer = new RecordingJwtIssuer();
        var useCase = new IssueAuthTokenUseCase(new FakeCredentialStore(consumer: null), jwtIssuer);

        Assert.Throws<InvalidClientCredentialsException>(
            () => useCase.Execute("unknown-client", "any-secret", null));

        Assert.False(jwtIssuer.WasCalled);
    }

    [Fact]
    public void Execute_WrongClientSecret_ThrowsInvalidClientCredentialsWithoutCallingIssuer()
    {
        var jwtIssuer = new RecordingJwtIssuer();
        var useCase = new IssueAuthTokenUseCase(new FakeCredentialStore(Consumer), jwtIssuer);

        Assert.Throws<InvalidClientCredentialsException>(
            () => useCase.Execute(Consumer.ClientId, "wrong-secret", null));

        Assert.False(jwtIssuer.WasCalled);
    }

    [Fact]
    public void Execute_ScopeOutsideAllowlist_ThrowsInvalidScopeRequestWithoutCallingIssuer()
    {
        var jwtIssuer = new RecordingJwtIssuer();
        var useCase = new IssueAuthTokenUseCase(new FakeCredentialStore(Consumer), jwtIssuer);

        Assert.Throws<InvalidScopeRequestException>(
            () => useCase.Execute(Consumer.ClientId, Consumer.ClientSecret, "some-other-scope"));

        Assert.False(jwtIssuer.WasCalled);
    }

    private sealed class FakeCredentialStore(AuthConsumer? consumer) : IClientCredentialStore
    {
        public AuthConsumer? FindByClientId(string clientId) =>
            consumer is not null && consumer.ClientId == clientId ? consumer : null;
    }

    private sealed class RecordingJwtIssuer : IJwtIssuer
    {
        public bool WasCalled { get; private set; }

        public string? Subject { get; private set; }

        public IReadOnlyCollection<string>? Scopes { get; private set; }

        public IssuedAuthToken Issue(string subject, IReadOnlyCollection<string> scopes)
        {
            WasCalled = true;
            Subject = subject;
            Scopes = scopes;
            return new IssuedAuthToken("fake-jwt", "Bearer", DateTimeOffset.UtcNow.AddHours(1), 3600, scopes);
        }
    }
}