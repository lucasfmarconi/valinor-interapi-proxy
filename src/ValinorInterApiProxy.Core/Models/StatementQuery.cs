namespace ValinorInterApiProxy.Core.Models;

/// <summary>
/// A requested bank statement period. Validated by <c>GetStatementUseCase</c>, not by this shape.
/// </summary>
public sealed record StatementQuery(DateOnly StartDate, DateOnly EndDate);
