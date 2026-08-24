namespace ValinorInterApiProxy.Core.Models;

/// <summary>
/// The statement data returned to a consumer for a requested period.
/// </summary>
public sealed record BankStatement(DateOnly StartDate, DateOnly EndDate, IReadOnlyList<StatementEntry> Entries);
