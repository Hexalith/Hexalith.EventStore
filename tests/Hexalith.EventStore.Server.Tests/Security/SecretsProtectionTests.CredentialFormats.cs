using System.Text.RegularExpressions;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>Recognizes credential-bearing formats with bounded scalar and argument boundaries.</summary>
public sealed partial class SecretsProtectionTests
{
    private static IEnumerable<int> FindCredentialFormatIndexes(string content)
    {
        foreach (Match constructor in CredentialConstructorPattern().Matches(content))
        {
            bool verbatim = IsVerbatimCredentialSource(content, constructor.Index);
            string invocation = verbatim
                ? content[constructor.Index..(CSharpLiteralEnd(content, content.LastIndexOf("@\"", constructor.Index, StringComparison.Ordinal)) + 1)]
                : ExtractBalancedInvocation(content, constructor.Index, constructor.Length);
            string type = constructor.Groups["type"].Value;
            int secretPosition = type == "NetworkCredential" ? 1 : type == "ClientSecretCredential" ? 2 : 0;
            int lineStart = content.LastIndexOf('\n', Math.Max(0, constructor.Index - 1)) + 1;
            string linePrefix = content[lineStart..constructor.Index];
            bool embedded = verbatim || IsInsideCSharpString(linePrefix, linePrefix.Length);
            bool sourceStringBoundary = embedded && !linePrefix.Contains("\"\"\"", StringComparison.Ordinal)
                && !linePrefix.Contains("@\"", StringComparison.Ordinal);
            var originalOffsets = new List<int>();
            if (embedded && (verbatim || invocation.Contains('\\')))
            {
                var decoded = new System.Text.StringBuilder();
                for (int index = 0; index < invocation.Length; index++)
                {
                    char character = invocation[index];
                    if (verbatim && character == '"' && index + 1 < invocation.Length && invocation[index + 1] == '"')
                        character = invocation[++index];
                    else if (!verbatim && character == '\\' && index + 5 < invocation.Length && invocation[index + 1] == 'u'
                        && ushort.TryParse(invocation.AsSpan(index + 2, 4), System.Globalization.NumberStyles.HexNumber,
                            System.Globalization.CultureInfo.InvariantCulture, out ushort unicode))
                    {
                        character = (char)unicode;
                        index += 5;
                    }
                    else if (!verbatim && character == '\\' && index + 1 < invocation.Length && invocation[index + 1] is '\\' or '"' or '\'')
                        character = invocation[++index];
                    originalOffsets.Add(index);
                    decoded.Append(character);
                }
                invocation = ExtractBalancedInvocation(decoded.ToString(), 0, constructor.Length);
            }
            int ordinal = 0;
            foreach ((int start, string argument) in ReadCredentialArguments(invocation))
            {
                Match named = Regex.Match(argument, @"\A\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*:", RegexOptions.CultureInvariant);
                bool sensitive = named.Success
                    ? named.Groups["name"].Value == (type == "NetworkCredential" ? "password" : type == "ClientSecretCredential" ? "clientSecret" : "key")
                    : ordinal == secretPosition;
                ordinal++;
                if (!sensitive) continue;
                foreach ((int index, _, string literal) in EnumerateQuotedLiterals(argument, includeBackticks: false))
                {
                    // A quote that closes the containing source literal is a source
                    // delimiter, not an argument literal in the generated invocation.
                    bool sourceDelimiter = sourceStringBoundary && originalOffsets.Count == 0 && (index == 0 || argument[index - 1] != '\\') && index + 1 < argument.Length
                        && argument.AsSpan(index + 1).TrimStart().StartsWith("+", StringComparison.Ordinal);
                    if (!sourceDelimiter && !IsInertPlaceholder(literal) && !IsRuntimeSourceLookupLiteral(invocation, start + index))
                        yield return constructor.Index + (originalOffsets.Count == 0 ? start + index : originalOffsets[start + index]);
                }
            }
        }

        foreach (Match option in ShellSecretOptionPattern().Matches(content))
        {
            string name = option.Groups["name"].Value;
            if (!IsCredentialName(name)) continue;
            int start = option.Index + option.Length;
            string value = ReadShellArgument(content, start).Replace("\\\"", "\"", StringComparison.Ordinal);
            // An option inventory supplies no argument, and the finite help-text
            // phrase "Use --option to ..." describes an option rather than invoking it.
            if (value.StartsWith("--", StringComparison.Ordinal)
                || value == "to" && content.AsSpan(0, option.Index).TrimEnd().EndsWith("Use", StringComparison.Ordinal)) continue;
            if (value.Length > 0 && IsUsableLiteral("argument.sh", name, value, isBare: true))
                yield return start;
        }
    }

