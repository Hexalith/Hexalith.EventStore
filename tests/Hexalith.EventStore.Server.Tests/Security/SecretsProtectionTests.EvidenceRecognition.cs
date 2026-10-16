using System.Text.RegularExpressions;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>Recognizes bounded Python source and demonstrably inert security probes.</summary>
public sealed partial class SecretsProtectionTests
{
    private static readonly string NegativeProbeMarker = "fixture-" + "secret";
    private static readonly string InspectionProbeMarker = "fixture-private-" + "value";

    private static bool IsBoundedPythonSourceAssignment(string path, string content, Match assignment)
    {
        if (!path.EndsWith(".py", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        Group name = assignment.Groups["name"];
        int start = content.LastIndexOf('\n', Math.Max(0, name.Index - 1)) + 1;
        int end = content.IndexOf('\n', name.Index);
        end = end < 0 ? content.Length : end;
        string line = content[start..end].TrimEnd('\r');
        int local = name.Index - start;
        Match mutation = PythonAccountTupleMutationPattern().Match(line);
        if (mutation.Success
            && (mutation.Groups["oldname"].Index == local || mutation.Groups["newname"].Index == local)
            && IsPythonRuntimeReference(mutation.Groups["first"].Value)
            && IsPythonRuntimeReference(mutation.Groups["second"].Value)
            && HasCompletePythonSourcePrefix(path, content, start, allowMutationList: true))
        {
            return true;
        }
        Match tuple = PythonAccountTuplePattern().Match(line, local);
        if (tuple.Success && tuple.Index == local
            && IsPythonRuntimeReference(tuple.Groups["first"].Value)
            && IsPythonRuntimeReference(tuple.Groups["second"].Value))
        {
            int after = tuple.Index + tuple.Length;
            string before = line[..local];
            return string.IsNullOrWhiteSpace(before)
                && string.IsNullOrWhiteSpace(line[after..])
                && HasCompletePythonSourcePrefix(path, content, start);
        }

        Match concatenation = PythonEnvironmentWritePattern().Match(line);
        if (concatenation.Success
            && concatenation.Groups["field"].Index == local
            && IsPythonRuntimeReference(concatenation.Groups["runtime"].Value)
            && HasCompletePythonSourcePrefix(path, content, start))
        {
            return true;
        }

        Match interpolated = PythonInterpolatedAssignmentPattern().Match(line);
        bool hasAdjacentContent = !interpolated.Success;
        if (!interpolated.Success && end < content.Length)
        {
            // The maintained executor uses exactly two adjacent strings in one parenthesized assignment.
            int nextEnd = content.IndexOf('\n', end + 1);
            nextEnd = nextEnd < 0 ? content.Length : nextEnd;
            interpolated = PythonParenthesizedInterpolationPattern().Match(content[start..nextEnd]);
        }
        if (!interpolated.Success || !HasCompletePythonSourcePrefix(path, content, start))
        {
            return false;
        }

        Group body = interpolated.Groups["body"];
        if (hasAdjacentContent && (body.Length == 0 || body.Value[^1] is not (' ' or '\t' or ';')))
        {
            return false;
        }
        string staticText = PythonBoundedInterpolationPattern().Replace(body.Value, match =>
            IsPythonRuntimeReference(match.Groups["runtime"].Value) ? string.Empty : match.Value);
        if (staticText.Contains('{') || staticText.Contains('}'))
        {
            return false;
        }

        Match field = PythonInterpolatedFieldPattern().Match(body.Value, Math.Max(0, local - body.Index));
        if (!field.Success || body.Index + field.Index != local
            || !IsPythonRuntimeReference(field.Groups["runtime"].Value))
        {
            return false;
        }
        int afterField = field.Index + field.Length;
        return afterField == body.Length || body.Value[afterField] is ' ' or '\t' or ';';
    }

    private static bool IsPythonRuntimeReference(string value)
        => PythonReferenceExpressionPattern().IsMatch(value)
            && value.Split('.').All(static segment => !PythonReservedWordPattern().IsMatch(segment));

    private static bool HasCompletePythonSourcePrefix(string path, string content, int target, bool allowMutationList = false)
    {
        if (!TryGetPythonSourceStart(path, content, target, out int start)) { return false; }
        string prefix = content[start..target];
        var delimiters = new Stack<(char Symbol, int Index)>();
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
            else if (character is '(' or '[' or '{') { delimiters.Push((character, index)); }
            else if (character is ')' or ']' or '}')
            {
                if (delimiters.Count == 0 || (delimiters.Pop().Symbol, character) is not (('(', ')') or ('[', ']') or ('{', '}')))
                {
                    return false;
                }
            }
        }
        if (quote != '\0' || comment || prefix.TrimEnd().EndsWith('\\')) { return false; }
        if (delimiters.Count == 0) { return true; }
        if (!allowMutationList || delimiters.Count != 1 || delimiters.Peek().Symbol != '[') { return false; }
        int open = delimiters.Peek().Index;
        int lineStart = prefix.LastIndexOf('\n', Math.Max(0, open - 1)) + 1;
        if (prefix[lineStart..(open + 1)].Trim() != "faults = [") { return false; }
        foreach (Match closure in PythonFaultListClosingPattern().Matches(content, target))
        {
            int close = closure.Groups["close"].Index;
            if (!TryGetPythonSourceStart(path, content, close, out int closeSource) || closeSource != start)
            {
                return false;
            }
            // Reuse the bounded delimiter check through this candidate closing bracket;
            // a quoted or mismatched bracket cannot close the owning source list.
            if (HasCompletePythonSourcePrefix(path, content, close + 1)) { return true; }
        }
        return false;
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

    private static string MaskBoundedPythonSecurityProbes(string path, string content)
    {
        if (!path.EndsWith(".py", StringComparison.OrdinalIgnoreCase)) { return content; }
        content = content.Replace("\r\n", "\n", StringComparison.Ordinal);
        char[]? masked = null;
        foreach ((string block, string marker, bool rejection) in BoundedPythonProbeBlocks())
        {
            int position = 0;
            while ((position = content.IndexOf(block, position, StringComparison.Ordinal)) >= 0)
            {
                int end = position + block.Length;
                bool completeMethod = IsCompletePythonMethodBlock(content, position, end);
                if (completeMethod && IsInPythonUnitTestClass(content, position, rejection)
                    && !IsPrecededByPythonDecorator(content, position)
                    && !IsInsidePythonLiteral(content, position))
                {
                    int markerPosition = block.IndexOf(marker, StringComparison.Ordinal);
                    masked ??= content.ToCharArray();
                    // Mask only this fixture occurrence; assertions and every surrounding byte stay visible.
                    Array.Fill(masked, ' ', position + markerPosition, marker.Length);
                    "[redacted]".CopyTo(0, masked, position + markerPosition, "[redacted]".Length);
                }
                position = end;
            }
        }
        return masked is null ? content : new string(masked);
    }

    private static bool IsCompletePythonMethodBlock(string content, int position, int end)
    {
        if (position > 0 && content[position - 1] != '\n'
            || end < content.Length && content[end] != '\n') { return false; }
        int cursor = end;
        while (cursor < content.Length)
        {
            int next = content.IndexOf('\n', cursor);
            next = next < 0 ? content.Length : next;
            string line = content[cursor..next];
            string statement = line.TrimStart();
            if (statement.Length > 0 && !statement.StartsWith('#'))
            {
                int indent = line.Length - statement.Length;
                if (indent == 0)
                {
                    return !Regex.IsMatch(statement, @"\Adef\b", RegexOptions.CultureInvariant)
                        || PythonSupportedMethodDeclarationPattern().IsMatch(statement);
                }
                return indent == 4 && PythonSupportedMethodDeclarationPattern().IsMatch(statement);
            }
            cursor = next + 1;
        }
        return true;
    }

    private static bool IsInPythonUnitTestClass(string content, int position, bool rejection)
    {
        Match? owner = null;
        foreach (Match declaration in PythonTopLevelDeclarationPattern().Matches(content))
        {
            if (declaration.Index >= position) { break; }
            owner = declaration;
        }
        if (owner is null || !PythonUnitTestClassPattern().IsMatch(owner.Value)
            || IsInsidePythonLiteral(content, owner.Index)) { return false; }
        if (IsPrecededByPythonDecorator(content, owner.Index)
            || !PythonUnittestImportPattern().Matches(content).Any(match => !IsInsidePythonLiteral(content, match.Index))
            || PythonProofShadowPattern().IsMatch(content)) { return false; }
        string className = owner.Value["class ".Length..owner.Value.IndexOf('(')];
        if (Regex.IsMatch(content, @"(?m)^[\t ]*" + Regex.Escape(className)
            + @"\.(?:assert[A-Z][A-Za-z]*|reject)[\t ]*(?:=(?!=)|:)", RegexOptions.CultureInvariant)) { return false; }
        int classEnd = content.Length;
        foreach (Match declaration in PythonTopLevelDeclarationPattern().Matches(content))
        {
            if (declaration.Index > owner.Index) { classEnd = declaration.Index; break; }
        }
        string classBody = content[owner.Index..classEnd];
        int nameEnd = content.IndexOf('(', position);
        string methodName = content[(position + "    def ".Length)..nameEnd];
        if (Regex.Matches(classBody, @"(?m)^    def[\t ]+" + Regex.Escape(methodName) + @"[\t ]*\(", RegexOptions.CultureInvariant).Count != 1)
        {
            return false;
        }
        if (!rejection) { return true; }
        if (PythonRejectionDefinitionPattern().Matches(classBody).Count != 1) { return false; }
        const string rejectionProof = "    def reject(self):\n        with self.assertRaises((ValueError, KeyError, OSError, json.JSONDecodeError)):\n            verifier.validate(self.directory)";
        int proof = content.IndexOf(rejectionProof, owner.Index, position - owner.Index, StringComparison.Ordinal);
        return proof >= 0 && !IsInsidePythonLiteral(content, proof)
            && !IsPrecededByPythonDecorator(content, proof)
            && IsCompletePythonMethodBlock(content, proof, proof + rejectionProof.Length);
    }

    private static bool IsPrecededByPythonDecorator(string content, int position)
    {
        int end = Math.Max(0, position - 1);
        while (end > 0)
        {
            int start = content.LastIndexOf('\n', end - 1) + 1;
            string line = content[start..end].Trim();
            if (line.Length > 0 && !line.StartsWith('#')) { return line.StartsWith('@'); }
            end = Math.Max(0, start - 1);
        }
        return false;
    }

    private static IEnumerable<(string Block, string Marker, bool Rejection)> BoundedPythonProbeBlocks()
    {
        string assignment = Assignment("pass" + "word", NegativeProbeMarker);
        yield return ("    def test_secret_bearing_receipt_is_rejected(self):\n"
            + "        self.commands[0][\"diagnostic\"] = '" + assignment + "'\n"
            + "        self.save()\n        self.reject()", NegativeProbeMarker, true);
        yield return ("    def test_retained_bare_bearer_token_is_rejected(self):\n"
            + "        self.commands[0][\"diagnostic\"] = \"Bearer " + NegativeProbeMarker + "\"\n"
            + "        self.save()\n        self.reject()", NegativeProbeMarker, true);
        foreach ((string name, string prefix) in new[]
        {
            ("test_authorization_redaction_removes_entire_bearer_token", "Authorization: Bearer "),
            ("test_bare_bearer_redaction_removes_token", "Bearer "),
        })
        {
            yield return ("    def " + name + "(self):\n"
                + "        redacted = self.runner.redact(\"" + prefix + NegativeProbeMarker + "\")\n"
                + "        self.assertNotIn(\"" + NegativeProbeMarker + "\", redacted)\n"
                + "        verifier.safe(redacted)", NegativeProbeMarker, false);
        }
        string inspection = "    def test_shared_inspection_never_retains_unrelated_environment(self):\n"
            + "        root = pathlib.Path(self.temp.name)\n"
            + "        binary = root / \"bin/docker\"\n"
            + "        binary.parent.mkdir()\n"
            + "        projection = {\"id\": \"a\" * 64, \"image\": \"sha256:\" + \"b\" * 64, \"running\": True, \"started\": \"fixture-time\", \"invocation\": \"\"}\n"
            + "        script = \"#!\" + sys.executable + \"\\nimport json,sys\\nif sys.argv[1]=='ps': print('\" + \"a\" * 64 + \"')\\nelif '--format' in sys.argv and sys.argv[sys.argv.index('--format')+1]==\" + repr(verifier.RESOURCE_INSPECT_FORMAT) + \": print(\" + repr(json.dumps(projection)) + \")\\nelse: print(json.dumps({'Config':{'Env':['" + Assignment("UNRELATED_" + "SECRET", InspectionProbeMarker) + "']},'Mounts':['fixture-mount']}))\\n\"\n"
            + "        binary.write_text(script)\n        binary.chmod(0o700)\n"
            + "        self.runner.env[\"PATH\"] = str(binary.parent)\n"
            + "        snapshot = self.runner.shared()\n"
            + "        self.assertEqual(snapshot[\"a\" * 64], {key: projection[key] for key in (\"image\", \"running\", \"started\")})\n"
            + "        retained = self.output.joinpath(\"commands.json\").read_text()\n"
            + "        self.assertNotIn(\"" + InspectionProbeMarker + "\", retained)\n"
            + "        self.assertNotIn(\"fixture-mount\", retained)\n"
            + "        self.assertTrue(any(c[\"argv\"][:3] == [\"docker\", \"inspect\", \"--format\"] for c in self.runner.commands))";
        yield return (inspection, InspectionProbeMarker, false);
    }

    [GeneratedRegex(@"^[\t ]*\('loop10 repair account kind aliases identity',[\t ]*'lifecycle',[\t ]*\[\(""(?<oldname>account_key)=\((?<first>[A-Za-z_][A-Za-z0-9_]*),(?<second>[A-Za-z_][A-Za-z0-9_]*)\)"",[\t ]*""(?<newname>account_key)=\k<second>""\)\]\),[\t ]*$", RegexOptions.CultureInvariant)]
    private static partial Regex PythonAccountTupleMutationPattern();

    [GeneratedRegex(@"account_key[\t ]*=[\t ]*\([\t ]*(?<first>[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*)[\t ]*,[\t ]*(?<second>[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*)[\t ]*\)", RegexOptions.CultureInvariant)]
    private static partial Regex PythonAccountTuplePattern();

    [GeneratedRegex("^[\\t ]*[A-Za-z_][A-Za-z0-9_.]*\\.write_text\\(\"(?<field>[A-Z_][A-Z0-9_]*)=\"[\\t ]*\\+[\\t ]*(?<runtime>[A-Za-z_][A-Za-z0-9_.]*)[\\t ]*\\+[\\t ]*\"\\\\n(?:\\\\.|[^\"\\\\])*\"\\)[\\t ]*$", RegexOptions.CultureInvariant)]
    private static partial Regex PythonEnvironmentWritePattern();

    [GeneratedRegex("^[\\t ]*[A-Za-z_][A-Za-z0-9_.]*[\\t ]*=[\\t ]*f(?<quote>[\"'])(?<body>(?:\\\\.|(?!\\k<quote>)[^\\\\\\r\\n])*)\\k<quote>[\\t ]*$", RegexOptions.CultureInvariant)]
    private static partial Regex PythonInterpolatedAssignmentPattern();

    [GeneratedRegex("^[\\t ]*[A-Za-z_][A-Za-z0-9_.]*[\\t ]*=[\\t ]*\\(f(?<quote>[\"'])(?<body>(?:\\\\.|(?!\\k<quote>)[^\\\\\\r\\n])*)\\k<quote>[\\t ]*\\r?\\n[\\t ]+\"dbname=eventstore sslmode=disable connect_timeout=10\"\\)[\\t ]*$", RegexOptions.CultureInvariant)]
    private static partial Regex PythonParenthesizedInterpolationPattern();

    [GeneratedRegex(@"\{(?<runtime>[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*)\}", RegexOptions.CultureInvariant)]
    private static partial Regex PythonBoundedInterpolationPattern();

    [GeneratedRegex(@"[A-Za-z_][A-Za-z0-9_]*=\{(?<runtime>[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*)\}", RegexOptions.CultureInvariant)]
    private static partial Regex PythonInterpolatedFieldPattern();

    [GeneratedRegex(@"(?m)^(?<fence>```|~~~)(?:bash|shell|sh)[\t ]*\r?\n(?<body>(?:(?!^(?:```|~~~)).*\n)*?)\k<fence>[\t ]*(?:\r?$)", RegexOptions.CultureInvariant)]
    private static partial Regex ClosedMarkdownShellFencePattern();

    [GeneratedRegex(@"(?m)^(?<fence>```|~~~)(?:python|py)[\t ]*\r?\n(?<body>(?:(?!^(?:```|~~~)).*\n)*?)\k<fence>[\t ]*(?:\r?$)", RegexOptions.CultureInvariant)]
    private static partial Regex ClosedMarkdownPythonFencePattern();

    [GeneratedRegex(@"\Apython3?[\t ]+-[\t ]+<<(?<quote>['""]?)(?<delimiter>[A-Za-z_][A-Za-z0-9_]*)\k<quote>[\t ]*\z", RegexOptions.CultureInvariant)]
    private static partial Regex PythonHereDocumentOpeningPattern();

    [GeneratedRegex(@"\A(?:False|None|True|and|as|assert|async|await|break|class|continue|def|del|elif|else|except|finally|for|from|global|if|import|in|is|lambda|nonlocal|not|or|pass|raise|return|try|while|with|yield)\z", RegexOptions.CultureInvariant)]
    private static partial Regex PythonReservedWordPattern();

    [GeneratedRegex(@"(?m)^[\t ]*(?<close>\])[\t ]*\r?$", RegexOptions.CultureInvariant)]
    private static partial Regex PythonFaultListClosingPattern();

    [GeneratedRegex(@"(?m)^import unittest[\t ]*$", RegexOptions.CultureInvariant)]
    private static partial Regex PythonUnittestImportPattern();

    [GeneratedRegex(@"(?m)^[\t ]*(?:(?:unittest(?:\.TestCase)?(?:\.assert[A-Z][A-Za-z]*)?|self\.(?:__class__\.)?(?:assert[A-Z][A-Za-z]*|reject|runner\.redact)|assert[A-Z][A-Za-z]*|verifier(?:\.(?:safe|validate))?)[\t ]*(?:=(?!=)|:)|(?:def|class)[\t ]+(?:unittest|assert[A-Z][A-Za-z]*)\b)|\b(?:as[\t ]+unittest|setattr\([^\r\n]*(?:TestCase|assert[A-Z]))", RegexOptions.CultureInvariant)]
    private static partial Regex PythonProofShadowPattern();

    [GeneratedRegex(@"(?m)^[^\s#][^\r\n]*", RegexOptions.CultureInvariant)]
    private static partial Regex PythonTopLevelDeclarationPattern();

    [GeneratedRegex(@"\Adef[\t ]+[A-Za-z_][A-Za-z0-9_]*[\t ]*\([\t ]*(?:\*{0,2}[A-Za-z_][A-Za-z0-9_]*(?:[\t ]*,[\t ]*\*{0,2}[A-Za-z_][A-Za-z0-9_]*)*)?[\t ]*\)[\t ]*:[\t ]*\z", RegexOptions.CultureInvariant)]
    private static partial Regex PythonSupportedMethodDeclarationPattern();

    [GeneratedRegex(@"(?m)^    def[\t ]+reject[\t ]*\(", RegexOptions.CultureInvariant)]
    private static partial Regex PythonRejectionDefinitionPattern();

    [GeneratedRegex(@"\Aclass [A-Za-z_][A-Za-z0-9_]*\(unittest\.TestCase\):\z", RegexOptions.CultureInvariant)]
    private static partial Regex PythonUnitTestClassPattern();
}
