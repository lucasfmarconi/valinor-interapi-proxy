using Microsoft.Extensions.Options;
using ValinorInterApiProxy.Core.Exceptions;
using ValinorInterApiProxy.Core.Models;
using ValinorInterApiProxy.Infrastructure.InterApi;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace ValinorInterApiProxy.Infrastructure.Tests;

/// <summary>
/// Integration tests for <see cref="InterHttpClient.GetStatementAsync"/> against a
/// <see cref="WireMockServer"/> standing in for Banco Inter's
/// <c>GET /banking/v2/extrato</c> (no live MTLS dependency; per plan.md's Testing strategy).
/// </summary>
public sealed class InterHttpClientExtratoTests : IDisposable
{
    private const string AccessTokenValue = "fake-access-token";
    private const string ContaCorrente = "1234-5";

    private readonly WireMockServer _server = WireMockServer.Start();

    [Fact]
    public async Task GetStatementAsync_Success_MapsTransacoesToStatementEntries()
    {
        _server
            .Given(Request.Create()
                .WithPath("/banking/v2/extrato")
                .WithParam("dataInicio", "2024-04-01")
                .WithParam("dataFim", "2024-04-10")
                .WithHeader("Authorization", $"Bearer {AccessTokenValue}")
                .WithHeader("x-conta-corrente", ContaCorrente)
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                    {
                      "transacoes": [
                        {
                          "cpmf": "0",
                          "dataEntrada": "2024-04-05",
                          "tipoTransacao": "C",
                          "tipoOperacao": "PIX",
                          "valor": "123.45",
                          "titulo": "Recebimento",
                          "descricao": "Transferencia recebida"
                        }
                      ]
                    }
                    """));

        var client = CreateClient();
        var query = new StatementQuery(new DateOnly(2024, 4, 1), new DateOnly(2024, 4, 10));

        var statement = await client.GetStatementAsync(query, CreateAccessToken());

        Assert.Equal(query.StartDate, statement.StartDate);
        Assert.Equal(query.EndDate, statement.EndDate);
        var entry = Assert.Single(statement.Entries);
        Assert.Equal(new DateOnly(2024, 4, 5), entry.EntryDate);
        Assert.Equal("0", entry.Cpmf);
        Assert.Equal("C", entry.TransactionType);
        Assert.Equal("PIX", entry.OperationType);
        Assert.Equal(123.45m, entry.Amount);
        Assert.Equal("Recebimento", entry.Title);
        Assert.Equal("Transferencia recebida", entry.Description);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(503)]
    public async Task GetStatementAsync_UpstreamErrorStatus_ThrowsInterApiExceptionWithStatus(int statusCode)
    {
        _server
            .Given(Request.Create().WithPath("/banking/v2/extrato").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(statusCode));

        var client = CreateClient();
        var query = new StatementQuery(new DateOnly(2024, 4, 1), new DateOnly(2024, 4, 10));

        var exception = await Assert.ThrowsAsync<InterApiException>(
            () => client.GetStatementAsync(query, CreateAccessToken()));

        Assert.Equal(statusCode, exception.UpstreamStatusCode);
    }

    private InterHttpClient CreateClient()
    {
        var httpClient = new HttpClient { BaseAddress = new Uri(_server.Urls[0]) };
        var options = Options.Create(new InterOptions
        {
            ClientId = "test-client-id",
            ClientSecret = "test-client-secret",
            CertificatePath = "unused.pfx",
            CertificatePassword = "unused",
            Scope = "extrato.read",
            ContaCorrente = ContaCorrente,
            BaseUrl = _server.Urls[0],
        });

        return new InterHttpClient(httpClient, options);
    }

    private static InterAccessToken CreateAccessToken() =>
        new(AccessTokenValue, "Bearer", DateTimeOffset.UtcNow.AddMinutes(5), "extrato.read");

    public void Dispose() => _server.Dispose();
}
