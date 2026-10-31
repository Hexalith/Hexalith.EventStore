namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Closed canonical ACL shapes with the Dapr 1.18 trie semantics, rather than conventional glob semantics.</summary>
public static class RouteCatalogDaprGrant
{
    /// <summary>Returns the literal fence prefix for supported SDK grants; wildcard segments never broaden the finite endpoint table.</summary>
    public static string? Prefix(string grant, bool technical)
    {
        ArgumentNullException.ThrowIfNull(grant);
        string path = grant.Trim('/');
        string[] parts = path.Split('/');
        if (path.Length is 0 or > 2048 || parts.Any(part => part.Length == 0 || part is "." or ".."
            || part.IndexOfAny(['\\', '%', '?', '#', '{', '}']) >= 0)) return null;
        int first = Array.FindIndex(parts, part => part.Contains('*', StringComparison.Ordinal));
        if (first < 0) return "/" + path;
        string suffix = string.Join('/', parts[first..]);
        if (suffix != "*" && (!technical || suffix is not ("**" or "*/*"))) return null;
        if (first == 0 && !technical) return null;
        return "/" + string.Join('/', parts[..first]);
    }

    /// <summary>Matches one retained grant as Dapr's trie does: literals are prefixes, scalar wildcards only select the final requested segment.</summary>
    public static bool Allows(string grant, string path)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(path);
        if (Prefix(grant, technical: true) is null) return false;
        string[] rule = grant.Trim('/').Split('/');
        string[] request = path.Trim('/').Split('/');
        int node = -1;
        for (int index = 0; index < request.Length; index++)
        {
            bool end = index == request.Length - 1;
            int next = node + 1;
            if (next >= rule.Length || !(rule[next].Equals(request[index], StringComparison.OrdinalIgnoreCase)
                || rule[next] == "**" || rule[next] == "*" && end)) return false;
            node = next;
            if (node == rule.Length - 1) return true;
            // Dapr's last-segment fallback can select one terminal wildcard child.
            if (end) return node == rule.Length - 2 && rule[node + 1] is "*" or "**";
        }
        return false;
    }

    /// <summary>Binds the declared wildcard spelling of a compiled endpoint without claiming that spelling permits a concrete request.</summary>
    public static bool Describes(string grant, string pattern)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(pattern);
        if (Prefix(grant, technical: true) is null) return false;
        string[] shape = pattern.Trim('/').Split('/');
        return grant.Trim('/').Equals(string.Join('/', shape.Select(part => part.StartsWith('{') && part.EndsWith('}') ? "*" : part)),
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Returns whether the grant can reach a fixed-depth compiled route, including concrete literal grants over scalar parameters.</summary>
    public static bool Intersects(string grant, string pattern)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(pattern);
        string? prefix = Prefix(grant, technical: true);
        if (prefix is null) return false;
        string[] literal = prefix.Trim('/').Length == 0 ? [] : prefix.Trim('/').Split('/');
        string[] shape = pattern.Trim('/').Split('/');
        for (int index = 0; index < Math.Min(literal.Length, shape.Length); index++)
            if (!(shape[index].StartsWith('{') && shape[index].EndsWith('}'))
                && !literal[index].Equals(shape[index], StringComparison.OrdinalIgnoreCase)) return false;
        string[] rule = grant.Trim('/').Split('/');
        int wildcards = rule.Length - literal.Length;
        if (wildcards == 0 || rule[^1] == "**") return shape.Length >= literal.Length;
        if (wildcards == 1) return shape.Length >= literal.Length && shape.Length <= literal.Length + 1;
        return shape.Length == literal.Length + 1;
    }
}
