using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Identity;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Events;

/// <summary>Checks independently constructed selected logical-model bytes, strict schemas and current scoped trust.</summary>
public sealed class DaprLogicalClaimCodecTests
{
    private static readonly byte[] Registry = SHA256.HashData("local registry"u8);
    private static readonly byte[] Logical = Vector("application", "sha256");
    private static readonly byte[] SourceHash = Vector("source", "sha256");
    private static readonly DateTimeOffset Timestamp = DateTimeOffset.UnixEpoch.ToOffset(TimeSpan.FromHours(2));

    /// <summary>Compares every selected production preimage and hash with independent Python vectors.</summary>
    [Fact]
    public void IndependentSourceMetadataListAccumulatorAndClaimsMatchAllPythonVectors()
    {
        DaprLogicalSourceBinding source = Source();
        DaprLogicalClaimCodec.EncodeSource(source).ShouldBe(Vector("source", "preimage"));
        DaprLogicalClaimCodec.ComputeSourceBindingHash(source).ShouldBe(SourceHash);
        DaprLogicalClaimCodec.ComputeConsumedMetadataHash(Metadata(), Logical, SourceHash).ShouldBe(Vector("consumed", "sha256"));
        var entries = new[] { new DaprLogicalDigestEntry(1, Logical) };
        DaprLogicalClaimCodec.ComputeOrderedList(SourceHash, entries).ShouldBe(Vector("ordered_list", "sha256"));
        byte[] genesis = DaprLogicalClaimCodec.ComputeGenesis(SourceHash, Registry);
        genesis.ShouldBe(Vector("genesis", "sha256"));
        DaprLogicalClaimCodec.ComputeAccumulatorStep(SourceHash, Registry, genesis, entries[0]).ShouldBe(Vector("step", "sha256"));
        byte[] route = DaprLogicalClaimCodec.EncodeRoute(Route()); route.ShouldBe(Vector("route", "preimage"));
        DaprLogicalClaimCodec.EncodeRoute(DaprLogicalClaimCodec.DecodeRoute(route)).ShouldBe(route);
        byte[] prefix = DaprLogicalClaimCodec.EncodePrefix(Prefix()); prefix.ShouldBe(Vector("prefix", "preimage"));
        DaprLogicalClaimCodec.EncodePrefix(DaprLogicalClaimCodec.DecodePrefix(prefix)).ShouldBe(prefix);
    }

    /// <summary>Checks exact logical optional presence, offsets, extension ordering and consumed metadata binding.</summary>
    [Fact]
    public void EveryConsumedMetadataOffsetPresenceExtensionAndAggregateTypeChangesTheHash()
    {
        DaprLogicalConsumedMetadata original = Metadata(); byte[] expected = DaprLogicalClaimCodec.ComputeConsumedMetadataHash(original, Logical, SourceHash);
        DaprLogicalConsumedMetadata[] changes = [original with { AggregateType = "other" }, original with { Timestamp = Timestamp.ToUniversalTime() },
            original with { StoredApplicationDigest = "" }, original with { UserId = null }, original with { CausationId = null },
            original with { Extensions = null }, original with { Extensions = new Dictionary<string, string>() },
            original with { Extensions = new Dictionary<string, string> { ["a"] = "2", ["z"] = "2" } },
            original with { DomainServiceVersion = "2.0" }, original with { GlobalPosition = 8 }, original with { ApplicationFormat = "other" }];
        foreach (DaprLogicalConsumedMetadata change in changes) { DaprLogicalClaimCodec.ComputeConsumedMetadataHash(change, Logical, SourceHash).ShouldNotBe(expected); }
        DaprLogicalClaimCodec.ComputeConsumedMetadataHash(original with { Extensions = new Dictionary<string, string> { ["z"] = "2", ["a"] = "1" } }, Logical, SourceHash).ShouldBe(expected);
    }

