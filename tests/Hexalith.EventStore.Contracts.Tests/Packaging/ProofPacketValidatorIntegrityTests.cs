namespace Hexalith.EventStore.Contracts.Tests.Packaging;

/// <summary>
/// Verifies the Story 1.20 proof packet retains the hardened shape of both operative
/// approval-role allowlist validators. The predicates are asserted by shape only — the
/// approved membership itself stays single-sourced in the allowlist, the packet, and the
/// spec verification commands, never restated here.
/// </summary>
public sealed class ProofPacketValidatorIntegrityTests
{
    private const string PacketRelativePath =
        "_bmad-output/implementation-artifacts/1-20-owner-approved-parity-closure-proof-packet.md";

    private const string SlurpedValidatorMarker = "jq -e -s '";

    /// <summary>
    /// Verifies both allowlist validators keep the single-document slurp guard and the
    /// exact-membership equality shape introduced by the ratified gate hardening.
    /// </summary>
    [Fact]
    public void PacketAllowlistValidatorsKeepSlurpGuardAndExactMembershipShape()
    {
        string packet = File.ReadAllText(Path.Combine(FindRepositoryRoot(), PacketRelativePath));

        string[] segments = packet.Split(SlurpedValidatorMarker, StringSplitOptions.None);
        segments.Length.ShouldBe(
            3,
            "The packet must contain exactly two slurped jq allowlist validators (candidate gate and evidence-commit-A re-check).");

        foreach (string segment in segments.Skip(1))
        {
            // Bound each validator to the text before its closing quote so assertions
            // cannot be satisfied by tokens from elsewhere in the packet.
            int closingQuote = segment.IndexOf("'", StringComparison.Ordinal);
            closingQuote.ShouldBeGreaterThan(0, "Each slurped validator must close its jq program quote.");
            string validator = segment[..closingQuote];

            validator.Contains("length == 1 and (.[0] |", StringComparison.Ordinal).ShouldBeTrue(
                "Each allowlist validator must keep the single-document slurp guard; without it a multi-root JSON stream whose last document is valid passes the fail-closed gate.");
            validator.Contains(".schema == \"hexalith.eventstore.github-approval-role-allowlist/v1\"", StringComparison.Ordinal).ShouldBeTrue(
                "Each allowlist validator must pin the allowlist schema identifier.");
            validator.Contains(".repository == \"Hexalith/Hexalith.EventStore\"", StringComparison.Ordinal).ShouldBeTrue(
                "Each allowlist validator must pin the repository identity.");
            validator.Contains("(.roles | keys | sort) ==", StringComparison.Ordinal).ShouldBeTrue(
                "Each allowlist validator must assert the exact role key set.");
            validator.Contains(".roles == {", StringComparison.Ordinal).ShouldBeTrue(
                "Each allowlist validator must assert exact role membership by object equality, not per-record containment alone.");
            validator.Contains("all(.roles[]; type == \"array\" and length > 0 and length == (unique | length)", StringComparison.Ordinal).ShouldBeTrue(
                "Each allowlist validator must keep the non-empty and uniqueness predicates as residual defense.");
        }
    }

    private static string FindRepositoryRoot()
    {
        string[] startPaths = [Directory.GetCurrentDirectory(), AppContext.BaseDirectory];
        foreach (string startPath in startPaths.Distinct(StringComparer.Ordinal))
        {
            DirectoryInfo? directory = new(startPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, PacketRelativePath.Replace('/', Path.DirectorySeparatorChar)))
                    && Directory.Exists(Path.Combine(directory.FullName, "src", "Hexalith.EventStore.Contracts")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root containing the Story 1.20 proof packet.");
    }
}
