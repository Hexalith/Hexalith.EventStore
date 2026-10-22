using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>Decodes bounded source captures without exempting their scalar contents.</summary>
public sealed partial class SecretsProtectionTests
{
    private const int EvidenceDecodeBytes = 256 * 1024 * 1024;
    private const int EvidenceDecodeDepth = 16;

    private static IEnumerable<string> FindDecodedEvidenceViolations(string path, string content)
    {
        int remaining = EvidenceDecodeBytes;
        return ScanDecodedEvidence(path, content, 0, ref remaining);
    }

    private static string[] ScanDecodedEvidence(string path, string content, int depth, ref int remaining)
    {
        // Existing exact seals remain authoritative. Newly decoded captures are scanned in full.
        content = MaskBoundedPythonSecurityProbes(path, content);
        content = MaskSealedPublishedExecutorSource(path, content);
        content = MaskKnownSyntheticBindingTestNames(path, content);
        content = MaskKnownSyntheticTrxTestNames(path, content);
        content = MaskKnownSyntheticXunitTestNames(content);
        string trimmed = content.TrimStart();
        if (trimmed.Length == 0 || trimmed[0] is not ('{' or '['))
        {
            return FindViolationsCore(path, content).ToArray();
        }
        if (depth >= EvidenceDecodeDepth || Encoding.UTF8.GetByteCount(content) > remaining) return [path + ":1"];
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
            ScanEvidenceElement(document.RootElement, null, null, path, content, utf8, ref reader, ref position,
                masked, lines, dependencies, depth, ref remaining);
            // Valid JSON retains JSON semantics even when a capture claims a source extension.
            string jsonPath = path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? path : path + ".json";
            foreach (string violation in FindViolationsCore(jsonPath, new string(masked)))
            {
                lines.Add(int.Parse(violation[(violation.LastIndexOf(':') + 1)..], System.Globalization.CultureInfo.InvariantCulture));
            }
            return lines.Select(line => path + ":" + line).ToArray();
        }
    }

    private static void ScanEvidenceElement(JsonElement element, JsonElement? parent, string? name, string path,
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
                    && IsUsableLiteral("evidence.json", property.Name, value!, isBare: property.Value.ValueKind != JsonValueKind.String))
                    lines.Add(LineNumber(content, GetJsonCharIndex(utf8, (int)reader.TokenStartIndex, ref position)));
                ScanEvidenceElement(property.Value, element, property.Name, path, content, utf8, ref reader, ref position,
                    masked, lines, dependencies, depth, ref remaining);
            }
            reader.Read();
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement child in element.EnumerateArray())
            {
                reader.Read();
                ScanEvidenceElement(child, element, null, path, content, utf8, ref reader, ref position,
                    masked, lines, dependencies, depth, ref remaining);
            }
            reader.Read();
        }
        else if (element.ValueKind == JsonValueKind.String)
        {
            int start = GetJsonCharIndex(utf8, (int)reader.TokenStartIndex, ref position);
            int end = GetJsonCharIndex(utf8, (int)reader.BytesConsumed, ref position);
            string value = element.GetString()!;
            string[]? variants = null;
            string decodedPath = path + ".scalar";
            bool replace = false;
            if (name == "content" && parent is { ValueKind: JsonValueKind.Object } capture
                && capture.TryGetProperty("path", out JsonElement sourcePath) && sourcePath.ValueKind == JsonValueKind.String
                && capture.TryGetProperty("sha256", out JsonElement seal) && seal.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(value))
            {
                string expected = seal.GetString()!;
                if (!Regex.IsMatch(expected, @"\A[0-9a-f]{64}\z", RegexOptions.CultureInvariant)
                    || Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant() != expected)
                {
                    lines.Add(LineNumber(content, start));
                }
                else decodedPath = sourcePath.GetString()!;
                replace = true;
            }
            else if (name == "output" && parent is { ValueKind: JsonValueKind.Object } tool
                && tool.TryGetProperty("exit_code", out JsonElement status) && status.ValueKind == JsonValueKind.Number
                && tool.TryGetProperty("chunk_id", out JsonElement chunk) && chunk.ValueKind == JsonValueKind.String
                && Regex.IsMatch(value, @"(?m)^@@ -[0-9]+(?:,[0-9]+)? \+[0-9]+(?:,[0-9]+)? @@", RegexOptions.CultureInvariant))
            {
                // Read both sides of each source hunk independently. Combining deleted
                // and added lines invents broken source context. Framing and all bytes
                // outside hunks remain ordinary scanned Markdown in both views.
                variants = [SourceDiffView(value, after: false), SourceDiffView(value, after: true)];
                decodedPath = path + ".tool.md";
                replace = true;
            }
            bool escaped = content.AsSpan(start, end - start).Contains('\\');
            if (replace || escaped)
            {
                if (Encoding.UTF8.GetByteCount(value) > remaining)
                {
                    lines.Add(LineNumber(content, start));
                }
                else
                {
                    foreach (string decoded in variants ?? [value])
                    {
                        remaining -= Encoding.UTF8.GetByteCount(decoded);
                        if (remaining < 0 || ScanDecodedEvidence(decodedPath, decoded, depth + 1, ref remaining).Length > 0)
                            lines.Add(LineNumber(content, start));
                    }
                }
            }
            if ((replace || escaped && value.TrimStart().StartsWith('{'))
                && (name is null || !IsCredentialName(name)))
            {
                for (int index = start + 1; index < end - 1; index++)
                    if (masked[index] is not ('\r' or '\n')) masked[index] = ' ';
            }
        }
    }

    private static string SourceDiffView(string value, bool after)
    {
        var result = new StringBuilder();
        bool hunk = false;
        string language = string.Empty;
        MatchCollection headers = Regex.Matches(value, @"(?m)^@@ -[0-9]+(?:,[0-9]+)? \+[0-9]+(?:,[0-9]+)? @@(?<scope>[^\r\n]*)", RegexOptions.CultureInvariant);
        string[] scopes = headers.Select(header => header.Groups["scope"].Value.Trim()).Distinct().ToArray();
        bool pythonFragment = headers.Count >= 2 && scopes.Length == 1
            && Regex.IsMatch(scopes[0], @"\Aclass [A-Za-z_][A-Za-z0-9_]*:\z", RegexOptions.CultureInvariant)
            && Regex.Matches(value, @"(?m)^[ +\-][\t ]+def [A-Za-z_][A-Za-z0-9_]*\([^\r\n]*\):[\t ]*$", RegexOptions.CultureInvariant).Count >= 2;
        foreach (string line in value.Split('\n'))
        {
            if (line.StartsWith("diff --git ", StringComparison.Ordinal)) language = string.Empty;
            if (line.StartsWith("+++ ", StringComparison.Ordinal))
            {
                // A source path determines language; hunk-looking output alone never
                // grants ordinary Python runtime-variable recognition.
                language = Path.GetExtension(line[4..].Trim()) switch
                {
                    ".py" => "p1r-python-fragment",
                    ".cs" => "csharp",
                    ".go" => "go",
                    ".sh" or ".bash" => "bash",
                    ".ps1" => "powershell",
                    ".js" or ".ts" => "javascript",
                    _ => "text",
                };
            }
            if (Regex.IsMatch(line, @"\A@@ -[0-9]+(?:,[0-9]+)? \+[0-9]+(?:,[0-9]+)? @@", RegexOptions.CultureInvariant))
            {
                if (hunk) result.AppendLine("```");
                // The header's function-context text is ordinary scanned Markdown in both
                // views; opening a fence must never drop it from the scan.
                result.AppendLine(line);
                result.AppendLine("```" + (language.Length > 0 ? language : pythonFragment ? "p1r-python-fragment" : "text"));
                hunk = true;
            }
            else if (hunk && line.Length > 0 && line[0] is '+' or '-' or ' ')
            {
                if (line[0] == ' ' || line[0] == (after ? '+' : '-')) result.AppendLine(line[1..]);
            }
            else
            {
                if (hunk) result.AppendLine("```");
                hunk = false;
                result.AppendLine(line);
            }
        }
        if (hunk) result.AppendLine("```");
        return result.ToString();
    }

    private static bool IsCheckoutPersistenceControl(string path, string content, Match assignment)
    {
        // Preserve the original exact-byte requirements on already enrolled history.
        if (SealedHistoricalSecretFiles.ContainsKey(path)
            || assignment.Groups["name"].Value != "persist-credentials" || !assignment.Groups["bare"].Success)
            return false;
        int start = content.LastIndexOf('\n', Math.Max(0, assignment.Groups["name"].Index - 1)) + 1;
        int end = content.IndexOf('\n', start);
        string line = content[start..(end < 0 ? content.Length : end)].TrimEnd('\r');
        Match setting = Regex.Match(line, @"\A(?<indent> *)persist-credentials:[\t ]*(?:true|false)[\t ]*\z", RegexOptions.CultureInvariant);
        if (!setting.Success) return false;
        string prefix = content[Math.Max(0, start - 2048)..start];
        Match step = Regex.Match(prefix,
            @"(?m)^(?<indent> *)- (?:(?:name:[^\r\n]*\r?\n)\k<indent>  )?uses: actions/checkout@[A-Za-z0-9_./-]+(?:[\t ]+#[^\r\n]*)?\r?\n(?<options>(?:(?!^ *- ).*\n)*)\z",
            RegexOptions.CultureInvariant);
        if (!step.Success || setting.Groups["indent"].Length != step.Groups["indent"].Length + 4)
            return false;
        int indentation = step.Groups["indent"].Length;
        return Regex.IsMatch(step.Groups["options"].Value,
            @"\A {" + (indentation + 2) + @"}with:[\t ]*\r?\n(?: {" + (indentation + 4)
                + @"}[A-Za-z0-9_-]+:[^\r\n]*\r?\n)*\z", RegexOptions.CultureInvariant);
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
        if (path.EndsWith(".go", StringComparison.OrdinalIgnoreCase))
        {
            Match field = Regex.Match(line, @"\A\s*(?<name>[A-Za-z_][A-Za-z0-9_]*):[\t ]*(?<runtime>[A-Za-z_][A-Za-z0-9_.]*),\s*\z", RegexOptions.CultureInvariant);
            return field.Success && field.Groups["name"].Index == local
                && IsGoCodePosition(content, name.Index)
                && field.Groups["runtime"].Value is not ("true" or "false");
        }
        if (!Regex.IsMatch(line, @"\A\s*(?:expected|actual)[\t ]*:[\t ]*0x[0-9a-f]+ &grpc\.ClientConn\{", RegexOptions.CultureInvariant)) return false;
        Match value = Regex.Match(line[local..], @"\A(?:PerRPCCredentials:\[\]credentials\.PerRPCCredentials\(nil\)|TransportCredentials:insecure\.insecureTC\{\}|CredsBundle:credentials\.Bundle\(nil\))(?=,|\})", RegexOptions.CultureInvariant);
        return value.Success;
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

    private static bool IsBoundedWrongSecretNegativeTest(string path, string content, Match assignment)
    {
        if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) return false;
        const string branch = "case \"wrong-secret\": " + "se" + "cret = new string('x', 64); break;";
        int position = content.LastIndexOf('\n', Math.Max(0, assignment.Groups["name"].Index - 1)) + 1;
        while (position < content.Length && content[position] is ' ' or '\t') position++;
        if (!content.AsSpan(position).StartsWith(branch, StringComparison.Ordinal)
            || assignment.Groups["name"].Index != position + "case \"wrong-secret\": ".Length
            || !IsCSharpCodePosition(content, position)) return false;
        const string signature = "public void QueryDigestCapabilityCannotWidenAnExactGrant(string fault)";
        int method = content.LastIndexOf(signature, position, StringComparison.Ordinal);
        if (method < 0 || position - method > 8192 || !IsCSharpCodePosition(content, method)) return false;
        int opening = content.IndexOf('{', method);
        int level = 0, closing = -1;
        for (int index = opening; index < Math.Min(content.Length, method + 8192); index++)
        {
            if (content[index] is not ('{' or '}') || !IsCSharpCodePosition(content, index)) continue;
            level += content[index] == '{' ? 1 : -1;
            if (level == 0) { closing = index; break; }
        }
        if (closing < position) return false;
        // Bind the complete wrong-secret branch, switch tail and denial assertion,
        // with no intervening statements that could turn this into a positive fixture.
        const string tail = branch + "\n"
            + "            case \"revoked\": _options.Grants[0].Revoked = true; break;\n"
            + "            case \"expired\": _options.Grants[0].ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1); break;\n"
            + "        }\n        _authority.Issue(client, secret, Scope(details), details).ShouldBeNull();\n    }";
        string normalized = content[method..(closing + 1)].Replace("\r\n", "\n", StringComparison.Ordinal);
        int assertion = content.IndexOf("_authority.Issue(client, secret, Scope(details), details).ShouldBeNull();", position, StringComparison.Ordinal);
        string prefix = content[Math.Max(0, method - 1024)..method];
        Match attributes = Regex.Match(prefix, @"\[Theory\]\s*(?:\[InlineData\(""[^""]*""\)\]\s*)+\z", RegexOptions.CultureInvariant);
        return normalized.EndsWith(tail, StringComparison.Ordinal) && assertion < closing && assertion > position
            && IsCSharpCodePosition(content, assertion)
            && content[method..position].Contains("string client = \"synthetic-client\", " + "se" + "cret = _secret;", StringComparison.Ordinal)
            && content[method..position].Contains("switch (fault)", StringComparison.Ordinal)
            && attributes.Success && attributes.Value.Contains("[InlineData(\"wrong-secret\")]", StringComparison.Ordinal)
            && IsCSharpCodePosition(content, method - prefix.Length + attributes.Index);
    }

    private static bool IsCSharpCodePosition(string content, int target)
    {
        bool line = false, block = false, verbatim = false;
        char quote = '\0';
        int raw = 0;
        for (int index = 0; index < target; index++)
        {
            char character = content[index];
            char next = index + 1 < content.Length ? content[index + 1] : '\0';
            if (line) { line = character != '\n'; continue; }
            if (block) { if (character == '*' && next == '/') { block = false; index++; } continue; }
            if (raw > 0)
            {
                if (character == '"' && CountRun(content, index, '"') >= raw) { index += raw - 1; raw = 0; }
                continue;
            }
            if (quote != '\0')
            {
                if (!verbatim && character == '\\') index++;
                else if (character == quote)
                {
                    if (verbatim && next == '"') index++;
                    else { quote = '\0'; verbatim = false; }
                }
                continue;
            }
            if (character == '/' && next == '/') { line = true; index++; }
            else if (character == '/' && next == '*') { block = true; index++; }
            else if (character == '"' && CountRun(content, index, '"') >= 3) { raw = CountRun(content, index, '"'); index += raw - 1; }
            else if (character is '"' or '\'') { quote = character; verbatim = index > 0 && content[index - 1] == '@'; }
        }
        return !line && !block && quote == '\0' && raw == 0;
    }
}
