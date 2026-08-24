namespace ValinorInterApiProxy.Infrastructure.InterApi;

/// <summary>
/// Banco Inter application credentials and connection settings, bound via the Options pattern
/// from secret configuration (.NET User Secrets in development; environment/Key Vault in
/// production). No literal secret values may appear in source (constitution Principle IV).
/// </summary>
public sealed class InterOptions
{
    public const string SectionName = "Inter";

    public required string ClientId { get; set; }

    public required string ClientSecret { get; set; }

    public required string CertificatePath { get; set; }

    public required string CertificatePassword { get; set; }

    public required string Scope { get; set; }

    public required string ContaCorrente { get; set; }

    public required string BaseUrl { get; set; }
}
