using System.Text.Json;

using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Sizes a proof row with bounded streaming buffers, then writes directly into one exact transport array.</summary>
internal static class ProjectionDeliveryStateSerialization
{
    internal static byte[] Serialize(ProjectionDeliveryState state, JsonSerializerOptions? options, int receiptLimit)
    {
        options ??= JsonSerializerOptions.Default;
        using var size = new SizeLimitedWriteStream(state.SchemaVersion == ProjectionDeliveryState.GuardSchemaVersion
            ? ProjectionDeliveryStateSize.AbsoluteLimit : long.MaxValue);
        try { Write(size, state, options); }
        catch (ProjectionRebuildPrefixSafetyLimitExceededException)
        { throw new ProjectionDeliveryHistoryValidationException("delivery_proof_limit_exceeded"); }
        finally { ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ProofSerializationBytes, size.AttemptedBytes); }
        ProjectionDeliveryStateSize.Validate(state, size.Length, receiptLimit);
        var bytes = new byte[checked((int)size.Length)];
        using var target = new MemoryStream(bytes, writable: true);
        try { Write(target, state, options); }
        finally { ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.ProofSerializationBytes, target.Position); }
        if (target.Position != bytes.Length) throw new ProjectionDeliveryHistoryValidationException("delivery_proof_limit_exceeded");
        return bytes;
    }

    private static void Write(Stream target, ProjectionDeliveryState state, JsonSerializerOptions options)
    {
        using var writer = new Utf8JsonWriter(target, new JsonWriterOptions
        { Indented = options.WriteIndented, Encoder = options.Encoder, MaxDepth = options.MaxDepth });
        JsonSerializer.Serialize(writer, state, options);
        writer.Flush();
    }
}
