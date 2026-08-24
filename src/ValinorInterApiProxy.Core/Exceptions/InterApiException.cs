namespace ValinorInterApiProxy.Core.Exceptions;

/// <summary>
/// Raised when Banco Inter's API returns an error status for a Token or Extrato call. Carries
/// only the upstream HTTP status and a consumer-safe message — never Inter's raw response body
/// or credential details (constitution Principle IV; FR-008/FR-009).
/// </summary>
public sealed class InterApiException(int upstreamStatusCode, string message) : Exception(message)
{
    public int UpstreamStatusCode { get; } = upstreamStatusCode;
}
