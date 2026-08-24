using System.Net;
using System.Net.Http.Json;
using ValinorInterApiProxy.Api.Tests.Infrastructure;
using ValinorInterApiProxy.Core.Models;

namespace ValinorInterApiProxy.Api.Tests;

/// <summary>
/// Contract test for <c>GET /extrato</c>'s success response shape, matching
/// <c>contracts/extrato.openapi.yaml</c>'s <c>StatementResponse</c> schema.
/// </summary>
public sealed class ExtratoEndpointTests : IClassFixture<ProxyWebApplicationFactory>
{
    private readonly ProxyWebApplicationFactory _factory;

    public ExtratoEndpointTests(ProxyWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.StatementClient.StatementToReturn = new BankStatement(
            new DateOnly(2024, 4, 1),
            new DateOnly(2024, 4, 10),
            [
                new StatementEntry(
                    new DateOnly(2024, 4, 5),
                    "0",
                    "C",
                    "PIX",
                    123.45m,
                    "Recebimento",
                    "Transferencia recebida"),
            ]);
    }

    [Fact]
    public async Task GetExtrato_WithValidScopeAndDateRange_ReturnsStatementMatchingContract()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, "consumer-app");
        client.DefaultRequestHeaders.Add(TestAuthHandler.ScopeHeader, "extrato-read");

        var response = await client.GetAsync("/extrato?startDate=2024-04-01&endDate=2024-04-10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<StatementResponseDto>();
        Assert.NotNull(body);
        Assert.Equal(new DateOnly(2024, 4, 1), body!.StartDate);
        Assert.Equal(new DateOnly(2024, 4, 10), body.EndDate);
        var entry = Assert.Single(body.Entries);
        Assert.Equal(new DateOnly(2024, 4, 5), entry.EntryDate);
        Assert.Equal("0", entry.Cpmf);
        Assert.Equal("C", entry.TransactionType);
        Assert.Equal("PIX", entry.OperationType);
        Assert.Equal(123.45m, entry.Amount);
        Assert.Equal("Recebimento", entry.Title);
        Assert.Equal("Transferencia recebida", entry.Description);
    }

    private sealed record StatementResponseDto(
        DateOnly StartDate,
        DateOnly EndDate,
        IReadOnlyList<StatementEntryDto> Entries);

    private sealed record StatementEntryDto(
        DateOnly EntryDate,
        string? Cpmf,
        string TransactionType,
        string OperationType,
        decimal Amount,
        string Title,
        string Description);
}
