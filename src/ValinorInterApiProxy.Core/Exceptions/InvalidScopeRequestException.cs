namespace ValinorInterApiProxy.Core.Exceptions;

/// <summary>
/// Raised when a <c>/auth/token</c> request asks for a scope outside the requesting consumer's
/// <see cref="Models.AuthConsumer.AllowedScopes"/>.
/// </summary>
public sealed class InvalidScopeRequestException(string message) : Exception(message);