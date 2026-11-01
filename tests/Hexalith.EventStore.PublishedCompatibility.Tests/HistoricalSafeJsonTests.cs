using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Shouldly;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Exercises the actual safe projection without retaining nested sensitive material.</summary>
public sealed class HistoricalSafeJsonTests
{
    [Theory]
    [InlineData("{\"p1rSafeCommitment\":true}")]
    [InlineData("{\"p1rSafeCommitment\":true,\"token\":\"unusable-marker\"}")]
    public void MarkerOnlyObjectsAreNotTrustedAndSensitiveChildrenAreProjected(string json)
    {
        var input = JsonSerializer.Deserialize<JsonElement>(json);
        HistoricalSafeJson.IsCommitment(input).ShouldBeFalse();
        var safe = HistoricalSafeJson.Capture(input);
        if (input.TryGetProperty("token", out var token))
        {
            var projected = safe.GetProperty("token");
            HistoricalSafeJson.IsCommitment(projected).ShouldBeTrue();
            projected.GetProperty("sha256").GetString().ShouldBe(HistoricalWireInput.HashValue(token));
            safe.GetRawText().ShouldNotContain("unusable-marker");
        }
    }

    [Theory]
    [InlineData("extra-child")]
    [InlineData("missing-hash")]
    [InlineData("duplicate-marker")]
    [InlineData("wrong-marker")]
    [InlineData("unknown-kind")]
    [InlineData("string-length")]
    [InlineData("negative-length")]
    [InlineData("bad-hash")]
    public void OnlyTheExactGeneratedCommitmentSchemaIsRecognized(string fault)
    {
        var input = JsonSerializer.SerializeToElement(new { proof = "unusable-marker" });
        var commitment = HistoricalSafeJson.Capture(input).GetProperty("proof");
        HistoricalSafeJson.IsCommitment(commitment).ShouldBeTrue();
        JsonElement.DeepEquals(HistoricalSafeJson.Capture(commitment), commitment).ShouldBeTrue();
        var changed = JsonNode.Parse(commitment.GetRawText())!.AsObject();
        if (fault == "extra-child") changed["token"] = "unusable-marker";
        if (fault == "missing-hash") changed.Remove("utf8Sha256");
        if (fault == "wrong-marker") changed["p1rSafeCommitment"] = false;
        if (fault == "unknown-kind") changed["kind"] = "Undefined";
        if (fault == "string-length") changed["length"] = "15";
        if (fault == "negative-length") changed["length"] = -1;
        if (fault == "bad-hash") changed["sha256"] = "not-a-digest";
        string json = changed.ToJsonString();
        if (fault == "duplicate-marker") json = json[..^1] + ",\"p1rSafeCommitment\":true}";
        var malformed = JsonSerializer.Deserialize<JsonElement>(json);
        HistoricalSafeJson.IsCommitment(malformed).ShouldBeFalse();
        if (fault == "extra-child")
        {
            var projected = HistoricalSafeJson.Capture(malformed);
            HistoricalSafeJson.IsCommitment(projected.GetProperty("token")).ShouldBeTrue();
            projected.GetRawText().ShouldNotContain("unusable-marker");
        }
        HistoricalSafeJson.NonemptyString(malformed).ShouldBeFalse();
        HistoricalSafeJson.EmptyString(malformed).ShouldBeFalse();
        var safe = HistoricalSafeJson.Capture(JsonSerializer.SerializeToElement(new { proof = malformed })).GetProperty("proof");
        HistoricalSafeJson.IsCommitment(safe).ShouldBeTrue();
        safe.GetProperty("kind").GetString().ShouldBe("Object");
        safe.GetProperty("sha256").GetString().ShouldBe(HistoricalWireInput.HashValue(malformed));
        safe.GetRawText().ShouldNotContain("unusable-marker");
    }

    [Theory]
    [InlineData("{ \"payload\": [\"unusable-marker\"], \"token\": \"unusable-nested-marker\" }", "Object", 2)]
    [InlineData("[ \"unusable-marker\", {\"payload\":\"unusable-nested-marker\"} ]", "Array", 2)]
    [InlineData("987", "Number", 3)]
    [InlineData("true", "True", 4)]
    [InlineData("false", "False", 5)]
    [InlineData("null", "Null", 4)]
    public void SensitiveValuesAreCommittedAsWholeTypedValues(string json, string kind, int length)
    {
        var value = JsonSerializer.Deserialize<JsonElement>(json);
        foreach (string field in new[] { "identityAdmissionProof", "wire", "cursor", "privateKey", "signingKey", "token", "proof", "signature", "boundProof" })
        {
            var input = JsonSerializer.Deserialize<JsonElement>("{\"" + field + "\":" + json + "}");
            var safe = HistoricalSafeJson.Capture(input).GetProperty(field);
            HistoricalSafeJson.IsCommitment(safe).ShouldBeTrue();
            safe.EnumerateObject().Count().ShouldBe(5);
            safe.GetProperty("kind").GetString().ShouldBe(kind);
            safe.GetProperty("length").GetInt32().ShouldBe(length);
            safe.GetProperty("sha256").GetString().ShouldBe(HistoricalWireInput.HashValue(value));
            safe.GetProperty("utf8Sha256").GetString().ShouldBe(QualificationRuntime.Hash(Encoding.UTF8.GetBytes(value.GetRawText())));
            safe.GetRawText().ShouldNotContain("unusable-marker");
            safe.GetRawText().ShouldNotContain("unusable-nested-marker");
            HistoricalSafeJson.NonemptyString(safe).ShouldBeFalse();
            HistoricalSafeJson.EmptyString(safe).ShouldBeFalse();
            HistoricalSafeJson.IsNull(safe).ShouldBe(kind == "Null");
            JsonElement.DeepEquals(HistoricalSafeJson.Capture(HistoricalSafeJson.Capture(input)), HistoricalSafeJson.Capture(input)).ShouldBeTrue();
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("unusable-marker")]
    public void StringProofChecksAndBothOriginalCommitmentsRemainUnchanged(string material)
    {
        ArgumentNullException.ThrowIfNull(material);
        var value = JsonSerializer.SerializeToElement(material);
        var safe = HistoricalSafeJson.Capture(JsonSerializer.SerializeToElement(new { proof = material })).GetProperty("proof");
        safe.GetProperty("kind").GetString().ShouldBe("String");
        safe.GetProperty("length").GetInt32().ShouldBe(material.Length);
        safe.GetProperty("sha256").GetString().ShouldBe(HistoricalWireInput.HashValue(value));
        HistoricalSafeJson.Utf8Hash(safe).ShouldBe(QualificationRuntime.Hash(Encoding.UTF8.GetBytes(material)));
        HistoricalSafeJson.NonemptyString(safe).ShouldBe(material.Length > 0);
        HistoricalSafeJson.EmptyString(safe).ShouldBe(material.Length == 0);
        HistoricalSafeJson.IsNull(safe).ShouldBeFalse();
    }
}
