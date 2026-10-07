using System.Buffers.Binary;
using System.Reflection;

using Hexalith.EventStore.Client.Events;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Events;

public sealed class EventEvolutionProofFramingTests
{
    [Fact]
    public void ExactOuterBytesRemainPrivateAndOpaqueUntilSeparateClaimVerification()
    {
        byte[] source = Frame([Entry("route"u8.ToArray())], Entry("prefix"u8.ToArray()));
        byte[] expected = source.ToArray();
        var budget = new EventBufferBudget();
        var frame = EventEvolutionProofFraming.Capture(source, budget, commandPage: false, allowCheckpoint: false, CancellationToken.None);
        byte[] privateBytes = PrivateBytes(frame);
        Array.Fill(source, (byte)0xa5);
        frame.ExactBytes.ToArray().ShouldBe(expected);
        frame.RouteCount.ShouldBe(1);
        frame.RouteClaim(0).ToArray().ShouldBe("route"u8.ToArray());
        frame.RouteKey(0).ToArray().ShouldBe("key"u8.ToArray());
        frame.RouteSignature(0).ToArray().ShouldBe(new byte[64]); // Framing alone intentionally cannot authenticate this signature.
        frame.PrefixClaim.ToArray().ShouldBe("prefix"u8.ToArray());
        frame.PrefixKey.ToArray().ShouldBe("key"u8.ToArray());
        frame.PrefixSignature.Length.ShouldBe(64);
        frame.HasCheckpoint.ShouldBeFalse();
        budget.LiveBytes.ShouldBeGreaterThan(expected.Length * 2);
        frame.Dispose();
        frame.Dispose();
        privateBytes.ShouldAllBe(static value => value == 0);
        source.ShouldAllBe(static value => value == 0xa5);
        budget.LiveBytes.ShouldBe(0);
        Should.Throw<ObjectDisposedException>(() => frame.RouteClaim(0).ToArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(256)]
    public void BoundaryCountsHaveExactlyOneMandatoryPrefix(int count)
    {
        byte[][] routes = Enumerable.Range(0, count).Select(_ => Entry([1])).ToArray();
        var budget = new EventBufferBudget();
        using var frame = EventEvolutionProofFraming.Capture(Frame(routes, Entry([2])), budget, false, false, CancellationToken.None);
        frame.RouteCount.ShouldBe(count);
        frame.PrefixClaim.ToArray().ShouldBe(new byte[] { 2 });
    }

    [Fact]
    public void OversizedRouteCountRefusesBeforeEntryOrPrivateAllocation()
    {
        byte[] source = Frame([], Entry([2]));
        BinaryPrimitives.WriteUInt32BigEndian(source.AsSpan(15, 4), 257);
        var budget = new EventBufferBudget();
        Should.Throw<InvalidOperationException>(() => EventEvolutionProofFraming.Capture(source, budget, false, false, CancellationToken.None))
            .Message.ShouldStartWith("ProofLimit:");
        budget.LiveBytes.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExactRouteClaimLimitAndNextByteAreIndependent(bool oversized)
    {
        byte[] source = Frame([Entry(new byte[1024 * 1024 + (oversized ? 1 : 0)])], Entry([2]));
        var budget = new EventBufferBudget();
        if (oversized)
        {
            Should.Throw<ArgumentException>(() => EventEvolutionProofFraming.Capture(source, budget, false, false, CancellationToken.None));
            budget.LiveBytes.ShouldBe(0);
        }
        else
        {
            using var frame = EventEvolutionProofFraming.Capture(source, budget, false, false, CancellationToken.None);
            frame.RouteClaim(0).Length.ShouldBe(1024 * 1024);
        }
    }

    [Fact]
    public void CommandWholeProofCeilingRefusesBeforeNestedAllocation()
    {
        var budget = new EventBufferBudget();
        Should.Throw<InvalidOperationException>(() => EventEvolutionProofFraming.Capture(new byte[2 * 1024 * 1024 + 1], budget, true, false, CancellationToken.None))
            .Message.ShouldStartWith("ProofLimit:");
        budget.LiveBytes.ShouldBe(0);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void StructurallyValidCommandFrameExercisesExactWholeByteBoundary(int delta)
    {
        byte[] source = LargeFrame(2 * 1024 * 1024 + delta);
        var budget = new EventBufferBudget();
        if (delta > 0)
        {
            Should.Throw<InvalidOperationException>(() => EventEvolutionProofFraming.Capture(source, budget, true, false, CancellationToken.None))
                .Message.ShouldStartWith("ProofLimit:");
            budget.LiveBytes.ShouldBe(0);
        }
        else
        {
            using var frame = EventEvolutionProofFraming.Capture(source, budget, true, false, CancellationToken.None);
            frame.ExactBytes.Length.ShouldBe(2 * 1024 * 1024 + delta);
            frame.PrefixClaim.Length.ShouldBe(source.Length - 99);
        }
    }

    [Fact]
    public void IndividuallyLegalPageMaximumRefusesCombinedLiveScratch()
    {
        byte[] source = LargeFrame(64 * 1024 * 1024);
        var budget = new EventBufferBudget();
        Should.Throw<InvalidOperationException>(() => EventEvolutionProofFraming.Capture(source, budget, false, false, CancellationToken.None))
            .Message.ShouldStartWith("ScratchLimit:");
        budget.LiveBytes.ShouldBe(0);
    }

    [Fact]
    public void ScratchChargeIsReservedBeforeCopyOrEntryBuffers()
    {
        byte[] source = Frame([], Entry([2]));
        var budget = new EventBufferBudget(source.Length);
        Should.Throw<InvalidOperationException>(() => EventEvolutionProofFraming.Capture(source, budget, false, false, CancellationToken.None))
            .Message.ShouldStartWith("ScratchLimit:");
        budget.LiveBytes.ShouldBe(0);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void CheckpointIsRejectedOutsideExplicitNonCommandPreparation(bool commandPage, bool allowCheckpoint)
    {
        byte[] source = Frame([], Entry([2]), Entry([3]));
        var budget = new EventBufferBudget();
        Should.Throw<ArgumentException>(() => EventEvolutionProofFraming.Capture(source, budget, commandPage, allowCheckpoint, CancellationToken.None));
        budget.LiveBytes.ShouldBe(0);
    }

    [Fact]
    public void OptionalCheckpointIsStructurallyRetainedWithoutGrantingAuthority()
    {
        var budget = new EventBufferBudget();
        using var frame = EventEvolutionProofFraming.Capture(Frame([], Entry([2]), Entry([3])), budget, false, true, CancellationToken.None);
        frame.HasCheckpoint.ShouldBeTrue();
        frame.CheckpointClaim.ToArray().ShouldBe(new byte[] { 3 });
        frame.CheckpointKey.ToArray().ShouldBe("key"u8.ToArray());
        frame.CheckpointSignature.Length.ShouldBe(64);
    }

    [Theory]
    [InlineData("signature")]
    [InlineData("utf8")]
    [InlineData("empty-key")]
    [InlineData("empty-claim")]
    [InlineData("trailing")]
    [InlineData("truncated")]
    [InlineData("discriminator")]
    [InlineData("codec")]
    [InlineData("prefix")]
    public void MalformedEntireFrameRefusesWithNoRetainedBudget(string vector)
    {
        byte[] entry = vector switch {
            "signature" => Entry([2], signature: new byte[63]),
            "utf8" => Entry([2], key: [0xff]),
            "empty-key" => Entry([2], key: []),
            "empty-claim" => Entry([]),
            _ => Entry([2]),
        };
        byte[] source = Frame([], entry);
        if (vector == "trailing") { source = [.. source, 1]; }
        if (vector == "truncated") { source = source[..^1]; }
        if (vector == "discriminator") { source[^1] = 2; }
        if (vector == "codec") { source[14] = 2; }
        if (vector == "prefix") { source = [.. source[..19], 0]; }
        var budget = new EventBufferBudget();
        _ = Should.Throw<Exception>(() => EventEvolutionProofFraming.Capture(source, budget, false, false, CancellationToken.None));
        budget.LiveBytes.ShouldBe(0);
    }

    [Fact]
    public void CancellationPrecedesMalformedInputAndPreservesOriginalToken()
    {
        var token = new CancellationToken(true);
        var budget = new EventBufferBudget();
        Should.Throw<OperationCanceledException>(() => EventEvolutionProofFraming.Capture([], budget, false, false, token))
            .CancellationToken.ShouldBe(token);
        budget.LiveBytes.ShouldBe(0);
    }

    private static byte[] PrivateBytes(UnverifiedEventEvolutionProofFrame frame)
        => (byte[])typeof(UnverifiedEventEvolutionProofFrame).GetField("_bytes", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(frame)!;

    // Independent exact outer framing: zero routes, one opaque prefix, key and 64-byte
    // signature, no checkpoint. No claim content/signature/source truth is asserted.
    private static byte[] LargeFrame(int totalBytes)
    {
        byte[] bytes = new byte[totalBytes];
        "HX-EV-PROOF-1\0"u8.CopyTo(bytes);
        bytes[14] = 1;
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(15), 0);
        int claimLength = totalBytes - 99;
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(19), checked((uint)claimLength));
        int keyLengthPosition = 23 + claimLength;
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(keyLengthPosition), 3);
        "key"u8.CopyTo(bytes.AsSpan(keyLengthPosition + 4));
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(keyLengthPosition + 7), 64);
        // Remaining 64 signature bytes and the final discriminator are already zero.
        return bytes;
    }

    private static byte[] Entry(byte[] claim, byte[]? key = null, byte[]? signature = null)
    {
        using var stream = new MemoryStream();
        Blob(stream, claim);
        Blob(stream, key ?? "key"u8.ToArray());
        Blob(stream, signature ?? new byte[64]);
        return stream.ToArray();
    }
    private static byte[] Frame(byte[][] routes, byte[] prefix, byte[]? checkpoint = null)
    {
        using var stream = new MemoryStream();
        stream.Write("HX-EV-PROOF-1\0"u8);
        stream.WriteByte(1);
        Span<byte> count = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(count, checked((uint)routes.Length));
        stream.Write(count);
        foreach (byte[] route in routes) { stream.Write(route); }
        stream.Write(prefix);
        stream.WriteByte(checkpoint is null ? (byte)0 : (byte)1);
        if (checkpoint is not null) { stream.Write(checkpoint); }
        return stream.ToArray();
    }
    private static void Blob(Stream stream, byte[] value)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, checked((uint)value.Length));
        stream.Write(length);
        stream.Write(value);
    }
}
