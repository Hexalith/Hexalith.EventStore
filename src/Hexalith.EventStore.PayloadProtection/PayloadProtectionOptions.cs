// Approved on-premises supplement: 43fc3400f9e01662eac5ad945407711d0d24a744685e92cef203c2162106e2d3; sections 1-7.
// Base authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e, sections 5.2-5.3; on-premises supplement sections 1-2, 6-7.
using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Closed explicit opt-in settings; loading never grants current backend or write authority.</summary>
/// <param name="Enabled">Whether enabled mode was explicitly selected.</param>
/// <param name="Backend">The exact closed backend choice.</param>
/// <param name="WriteMode">The exact closed writer mode.</param>
/// <param name="StaleBackendConfiguration">Whether disabled registration contained unused backend configuration.</param>
/// <param name="PlatformCustody">The closed installed binding selection, if explicitly enabled.</param>
/// <param name="LegacyWriterId">The exact retained writer identity, if selected.</param>
/// <param name="MaxProtectedValueBytes">The lowered new-write value bound.</param>
/// <param name="KeyCacheTtl">The bounded absolute cache lifetime.</param>
/// <param name="KeyCacheEntries">The bounded per-process cache capacity.</param>
/// <param name="OperationTimeout">The enclosing operation deadline.</param>
internal sealed record PayloadProtectionOptions(bool Enabled, string? Backend, string WriteMode, bool StaleBackendConfiguration,
    PlatformCustodyPayloadProtectionOptions? PlatformCustody, string? LegacyWriterId, int MaxProtectedValueBytes,
    TimeSpan KeyCacheTtl, int KeyCacheEntries, TimeSpan OperationTimeout)
{
    private static readonly string[] Fields = ["Mode", "Backend", "WriteMode", "LegacyWriterId", "MaxProtectedValueBytes", "KeyCacheTtl", "KeyCacheEntries", "OperationTimeout", "AzureKeyVault", "PlatformCustody"];

    /// <summary>Loads exact names and bounded values without provider fallback or arbitrary authority configuration.</summary>
    internal static PayloadProtectionOptions Load(IConfigurationSection section)
    {
        try
        {
            if (section.GetChildren().Any(c => !Fields.Contains(c.Key, StringComparer.Ordinal)
                || (c.Key is not ("AzureKeyVault" or "PlatformCustody") && c.GetChildren().Any()))) { throw Invalid(); }
            // Unknown installed-backend fields are invalid even when stale configuration is unused.
            if (section.GetSection("PlatformCustody").GetChildren().Any(c => c.Key is not ("BindingName" or "InstallationId" or "ProfileSha256") || c.GetChildren().Any())) { throw Invalid(); }
            string? mode = section["Mode"];
            if (mode is not ("Enabled" or "Disabled")) { throw Invalid(); }
            bool stale = section["Backend"] is not null || section.GetSection("AzureKeyVault").GetChildren().Any() || section.GetSection("PlatformCustody").GetChildren().Any();
            if (mode == "Disabled") { return new(false, null, "V2", stale, null, null, 1048576, TimeSpan.FromMinutes(1), 1024, TimeSpan.FromSeconds(10)); }
            string? backend = section["Backend"];
            string write = section["WriteMode"] ?? "V2";
            if (backend is not ("LocalDevelopment" or "AzureKeyVault" or "PlatformCustody") || write is not ("V2" or "ReadOnly" or "CompatibilityLegacy" or "RollbackLegacy")) { throw Invalid(); }
            if (write is "CompatibilityLegacy" or "RollbackLegacy") { PersonalDataPolicyRegistry.ValidateId(section["LegacyWriterId"] ?? ""); }
            int maximum = RequireInteger(section["MaxProtectedValueBytes"], 1048576, 1048576);
            int entries = RequireInteger(section["KeyCacheEntries"], 1024, 4096);
            TimeSpan ttl = RequireDuration(section["KeyCacheTtl"], TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));
            TimeSpan timeout = RequireDuration(section["OperationTimeout"], TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30));
            PlatformCustodyPayloadProtectionOptions? platform = backend == "PlatformCustody" ? PlatformCustodyPayloadProtectionOptions.Load(section.GetSection("PlatformCustody")) : null;
            return new(true, backend, write, false, platform, section["LegacyWriterId"], maximum, ttl, entries, timeout);
        }
        catch { throw Invalid(); }
    }

    /// <summary>Gets the authority only from a closed explicit backend choice.</summary>
    internal PayloadProtectionAuthorityProfile AuthorityProfile => Backend == "PlatformCustody"
        ? PayloadProtectionAuthorityProfile.PlatformCustody : PayloadProtectionAuthorityProfile.Predecessor;

    /// <summary>Never formats selected providers or configuration values.</summary>
    public override string ToString() => nameof(PayloadProtectionOptions);
    private static int RequireInteger(string? value, int fallback, int maximum)
    {
        int parsed = value is null ? fallback : int.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture);
        if (parsed < 1 || parsed > maximum) { throw Invalid(); }
        return parsed;
    }
    private static TimeSpan RequireDuration(string? value, TimeSpan fallback, TimeSpan maximum)
    {
        TimeSpan parsed = value is null ? fallback : TimeSpan.ParseExact(value, "c", CultureInfo.InvariantCulture);
        if (parsed <= TimeSpan.Zero || parsed > maximum) { throw Invalid(); }
        return parsed;
    }
    private static InvalidOperationException Invalid() => new("Payload protection options are invalid.");
}
