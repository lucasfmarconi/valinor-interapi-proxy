namespace ValinorInterApiProxy.Core.Models;

/// <summary>
/// A single transaction line within a <see cref="BankStatement"/>, mapped from Banco Inter's
/// <c>transacoes[]</c> wire format by the Infrastructure layer.
/// </summary>
public sealed record StatementEntry(
    DateOnly EntryDate,
    string Cpmf,
    string TransactionType,
    string OperationType,
    decimal Amount,
    string Title,
    string Description);
