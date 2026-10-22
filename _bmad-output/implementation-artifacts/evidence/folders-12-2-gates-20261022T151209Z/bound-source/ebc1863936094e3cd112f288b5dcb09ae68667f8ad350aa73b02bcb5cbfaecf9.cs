using System.Text;
using System.Text.Json;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>Preserves secret scanning on bounded summaries and external proof metadata.</summary>
public sealed partial class SecretsProtectionTests
{
    /// <summary>Metadata and generated-output paths cannot hide adjacent live credentials.</summary>
    [Theory]
    [InlineData("_bmad-output/implementation-artifacts/evidence/story-3-22/summary.json")]
    [InlineData("_bmad-output/implementation-artifacts/evidence/story-3-22/commands.json")]
    [InlineData("_bmad-output/implementation-artifacts/evidence/story-3-22/results/result.json")]
    [InlineData("tools/p1r_evidence_storage.py")]
    public void BoundedEvidenceMetadata_DoesNotExemptGeneratedCredentials(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        string credential = RandomSecret();
        string content = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["schema"] = "hexalith.p1r.attempt-summary.v1",
            ["disposition"] = "diagnostic-only",
            ["password"] = credential,
        });
        FindViolations(path, path.EndsWith(".py", StringComparison.Ordinal)
            ? Assignment("password", JsonSerializer.Serialize(credential)) : content).ShouldNotBeEmpty();
    }

    /// <summary>UTF-8 replacement decoding retains adjacent credential detection.</summary>
    [Fact]
    public void BoundedEvidenceMalformedBytes_StillScanAdjacentCredential()
    {
        string content = Assignment("password", RandomSecret());
        byte[] bytes = [0xff, .. Encoding.UTF8.GetBytes(content)];
        string? text = DecodeTrackedText("evidence/summary.json", bytes);
        text.ShouldNotBeNull();
        FindViolations("evidence/summary.json", text).ShouldNotBeEmpty();
    }

    /// <summary>Benign bounded metadata remains ordinary scanned JSON.</summary>
    [Fact]
    public void BoundedEvidenceDiagnosticSummary_ContainsNoSecretMaterial()
    {
        string content = JsonSerializer.Serialize(new
        {
            schema = "hexalith.p1r.attempt-summary.v1",
            disposition = "diagnostic-only",
            files = 5116,
            p1r_usable = false,
        });
        FindViolations("evidence/summary.json", content).ShouldBeEmpty();
    }
}
