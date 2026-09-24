using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Vispl.HRManagement.Identity.Admin;

public sealed class MobileTokenOptions
{
    public const string SectionName = "MobileApi";

    /// <summary>The minimum key size that HMAC-SHA256 signing is safe with.</summary>
    public const int MinimumKeyBytes = 32;

    public string Issuer { get; set; } = "vispl.hr";
    public string Audience { get; set; } = "vispl.hr.api";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 14;

    /// <summary>Base64-encoded HMAC key. Never committed; see MobileSigningKeyProvider.</summary>
    public string? SigningKey { get; set; }
}

/// <summary>
/// Resolves the token signing key.
///
/// In any environment other than Development the key must come from configuration
/// (environment variable, user secrets or a secret store) and startup fails without it —
/// a predictable or absent key would let anyone mint valid tokens.
///
/// In Development only, a random key is generated once and persisted next to the data
/// protection keys so restarts do not invalidate existing tokens. That file is local
/// state, not source, and must never be committed or copied to a deployed environment.
/// </summary>
public sealed class MobileSigningKeyProvider
{
    private const string DevelopmentKeyFileName = "mobile-signing.key";

    public byte[] Key { get; }
    public bool IsDevelopmentKey { get; }

    public MobileSigningKeyProvider(IConfiguration configuration, IHostEnvironment environment)
    {
        var configured = configuration[$"{MobileTokenOptions.SectionName}:SigningKey"];

        if (!string.IsNullOrWhiteSpace(configured))
        {
            byte[] decoded;
            try
            {
                decoded = Convert.FromBase64String(configured);
            }
            catch (FormatException)
            {
                throw new InvalidOperationException(
                    "MobileApi:SigningKey must be base64-encoded.");
            }

            if (decoded.Length < MobileTokenOptions.MinimumKeyBytes)
            {
                throw new InvalidOperationException(
                    $"MobileApi:SigningKey must be at least {MobileTokenOptions.MinimumKeyBytes} bytes.");
            }

            Key = decoded;
            IsDevelopmentKey = false;
            return;
        }

        if (!environment.IsDevelopment())
        {
            // Fail closed. Generating a key here would silently invalidate every token on
            // each restart and differ across instances.
            throw new InvalidOperationException(
                "MobileApi:SigningKey is required outside Development. Supply a base64 key of at " +
                "least 32 bytes through configuration or a secret store.");
        }

        var keysPath = configuration["DataProtection:KeysPath"] ?? ".data-protection-keys";
        var directory = Path.IsPathRooted(keysPath)
            ? keysPath
            : Path.Combine(environment.ContentRootPath, keysPath);
        Directory.CreateDirectory(directory);

        var keyFile = Path.Combine(directory, DevelopmentKeyFileName);

        if (File.Exists(keyFile))
        {
            var existing = Convert.FromBase64String(File.ReadAllText(keyFile).Trim());
            if (existing.Length >= MobileTokenOptions.MinimumKeyBytes)
            {
                Key = existing;
                IsDevelopmentKey = true;
                return;
            }
        }

        var generated = RandomNumberGenerator.GetBytes(64);
        File.WriteAllText(keyFile, Convert.ToBase64String(generated));
        Key = generated;
        IsDevelopmentKey = true;
    }
}
