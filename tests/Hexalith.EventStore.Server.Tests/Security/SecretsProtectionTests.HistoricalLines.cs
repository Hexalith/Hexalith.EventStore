using System.Security.Cryptography;
using System.Text;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Security;

public sealed partial class SecretsProtectionTests
{
    /// <summary>
    /// Historical evidence lines whose bytes are sealed. A changed file loses the exemption.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, (string Sha256, string[] LineHashes)> SealedHistoricalSecretFiles =
        new Dictionary<string, (string Sha256, string[] LineHashes)>(StringComparer.Ordinal)
        {
            ["_bmad-output/implementation-artifacts/evidence/6-1-p1r-31210-blocker-repair-20261014/tooling-before/tests/test_p1r_published_executor.py"] =
                ("e7cb8298ada4b1cb159076411ae19158505b0118119bbf9c86579b53e3a71256", ["f83d792143633f7793983b1f64aa87da3525eb180635a51f5a2a995a13b3597a"]),
            ["_bmad-output/implementation-artifacts/evidence/6-1-p1r-31210-blocker-repair-20261014/tooling-before/tests/test_p1r_published_qualification.py"] =
                ("d607a04a79b4a24a8f84aea2f6e00d3c7e7f474ef51cedf235dcd8dc8ca0bc9d", ["1524aab9822def7d5396ffe7fe461e8ffd75d6be1d8c8c98a4b26b96687f9c52"]),
            ["_bmad-output/implementation-artifacts/evidence/6-1-p1r-canonical-reconciliation-20261016/pre-canonical-refresh-reviewed-reconciliation.diff"] =
                ("396b80abbf05cd56a86b065910919c69e5fb73d1cf63221860c216a66123915e", ["e83be9bbd57c3b2f94ee9e576f717e12547fc32614250fa233e640c18ca9da0b", "eab7e2ab144bb388112b69ce2c630bca8a7da721561b06465081899fb8ac1629"]),
            ["_bmad-output/implementation-artifacts/evidence/6-1-p1r-canonical-reconciliation-20261016/review-v1.diff"] =
                ("1e42232b1cdeb7637b2cd628a0822ddd527bdfde12d263955f11934665231c22", ["e83be9bbd57c3b2f94ee9e576f717e12547fc32614250fa233e640c18ca9da0b", "c413772a6a4011640f87eeaa2f0ad181f2c3e52d1650d57aca9a261d12ba4947", "e7e43ebc36b5e3fb7ac50a4b10bbbde4fe5e43d98fdc6bf11b7d87cae866379e"]),
            ["_bmad-output/implementation-artifacts/evidence/6-1-p1r-canonical-reconciliation-20261016/reviewed-reconciliation.diff"] =
                ("006a4bfee5d3e3e9a0eeef8f4a2f23eadf81a567bf99ca9541f5e5517b84b603", ["e83be9bbd57c3b2f94ee9e576f717e12547fc32614250fa233e640c18ca9da0b", "eab7e2ab144bb388112b69ce2c630bca8a7da721561b06465081899fb8ac1629"]),
            ["_bmad-output/implementation-artifacts/evidence/6-1-p1r-successor-enrollment-20261014/release-analysis/bexec-preflight.txt"] =
                ("f87b48e649092cc68de8f07977bae48c65f0570007f60de42887201988ee0062", ["b97b4ce96a582dcb5ed66aec4f94f4f7a57fb647323aeec81f1f903583c1920a"]),
            ["_bmad-output/implementation-artifacts/evidence/6-1-p1r-successor-enrollment-20261014/release-analysis/canonical-ci-workflow.txt"] =
                ("1388d7b2f4cf96d01da3f078c09c975768c89ac79e14ef649635b7c2f79a0ae7", ["c5b6e30a9485fed934bb3e037506a78bb2dfa6c0098610d3ff88d859c64e0b58"]),
            ["_bmad-output/implementation-artifacts/evidence/6-1-p1r-successor-enrollment-20261014/release-analysis/canonical-p1r-workflow.txt"] =
                ("d88063ae47f84b68d362ac8c985d0ed00809a48c387de28c5801418875a59885", ["c5b6e30a9485fed934bb3e037506a78bb2dfa6c0098610d3ff88d859c64e0b58"]),
            ["_bmad-output/implementation-artifacts/evidence/6-1-p1r-successor-enrollment-20261014/release-analysis/canonical-release-workflow.txt"] =
                ("91f7b28d0ae1e805492af3a937849bafee0f071809f4757024bf380cc6bac31b", ["c5b6e30a9485fed934bb3e037506a78bb2dfa6c0098610d3ff88d859c64e0b58"]),
            ["_bmad-output/implementation-artifacts/evidence/6-1-p1r-tooling-hardening-20261016/independent-review.md"] =
                ("e489cc6114a99ae99fc38512813bd08441df1ccb566d78744fa114f50b4f3152", ["96fc74e6f25bfc03f70b220eb0332e9332bc731734490e7339279d71cea1965a"]),
            ["_bmad-output/implementation-artifacts/evidence/6-1-p1r-tooling-hardening-20261016/review-v1.diff"] =
                ("616eb2598715ebb58a3f8c2a8f2f04e0abcc83f92ffee24ad6ee20ed6da7e6ec", ["0734eac0889dd0867a981e9f11268fa64ddead733bfd613570f25bd826999306"]),
            ["_bmad-output/implementation-artifacts/evidence/6-1-p1r-tooling-hardening-20261016/review-v2.diff"] =
                ("3868c28072efd3fc34213697beaec76ca6190d52a15ac65a6f66030eaac6e7c8", ["c413772a6a4011640f87eeaa2f0ad181f2c3e52d1650d57aca9a261d12ba4947", "e7e43ebc36b5e3fb7ac50a4b10bbbde4fe5e43d98fdc6bf11b7d87cae866379e"]),
            ["_bmad-output/implementation-artifacts/evidence/6-1-p1r-tooling-hardening-20261016/review-v3.diff"] =
                ("8e7abfc544afc463dd853b3ae7882ad6eb3a76978dad5fe0d0060ad82024947d", ["c413772a6a4011640f87eeaa2f0ad181f2c3e52d1650d57aca9a261d12ba4947", "e7e43ebc36b5e3fb7ac50a4b10bbbde4fe5e43d98fdc6bf11b7d87cae866379e"]),
            ["_bmad-output/implementation-artifacts/evidence/6-1-p1r-tooling-hardening-20261016/review-v4.diff"] =
                ("0141061615e01e4aba0ad1e117181474e7b6e8d9dcc7eca71ae489ed9f450f92", ["c413772a6a4011640f87eeaa2f0ad181f2c3e52d1650d57aca9a261d12ba4947", "e7e43ebc36b5e3fb7ac50a4b10bbbde4fe5e43d98fdc6bf11b7d87cae866379e"]),
        };

