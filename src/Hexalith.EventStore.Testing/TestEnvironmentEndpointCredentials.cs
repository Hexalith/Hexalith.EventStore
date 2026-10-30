namespace Hexalith.EventStore.Testing;

using System.Text;
using System.Text.Json;

/// <summary>Reads only explicit, privately held Dapr API credentials for exact supplied endpoint bindings.</summary>
internal static class TestEnvironmentEndpointCredentials
{
    /// <summary>The fixed private sibling file.</summary>
    internal const string FileName = "native-endpoint-credentials.json";
    /// <summary>The explicit separate private channel binding.</summary>
    internal const string EnvironmentVariable = "HEXALITH_TEST_ENDPOINT_CREDENTIALS_PATH";
    /// <summary>The private credential document version.</summary>
    internal const string Schema = "hexalith.test-endpoint-credentials.v1";
    /// <summary>The maximum private UTF-8 document size.</summary>
    internal const int MaximumBytes = 65536;

    /// <summary>Captures bounded credentials without exposing their source text in a refusal.</summary>
    /// <param name="path">The explicit private channel path.</param>
    /// <param name="descriptorPath">The supplied owner descriptor path that anchors the fixed sibling file.</param>
    /// <param name="environment">The validated owner identity and endpoint bindings.</param>
    /// <returns>Only exact private endpoint values, held within the consumer.</returns>
    internal static Dictionary<(string Resource, string Module, string Endpoint), string> Read(string path, string descriptorPath, TestEnvironmentDescriptor environment)
    {
        try
        {
            string expected = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(descriptorPath))!, FileName);
            if (!Path.IsPathFullyQualified(path) || Path.GetFullPath(path) != expected || !HasUnlinkedParents(path)) throw new InvalidDataException();
            using FileStream stream = TestEnvironmentDescriptorFile.OpenWithoutBlocking(path);
            if (!stream.CanSeek || stream.Length is <= 0 or > MaximumBytes) throw new InvalidDataException();
            if (!OperatingSystem.IsWindows())
            {
                UnixFileMode mode = File.GetUnixFileMode(stream.SafeFileHandle);
                if ((mode & ~(UnixFileMode.UserRead | UnixFileMode.UserWrite)) != 0 || (mode & UnixFileMode.UserRead) == 0) throw new InvalidDataException();
            }
            using var content = new MemoryStream();
            byte[] buffer = new byte[8192];
            int read;
            while ((read = stream.Read(buffer)) > 0)
            {
                if (content.Length + read > MaximumBytes) throw new InvalidDataException();
                content.Write(buffer, 0, read);
            }
            string json = new UTF8Encoding(false, true).GetString(content.ToArray());
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
            JsonElement root = document.RootElement;
            RequireProperties(root, "schema", "runId", "syntheticTenantId", "dataIsolationId", "endpoints");
            if (root.GetProperty("schema").GetString() != Schema || root.GetProperty("runId").GetString() != environment.RunId
                || root.GetProperty("syntheticTenantId").GetString() != environment.SyntheticTenantId
                || root.GetProperty("dataIsolationId").GetString() != environment.DataIsolationId) throw new InvalidDataException();
            JsonElement entries = root.GetProperty("endpoints");
            if (entries.ValueKind != JsonValueKind.Array || entries.GetArrayLength() is < 1 or > 32) throw new InvalidDataException();
            var result = new Dictionary<(string Resource, string Module, string Endpoint), string>();
            foreach (JsonElement entry in entries.EnumerateArray())
            {
                RequireProperties(entry, "resourceId", "moduleId", "endpointName", "uri", "headerName", "value");
                string resource = entry.GetProperty("resourceId").GetString()!;
                string module = entry.GetProperty("moduleId").GetString()!;
                string name = entry.GetProperty("endpointName").GetString()!;
                Uri endpoint = environment.GetEndpoint(resource, module, name);
                string? value = entry.GetProperty("value").GetString();
                if (entry.GetProperty("uri").GetString() != endpoint.AbsoluteUri || !IsInvocationEndpoint(endpoint)
                    || entry.GetProperty("headerName").GetString() != "dapr-api-token" || !IsToken(value)
                    || !result.TryAdd((resource, module, name), value!)) throw new InvalidDataException();
            }
            return result;
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException
            or NotSupportedException or JsonException or DecoderFallbackException or KeyNotFoundException or TestEnvironmentDescriptorException)
        {
            throw new TestEnvironmentDescriptorException("HXT110", "Require a private bounded credential file with exact run, isolation, resource, module, endpoint and URI bindings.");
        }
    }

    /// <summary>Accepts only the canonical supplied Dapr invocation prefix; no target is constructed by the consumer.</summary>
    /// <param name="endpoint">The explicit supplied URI.</param>
    /// <returns>Whether the URI is safe for an invocation-bound credential.</returns>
    internal static bool IsInvocationEndpoint(Uri endpoint)
    {
        string[] segments = endpoint.AbsolutePath.Split('/');
        return endpoint.IsAbsoluteUri && endpoint.IsLoopback && endpoint.Scheme is "http" or "https"
            && endpoint.UserInfo.Length == 0 && endpoint.Query.Length == 0 && endpoint.Fragment.Length == 0
            && endpoint.OriginalString == endpoint.AbsoluteUri && segments.Length >= 6 && segments[0].Length == 0
            && segments[1] == "v1.0" && segments[2] == "invoke" && segments[3].Length > 0
            && segments[3].All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.')
            && segments[3] is not ("." or "..") && segments[4] == "method" && segments[^1].Length == 0
            && segments.Skip(5).SkipLast(1).All(segment => segment.Length > 0 && segment is not ("." or "..")
                && segment.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.'));
    }

    private static bool IsToken(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 8192
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '.' or '_' or '~' or '+' or '/' or '=');

    private static void RequireProperties(JsonElement value, params string[] names)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new InvalidDataException();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in value.EnumerateObject())
            if (!names.Contains(property.Name, StringComparer.Ordinal) || !seen.Add(property.Name)) throw new InvalidDataException();
        if (seen.Count != names.Length) throw new InvalidDataException();
    }

    private static bool HasUnlinkedParents(string path)
    {
        for (DirectoryInfo? directory = Directory.GetParent(path); directory is not null; directory = directory.Parent)
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0) return false;
        return true;
    }
}