    /// <summary>Rejects altered framing and foreign or historical evidence models.</summary>
    [Theory]
    [InlineData("separator")]
    [InlineData("count")]
    [InlineData("tag")]
    [InlineData("trailing")]
    [InlineData("model")]
    public void MalformedOrHistoricalRouteClaimsRefuse(string mutation)
    {
        byte[] bytes = DaprLogicalClaimCodec.EncodeRoute(Route());
        int header = "HX-EV-DAPR-ROUTE-1\0"u8.Length;
        if (mutation == "separator") { bytes[6] = (byte)'X'; }
        if (mutation == "count") { BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(header + 1), 19); }
        if (mutation == "tag") { bytes[header + 3] = 2; }
        if (mutation == "trailing") { bytes = [.. bytes, 0]; }
        if (mutation == "model") { bytes[^1] ^= 1; }
        Should.Throw<ArgumentException>(() => DaprLogicalClaimCodec.DecodeRoute(bytes));
    }

    /// <summary>Checks the shared encode/decode scalar ceiling without changing legacy identity compatibility.</summary>
    [Fact]
    public void LargeAdmittedLegacyScalarRoundTripsAndNextByteRefusesConsistently()
    {
        DaprLogicalRouteClaim fields = Route() with { StoredEventType = new string('x', 512 * 1024) };
        byte[] encoded = DaprLogicalClaimCodec.EncodeRoute(fields);
        DaprLogicalClaimCodec.DecodeRoute(encoded).StoredEventType.ShouldBe(fields.StoredEventType);
        Should.Throw<InvalidOperationException>(() => DaprLogicalClaimCodec.EncodeRoute(fields with { StoredEventType = new string('x', 512 * 1024 + 1) }));
    }

    /// <summary>Rejects inconsistent V2 stored identities during both encoding and decoding.</summary>
    [Fact]
    public void V2StoredEventTypeMustMatchTheExactCanonicalTupleOnEncodeAndDecode()
    {
        DaprLogicalRouteClaim fields = Route() with { StoredMetadataVersion = 2, StoredEventType = "stored", StoredCanonicalType = "stored", StoredPayloadVersion = 1 };
        byte[] encoded = DaprLogicalClaimCodec.EncodeRoute(fields);
        DaprLogicalClaimCodec.DecodeRoute(encoded).StoredEventType.ShouldBe("stored");
        Should.Throw<ArgumentException>(() => DaprLogicalClaimCodec.EncodeRoute(fields with { StoredEventType = "other" }));
        encoded[encoded.AsSpan().IndexOf("stored"u8)] = (byte)'x';
        Should.Throw<ArgumentException>(() => DaprLogicalClaimCodec.DecodeRoute(encoded));
    }

    /// <summary>Refuses unbounded extension maps within admitted encoded size and entry count.</summary>
    [Fact]
    public void UnboundedExtensionEnumerationAndHugeFirstKeyRefuseBeforeUnboundedCapture()
    {
        var streamed = new DaprLogicalStreamingExtensions("x");
        Should.Throw<InvalidOperationException>(() => DaprLogicalClaimCodec.ComputeConsumedMetadataHash(Metadata() with { Extensions = streamed }, Logical, SourceHash));
        streamed.Enumerated.ShouldBeLessThan(65537);
        var huge = new DaprLogicalStreamingExtensions(new string('x', 512 * 1024));
        Should.Throw<InvalidOperationException>(() => DaprLogicalClaimCodec.ComputeConsumedMetadataHash(Metadata() with { Extensions = huge }, Logical, SourceHash));
        huge.Enumerated.ShouldBe(1);
    }

    /// <summary>Checks empty, terminal and anchorless complete-prefix invariants.</summary>
    [Fact]
    public void EmptyTargetAndTerminalRangeNeverIncrementLongMaxValueAndAnchorsRefuse()
    {
        DaprLogicalPrefixClaim empty = Prefix() with { StartSequence = 1, EndSequence = 0, Count = 0, TargetSequence = 0 };
        DaprLogicalClaimCodec.DecodePrefix(DaprLogicalClaimCodec.EncodePrefix(empty)).Count.ShouldBe(0);
        DaprLogicalPrefixClaim terminal = Prefix() with { StartSequence = long.MaxValue, EndSequence = long.MaxValue, TargetSequence = long.MaxValue, ActorHead = long.MaxValue };
        DaprLogicalClaimCodec.DecodePrefix(DaprLogicalClaimCodec.EncodePrefix(terminal)).EndSequence.ShouldBe(long.MaxValue);
        Should.Throw<ArgumentException>(() => DaprLogicalClaimCodec.EncodePrefix(empty with { TargetSequence = 1 }));
        byte[] anchored = DaprLogicalClaimCodec.EncodePrefix(Prefix());
        var reader = new EventEvolutionBinaryReader(anchored); reader.ReadRaw("HX-EV-DAPR-PREFIX-1\0"u8.Length + 3);
        for (int index = 0; index < 4; index++) { reader.ReadByte(); reader.ReadString(64); }
        for (int index = 0; index < 4; index++) { reader.ReadByte(); reader.ReadInt64(); }
        reader.ReadByte(); reader.ReadInt32();
        for (int index = 0; index < 3; index++) { reader.ReadByte(); reader.ReadHash(); }
        reader.ReadByte(); anchored[reader.Position] = 1;
        Should.Throw<ArgumentException>(() => DaprLogicalClaimCodec.DecodePrefix(anchored));
    }

    /// <summary>Checks purpose-specific signing, scoped verified-owner charges and private image cleanup.</summary>
    [Fact]
    public void CurrentScopedKeySignsAndVerifiesBothClaimsThenClearsItsRetainedImage()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using DaprLogicalClaimTrust trust = Trust(key); var budget = new EventBufferBudget();
        byte[] source = DaprLogicalClaimCodec.EncodeRoute(Route());
        var signed = trust.Sign(1, source, key, budget, CancellationToken.None);
        MemoryMarshal.TryGetArray(signed.Claim, out ArraySegment<byte> privateBytes).ShouldBeTrue();
        Array.Fill(source, (byte)0xa5);
        using var verifiedRoute = trust.VerifyRoute(signed.Claim.Span, signed.KeyId, signed.Signature.Span, budget, CancellationToken.None);
        verifiedRoute.Value.SequenceNumber.ShouldBe(1);
        using var prefix = trust.Sign(3, DaprLogicalClaimCodec.EncodePrefix(Prefix()), key, budget, CancellationToken.None);
        using var verifiedPrefix = trust.VerifyPrefix(prefix.Claim.Span, prefix.KeyId, prefix.Signature.Span, budget, CancellationToken.None);
        verifiedPrefix.Value.Count.ShouldBe(1);
        Should.Throw<InvalidOperationException>(() => trust.VerifyPrefix(signed.Claim.Span, signed.KeyId, signed.Signature.Span, budget, CancellationToken.None));
        budget.LiveBytes.ShouldBeGreaterThan(source.Length); signed.Dispose(); privateBytes.Array!.ShouldAllBe(static b => b == 0);
        prefix.Dispose(); budget.LiveBytes.ShouldBeGreaterThan(source.Length);
        verifiedRoute.Dispose(); verifiedPrefix.Dispose(); budget.LiveBytes.ShouldBe(0); source.ShouldAllBe(static b => b == 0xa5);
    }

    /// <summary>Refuses final decode cancellation or capability loss and releases every private reservation.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FinalDecodeBoundaryCancellationOrObservedLossRefusesAndReleasesCapacity(bool loss)
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256); using DaprLogicalClaimTrust signing = Trust(key);
        var signingBudget = new EventBufferBudget(); using var signed = signing.Sign(1, DaprLogicalClaimCodec.EncodeRoute(Route()), key, signingBudget, CancellationToken.None);
        var time = new DaprLogicalClaimTimeProvider(); var capability = new EventEvolutionCapabilityLoss(); using var cancellation = new CancellationTokenSource();
        time.OnRead = count => { if (count == 4) { if (loss) { capability.ObserveViolation(); } else { cancellation.Cancel(); } } };
        using DaprLogicalClaimTrust verification = Trust(key, time, capability); var budget = new EventBufferBudget();
        if (loss) { Should.Throw<InvalidOperationException>(() => verification.VerifyRoute(signed.Claim.Span, signed.KeyId, signed.Signature.Span, budget, cancellation.Token)); }
        else { Should.Throw<OperationCanceledException>(() => verification.VerifyRoute(signed.Claim.Span, signed.KeyId, signed.Signature.Span, budget, cancellation.Token)).CancellationToken.ShouldBe(cancellation.Token); }
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Refuses trust disposal during the final time callback and releases decoding capacity.</summary>
    [Fact]
    public void FinalDecodeBoundaryTrustDisposalRefusesAndReleasesCapacity()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256); using DaprLogicalClaimTrust signing = Trust(key);
        var signingBudget = new EventBufferBudget(); using var signed = signing.Sign(1, DaprLogicalClaimCodec.EncodeRoute(Route()), key, signingBudget, CancellationToken.None);
        var time = new DaprLogicalClaimTimeProvider(); using DaprLogicalClaimTrust verification = Trust(key, time); var budget = new EventBufferBudget();
        time.OnRead = count => { if (count == 4) { verification.Dispose(); } };
        Should.Throw<ObjectDisposedException>(() => verification.VerifyRoute(signed.Claim.Span, signed.KeyId, signed.Signature.Span, budget, CancellationToken.None));
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Rejects foreign scope, stale current keys and altered signed bytes.</summary>
    [Fact]
    public void KeyRotationWrongModelDomainRegistryExpiryAndChangedBytesRefuse()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256); using DaprLogicalClaimTrust trust = Trust(key);
        var budget = new EventBufferBudget(); using var signed = trust.Sign(1, DaprLogicalClaimCodec.EncodeRoute(Route()), key, budget, CancellationToken.None);
        using var other = new DaprLogicalClaimTrust("d", DaprLogicalSourceBinding.ModelId, "new-key", key.ExportSubjectPublicKeyInfo(), Registry,
            DateTimeOffset.UnixEpoch.AddDays(-1), DateTimeOffset.UnixEpoch.AddDays(1), new EventEvolutionCapabilityLoss(), new DaprLogicalClaimTimeProvider());
        Should.Throw<InvalidOperationException>(() => other.VerifyRoute(signed.Claim.Span, signed.KeyId, signed.Signature.Span, budget, CancellationToken.None));
        Should.Throw<InvalidOperationException>(() => trust.Sign(1, DaprLogicalClaimCodec.EncodeRoute(Route() with { Domain = "foreign" }), key, budget, CancellationToken.None));
        Should.Throw<InvalidOperationException>(() => trust.Sign(1, DaprLogicalClaimCodec.EncodeRoute(Route() with { RegistryFingerprint = new byte[32] }), key, budget, CancellationToken.None));
        byte[] changed = signed.Claim.ToArray(); changed[^1] ^= 1;
        Should.Throw<InvalidOperationException>(() => trust.VerifyRoute(changed, signed.KeyId, signed.Signature.Span, budget, CancellationToken.None));
        var time = new DaprLogicalClaimTimeProvider { Now = DateTimeOffset.UnixEpoch.AddDays(1) };
        using DaprLogicalClaimTrust expired = Trust(key, time);
        Should.Throw<InvalidOperationException>(() => expired.VerifyRoute(signed.Claim.Span, signed.KeyId, signed.Signature.Span, budget, CancellationToken.None));
        Should.Throw<ArgumentException>(() => new DaprLogicalClaimTrust("d", "historical-provider", "key", key.ExportSubjectPublicKeyInfo(), Registry,
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(1), new EventEvolutionCapabilityLoss()));
    }

    private static DaprLogicalClaimTrust Trust(ECDsa key, DaprLogicalClaimTimeProvider? time = null, EventEvolutionCapabilityLoss? loss = null)
        => new("d", DaprLogicalSourceBinding.ModelId, "local-key", key.ExportSubjectPublicKeyInfo(), Registry,
            DateTimeOffset.UnixEpoch.AddDays(-1), DateTimeOffset.UnixEpoch.AddDays(1), loss ?? new EventEvolutionCapabilityLoss(), time ?? new DaprLogicalClaimTimeProvider());
    private static DaprLogicalSourceBinding Source() => new("local-app", "local-ns", "AggregateActor", new AggregateIdentity("tenant", "d", "aggregate"),
        "r", 1, 1, 1, SHA256.HashData("local source"u8), "etag", Timestamp);
    private static DaprLogicalConsumedMetadata Metadata() => new("message", "aggregate", "r", "tenant", "d", 1, 7, Timestamp,
        "correlation", "cause", "", "1.0", "Legacy.Event", 1, "json", null, null, null,
        new Dictionary<string, string> { ["a"] = "1", ["z"] = "2" }, "json");
    private static DaprLogicalRouteClaim Route() => new(Logical, "tenant", "d", "aggregate", "r", 1, "message", "Legacy.Event", 1,
        null, null, "json", "evt", 1, Registry, SHA256.HashData("{}"u8), "json", Vector("consumed", "sha256"), SourceHash);
    private static DaprLogicalPrefixClaim Prefix() => new("tenant", "d", "aggregate", "r", 1, 1, 1, 1, 1,
        Vector("ordered_list", "sha256"), Vector("step", "sha256"), Registry, SourceHash);
    private static byte[] Vector(string name, string field)
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Hexalith.EventStore.slnx"))) { root = root.Parent; }
        using JsonDocument vectors = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root!.FullName,
            "_bmad-output/implementation-artifacts/evidence/story-6-6/dapr-logical-model-2026-10-08/vectors.json")));
        return Convert.FromHexString(vectors.RootElement.GetProperty(name).GetProperty(field).GetString()!);
    }
}
