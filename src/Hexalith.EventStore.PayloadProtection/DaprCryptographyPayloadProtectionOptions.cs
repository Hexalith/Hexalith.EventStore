// Approved Dapr Kubernetes cryptography supplement: 3489775e15bdc5b7ffad4a67f37c6372a23d50b213095e26bd45e1514ad285bd; sections 3-4.
// Profile authority: 5d11b464555ac1f40fde3eadfd03c2a3f984f0770d594b30062a4cc641e34204.
using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>The closed EventStore:PayloadProtection:DaprKubernetesCrypto selection: one component and an explicit KEK address inventory.</summary>
/// <remarks>No field carries credentials, an endpoint or key material. The inventory changes only through the two-phase rollout of supplement section 4.</remarks>
/// <param name="ComponentName">The stock crypto.dapr.kubernetes.secrets component name.</param>
/// <param name="CurrentWriteKey">The only address used for new wraps.</param>
/// <param name="RetainedKeys">Distinct older addresses still admitted for exact wrap and unwrap.</param>
internal sealed record DaprCryptographyPayloadProtectionOptions(string ComponentName, string CurrentWriteKey, IReadOnlyList<string> RetainedKeys)
{
    private const int MaximumRetainedKeys = 64;

    /// <summary>Loads and validates only the three approved fields; unknown fields fail validation.</summary>
    internal static DaprCryptographyPayloadProtectionOptions Load(IConfigurationSection section)
    {
        if (section.GetChildren().Any(c => c.Key is not ("ComponentName" or "CurrentWriteKey" or "RetainedKeys")
            || (c.Key != "RetainedKeys" && c.GetChildren().Any()))) { throw new PayloadProtectionFormatException(); }
        var options = new DaprCryptographyPayloadProtectionOptions(section["ComponentName"] ?? "", section["CurrentWriteKey"] ?? "", ReadRetainedKeys(section.GetSection("RetainedKeys")));
        options.Validate();
        return options;
    }

    /// <summary>Reads the retained array; its children must be leaf values at exact zero-based ordinals, even when the section is unused.</summary>
    internal static string[] ReadRetainedKeys(IConfigurationSection retained)
    {
        if (retained.Value is not null) { throw new PayloadProtectionFormatException(); }
        IConfigurationSection[] items = retained.GetChildren().ToArray();
        if (items.Length > MaximumRetainedKeys || items.Any(c => c.GetChildren().Any())) { throw new PayloadProtectionFormatException(); }
        var addresses = new string[items.Length];
        for (int index = 0; index < items.Length; index++)
        {
            // Configuration arrays are exact zero-based ordinals; gaps or names are not an inventory.
            IConfigurationSection item = items.SingleOrDefault(c => c.Key == index.ToString(CultureInfo.InvariantCulture)) ?? throw new PayloadProtectionFormatException();
            addresses[index] = item.Value ?? throw new PayloadProtectionFormatException();
        }

        return addresses;
    }

    /// <summary>Validates the component grammar, every address and the closed distinct inventory.</summary>
    internal void Validate()
    {
        if (ComponentName is not { Length: >= 1 and <= 128 } || ComponentName.Any(c => c is not (>= 'a' and <= 'z' or >= '0' and <= '9' or '-'))
            || RetainedKeys is null || RetainedKeys.Count > MaximumRetainedKeys) { throw new PayloadProtectionFormatException(); }
        ValidateKeyAddress(CurrentWriteKey);
        var seen = new HashSet<string>(StringComparer.Ordinal) { CurrentWriteKey };
        foreach (string address in RetainedKeys)
        {
            ValidateKeyAddress(address);
            if (!seen.Add(address)) { throw new PayloadProtectionFormatException(); }
        }
    }

    /// <summary>Checks that an exact address is the current write key or a retained key.</summary>
    internal bool Admits(string address) => address == CurrentWriteKey || RetainedKeys.Contains(address, StringComparer.Ordinal);

    /// <summary>Validates the exact namespace/secretName/dataKey grammar; the address is the trusted key identity.</summary>
    internal static void ValidateKeyAddress(string? address)
    {
        if (address is not { Length: >= 5 and <= 256 } || !address.All(char.IsAscii)) { throw new PayloadProtectionFormatException(); }
        string[] parts = address.Split('/');
        if (parts.Length != 3 || !IsLabel(parts[0], 63) || !IsSubdomain(parts[1]) || parts[2] is not { Length: >= 1 and <= 63 }
            || parts[2].Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '.' or '_'))) { throw new PayloadProtectionFormatException(); }
    }

    /// <summary>Never formats component names or key addresses.</summary>
    public override string ToString() => nameof(DaprCryptographyPayloadProtectionOptions);

    // DNS-1123 label: lowercase alphanumerics or '-', starting and ending alphanumeric.
    private static bool IsLabel(string value, int maximum) => value.Length >= 1 && value.Length <= maximum
        && value.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-') && value[0] != '-' && value[^1] != '-';

    // DNS-1123 subdomain of 1-128 characters: dot-separated labels with the label alphabet.
    private static bool IsSubdomain(string value) => value.Length is >= 1 and <= 128
        && value.Split('.').All(label => IsLabel(label, 128));
}
