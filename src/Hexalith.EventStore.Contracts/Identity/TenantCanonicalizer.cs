namespace Hexalith.EventStore.Contracts.Identity;

/// <summary>Canonicalizes public tenant identifiers at request and grant boundaries.</summary>
public static class TenantCanonicalizer
{
    /// <summary>The reserved historical storage partition, which is never a public tenant.</summary>
    public const string ReservedStorageTenant = "system";

    /// <summary>Canonicalizes one explicit public tenant without trimming malformed input.</summary>
    /// <param name="value">The tenant supplied by a request or a grant.</param>
    /// <param name="canonicalTenant">The lowercase tenant when the input is valid.</param>
    /// <returns>Whether the identifier satisfies the AD-27 public tenant contract.</returns>
    public static bool TryCanonicalize(string? value, out string canonicalTenant)
    {
        canonicalTenant = string.Empty;
        if (value is null or { Length: < 1 or > 64 })
        {
            return false;
        }

        string candidate = value.ToLowerInvariant();
        if (candidate == ReservedStorageTenant || !IsAlphanumeric(candidate[0]) || !IsAlphanumeric(candidate[^1]))
        {
            return false;
        }

        foreach (char character in candidate)
        {
            if (!IsAlphanumeric(character) && character != '-')
            {
                return false;
            }
        }

        canonicalTenant = candidate;
        return true;
    }

    /// <summary>Canonicalizes all grants and rejects malformed or duplicate normalized entries.</summary>
    /// <param name="grants">The caller's tenant grants.</param>
    /// <param name="canonicalGrants">Distinct canonical grants when every grant is valid.</param>
    /// <returns>Whether the grant set is unambiguous and valid.</returns>
    public static bool TryCanonicalizeGrants(IEnumerable<string?> grants, out IReadOnlySet<string> canonicalGrants)
    {
        ArgumentNullException.ThrowIfNull(grants);
        var values = new HashSet<string>(StringComparer.Ordinal);
        foreach (string? grant in grants)
        {
            if (!TryCanonicalize(grant, out string canonical) || !values.Add(canonical))
            {
                canonicalGrants = new HashSet<string>(StringComparer.Ordinal);
                return false;
            }
        }

        canonicalGrants = values;
        return true;
    }

    /// <summary>Checks the distinct lowercase platform namespace grammar used by catalog declarations.</summary>
    /// <param name="value">The explicitly requested namespace.</param>
    /// <returns>Whether the value has the canonical platform namespace shape.</returns>
    public static bool IsPlatformScope(string? value)
    {
        if (value is null || !value.StartsWith("platform:", StringComparison.Ordinal) || value.Length <= "platform:".Length)
        {
            return false;
        }

        ReadOnlySpan<char> suffix = value.AsSpan("platform:".Length);
        if (!IsAlphanumeric(suffix[0]) || !IsAlphanumeric(suffix[^1]))
        {
            return false;
        }

        foreach (char character in suffix)
        {
            if (!IsAlphanumeric(character) && character != '-')
            {
                return false;
            }
        }

        return !suffix.Contains("--", StringComparison.Ordinal);
    }

    private static bool IsAlphanumeric(char value) => value is >= 'a' and <= 'z' or >= '0' and <= '9';
}