    private static (int Index, string Scalar) ReadXmlCredentialScalar(Group value)
    {
        string raw = value.Value;
        var scalar = new System.Text.StringBuilder();
        int first = value.Index;
        bool found = false;
        bool cdata = false;
        for (int index = 0; index < raw.Length; index++)
        {
            if (!cdata && raw.AsSpan(index).StartsWith("<![CDATA[", StringComparison.Ordinal)) { cdata = true; index += 8; continue; }
            if (cdata && raw.AsSpan(index).StartsWith("]]>", StringComparison.Ordinal)) { cdata = false; index += 2; continue; }
            if (!cdata && (raw.AsSpan(index).StartsWith("<!--", StringComparison.Ordinal) || raw.AsSpan(index).StartsWith("<?", StringComparison.Ordinal)))
            {
                string close = raw[index + 1] == '?' ? "?>" : "-->";
                int end = raw.IndexOf(close, index + 2, StringComparison.Ordinal);
                if (end >= 0) { index = end + close.Length - 1; continue; }
            }
            char character = raw[index];
            if (!found && !char.IsWhiteSpace(character)) { first = value.Index + index; found = true; }
            scalar.Append(character);
        }
        return (first, scalar.ToString().Trim());
    }

    private static IEnumerable<(int Index, string Argument)> ReadCredentialArguments(string invocation)
    {
        string code = MaskCSharpComments(invocation);
        int start = code.IndexOf('(') + 1;
        int depth = 0;
        for (int index = start; index < code.Length; index++)
        {
            int literal = CSharpLiteralEnd(code, index);
            if (literal > index) { index = literal; continue; }
            char character = code[index];
            if (character == '<') { int generic = CSharpGenericEnd(code, index); if (generic > index) { index = generic; continue; } }
            if (character is '(' or '[' or '{') depth++;
            else if (character is ')' or ']' or '}')
            {
                if (depth == 0) { yield return (start, code[start..index]); yield break; }
                depth--;
            }
            if (character == ',' && depth == 0) { yield return (start, code[start..index]); start = index + 1; }
        }
    }

    private static string ReadShellArgument(string content, int start)
    {
        char quote = '\0';
        bool escapedQuotes = content.AsSpan(start).StartsWith("\\\"", StringComparison.Ordinal);
        bool escapedQuoteOpen = false;
        int depth = 0;
        int index = start;
        for (; index < content.Length; index++)
        {
            char character = content[index];
            if (character is '\r' or '\n' && quote == '\0' && depth == 0) break;
            if (escapedQuotes && character == '\\' && index + 1 < content.Length && content[index + 1] == '"')
            {
                escapedQuoteOpen = !escapedQuoteOpen;
                index++;
                continue;
            }
            if (escapedQuotes && !escapedQuoteOpen && character == '"') break;
            if (character == '\\') { index++; continue; }
            if (quote != '\0')
            {
                if (character == quote) quote = '\0';
                continue;
            }
            if (character is '"' or '\'') { quote = character; continue; }
            if (character == '(') depth++;
            else if (character == ')' && depth > 0) depth--;
            if (!escapedQuoteOpen && depth == 0 && (char.IsWhiteSpace(character) || character is ';' or '|' or '&' or '<' or '>')) break;
        }
        return content[start..Math.Min(index, content.Length)];
    }

