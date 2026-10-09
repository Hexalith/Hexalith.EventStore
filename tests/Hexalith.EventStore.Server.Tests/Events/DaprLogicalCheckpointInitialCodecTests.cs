using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Server.Events;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Events;
/// <summary>Checks unsigned preparation framing against independent vectors, including structural-only MAX boundaries.</summary>
public sealed class DaprLogicalCheckpointInitialCodecTests
{
    /// <summary>Matches exact measured images and strict roundtrips without claiming proof or MAX runtime admission.</summary>
    [Theory]
    [InlineData("current-selection")]
    [InlineData("below-head-selection")]
    [InlineData("tail-selection")]
    [InlineData("bounded-tail-selection")]
    [InlineData("max-selection-structural-only")]
    [InlineData("max-last-tail-selection-structural-only")]
    [InlineData("current-zero")]
    [InlineData("tail-one")]
    [InlineData("tail-bounded")]
    [InlineData("max-zero-structural-only")]
    [InlineData("max-last-tail-structural-only")]
    public void IndependentUnsignedVectorsMatchRuntimeExactMeasurement(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        using JsonDocument vectors = JsonDocument.Parse(File.ReadAllBytes(FindVectors()));
        JsonElement row = vectors.RootElement.GetProperty(name);
        JsonElement f = row.GetProperty("fields");
        byte[] expected = Convert.FromHexString(row.GetProperty("hex").GetString()!);
        byte[] actual;
        if (row.GetProperty("kind").GetString() == "selection")
        {
            var fields = new DaprLogicalCheckpointInitialSelection(Hash(f, "checkpoint"), Hash(f, "requested"), Hash(f, "fold"), Hash(f, "reconstruction"), Hash(f, "registry"), Hash(f, "state"), Hash(f, "root"), Hash(f, "witness"), Number(f, "k"), Number(f, "head"), Number(f, "target"), f.GetProperty("model").GetString()!);
            actual = DaprLogicalCheckpointInitialCodec.Encode(fields);
            DaprLogicalCheckpointInitialCodec.Measure(fields).ShouldBe(actual.Length);
            DaprLogicalCheckpointInitialCodec.Encode(DaprLogicalCheckpointInitialCodec.Decode(actual)).ShouldBe(actual);
        }
        else
        {
            var fields = new DaprLogicalCheckpointRangePreparation(Hash(f, "selection"), Hash(f, "requested"), Number(f, "k"), Number(f, "head"), Number(f, "target"), Number(f, "start"), Number(f, "end"), (int)Number(f, "count"), (DaprLogicalCheckpointRangeKind)Number(f, "kind"), f.GetProperty("model").GetString()!);
            actual = DaprLogicalCheckpointRangeCodec.Encode(fields);
            DaprLogicalCheckpointRangeCodec.Measure(fields).ShouldBe(actual.Length);
            DaprLogicalCheckpointRangeCodec.Encode(DaprLogicalCheckpointRangeCodec.Decode(actual)).ShouldBe(actual);
        }

        actual.ShouldBe(expected);
        actual.Length.ShouldBe(row.GetProperty("bytes").GetInt32());
        Convert.ToHexString(SHA256.HashData(actual)).ToLowerInvariant().ShouldBe(row.GetProperty("sha256").GetString());
    }

    /// <summary>Rejects malformed header, order, end, policy and excessive image capacity for both distinct schemas.</summary>
    [Theory]
    [InlineData(false, "separator")]
    [InlineData(false, "version")]
    [InlineData(false, "count")]
    [InlineData(false, "tag")]
    [InlineData(false, "trailing")]
    [InlineData(false, "model")]
    [InlineData(false, "capacity")]
    [InlineData(true, "separator")]
    [InlineData(true, "version")]
    [InlineData(true, "count")]
    [InlineData(true, "tag")]
    [InlineData(true, "trailing")]
    [InlineData(true, "model")]
    [InlineData(true, "capacity")]
    public void StrictUnsignedIntakeRefusesMalformedImages(bool range, string point)
    {
        ArgumentNullException.ThrowIfNull(point);
        byte[] image = range ? DaprLogicalCheckpointRangeCodec.Encode(Range()) : DaprLogicalCheckpointInitialCodec.Encode(Selection());
        int header = System.Text.Encoding.ASCII.GetByteCount(range ? "HX-EV-DAPR-CHECKPOINT-RANGE-PREPARE-1\0" : "HX-EV-DAPR-CHECKPOINT-INITIAL-PREPARE-1\0");
        if (point == "separator")
            image[0] ^= 1;
        else if (point == "version")
            image[header] = 2;
        else if (point == "count")
            image[header + 2] = 1;
        else if (point == "tag")
            image[header + 3] = 2;
        else if (point == "trailing")
            image = [..image, 0];
        else if (point == "model")
            image[^1] ^= 1;
        else
            image = new byte[1025];
        Should.Throw<ArgumentException>(() =>
        {
            if (range)
                _ = DaprLogicalCheckpointRangeCodec.Decode(image);
            else
                _ = DaprLogicalCheckpointInitialCodec.Decode(image);
        });
    }

