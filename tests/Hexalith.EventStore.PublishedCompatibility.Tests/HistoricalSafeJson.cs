using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Retains signed-field geometry and original byte commitments without retaining signed material.</summary>
internal static class HistoricalSafeJson
{
    internal static JsonElement Capture(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            if (IsCommitment(value)) return value.Clone();
            var result = new JsonObject();
            foreach (var field in value.EnumerateObject())
                result[field.Name] = JsonSerializer.SerializeToNode(Sensitive(field.Name)
                    ? IsCommitment(field.Value) ? field.Value.Clone() : Commit(field.Value) : Capture(field.Value));
            return JsonSerializer.SerializeToElement(result);
        }
        if (value.ValueKind == JsonValueKind.Array)
            return JsonSerializer.SerializeToElement(value.EnumerateArray().Select(Capture).ToArray());
        return value.Clone();
    }

    internal static bool IsCommitment(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object) return false;
        string[] fields = ["kind", "length", "p1rSafeCommitment", "sha256", "utf8Sha256"];
        return value.EnumerateObject().Select(field => field.Name).Order(StringComparer.Ordinal).SequenceEqual(fields)
            && value.GetProperty("p1rSafeCommitment").ValueKind == JsonValueKind.True
            && value.GetProperty("kind").ValueKind == JsonValueKind.String
            && value.GetProperty("kind").GetString() is "String" or "Object" or "Array" or "Number" or "True" or "False" or "Null"
            && value.GetProperty("length").ValueKind == JsonValueKind.Number
            && value.GetProperty("length").TryGetInt32(out int length) && length >= 0
            && Digest(value.GetProperty("sha256")) && Digest(value.GetProperty("utf8Sha256"));
    }

    internal static bool IsNull(JsonElement value) => value.ValueKind == JsonValueKind.Null
        || IsCommitment(value) && value.GetProperty("kind").GetString() == "Null";

    private static bool Digest(JsonElement value) => value.ValueKind == JsonValueKind.String
        && value.GetString() is { Length: 64 } text && text.All(char.IsAsciiHexDigitLower);

    internal static bool NonemptyString(JsonElement value) => value.ValueKind == JsonValueKind.String
        ? !string.IsNullOrWhiteSpace(value.GetString())
        : IsCommitment(value) && value.GetProperty("kind").GetString() == "String" && value.GetProperty("length").GetInt32() > 0;

    internal static bool EmptyString(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString() == ""
        : IsCommitment(value) && value.GetProperty("kind").GetString() == "String" && value.GetProperty("length").GetInt32() == 0;

    internal static string Utf8Hash(JsonElement value) => IsCommitment(value) ? value.GetProperty("utf8Sha256").GetString()!
        : QualificationRuntime.Hash(Encoding.UTF8.GetBytes(value.GetString()!));

    internal static string Text(string text)
    {
        string[] lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string prefix = lines[i].StartsWith("P1R_RESULT ", StringComparison.Ordinal) ? "P1R_RESULT " : "";
            try
            {
                using var document = JsonDocument.Parse(prefix.Length == 0 ? lines[i] : lines[i][prefix.Length..]);
                var safe = Capture(document.RootElement);
                if (!JsonElement.DeepEquals(safe, document.RootElement)) lines[i] = prefix + safe.GetRawText();
            }
            catch (JsonException)
            {
                if (prefix.Length != 0) lines[i] = "P1R_UNPARSED_RESULT_SHA256 " + QualificationRuntime.Hash(Encoding.UTF8.GetBytes(lines[i]));
            }
        }
        // Private reader commands can return pretty-printed JSON instead of one result line.
        string result = string.Join('\n', lines);
        try
        {
            using var document = JsonDocument.Parse(result);
            var safe = Capture(document.RootElement);
            return JsonElement.DeepEquals(safe, document.RootElement) ? result : safe.GetRawText();
        }
        catch (JsonException) { return result; }
    }

    internal static string Error(QualificationCommand command, string text)
    {
        if (string.IsNullOrEmpty(text) || command.Input is null) return Text(text);
        try
        {
            using var document = JsonDocument.Parse(command.Input);
            if (!document.RootElement.TryGetProperty("operation", out var operation)
                || operation.GetString() is not ("signed-query" or "wire" or "make-wire" or "cursor")) return Text(text);
        }
        catch (JsonException) { return Text(text); }
        const string xml = "Unhandled exception. System.Runtime.Serialization.InvalidDataContractException: Type 'Hexalith.EventStore.Contracts.Projections.ProjectionEventDto' cannot be serialized.";
        return text.Contains(xml, StringComparison.Ordinal) ? xml : "Probe diagnostics SHA256 " + QualificationRuntime.Hash(Encoding.UTF8.GetBytes(text));
    }

    private static bool Sensitive(string name) => name.Equals("identityAdmissionProof", StringComparison.OrdinalIgnoreCase)
        || name.Equals("wire", StringComparison.OrdinalIgnoreCase) || name.Equals("cursor", StringComparison.OrdinalIgnoreCase)
        || name.Contains("privatekey", StringComparison.OrdinalIgnoreCase) || name.Contains("signingkey", StringComparison.OrdinalIgnoreCase)
        || name.Equals("token", StringComparison.OrdinalIgnoreCase) || name.Equals("proof", StringComparison.OrdinalIgnoreCase)
        || name.Equals("signature", StringComparison.OrdinalIgnoreCase) || name.EndsWith("Proof", StringComparison.OrdinalIgnoreCase);

    private static JsonElement Commit(JsonElement value)
    {
        string original = value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();
        int length = value.ValueKind switch
        {
            JsonValueKind.String => original.Length,
            JsonValueKind.Array => value.GetArrayLength(),
            JsonValueKind.Object => value.EnumerateObject().Count(),
            _ => Encoding.UTF8.GetByteCount(original),
        };
        return JsonSerializer.SerializeToElement(new
        {
            p1rSafeCommitment = true, kind = value.ValueKind.ToString(), length,
            sha256 = HistoricalWireInput.HashValue(value), utf8Sha256 = QualificationRuntime.Hash(Encoding.UTF8.GetBytes(original)),
        });
    }
}
