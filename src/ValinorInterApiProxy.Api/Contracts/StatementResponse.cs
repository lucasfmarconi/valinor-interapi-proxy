using ValinorInterApiProxy.Core.Models;

namespace ValinorInterApiProxy.Api.Contracts;

/// <summary>
/// Consumer-facing response shape for <c>GET /extrato</c>, matching
/// <c>contracts/extrato.openapi.yaml</c>'s <c>StatementResponse</c> schema.
/// </summary>
public sealed record StatementResponse(
    DateOnly StartDate,
    DateOnly EndDate,
    IReadOnlyList<StatementEntryResponse> Entries)
{
    public static StatementResponse FromBankStatement(BankStatement statement) =>
        new(
            statement.StartDate,
            statement.EndDate,
            statement.Entries.Select(StatementEntryResponse.FromStatementEntry).ToList());
}

/// <summary>
/// Consumer-facing shape for a single statement entry, matching
/// <c>contracts/extrato.openapi.yaml</c>'s <c>StatementEntry</c> schema.
/// </summary>
public sealed record StatementEntryResponse(
    DateOnly EntryDate,
    string Cpmf,
    string TransactionType,
    string OperationType,
    decimal Amount,
    string Title,
    string Description)
{
    public static StatementEntryResponse FromStatementEntry(StatementEntry entry) =>
        new(
            entry.EntryDate,
            entry.Cpmf,
            entry.TransactionType,
            entry.OperationType,
            entry.Amount,
            entry.Title,
            entry.Description);
}
