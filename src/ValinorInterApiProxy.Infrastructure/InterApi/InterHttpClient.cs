using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ValinorInterApiProxy.Core.Exceptions;
using ValinorInterApiProxy.Core.Models;
using ValinorInterApiProxy.Core.Ports;

namespace ValinorInterApiProxy.Infrastructure.InterApi;

/// <summary>
/// Implements <see cref="IInterTokenClient"/> and <see cref="IInterStatementClient"/> over MTLS.
/// This is the only type in the solution permitted to hold the Inter client certificate
/// (constitution Principle I).
/// </summary>
public sealed class InterHttpClient(HttpClient httpClient, IOptions<InterOptions> options)
    : IInterTokenClient, IInterStatementClient
{
    private const string ExtratoDateFormat = "yyyy-MM-dd";

    private readonly HttpClient _httpClient = httpClient;
    private readonly InterOptions _options = options.Value;

    public async Task<InterAccessToken> IssueTokenAsync(CancellationToken cancellationToken = default)
    {
        var issuedAt = DateTimeOffset.UtcNow;

        using var content = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("client_id", _options.ClientId),
            new KeyValuePair<string, string>("client_secret", _options.ClientSecret),
            new KeyValuePair<string, string>("scope", _options.Scope),
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
        ]);

        using var response = await _httpClient.PostAsync("/oauth/v2/token", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InterApiException((int)response.StatusCode, "Banco Inter's token service returned an error.");
        }

        var payload = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
            ?? throw new InterApiException((int)response.StatusCode, "Banco Inter returned an empty token response.");

        return new InterAccessToken(
            payload.AccessToken,
            payload.TokenType,
            issuedAt.AddSeconds(payload.ExpiresIn),
            payload.Scope);
    }

    public async Task<BankStatement> GetStatementAsync(
        StatementQuery query,
        InterAccessToken accessToken,
        CancellationToken cancellationToken = default)
    {
        var startDate = query.StartDate.ToString(ExtratoDateFormat, CultureInfo.InvariantCulture);
        var endDate = query.EndDate.ToString(ExtratoDateFormat, CultureInfo.InvariantCulture);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/banking/v2/extrato?dataInicio={startDate}&dataFim={endDate}");
        request.Headers.Authorization = new AuthenticationHeaderValue(accessToken.TokenType, accessToken.Value);
        request.Headers.Add("x-conta-corrente", _options.ContaCorrente);

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InterApiException(
                (int)response.StatusCode,
                "Banco Inter's statement service returned an error.");
        }

        var payload = await response.Content.ReadFromJsonAsync<ExtratoResponse>(cancellationToken)
            ?? throw new InterApiException((int)response.StatusCode, "Banco Inter returned an empty statement response.");

        var entries = payload.Transacoes.Select(transacao => MapEntry(transacao, (int)response.StatusCode)).ToList();

        return new BankStatement(query.StartDate, query.EndDate, entries);
    }

    private static StatementEntry MapEntry(ExtratoTransacao transacao, int upstreamStatusCode)
    {
        if (!decimal.TryParse(transacao.Valor, CultureInfo.InvariantCulture, out var amount))
        {
            throw new InterApiException(
                upstreamStatusCode,
                "Banco Inter returned a non-numeric transaction amount.");
        }

        var entryDate = DateOnly.ParseExact(transacao.DataEntrada, ExtratoDateFormat, CultureInfo.InvariantCulture);

        return new StatementEntry(
            entryDate,
            transacao.Cpmf,
            transacao.TipoTransacao,
            transacao.TipoOperacao,
            amount,
            transacao.Titulo,
            transacao.Descricao);
    }

    /// <summary>Banco Inter's raw <c>POST /oauth/v2/token</c> response shape.</summary>
    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;

        [JsonPropertyName("token_type")]
        public string TokenType { get; init; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }

        [JsonPropertyName("scope")]
        public string Scope { get; init; } = string.Empty;
    }

    /// <summary>Banco Inter's raw <c>GET /banking/v2/extrato</c> response shape.</summary>
    private sealed class ExtratoResponse
    {
        [JsonPropertyName("transacoes")]
        public List<ExtratoTransacao> Transacoes { get; init; } = [];
    }

    /// <summary>A single entry in Banco Inter's raw <c>transacoes[]</c> response array.</summary>
    private sealed class ExtratoTransacao
    {
        [JsonPropertyName("cpmf")]
        public string Cpmf { get; init; } = string.Empty;

        [JsonPropertyName("dataEntrada")]
        public string DataEntrada { get; init; } = string.Empty;

        [JsonPropertyName("tipoTransacao")]
        public string TipoTransacao { get; init; } = string.Empty;

        [JsonPropertyName("tipoOperacao")]
        public string TipoOperacao { get; init; } = string.Empty;

        [JsonPropertyName("valor")]
        public string Valor { get; init; } = string.Empty;

        [JsonPropertyName("titulo")]
        public string Titulo { get; init; } = string.Empty;

        [JsonPropertyName("descricao")]
        public string Descricao { get; init; } = string.Empty;
    }
}

/// <summary>
/// Registers <see cref="InterHttpClient"/> as a typed <see cref="HttpClient"/> via
/// <see cref="IHttpClientFactory"/>, attaching the Inter MTLS client certificate to the handler
/// and a standard resilience policy (bounded retry + timeout) to the request pipeline.
/// </summary>
public static class InterHttpClientServiceCollectionExtensions
{
    public static IHttpClientBuilder AddInterHttpClient(this IServiceCollection services)
    {
        var builder = services
            .AddHttpClient<InterHttpClient>((serviceProvider, httpClient) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<InterOptions>>().Value;
                httpClient.BaseAddress = new Uri(options.BaseUrl);
            })
            .ConfigurePrimaryHttpMessageHandler(serviceProvider =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<InterOptions>>().Value;
                var certificate = X509CertificateLoader.LoadPkcs12FromFile(
                    options.CertificatePath,
                    options.CertificatePassword);

                return new SocketsHttpHandler
                {
                    SslOptions = new SslClientAuthenticationOptions
                    {
                        ClientCertificates = [certificate],
                    },
                };
            });

        builder.AddStandardResilienceHandler();

        return builder;
    }
}
