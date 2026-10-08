using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Server.Events;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Events;

public sealed class DaprLogicalReconstructionCodecTests
{
    [Theory]
    [InlineData("canonical-successor")]
    [InlineData("canonical-empty-final")]
    [InlineData("source-only-final")]
    public void BoundedLedgerEncodingMatchesIndependentCanonicalParticipantVectors(string name)
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Hexalith.EventStore.slnx")))
        {
            root = root.Parent;
        }

        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root!.FullName,
            "_bmad-output/implementation-artifacts/evidence/story-6-6/dapr-logical-reconstruction-2026-10-08/vectors.json")));
        JsonElement vector = document.RootElement.GetProperty("vectors").GetProperty(name);
        DaprReplayPageLedger ledger = JsonSerializer.Deserialize<DaprReplayPageLedger>(vector.GetProperty("fields").GetRawText(),
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })!;
        // The JSON participant hashes are hex, whereas actor JSON encodes byte arrays as base64.
        JsonElement fields = vector.GetProperty("fields");
        ledger = ledger with
        {
            RequestHash = Hash(fields, "requestHash"),
            PreviousAccumulator = Hash(fields, "previousAccumulator"),
            Accumulator = Hash(fields, "accumulator"),
            ResponseHash = Hash(fields, "responseHash"),
            PriorStateHash = OptionalHash(fields, "priorStateHash"),
            CanonicalStateHash = OptionalHash(fields, "canonicalStateHash"),
        };
        byte[] bytes = DaprReplayLedgerCodec.Encode(ledger);
        bytes.ShouldBe(Convert.FromHexString(vector.GetProperty("preimageHex").GetString()!));
        SHA256.HashData(bytes).ShouldBe(Convert.FromHexString(vector.GetProperty("sha256").GetString()!));
    }

    private static byte[] Hash(JsonElement fields, string name) => Convert.FromHexString(fields.GetProperty(name).GetString()!);

    private static byte[]? OptionalHash(JsonElement fields, string name)
        => fields.GetProperty(name).ValueKind == JsonValueKind.Null ? null : Hash(fields, name);
}
