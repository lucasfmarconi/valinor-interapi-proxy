# C# sample for Extrato request

```csharp
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace Get;

class Program
{
    static void Main(string[] args)
    {
        HttpClient client;
        String? bearerToken;
        String permissoes = "extrato.read";
        String contaCorrente = "<conta corrente selecionada>";

        X509Certificate cert = obterCert();

        //Obtendo bearer token 
        bearerToken = obterBearerToken(permissoes, out client, cert);
        Console.WriteLine("Bearer Token: {0}", bearerToken);

        //Obter o extrato
        String extrato = obterExtrato(contaCorrente, out client, cert, bearerToken);
        Console.WriteLine("Extrato: {0}", extrato);

    }

    private static String? obterExtrato(String contaCorrente, out HttpClient client, X509Certificate cert, String bearerToken) {
        var clientHandlerOauth = new HttpClientHandler();
        clientHandlerOauth.ClientCertificateOptions = ClientCertificateOption.Manual;
        clientHandlerOauth.ClientCertificates.Add(cert);

        String uriExtrato = "https://cdpj.partners.bancointer.com.br/banking/v2/extrato";

        using (client = new HttpClient(clientHandlerOauth))
        {
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Add("Authorization", "Bearer " + bearerToken);
            client.DefaultRequestHeaders.Add("x-conta-corrente", contaCorrente);

            string query;
            using(var content = new FormUrlEncodedContent(new Dictionary<string, string>() {
                { "dataInicio", "2024-04-01"},
                { "dataFim", "2024-04-10"},
            })) {
                query = content.ReadAsStringAsync().Result;
            }

            HttpResponseMessage response_detalhe= client.GetAsync(uriExtrato + "?" + query).GetAwaiter().GetResult();
            if (response_detalhe.IsSuccessStatusCode)
            {
                string content = response_detalhe.Content.ReadAsStringAsync().Result;
                return content;
            } else {
                Console.WriteLine("Error, received status code {0}: {1}", response_detalhe.StatusCode, response_detalhe.ReasonPhrase);
            }

            client.Dispose();

            return null;
        }
    }

    private static X509Certificate obterCert()
    {
        String certPem = File.ReadAllText("<nome arquivo certificado>.crt");
        String keyPem = File.ReadAllText("<nome arquivo chave privada>.key");

        X509Certificate2 cert = X509Certificate2.CreateFromPem(certPem, keyPem);

        return cert;
    }

    private static String? obterBearerToken(String permissoes, out HttpClient client, X509Certificate cert)
    {
        var clientHandlerOauth = new HttpClientHandler();
        clientHandlerOauth.ClientCertificateOptions = ClientCertificateOption.Manual;
        clientHandlerOauth.ClientCertificates.Add(cert);

        String URI_Token = "https://cdpj.partners.bancointer.com.br/oauth/v2/token";

        var data = new[]
        {
            new KeyValuePair<string, string>("client_id", "<clientId de sua aplicação>"),
            new KeyValuePair<string, string>("client_secret", "clientSecret de sua aplicação>"),
            new KeyValuePair<string, string>("scope", permissoes),
            new KeyValuePair<string, string>("grant_type", "client_credentials")
        };

        using (client = new HttpClient(clientHandlerOauth))
        {
            var response = client.PostAsync(URI_Token, new FormUrlEncodedContent(data)).GetAwaiter().GetResult();

            String jsonStr = response.Content.ReadAsStringAsync().Result;

            TokenModel? tokenModel = JsonSerializer.Deserialize<TokenModel>(jsonStr);
            String bearerToken = tokenModel?.access_token;

            client.Dispose();

            return bearerToken;
        }
    }

    public class TokenModel
    {
        public string? access_token { get; set; }
        public string? token_type { get; set; }
        public int expires_in { get; set; }
        public string? scope { get; set; }
    }

}
```

# JSON sample for token response
## Responses - HTTP Codes - Portuguese lang result description
200 Sucesso
400 Requisição com formato inválido.
403 Requisição de participante autenticado que viola alguma regra de autorização.
404 Recurso solicitado não foi encontrado.
503 Serviço não está disponível no momento. Serviço solicitado pode estar em manutenção ou fora da janela de funcionamento

### 200 sample
```json
{
"transacoes": [
{
"cpmf": "string",
"dataEntrada": "2019-08-24",
"tipoTransacao": "string",
"tipoOperacao": "string",
"valor": "string",
"titulo": "string",
"descricao": "string"
}
]
}
```
