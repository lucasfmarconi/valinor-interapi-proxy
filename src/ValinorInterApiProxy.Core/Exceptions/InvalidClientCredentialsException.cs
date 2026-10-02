namespace ValinorInterApiProxy.Core.Exceptions;

/// <summary>
/// Raised when a <c>/auth/token</c> request's client id is unknown or its client secret does not
/// match the registered <see cref="Models.AuthConsumer"/>. Deliberately does not distinguish
/// between the two cases, to avoid client-id enumeration.
/// </summary>
public sealed class InvalidClientCredentialsException() : Exception("Invalid client_id or client_secret.");