    /// <summary>Refuses unsupported k/H/T, hash, zero and tail shapes before writer allocation.</summary>
    [Theory]
    [InlineData("below-head-zero")]
    [InlineData("covered-after-target")]
    [InlineData("target-after-head")]
    [InlineData("zero-start")]
    [InlineData("zero-count")]
    [InlineData("tail-zero")]
    [InlineData("tail-start")]
    [InlineData("tail-count")]
    [InlineData("tail-end")]
    [InlineData("unknown")]
    [InlineData("hash")]
    [InlineData("max-zero-increment")]
    public void StrictRangeShapesRefuseInvalidPlanning(string point)
    {
        ArgumentNullException.ThrowIfNull(point);
        var value = point switch
        {
            "below-head-zero" => Range()with
            {
                ActorHead = 3
            },
            "covered-after-target" => Range()with
            {
                TargetSequence = 1
            },
            "target-after-head" => Range()with
            {
                TargetSequence = 3
            },
            "zero-start" => Range()with
            {
                StartSequence = 2
            },
            "zero-count" => Range()with
            {
                PlannedCount = 1
            },
            "tail-zero" => Range()with
            {
                Kind = DaprLogicalCheckpointRangeKind.Tail
            },
            "tail-start" => Tail()with
            {
                StartSequence = 2,
                EndSequence = 2
            },
            "tail-count" => Tail()with
            {
                PlannedCount = 257
            },
            "tail-end" => Tail()with
            {
                EndSequence = 4
            },
            "unknown" => Range()with
            {
                Kind = (DaprLogicalCheckpointRangeKind)2
            },
            "hash" => Range()with
            {
                SelectionHash = new byte[31]
            },
            _ => Range()with
            {
                CoveredSequence = long.MaxValue,
                ActorHead = long.MaxValue,
                TargetSequence = long.MaxValue,
                StartSequence = long.MinValue,
                EndSequence = long.MaxValue
            }
        };
        Should.Throw<ArgumentException>(() => DaprLogicalCheckpointRangeCodec.Measure(value));
    }

    /// <summary>Refuses unsupported selection shapes and keeps checkpoint bytes distinct from ordinary signed prefix parsers.</summary>
    [Theory]
    [InlineData("hash")]
    [InlineData("zero")]
    [InlineData("after-target")]
    [InlineData("after-head")]
    [InlineData("model")]
    public void StrictSelectionShapesRefuseInvalidPrior(string point)
    {
        ArgumentNullException.ThrowIfNull(point);
        var value = point switch
        {
            "hash" => Selection()with
            {
                WitnessHash = new byte[31]
            },
            "zero" => Selection()with
            {
                CoveredSequence = 0
            },
            "after-target" => Selection()with
            {
                CoveredSequence = 3
            },
            "after-head" => Selection()with
            {
                TargetSequence = 3
            },
            _ => Selection()with
            {
                Model = "dapr-actor-logical-v1"
            }
        };
        Should.Throw<ArgumentException>(() => DaprLogicalCheckpointInitialCodec.Measure(value));
    }

    private static DaprLogicalCheckpointInitialSelection Selection() => new(new byte[32], new byte[32], new byte[32], new byte[32], new byte[32], new byte[32], new byte[32], new byte[32], 2, 2, 2, DaprLogicalCheckpointInitialCodec.ModelId);
    private static DaprLogicalCheckpointRangePreparation Range() => new(new byte[32], new byte[32], 2, 2, 2, 3, 2, 0, DaprLogicalCheckpointRangeKind.CurrentZero, DaprLogicalCheckpointRangeCodec.ModelId);
    private static DaprLogicalCheckpointRangePreparation Tail() => Range()with
    {
        ActorHead = 3,
        TargetSequence = 3,
        StartSequence = 3,
        EndSequence = 3,
        PlannedCount = 1,
        Kind = DaprLogicalCheckpointRangeKind.Tail
    };
    private static byte[] Hash(JsonElement row, string name) => Convert.FromHexString(row.GetProperty(name).GetString()!);
    private static long Number(JsonElement row, string name) => row.GetProperty(name).GetInt64();
    private static string FindVectors()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Hexalith.EventStore.slnx")))
            directory = directory.Parent;
        return Path.Combine(directory!.FullName, "_bmad-output/implementation-artifacts/evidence/story-6-6/dapr-logical-checkpoint-initial-2026-10-09/vectors.json");
    }
}
