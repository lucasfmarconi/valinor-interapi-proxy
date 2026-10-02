using System.Security.Cryptography;
using ValinorInterApiProxy.Core.Exceptions;
using ValinorInterApiProxy.Core.Models;
using ValinorInterApiProxy.Core.Ports;

namespace ValinorInterApiProxy.Core.UseCases;

/// <summary>
/// Orchestrates self-issued JWT issuance for <c>POST /auth/token</c> (constitution Principle II):
/// validates the requesting consumer's credentials and requested scope, then delegates signing to
/// <see cref="IJwtIssuer"/>. Synchronous — unlike <see cref="IssueTokenUseCase"/> and
/// <see cref="GetStatementUseCase"/>, this performs no I/O (credential lookup and signing are
/// both in-memory).
/// </summary>
public sealed class IssueAuthTokenUseCase(IClientCredentialStore credentialStore, IJwtIssuer jwtIssuer)
{
    public IssuedAuthToken Execute(string clientId, string clientSecret, string? requestedScope)
    {
        var consumer = credentialStore.FindByClientId(clientId);
        if (consumer is null || !SecretMatches(consumer.ClientSecret, clientSecret))
        {
            throw new InvalidClientCredentialsException();
        }

        var scopes = ResolveScopes(consumer, requestedScope);

        return jwtIssuer.Issue(consumer.ClientId, scopes);
    }

    private static bool SecretMatches(string expected, string actual)
    {
        var expectedBytes = System.Text.Encoding.UTF8.GetBytes(expected);
        var actualBytes = System.Text.Encoding.UTF8.GetBytes(actual);
        return expectedBytes.Length == actualBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    private static IReadOnlyCollection<string> ResolveScopes(AuthConsumer consumer, string? requestedScope)
    {
        if (string.IsNullOrWhiteSpace(requestedScope))
        {
            return consumer.AllowedScopes;
        }

        var requested = requestedScope.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var disallowed = requested.Where(scope => !consumer.AllowedScopes.Contains(scope, StringComparer.Ordinal));
        if (disallowed.Any())
        {
            throw new InvalidScopeRequestException(
                $"Requested scope '{requestedScope}' is not allowed for this client.");
        }

        return requested;
    }
}