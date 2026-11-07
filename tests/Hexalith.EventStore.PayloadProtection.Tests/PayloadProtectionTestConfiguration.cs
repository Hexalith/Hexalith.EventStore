using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Minimal dictionary section fixture, without a new configuration package dependency.</summary>
internal sealed class PayloadProtectionTestConfiguration(IReadOnlyDictionary<string, string?> values, string path = "") : IConfigurationSection
{
    /// <summary>Gets or sets one exact fixture value.</summary>
    public string? this[string key] { get => values.GetValueOrDefault(Prefix + key); set => throw new NotSupportedException(); }
    /// <summary>Gets the leaf fixture key.</summary>
    public string Key => path.Split(':').Last();
    /// <summary>Gets the fixture path.</summary>
    public string Path => path;
    /// <summary>Gets or sets the section fixture value.</summary>
    public string? Value { get => values.GetValueOrDefault(path); set => throw new NotSupportedException(); }
    private string Prefix => path.Length == 0 ? "" : path + ":";
    /// <summary>Enumerates immediate fixture children.</summary>
    public IEnumerable<IConfigurationSection> GetChildren() => values.Keys.Where(k => k.StartsWith(Prefix, StringComparison.Ordinal))
        .Select(k => k[Prefix.Length..].Split(':')[0]).Distinct(StringComparer.Ordinal).Select(GetSection);
    /// <summary>Gets an exact child fixture section.</summary>
    public IConfigurationSection GetSection(string key) => new PayloadProtectionTestConfiguration(values, Prefix + key);
    /// <summary>Reports an immutable fixture.</summary>
    public IChangeToken GetReloadToken() => new CancellationChangeToken(CancellationToken.None);
}