    private static IEnumerable<int> FindYamlCredentialIndexes(string path, string content)
    {
        foreach (Match assignment in SecretAssignmentStartPattern().Matches(content))
        {
            Group name = assignment.Groups["name"];
            if (!IsCredentialName(name.Value) || !HasStructuralColonPrefix(content, name.Index)) continue;
            int colon = name.Index + name.Length;
            while (colon < content.Length && content[colon] is '"' or '\'' or ' ' or '\t' or ']') colon++;
            if (colon >= content.Length || content[colon] != ':') continue;
            int ownerStart = content.LastIndexOf('\n', Math.Max(0, name.Index - 1)) + 1;
            int ownerIndent = 0;
            while (ownerStart + ownerIndent < content.Length && content[ownerStart + ownerIndent] is ' ' or '\t') ownerIndent++;
            int start = colon + 1;
            while (start < content.Length && content[start] is ' ' or '\t') start++;
            if (start < content.Length && content[start] == '#')
            {
                start = content.IndexOf('\n', start);
                if (start < 0) continue;
            }
            while (start < content.Length)
            {
                while (start < content.Length && char.IsWhiteSpace(content[start])) start++;
                if (start >= content.Length || content[start] != '#') break;
                start = content.IndexOf('\n', start);
                if (start < 0) { start = content.Length; break; }
            }
            if (start >= content.Length) continue;
            string effective = GetEffectiveSourcePath(path, content, name.Index);
            bool yaml = Path.GetExtension(effective).ToLowerInvariant() is ".yml" or ".yaml";
            bool sequence = content[start] == '-' && start + 1 < content.Length && char.IsWhiteSpace(content[start + 1]);
            if (!yaml && !sequence) continue;
            int scalarLine = content.LastIndexOf('\n', Math.Max(0, start - 1)) + 1;
            if (scalarLine > ownerStart && start - scalarLine <= ownerIndent && !sequence) continue;
            foreach ((int index, string scalar, bool bare) in ReadYamlNode(content, start, ownerIndent, 0))
                if (!GitHubExpressionPattern().IsMatch(scalar) && IsUsableLiteral(effective, name.Value, scalar, bare)) yield return index;
        }
    }

    private static IEnumerable<(int Index, string Scalar, bool Bare)> ReadYamlNode(string content, int start, int ownerIndent, int depth)
    {
        if (depth > 64) { yield return (start, "unreadable-value", false); yield break; }
        bool forceString = false;
        while (start < content.Length && content[start] is '&' or '!')
        {
            int end = start;
            if (content[start] == '!' && start + 1 < content.Length && content[start + 1] == '<')
            {
                end = content.IndexOf('>', start + 2);
                if (end < 0) { yield return (start, content[start..], false); yield break; }
                end++;
            }
            else while (end < content.Length && !char.IsWhiteSpace(content[end])) end++;
            string property = content[start..end];
            forceString |= property.StartsWith('!') && property is not ("!!bool" or "!!null" or "!!int" or "!!float");
            start = end;
            while (start < content.Length && char.IsWhiteSpace(content[start])) start++;
        }
        if (start >= content.Length) yield break;
        if (content[start] == '-' && start + 1 < content.Length && char.IsWhiteSpace(content[start + 1]))
        {
            int cursor = content.LastIndexOf('\n', Math.Max(0, start - 1)) + 1;
            int sequenceIndent = start - cursor;
            while (cursor < content.Length)
            {
                int end = content.IndexOf('\n', cursor);
                if (end < 0) end = content.Length;
                int item = cursor;
                while (item < end && content[item] is ' ' or '\t') item++;
                if (item < end && content[item] is not ('\r' or '#'))
                {
                    if (item - cursor < sequenceIndent || item - cursor == sequenceIndent && content[item] != '-') yield break;
                    if (item - cursor == sequenceIndent && content[item] == '-' && item + 1 < end && char.IsWhiteSpace(content[item + 1]))
                    {
                        item++;
                        while (item < end && content[item] is ' ' or '\t') item++;
                        Match mapping = Regex.Match(content[item..end], @"\A(?<name>[A-Za-z_][A-Za-z0-9_-]*):[\t ]+(?<value>.+)", RegexOptions.CultureInvariant);
                        if (!mapping.Success || IsCredentialName(mapping.Groups["name"].Value))
                            foreach (var scalar in ReadYamlNode(content, mapping.Success ? item + mapping.Groups["value"].Index : item, sequenceIndent, depth + 1)) yield return scalar;
                    }
                }
                cursor = end + 1;
            }
            yield break;
        }
        if (content[start] == '[')
        {
            int cursor = start + 1;
            while (cursor < content.Length)
            {
                while (cursor < content.Length && (char.IsWhiteSpace(content[cursor]) || content[cursor] == ',')) cursor++;
                if (cursor >= content.Length || content[cursor] == ']') yield break;
                int end = YamlFlowMemberEnd(content, cursor);
                foreach (var scalar in ReadYamlNode(content[..end], cursor, ownerIndent, depth + 1)) yield return scalar;
                cursor = end;
            }
            yield break;
        }
        if (content[start] is '"' or '\'')
        {
            char quote = content[start];
            var value = new System.Text.StringBuilder();
            for (int cursor = start + 1; cursor < content.Length; cursor++)
            {
                char character = content[cursor];
                if (character == quote)
                {
                    if (quote == '\'' && cursor + 1 < content.Length && content[cursor + 1] == quote) { value.Append(quote); cursor++; continue; }
                    yield return (start, value.ToString().Trim(), false);
                    yield break;
                }
                if (quote == '"' && character == '\\' && cursor + 1 < content.Length)
                {
                    character = content[++cursor];
                    if (character is '\r' or '\n')
                    {
                        while (cursor + 1 < content.Length && char.IsWhiteSpace(content[cursor + 1])) cursor++;
                        continue;
                    }
                }
                if (character is '\r' or '\n')
                {
                    value.Append(' ');
                    while (cursor + 1 < content.Length && char.IsWhiteSpace(content[cursor + 1])) cursor++;
                }
                else value.Append(character);
            }
            yield return (start, value.ToString(), false);
            yield break;
        }
        int lineStart = content.LastIndexOf('\n', Math.Max(0, start - 1)) + 1;
        int indent = 0;
        while (lineStart + indent < content.Length && content[lineStart + indent] is ' ' or '\t') indent++;
        int lineEnd = content.IndexOf('\n', start);
        if (lineEnd < 0) lineEnd = content.Length;
        string first = TrimYamlComment(content[start..lineEnd]).Trim();
        bool block = Regex.IsMatch(first, @"\A[|>][-+1-9]*\z", RegexOptions.CultureInvariant);
        var parts = new List<string>();
        if (!block) parts.Add(first);
        int scalarStart = start;
        for (int cursor = lineEnd + 1; cursor < content.Length;)
        {
            int end = content.IndexOf('\n', cursor);
            if (end < 0) end = content.Length;
            string line = content[cursor..end];
            int leading = line.Length - line.TrimStart(' ', '\t').Length;
            string scalar = (block ? line : TrimYamlComment(line)).Trim();
            if (scalar.Length > 0)
            {
                if (leading <= indent || !block && scalar.StartsWith("- ", StringComparison.Ordinal)) break;
                if (parts.Count == 0) scalarStart = cursor + leading;
                parts.Add(scalar);
            }
            cursor = end + 1;
        }
        yield return (scalarStart, string.Join(' ', parts), !block && !forceString);
    }

