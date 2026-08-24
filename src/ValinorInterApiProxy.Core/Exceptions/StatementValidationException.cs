namespace ValinorInterApiProxy.Core.Exceptions;

/// <summary>
/// Raised when a requested <see cref="Models.StatementQuery"/> fails validation (out-of-order
/// dates, a future end date, or a period exceeding what Banco Inter supports) before any Inter
/// call is made (spec FR-007).
/// </summary>
public sealed class StatementValidationException(string message) : Exception(message);