    /// <summary>
    /// Verifies catalog counts and JSON structure are not usable secrets, while real literals remain violations.
    /// </summary>
    [Fact]
    public void QualifiedCountAndStructuralJsonValues_AreNotUsableSecrets()
    {
        const string path = "evidence/catalog.json";
        FindViolations(
            path,
            "\"Hexalith.Tests.RequiresCredentials\": 128,\n\"oci_credentials\": {\n")
            .ShouldBeEmpty();
        FindViolations(path, "api.token: " + new string('9', 9)).ShouldNotBeEmpty();
        FindViolations(path, Assignment("password", RandomSecret())).ShouldNotBeEmpty();
    }

    /// <summary>
    /// Verifies sealed historical evidence stays quiet only while its bytes match, and a changed copy is scanned.
    /// </summary>
    [Fact]
    public void SealedHistoricalSecretLines_RequireExactBytes()
    {
        foreach (string path in SealedHistoricalSecretFiles.Keys)
        {
            ScanTrackedFile(path).ShouldBeEmpty($"Sealed historical evidence changed: {path}");
        }

        string sample = "_bmad-output/implementation-artifacts/evidence/6-1-p1r-successor-enrollment-20261014/release-analysis/canonical-p1r-workflow.txt";
        WithEvidenceRepository(root =>
        {
            byte[] original = File.ReadAllBytes(Path.Combine(RepoRoot, sample));
            WriteEvidenceFile(root, sample, original);
            ScanTrackedFile(sample, root).ShouldBeEmpty();
            WriteEvidenceFile(root, sample, [..original, (byte)'\n']);
            ScanTrackedFile(sample, root).ShouldNotBeEmpty();
        });
    }

    private static IEnumerable<string> SuppressSealedHistoricalSecretLines(
        string path,
        string content,
        string? repositoryRoot,
        IEnumerable<string> violations)
    {
        if (!SealedHistoricalSecretFiles.TryGetValue(path, out (string Sha256, string[] LineHashes) seal))
        {
            return violations;
        }

        string root = repositoryRoot ?? RepoRoot;
        string actualHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root, path))))
            .ToLowerInvariant();
        if (!string.Equals(actualHash, seal.Sha256, StringComparison.Ordinal))
        {
            return violations;
        }

        string[] lines = content.Split('\n');
        HashSet<string> sealedLines = new(seal.LineHashes, StringComparer.Ordinal);
        return violations.Where(violation => !IsSealedHistoricalLine(path, lines, sealedLines, violation));
    }

    private static bool IsSealedHistoricalLine(string path, string[] lines, HashSet<string> sealedLines, string violation)
    {
        string prefix = path + ":";
        if (!violation.StartsWith(prefix, StringComparison.Ordinal)
            || !int.TryParse(violation[prefix.Length..], out int lineNumber)
            || lineNumber < 1
            || lineNumber > lines.Length)
        {
            return false;
        }

        string line = lines[lineNumber - 1].TrimEnd('\r');
        string lineHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(line))).ToLowerInvariant();
        return sealedLines.Contains(lineHash);
    }
}
