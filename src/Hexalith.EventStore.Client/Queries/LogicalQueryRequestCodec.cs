using System.Security.Cryptography;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.EventStore.Client.Queries;
/// <summary>Hashes every consumed typed query member without serializing or copying its payload body.</summary>
internal static class LogicalQueryRequestCodec
{
    /// <summary>Uses a precharged bounded metadata writer and the exact payload digest.</summary>
    internal static byte[] Compute(QueryEnvelope query, EventBufferBudget budget, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using EventBufferReservation charge = budget.Reserve(2 * (64 * 1024 + 4096));
        using var writer = new EventEvolutionBinaryWriter(64 * 1024 + 4096);
        writer.WriteRaw("HX-EV-DAPR-LOGICAL-QUERY-REQUEST-1\0"u8);
        writer.WriteUInt16(1);
        writer.WriteString(query.TenantId);
        writer.WriteString(query.Domain);
        writer.WriteString(query.AggregateId);
        writer.WriteString(query.QueryType);
        byte[] payload = SHA256.HashData(query.Payload);
        try
        {
            writer.WriteHash(payload);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(payload);
        }

        writer.WriteString(query.CorrelationId);
        writer.WriteString(query.UserId);
        Text(writer, query.EntityId);
        writer.WriteByte(query.IsGlobalAdmin ? (byte)1 : (byte)0);
        writer.WriteByte(query.Paging is null ? (byte)0 : (byte)2);
        if (query.Paging is not null)
        {
            Integer(writer, query.Paging.PageSize);
            Integer(writer, query.Paging.Offset);
            Text(writer, query.Paging.Cursor);
        }

        Text(writer, query.OriginalActorId);
        Text(writer, query.AuthenticatedWorkloadId);
        writer.WriteByte(query.IsDelegated ? (byte)1 : (byte)0);
        Strings(writer, query.Scopes);
        Strings(writer, query.Audience);
        Text(writer, query.DelegationId);
        Text(writer, query.IdentityAdmissionProof);
        token.ThrowIfCancellationRequested();
        return writer.ComputeSha256();
    }

    private static void Text(EventEvolutionBinaryWriter writer, string? value)
    {
        writer.WriteByte(value is null ? (byte)0 : (byte)2);
        if (value is not null)
        {
            writer.WriteString(value);
        }
    }

    private static void Integer(EventEvolutionBinaryWriter writer, int? value)
    {
        writer.WriteByte(value is null ? (byte)0 : (byte)2);
        if (value is not null)
        {
            writer.WriteInt32(value.Value);
        }
    }

    private static void Strings(EventEvolutionBinaryWriter writer, IReadOnlyList<string>? values)
    {
        writer.WriteByte(values is null ? (byte)0 : (byte)2);
        if (values is null)
        {
            return;
        }

        if (values.Count > 256)
        {
            throw new InvalidOperationException("ReadModelQueryLimit: too many scope/audience entries.");
        }

        writer.WriteUInt32(checked((uint)values.Count));
        foreach (string value in values)
        {
            writer.WriteString(value);
        }
    }
}