    private static int YamlFlowMemberEnd(string content, int start)
    {
        int depth = 0;
        char quote = '\0';
        for (int index = start; index < content.Length; index++)
        {
            char character = content[index];
            if (quote != '\0')
            {
                if (quote == '"' && character == '\\') index++;
                else if (character == quote)
                {
                    if (quote == '\'' && index + 1 < content.Length && content[index + 1] == quote) index++;
                    else quote = '\0';
                }
                continue;
            }
            if (character is '\'' or '"') quote = character;
            else if (character == '[') depth++;
            else if (character == ']' && depth-- == 0 || character == ',' && depth == 0) return index;
        }
        return content.Length;
    }

    private static string TrimYamlComment(string value)
    {
        for (int index = 0; index < value.Length; index++)
            if (value[index] == '#' && (index == 0 || char.IsWhiteSpace(value[index - 1]))) return value[..index];
        return value;
    }

    private static int CSharpLiteralEnd(string text, int start)
    {
        bool verbatim = text[start] == '@' && start + 1 < text.Length && text[start + 1] == '"';
        int quoteStart = verbatim ? start + 1 : start;
        if (text[quoteStart] is not ('"' or '\'')) return start;
        char quote = text[quoteStart];
        int run = CountRun(text, quoteStart, quote);
        if (quote == '"' && run >= 3)
        {
            int close = text.IndexOf(new string('"', run), quoteStart + run, StringComparison.Ordinal);
            return close < 0 ? text.Length - 1 : close + run - 1;
        }
        for (int index = quoteStart + 1; index < text.Length; index++)
        {
            if (!verbatim && text[index] == '\\') { index++; continue; }
            if (text[index] != quote) continue;
            if (verbatim && index + 1 < text.Length && text[index + 1] == quote) { index++; continue; }
            return index;
        }
        return text.Length - 1;
    }

