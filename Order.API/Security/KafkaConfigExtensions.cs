using Confluent.Kafka;

namespace Order.API.Security;

/// <summary>
/// Applies TLS/SSL and SASL security settings from configuration to any Kafka client.
///
/// ── SECURITY PROTOCOL MATRIX ──────────────────────────────────────────────────
///  Ssl.Enabled  Sasl.Enabled  →  SecurityProtocol
///  false        false         →  Plaintext        (local dev only)
///  true         false         →  Ssl              (wire encryption only)
///  false        true          →  SaslPlaintext    (auth without encryption — avoid in prod)
///  true         true          →  SaslSsl          (recommended for production)
///
/// ── SASL MECHANISMS ───────────────────────────────────────────────────────────
///  "Plain"       SASL/PLAIN — username+password in clear; always pair with SSL.
///  "ScramSha256" SASL/SCRAM-SHA-256 — salted challenge-response; preferred over PLAIN.
///  "ScramSha512" SASL/SCRAM-SHA-512 — stronger SCRAM variant.
///
/// ── KAFKA ACL AUTHORIZATION (broker-side) ─────────────────────────────────────
///  Assuming SASL usernames "order-api" (producer) and "order-consumer" (consumer):
///
///  Producer — Order.API:
///    kafka-acls.sh --add --allow-principal User:order-api \
///      --operation Write --topic order-events --bootstrap-server broker:9093
///
///  Idempotent producer also needs:
///    kafka-acls.sh --add --allow-principal User:order-api \
///      --operation IdempotentWrite --cluster
///
/// ── CREDENTIAL MANAGEMENT ─────────────────────────────────────────────────────
///  Credentials must NEVER be committed to source control. Use:
///  • Local dev:    dotnet user-secrets set "Kafka:Security:Sasl:Password" "secret"
///  • CI/CD:        environment variable KAFKA__SECURITY__SASL__PASSWORD=secret
///  • Production:   Azure Key Vault → AddAzureKeyVault() in Program.cs
///                  AWS Secrets Manager → AddSecretsManager() in Program.cs
///                  HashiCorp Vault → community provider package
/// </summary>
public static class KafkaConfigExtensions
{
    public static void ApplySecurity(this ClientConfig config, IConfiguration configuration)
    {
        var sslEnabled  = configuration.GetValue<bool>("Kafka:Security:Ssl:Enabled");
        var saslEnabled = configuration.GetValue<bool>("Kafka:Security:Sasl:Enabled");

        if (!sslEnabled && !saslEnabled)
            return;

        if (sslEnabled)
        {
            SetIfPresent(configuration["Kafka:Security:Ssl:CaLocation"],
                v => config.SslCaLocation = v);

            SetIfPresent(configuration["Kafka:Security:Ssl:CertificateLocation"],
                v => config.SslCertificateLocation = v);

            SetIfPresent(configuration["Kafka:Security:Ssl:KeyLocation"],
                v => config.SslKeyLocation = v);

            SetIfPresent(configuration["Kafka:Security:Ssl:KeyPassword"],
                v => config.SslKeyPassword = v);
        }

        if (saslEnabled)
        {
            var mechanism = configuration["Kafka:Security:Sasl:Mechanism"] ?? "Plain";

            config.SaslMechanism = mechanism switch
            {
                "Plain"       => SaslMechanism.Plain,
                "ScramSha256" => SaslMechanism.ScramSha256,
                "ScramSha512" => SaslMechanism.ScramSha512,
                _ => throw new InvalidOperationException(
                    $"Unsupported SASL mechanism: '{mechanism}'. " +
                    $"Supported values: Plain, ScramSha256, ScramSha512.")
            };

            config.SaslUsername = configuration["Kafka:Security:Sasl:Username"];
            config.SaslPassword = configuration["Kafka:Security:Sasl:Password"];
        }

        config.SecurityProtocol = (sslEnabled, saslEnabled) switch
        {
            (true,  true)  => SecurityProtocol.SaslSsl,
            (true,  false) => SecurityProtocol.Ssl,
            (false, true)  => SecurityProtocol.SaslPlaintext,
            _              => SecurityProtocol.Plaintext
        };
    }

    private static void SetIfPresent(string? value, Action<string> setter)
    {
        if (!string.IsNullOrEmpty(value))
            setter(value);
    }
}
