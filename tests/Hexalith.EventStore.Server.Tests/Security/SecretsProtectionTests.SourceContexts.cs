using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>Decodes JSON string values without exempting their scalar contents.</summary>
public sealed partial class SecretsProtectionTests
{
    private const int DecodeBudgetBytes = 256 * 1024 * 1024;
    private const int DecodeDepthLimit = 16;

    private static IEnumerable<string> FindDecodedViolations(string path, string content)
    {
        int remaining = DecodeBudgetBytes;
        return ScanDecodedJson(path, content, 0, ref remaining);
    }

    private static string[] ScanDecodedJson(string path, string content, int depth, ref int remaining)
    {
        string trimmed = content.TrimStart();
        if (trimmed.Length == 0 || trimmed[0] is not ('{' or '['))
        {
            return FindViolationsCore(path, content).ToArray();
        }
        if (depth >= DecodeDepthLimit || Encoding.UTF8.GetByteCount(content) > remaining) return [path + ":1"];
        remaining -= Encoding.UTF8.GetByteCount(content);
        JsonDocument document;
        try { document = JsonDocument.Parse(content, new JsonDocumentOptions { MaxDepth = 64 }); }
        catch (JsonException) { return FindViolationsCore(path, content).ToArray(); }
        using (document)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(content);
            var reader = new Utf8JsonReader(utf8, new JsonReaderOptions { MaxDepth = 64 });
            reader.Read();
            var position = (ByteIndex: 0, CharIndex: 0);
            var lines = new SortedSet<int>();
            char[] masked = content.ToCharArray();
            (_, HashSet<int> dependencies) = GetJsonNonSecretMetadata(content);
            ScanJsonElement(document.RootElement, null, path, content, utf8, ref reader, ref position,
                masked, lines, dependencies, depth, ref remaining);
            // Valid JSON retains JSON semantics even when its path claims a source extension.
            string jsonPath = path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? path : path + ".json";
            foreach (string violation in FindViolationsCore(jsonPath, new string(masked)))
            {
                lines.Add(int.Parse(violation[(violation.LastIndexOf(':') + 1)..], System.Globalization.CultureInfo.InvariantCulture));
            }
            return lines.Select(line => path + ":" + line).ToArray();
        }
    }

    private static void ScanJsonElement(JsonElement element, string? name, string path,
        string content, byte[] utf8, ref Utf8JsonReader reader, ref (int ByteIndex, int CharIndex) position,
        char[] masked, SortedSet<int> lines, HashSet<int> dependencies, int depth, ref int remaining)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                reader.Read();
                int key = GetJsonCharIndex(utf8, (int)reader.TokenStartIndex, ref position) + 1;
                if (!names.Add(property.Name)) lines.Add(LineNumber(content, key));
                reader.Read();
                // Property names are decoded by the JSON reader. Escaping a key
                // cannot detach a credential string from its owning field.
                bool scalar = property.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False;
                string? value = !scalar ? null : property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString() : property.Value.GetRawText();
                if (Regex.IsMatch(property.Name, @"\A[A-Za-z_][A-Za-z0-9_.:-]*\z", RegexOptions.CultureInvariant)
                    && IsCredentialName(property.Name) && !dependencies.Contains(key) && scalar
                    && !IsPackageDependencyMetadata(path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? path : path + ".json",
                        content, key, property.Name, value!)
                    && IsUsableLiteral("decoded.json", property.Name, value!, isBare: property.Value.ValueKind != JsonValueKind.String))
                    lines.Add(LineNumber(content, GetJsonCharIndex(utf8, (int)reader.TokenStartIndex, ref position)));
                ScanJsonElement(property.Value, property.Name, path, content, utf8, ref reader, ref position,
                    masked, lines, dependencies, depth, ref remaining);
            }
            reader.Read();
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement child in element.EnumerateArray())
            {
                reader.Read();
                ScanJsonElement(child, null, path, content, utf8, ref reader, ref position,
                    masked, lines, dependencies, depth, ref remaining);
            }
            reader.Read();
        }
        else if (element.ValueKind == JsonValueKind.String)
        {
            int start = GetJsonCharIndex(utf8, (int)reader.TokenStartIndex, ref position);
            int end = GetJsonCharIndex(utf8, (int)reader.BytesConsumed, ref position);
            string value = element.GetString()!;
            // An escaped string can hide assignment syntax from the raw scan, so its decoded value is scanned too.
            bool escaped = content.AsSpan(start, end - start).Contains('\\');
            if (escaped)
            {
                if (Encoding.UTF8.GetByteCount(value) > remaining)
                {
                    lines.Add(LineNumber(content, start));
                }
                else
                {
                    remaining -= Encoding.UTF8.GetByteCount(value);
                    if (remaining < 0 || ScanDecodedJson(path + ".scalar", value, depth + 1, ref remaining).Length > 0)
                        lines.Add(LineNumber(content, start));
                }
            }
            if (escaped && value.TrimStart().StartsWith('{')
                && (name is null || !IsCredentialName(name)))
            {
                for (int index = start + 1; index < end - 1; index++)
                    if (masked[index] is not ('\r' or '\n')) masked[index] = ' ';
            }
        }
    }

    private static bool IsBoundedPythonCallOrDictionary(string path, string content, Match assignment)
    {
        if (!path.EndsWith(".py", StringComparison.OrdinalIgnoreCase)) return false;
        Group name = assignment.Groups["name"];
        int start = content.LastIndexOf('\n', Math.Max(0, name.Index - 1)) + 1;
        int end = content.IndexOf('\n', name.Index);
        end = end < 0 ? content.Length : end;
        string line = content[start..end].TrimEnd('\r');
        int local = name.Index - start;
        // A keyword is a runtime boolean expression, never a quoted value or literal concatenation.
        Match keyword = Regex.Match(line, @"\b(?<name>[A-Za-z_][A-Za-z0-9_]*)=(?<first>[A-Za-z_][A-Za-z0-9_.]*) or (?<second>[A-Za-z_][A-Za-z0-9_.]*)(?=,|\))", RegexOptions.CultureInvariant);
        if (keyword.Success && keyword.Groups["name"].Index == local
            && IsPythonRuntimeReference(keyword.Groups["first"].Value) && IsPythonRuntimeReference(keyword.Groups["second"].Value)
            && !IsInsidePythonSourceLiteral(path, content, name.Index)
            && HasCompletePythonSourcePrefix(path, content, start)) return true;
        Match dictionary = Regex.Match(line, @"\A[\t ]*(?<name>self\.[A-Za-z_][A-Za-z0-9_]*)[\t ]*=[\t ]*\{""[A-Za-z_][A-Za-z0-9_]*"": f""(?<body>(?:\\.|[^""\\])*)""\}[\t ]*\z", RegexOptions.CultureInvariant);
        if (dictionary.Success && dictionary.Groups["name"].Index == local
            && HasCompletePythonSourcePrefix(path, content, start))
        {
            // This credential container holds a broker endpoint, whose password slot
            // must be exactly one runtime value. Arbitrary dictionaries/f-string
            // literal operands cannot inherit this structured endpoint recognition.
            if (dictionary.Groups["name"].Value != "self.broker_secret") return false;
            Match endpoint = Regex.Match(line, @"\A[\t ]*self\.broker_secret = \{(?:""connectionString"": f""amqps://qualification:\{(?<password>[A-Za-z_][A-Za-z0-9_.]*)\}@127\.0\.0\.1:\{(?<port>[A-Za-z_][A-Za-z0-9_.]*)\}/%2F""|""brokers"": f""127\.0\.0\.1:\{(?<port>[A-Za-z_][A-Za-z0-9_.]*)\}"")\}[\t ]*\z", RegexOptions.CultureInvariant);
            return endpoint.Success && IsPythonRuntimeReference(endpoint.Groups["port"].Value)
                && (!endpoint.Groups["password"].Success || IsPythonRuntimeReference(endpoint.Groups["password"].Value));
        }
        return false;
    }

    private static bool IsBoundedPythonRuntimeUri(string path, string content, Match uri)
    {
        if (!path.EndsWith(".py", StringComparison.OrdinalIgnoreCase)) return false;
        Group user = uri.Groups["userinfo"];
        int colon = user.Value.IndexOf(':');
        if (colon < 0 || !Regex.IsMatch(user.Value[(colon + 1)..], @"\A\{[A-Za-z_][A-Za-z0-9_.]*\}\z", RegexOptions.CultureInvariant)) return false;
        string expression = user.Value[(colon + 2)..^1];
        if (!IsPythonRuntimeReference(expression)) return false;
        int start = content.LastIndexOf('\n', Math.Max(0, uri.Index - 1)) + 1;
        int end = content.IndexOf('\n', uri.Index);
        end = end < 0 ? content.Length : end;
        string line = content[start..end].TrimEnd('\r');
        Match owner = Regex.Match(line, @"\A[\t ]*self\.[A-Za-z_][A-Za-z0-9_]*(?:\[""[A-Za-z_][A-Za-z0-9_]*""\])?[\t ]*=[\t ]*(?:\{""[A-Za-z_][A-Za-z0-9_]*"": )?f""(?<body>(?:\\.|[^""\\])*)""\}?[\t ]*\z", RegexOptions.CultureInvariant);
        return owner.Success && owner.Groups["body"].Index <= uri.Index - start
            && HasCompletePythonSourcePrefix(path, content, start)
            && !PythonBoundedInterpolationPattern().Replace(owner.Groups["body"].Value,
                match => IsPythonRuntimeReference(match.Groups["runtime"].Value) ? string.Empty : match.Value).Contains('{');
    }

    private static bool IsBoundedGoRuntimeValue(string path, string content, Match assignment)
    {
        int start = content.LastIndexOf('\n', Math.Max(0, assignment.Index - 1)) + 1;
        int end = content.IndexOf('\n', assignment.Index);
        end = end < 0 ? content.Length : end;
        string line = content[start..end];
        Group name = assignment.Groups["name"];
        int local = name.Index - start;
        if (!path.EndsWith(".go", StringComparison.OrdinalIgnoreCase)) return false;
        Match field = Regex.Match(line, @"\A\s*(?<name>[A-Za-z_][A-Za-z0-9_]*):[\t ]*(?<runtime>[A-Za-z_][A-Za-z0-9_.]*),\s*\z", RegexOptions.CultureInvariant);
        return field.Success && field.Groups["name"].Index == local
            && IsGoCodePosition(content, name.Index)
            && field.Groups["runtime"].Value is not ("true" or "false");
    }

    private static bool IsGoCodePosition(string content, int target)
    {
        bool line = false, block = false;
        char quote = '\0';
        for (int index = 0; index < target; index++)
        {
            char character = content[index];
            char next = index + 1 < content.Length ? content[index + 1] : '\0';
            if (line) { line = character != '\n'; continue; }
            if (block) { if (character == '*' && next == '/') { block = false; index++; } continue; }
            if (quote != '\0')
            {
                if (quote != '`' && character == '\\') index++;
                else if (character == quote) quote = '\0';
                continue;
            }
            if (character == '/' && next == '/') { line = true; index++; }
            else if (character == '/' && next == '*') { block = true; index++; }
            else if (character is '"' or '\'' or '`') quote = character;
        }
        return !line && !block && quote == '\0';
    }
}