    private static string MaskCSharpComments(string content)
    {
        char[] masked = content.ToCharArray();
        for (int index = 0; index < content.Length; index++)
        {
            int literal = CSharpLiteralEnd(content, index);
            if (literal > index) { index = literal; continue; }
            if (content[index] != '/' || index + 1 >= content.Length || content[index + 1] is not ('/' or '*')) continue;
            int end = content[index + 1] == '/' ? content.IndexOf('\n', index + 2) : content.IndexOf("*/", index + 2, StringComparison.Ordinal);
            if (end < 0) end = content.Length;
            else if (content[index + 1] == '*') end += 2;
            for (; index < end; index++) if (masked[index] is not ('\r' or '\n')) masked[index] = ' ';
            index--;
        }
        return new string(masked);
    }

    private static int CSharpGenericEnd(string content, int start)
    {
        int depth = 0;
        for (int index = start; index < content.Length; index++)
        {
            char character = content[index];
            if (character == '<') depth++;
            else if (character == '>' && --depth == 0) return index;
            else if (!char.IsLetterOrDigit(character) && !char.IsWhiteSpace(character) && character is not ('_' or '.' or ':' or ',' or '?' or '[' or ']' or '>')) return start;
        }
        return start;
    }

    private static bool IsVerbatimCredentialSource(string content, int index)
    {
        int open = content.LastIndexOf("@\"", index, StringComparison.Ordinal);
        return open >= 0 && !IsInsideCSharpString(content, open) && CSharpLiteralEnd(content, open) >= index;
    }

    private static bool IsCurlTokenAcquisition(string command)
    {
        Match pipeline = Regex.Match(command,
            """\A(?<curl>curl[\t ][\s\S]+?)[\t ]*\|[\t ]*jq[\t ]+-(?:r|er)[\t ]+(?<quote>['"])(?<filter>[\s\S]*?)\k<quote>[\t ]*\z""",
            RegexOptions.CultureInvariant);
        if (!pipeline.Success) return false;
        string curl = Regex.Replace(pipeline.Groups["curl"].Value, @"\\\r?\n", "", RegexOptions.CultureInvariant);
        return IsSingleShellRuntimeCommand(curl)
            && Regex.IsMatch(pipeline.Groups["filter"].Value,
                """\A\s*\.access_token(?:\s*\|\s*select\(\s*type\s*==\s*"string"\s+and\s+length\s*>\s*0\s*\))?\s*\z""", RegexOptions.CultureInvariant);
    }

    private static bool IsPowerShellTokenAcquisition(string candidate)
    {
        string invocation = ExtractBalancedInvocation(candidate, 0, 1);
        string suffix = candidate[invocation.Length..].Trim();
        if (!invocation.EndsWith(')') || suffix.Length > 0 && suffix != ".access_token") return false;
        string command = invocation[1..^1];
        char quote = '\0';
        int hashtable = 0;
        for (int index = 0; index < command.Length; index++)
        {
            char character = command[index];
            if (character == '`' && quote != '\'') { index++; continue; }
            if (quote == '\'')
            {
                if (character == quote)
                {
                    if (index + 1 < command.Length && command[index + 1] == quote) index++;
                    else quote = '\0';
                }
                continue;
            }
            if (character == '$' && index + 1 < command.Length && command[index + 1] == '(') return false;
            if (quote == '"') { if (character == quote) quote = '\0'; continue; }
            if (character is '"' or '\'') quote = character;
            else if (character == '@' && index + 1 < command.Length && command[index + 1] == '{') { hashtable++; index++; }
            else if (character == '}') { if (hashtable-- == 0) return false; }
            else if (character == ';' && hashtable == 0 || character is '|' or '&' or '(' or ')' or '{' or '#' or '\r' or '\n') return false;
        }
        return quote == '\0' && hashtable == 0;
    }

    private static bool IsSingleShellRuntimeCommand(string command)
    {
        char quote = '\0';
        for (int index = 0; index < command.Length; index++)
        {
            char character = command[index];
            if (character == '\\' && quote != '\'') { index++; continue; }
            if (quote == '\'') { if (character == quote) quote = '\0'; continue; }
            if (character == '`' || character == '$' && index + 1 < command.Length && command[index + 1] == '(') return false;
            if (quote == '"') { if (character == quote) quote = '\0'; continue; }
            if (character is '"' or '\'') quote = character;
            else if (character is ';' or '|' or '&' or '\r' or '\n' or '<' or '>') return false;
        }
        return quote == '\0';
    }

    [GeneratedRegex(@"(?<![A-Za-z0-9_-])--(?<name>[A-Za-z][A-Za-z0-9_-]*)(?:[\t ]|\\\r?\n)+", RegexOptions.CultureInvariant)]
    private static partial Regex ShellSecretOptionPattern();
}
