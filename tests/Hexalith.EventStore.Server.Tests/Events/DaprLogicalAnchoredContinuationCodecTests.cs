using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Server.Events;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Events;
/// <summary>Checks distinct anchored canonical images against independent Python vectors and strict negative shapes.</summary>
public sealed class DaprLogicalAnchoredContinuationCodecTests
{
    /// <summary>Matches all independent prefix, effective, transcript, ledger and request images or digests.</summary>
    [Theory]
    [InlineData("tail-prefix")]
    [InlineData("zero-tail-prefix")]
    [InlineData("max-zero-tail-prefix")]
    [InlineData("effective-step")]
    [InlineData("transcript-genesis")]
    [InlineData("page-entry")]
    [InlineData("transcript-step")]
    [InlineData("ledger")]
    [InlineData("request")]
    public void IndependentContinuationVectorsMatchRuntime(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        using JsonDocument vectors = JsonDocument.Parse(File.ReadAllBytes(FindVectors()));
        JsonElement vector = vectors.RootElement.GetProperty("vectors").EnumerateArray().Single(row => row.GetProperty("name").GetString() == name);
        JsonElement f = vector.GetProperty("fields");
        using var budget = new EventBufferBudget();
        byte[] expected = Convert.FromHexString(vector.GetProperty("hex").GetString()!);
        byte[] actual;
        if (name.EndsWith("prefix", StringComparison.Ordinal))
        {
            var claim = new DaprLogicalAnchoredPrefixClaim(new DaprLogicalPrefixClaim(Text(f, "tenant"), Text(f, "domain"), Text(f, "aggregate"), Text(f, "type"), Number(f, "start"), Number(f, "end"), Number(f, "head"), Number(f, "target"), (int)Number(f, "count"), Hash(f, "list"), Hash(f, "accumulator"), Hash(f, "registry"), Hash(f, "source"), Text(f, "model")), Hash(f, "selection"), Number(f, "covered"), Hash(f, "state"));
            actual = DaprLogicalAnchoredPrefixCodec.Encode(claim);
            DaprLogicalAnchoredPrefixCodec.Measure(claim).ShouldBe(actual.Length);
            DaprLogicalAnchoredPrefixCodec.Encode(DaprLogicalAnchoredPrefixCodec.Decode(actual)).ShouldBe(actual);
            if (name != "tail-prefix")
            {
                claim.Prefix.OrderedLogicalDigestListHash.ToArray().ShouldBe(DaprLogicalClaimCodec.ComputeOrderedList(Hash(f, "source"), []));
            }
        }
        else
        {
            DaprLogicalPageTranscriptEntry? entry = name is "page-entry" or "transcript-step" or "ledger" ? Entry(vectors) : null;
            actual = name switch
            {
                "effective-step" => DaprLogicalReplayCommitmentCodec.EffectiveStep(Hash(f, "source"), Hash(f, "registry"), Hash(f, "binding"), Hash(f, "effective"), Number(f, "sequence"), Hash(f, "route"), Text(f, "type"), (int)Number(f, "version"), Text(f, "format"), Hash(f, "payload"), budget, Hash(f, "selection")),
                "transcript-genesis" => DaprLogicalReplayCommitmentCodec.AnchoredTranscriptGenesis(Text(f, "tenant"), Text(f, "operation"), Hash(f, "selection"), Hash(f, "transcript"), Hash(f, "state"), budget),
                "page-entry" => DaprLogicalReplayCommitmentCodec.EncodeTranscriptEntry(entry!),
                "transcript-step" => DaprLogicalReplayCommitmentCodec.AnchoredTranscriptStep(Text(f, "tenant"), Text(f, "operation"), Hash(f, "selection"), Hash(f, "transcript"), entry!, budget),
                "ledger" => DaprAnchoredReplayLedgerCodec.Encode(Ledger(entry!, Hash(f, "selection"), Hash(f, "transcript")), budget),
                "request" => DaprLogicalReplayCommitmentCodec.AnchoredRequestHash(Text(f, "tenant"), Text(f, "operation"), Text(f, "owner"), 1, 1, Text(f, "request"), (int)Number(f, "count"), Hash(f, "selection"), Hash(f, "source"), Hash(f, "registry"), budget),
                _ => throw new ArgumentException("Unknown vector.")};
            if (name is "effective-step" or "transcript-genesis" or "transcript-step" or "request")
            {
                expected = SHA256.HashData(expected);
            }
        }

        actual.ShouldBe(expected);
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Rejects ordinary, reordered, trailing, below-head zero and noncontiguous anchored claim shapes.</summary>
    [Theory]
    [InlineData("ordinary")]
    [InlineData("reordered")]
    [InlineData("trailing")]
    [InlineData("below-head-zero")]
    [InlineData("covered-tail")]
    [InlineData("wrong-count")]
    public void StrictAnchoredPrefixRefusesUnsupportedShapes(string mode)
    {
        var prefix = Prefix();
        if (mode is "below-head-zero" or "covered-tail" or "wrong-count")
        {
            prefix = mode switch
            {
                "below-head-zero" => prefix with
                {
                    Prefix = prefix.Prefix with
                    {
                        StartSequence = 2,
                        EndSequence = 1,
                        Count = 0,
                        TargetSequence = 1
                    }
                },
                "covered-tail" => prefix with
                {
                    Prefix = prefix.Prefix with
                    {
                        StartSequence = 1,
                        EndSequence = 1
                    }
                },
                _ => prefix with
                {
                    Prefix = prefix.Prefix with
                    {
                        Count = 2
                    }
                }
            };
            Should.Throw<ArgumentException>(() => DaprLogicalAnchoredPrefixCodec.Encode(prefix));
            return;
        }

        byte[] image = mode == "ordinary" ? DaprLogicalClaimCodec.EncodePrefix(prefix.Prefix with { LogicalEvidenceModelId = DaprLogicalSourceBinding.ModelId }) : DaprLogicalAnchoredPrefixCodec.Encode(prefix);
        if (mode == "trailing")
        {
            image = [..image, 0];
        }

        if (mode == "reordered")
        {
            image["HX-EV-DAPR-ANCHORED-PREFIX-1\0"u8.Length + 3] = 2;
        }

        Should.Throw<ArgumentException>(() => DaprLogicalAnchoredPrefixCodec.Decode(image));
    }

    /// <summary>Requires exact selection scope directly in trust and retains decoded capacity until disposal.</summary>
    [Theory]
    [InlineData("tenant")]
    [InlineData("aggregate")]
    [InlineData("type")]
    [InlineData("head")]
    [InlineData("target")]
    [InlineData("source")]
    [InlineData("covered")]
    [InlineData("state")]
    [InlineData("selection")]
    public void ExactAnchoredTrustRefusesSelectionSubstitution(string point)
    {
        using var fixture = new DaprLogicalReplayFixture();
        using var trust = new DaprLogicalAnchoredClaimTrust(DaprLogicalReplayAnchorCodec.ModelId, fixture.Trust, CancellationToken.None);
        using var budget = new EventBufferBudget();
        DaprLogicalAnchoredPrefixClaim claim = Prefix()with
        {
            Prefix = Prefix().Prefix with
            {
                RegistryFingerprint = fixture.Trust.RegistryFingerprint
            }
        };
        var selection = new DaprLogicalReplayAnchorSelection("tenant", "d", "aggregate", "r", Hash(1), fixture.Trust.RegistryFingerprint, Hash(1), Hash(1), Hash(1), Hash(1), Hash(1), Hash(1), 1, 3, 3, DaprLogicalReplayAnchorCodec.ModelId);
        using DaprLogicalSignedClaim signed = trust.SignPrefix(DaprLogicalAnchoredPrefixCodec.Encode(claim), fixture.Key, budget, CancellationToken.None);
        int baseline = budget.LiveBytes;
        using (DaprLogicalVerifiedClaim<DaprLogicalPrefixClaim> verified = trust.VerifyPrefix(signed.Claim.Span, signed.KeyId, signed.Signature.Span, selection, Hash(1), budget, CancellationToken.None))
        {
            (budget.LiveBytes - baseline).ShouldBe(signed.Claim.Length * 4 + 4096);
            verified.Value.TenantId.ShouldBe("tenant");
        }

        budget.LiveBytes.ShouldBe(baseline);
        selection = point switch
        {
            "tenant" => selection with
            {
                TenantId = "other"
            },
            "aggregate" => selection with
            {
                AggregateId = "other"
            },
            "type" => selection with
            {
                AggregateType = "other"
            },
            "head" => selection with
            {
                ActorHead = 4
            },
            "target" => selection with
            {
                TargetSequence = 2
            },
            "source" => selection with
            {
                SourceBindingHash = Hash(2)
            },
            "covered" => selection with
            {
                CoveredSequence = 2
            },
            "state" => selection with
            {
                CanonicalStateHash = Hash(2)
            },
            _ => selection
        };
        Should.Throw<InvalidOperationException>(() => trust.VerifyPrefix(signed.Claim.Span, signed.KeyId, signed.Signature.Span, selection, Hash(point == "selection" ? (byte)2 : (byte)1), budget, CancellationToken.None));
        Should.Throw<ArgumentException>(() => fixture.Trust.VerifyPrefix(signed.Claim.Span, signed.KeyId, signed.Signature.Span, budget, CancellationToken.None));
        budget.LiveBytes.ShouldBe(baseline);
    }

    private static DaprLogicalAnchoredPrefixClaim Prefix() => new(new DaprLogicalPrefixClaim("tenant", "d", "aggregate", "r", 2, 2, 3, 3, 1, Hash(1), Hash(1), Hash(1), Hash(1), DaprLogicalReplayAnchorCodec.ModelId), Hash(1), 1, Hash(1));
    /// <summary>Admits exact strict UTF-8 scope boundaries and rejects excess bytes before allocating an encoded writer.</summary>
    [Fact]
    public void ExactUtf8BoundaryUsesMeasuredWriterBytes()
    {
        string maximum = new('a', 512);
        var claim = Prefix()with
        {
            Prefix = Prefix().Prefix with
            {
                TenantId = maximum,
                Domain = maximum,
                AggregateId = maximum,
                AggregateType = maximum
            }
        };
        byte[] encoded = DaprLogicalAnchoredPrefixCodec.Encode(claim);
        DaprLogicalAnchoredPrefixCodec.Measure(claim).ShouldBe(encoded.Length);
        DaprLogicalAnchoredPrefixCodec.Encode(DaprLogicalAnchoredPrefixCodec.Decode(encoded)).ShouldBe(encoded);
        string unicode = string.Concat(Enumerable.Repeat("€", 170)) + "ab";
        DaprLogicalAnchoredPrefixCodec.Encode(claim with { Prefix = claim.Prefix with { TenantId = unicode } }).Length.ShouldBe(encoded.Length);
        Should.Throw<ArgumentException>(() => DaprLogicalAnchoredPrefixCodec.Encode(claim with { Prefix = claim.Prefix with { TenantId = maximum + "a" } }));
        Should.Throw<ArgumentException>(() => DaprLogicalAnchoredPrefixCodec.Encode(claim with { Prefix = claim.Prefix with { TenantId = unicode + "a" } }));
        Should.Throw<ArgumentException>(() => DaprLogicalAnchoredPrefixCodec.Decode(new byte[DaprLogicalAnchoredPrefixCodec.MaximumBytes + 1]));
    }

    private static DaprLogicalPageTranscriptEntry Entry(JsonDocument vectors)
    {
        JsonElement f = vectors.RootElement.GetProperty("vectors").EnumerateArray().Single(row => row.GetProperty("name").GetString() == "page-entry").GetProperty("fields");
        return new(1, 1, Hash(f, "request"), Hash(f, "accumulator"), Hash(f, "nextAccumulator"), 3, 4, 2, Hash(f, "response"), false, Hash(f, "state"), Hash(f, "nextState"), Hash(f, "effective"), Hash(f, "nextEffective"));
    }

    private static DaprReplayPageLedger Ledger(DaprLogicalPageTranscriptEntry entry, byte[] selection, byte[] transcript) => new(entry.PageOrdinal, entry.Generation, entry.RequestHash.ToArray(), entry.PreviousAccumulator.ToArray(), entry.Accumulator.ToArray(), entry.StartSequence, entry.EndSequence, entry.Count, entry.ResponseHash.ToArray(), entry.IsFinal)
    {
        PriorStateHash = entry.PriorStateHash!.Value.ToArray(),
        CanonicalStateHash = entry.CanonicalStateHash!.Value.ToArray(),
        PreviousEffectiveChainHash = entry.PreviousEffectiveChain.ToArray(),
        EffectiveChainHash = entry.EffectiveChain.ToArray(),
        PreviousTranscriptHash = transcript,
        TranscriptHash = SHA256.HashData("successor-transcript"u8),
        AnchorSelectionHash = selection
    };
    private static byte[] Hash(JsonElement fields, string key) => Convert.FromHexString(Text(fields, key));
    private static string Text(JsonElement fields, string key) => fields.GetProperty(key).GetString()!;
    private static long Number(JsonElement fields, string key) => fields.GetProperty(key).GetInt64();
    private static byte[] Hash(byte value) => Enumerable.Repeat(value, 32).ToArray();
    private static string FindVectors()
    {
        for (DirectoryInfo? current = new(Environment.CurrentDirectory); current is not null; current = current.Parent)
        {
            string path = Path.Combine(current.FullName, "_bmad-output/implementation-artifacts/evidence/story-6-6/dapr-logical-anchored-continuation-2026-10-09/vectors.json");
            if (File.Exists(path))
            {
                return path;
            }
        }

        throw new FileNotFoundException("Independent continuation vectors missing.");
    }
}
