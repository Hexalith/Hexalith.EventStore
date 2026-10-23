using System.Text.RegularExpressions;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>Recognizes bounded Python source contexts.</summary>
public sealed partial class SecretsProtectionTests
{
    private static bool IsPythonRuntimeReference(string value)
        => PythonReferenceExpressionPattern().IsMatch(value)
            && value.Split('.').All(static segment => !PythonReservedWordPattern().IsMatch(segment));

    private static bool HasCompletePythonSourcePrefix(string path, string content, int target)
    {
        if (!TryGetPythonSourceStart(path, content, target, out int start)) { return false; }
        string prefix = content[start..target];
        var delimiters = new Stack<char>();
        char quote = '\0';
        bool triple = false;
        bool comment = false;
        for (int index = 0; index < prefix.Length; index++)
        {
            char character = prefix[index];
            if (comment) { comment = character != '\n'; continue; }
            if (quote != '\0')
            {
                if (character == '\\') { index++; }
                else if (character == quote && (!triple
                    || index + 2 < prefix.Length && prefix[index + 1] == quote && prefix[index + 2] == quote))
                {
                    quote = '\0';
                    if (triple) { index += 2; triple = false; }
                }
                continue;
            }
            if (character == '#') { comment = true; }
            else if (character is '\'' or '"')
            {
                quote = character;
                triple = index + 2 < prefix.Length && prefix[index + 1] == quote && prefix[index + 2] == quote;
                if (triple) { index += 2; }
            }
            else if (character is '(' or '[' or '{') { delimiters.Push(character); }
            else if (character is ')' or ']' or '}')
            {
                if (delimiters.Count == 0 || (delimiters.Pop(), character) is not (('(', ')') or ('[', ']') or ('{', '}')))
                {
                    return false;
                }
            }
        }
        return quote == '\0' && !comment && !prefix.TrimEnd().EndsWith('\\') && delimiters.Count == 0;
    }

    private static bool IsInsidePythonSourceLiteral(string path, string content, int target)
        => !TryGetPythonSourceStart(path, content, target, out int start)
            || IsInsidePythonLiteral(content[start..], target - start);

    private static bool TryGetPythonSourceStart(string path, string content, int target, out int start)
    {
        start = 0;
        if (!path.EndsWith(".md.py", StringComparison.OrdinalIgnoreCase)) { return true; }
        foreach (Match fence in ClosedMarkdownShellFencePattern().Matches(content))
        {
            Group body = fence.Groups["body"];
            foreach ((int offset, int length) in GetOwnedPythonHereDocuments(body.Value))
            {
                int documentStart = body.Index + offset;
                if (target >= documentStart && target < documentStart + length)
                {
                    start = documentStart;
                    return true;
                }
            }
        }
        foreach (Match fence in ClosedMarkdownPythonFencePattern().Matches(content))
        {
            Group source = fence.Groups["body"];
            if (target >= source.Index && target < source.Index + source.Length)
            {
                start = source.Index;
                return true;
            }
        }
        return false;
    }

    private static bool IsInsidePythonLiteral(string content, int target)
    {
        char quote = '\0';
        bool triple = false;
        bool comment = false;
        for (int index = 0; index < target; index++)
        {
            char character = content[index];
            if (comment)
            {
                comment = character != '\n';
            }
            else if (quote != '\0')
            {
                if (character == '\\') { index++; }
                else if (character == quote)
                {
                    if (!triple) { quote = '\0'; }
                    else if (index + 2 < target && content[index + 1] == quote && content[index + 2] == quote)
                    {
                        quote = '\0';
                        triple = false;
                        index += 2;
                    }
                }
            }
            else if (character == '#') { comment = true; }
            else if (character is '\'' or '"')
            {
                quote = character;
                triple = index + 2 < target && content[index + 1] == quote && content[index + 2] == quote;
                if (triple) { index += 2; }
            }
        }
        return quote != '\0' || comment;
    }

    private static bool IsInClosedMarkdownPythonHereDocument(string content, int target)
    {
        foreach (Match fence in ClosedMarkdownShellFencePattern().Matches(content))
        {
            Group body = fence.Groups["body"];
            foreach ((int offset, int length) in GetOwnedPythonHereDocuments(body.Value))
            {
                int start = body.Index + offset;
                if (target >= start && target < start + length) { return true; }
            }
        }
        return false;
    }

    private static IReadOnlyList<(int Start, int Length)> GetOwnedPythonHereDocuments(string body)
    {
        // Accept only a sequence of top-level supported commands, blank lines and shell comments.
        // Unsupported shell data/quotes and malformed/nested documents reject the complete fence;
        // discovery never restarts at text inside an enclosing document.
        var documents = new List<(int Start, int Length)>();
        int cursor = 0;
        while (cursor < body.Length)
        {
            int end = body.IndexOf('\n', cursor);
            end = end < 0 ? body.Length : end;
            string line = body[cursor..end].TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#')) { cursor = end + 1; continue; }
            Match opening = PythonHereDocumentOpeningPattern().Match(line);
            if (!opening.Success) { return []; }
            int start = end + 1;
            cursor = start;
            bool closed = false;
            while (cursor < body.Length)
            {
                end = body.IndexOf('\n', cursor);
                end = end < 0 ? body.Length : end;
                line = body[cursor..end].TrimEnd('\r');
                if (line == opening.Groups["delimiter"].Value)
                {
                    documents.Add((start, cursor - start));
                    cursor = end + 1;
                    closed = true;
                    break;
                }
                if (PythonHereDocumentOpeningPattern().IsMatch(line)) { return []; }
                cursor = end + 1;
            }
            if (!closed) { return []; }
        }
        return documents;
    }

    [GeneratedRegex(@"\{(?<runtime>[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*)\}", RegexOptions.CultureInvariant)]
    private static partial Regex PythonBoundedInterpolationPattern();

    [GeneratedRegex(@"(?m)^(?<fence>```|~~~)(?:bash|shell|sh)[\t ]*\r?\n(?<body>(?:(?!^(?:```|~~~)).*\n)*?)\k<fence>[\t ]*(?:\r?$)", RegexOptions.CultureInvariant)]
    private static partial Regex ClosedMarkdownShellFencePattern();

    [GeneratedRegex(@"(?m)^(?<fence>```|~~~)(?:python|py)[\t ]*\r?\n(?<body>(?:(?!^(?:```|~~~)).*\n)*?)\k<fence>[\t ]*(?:\r?$)", RegexOptions.CultureInvariant)]
    private static partial Regex ClosedMarkdownPythonFencePattern();

    [GeneratedRegex(@"\Apython3?[\t ]+-[\t ]+<<(?<quote>['""]?)(?<delimiter>[A-Za-z_][A-Za-z0-9_]*)\k<quote>[\t ]*\z", RegexOptions.CultureInvariant)]
    private static partial Regex PythonHereDocumentOpeningPattern();

    [GeneratedRegex(@"\A(?:False|None|True|and|as|assert|async|await|break|class|continue|def|del|elif|else|except|finally|for|from|global|if|import|in|is|lambda|nonlocal|not|or|pass|raise|return|try|while|with|yield)\z", RegexOptions.CultureInvariant)]
    private static partial Regex PythonReservedWordPattern();
}
