using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>Creates and validates domain-separated internal fence capabilities.</summary>
public sealed class IdempotencyExecutionContextProtector(IIdempotencyDigestKeyProvider keyProvider)
{
    private static readonly byte[] _proofDomain = "execution-fence-v1\0"u8.ToArray();
    private readonly IIdempotencyDigestKeyProvider _keyProvider = keyProvider
        ?? throw new ArgumentNullException(nameof(keyProvider));

    /// <summary>Creates a capability bound to the exact protected command boundary.</summary>
    public async ValueTask<IdempotencyExecutionContext> ProtectAsync(
        string admissionActorId,
        long fencingToken,
        string digestKeyVersion,
        SubmitCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateFields(admissionActorId, fencingToken, digestKeyVersion, command);
        var unsigned = new IdempotencyExecutionContext(
            IdempotencyExecutionContext.CurrentSchemaVersion,
            admissionActorId,
            fencingToken,
            digestKeyVersion,
            command.MessageId,
            command.CorrelationId,
            command.Tenant,
            command.Domain,
            command.AggregateId,
            command.CommandType,
            Proof: string.Empty);
        string proof = await ComputeProofAsync(unsigned, cancellationToken).ConfigureAwait(false);
        return unsigned with { Proof = proof };
    }

    /// <summary>Fails closed unless the capability is current, non-zero, untampered, and command-bound.</summary>
    public async ValueTask ValidateAsync(
        IdempotencyExecutionContext context,
        SubmitCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);
        ValidateBoundary(
            context,
            command.MessageId,
            command.CorrelationId,
            command.Tenant,
            command.Domain,
            command.AggregateId,
            command.CommandType);
        await ValidateProofAsync(context, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Validates a capability immediately inside the aggregate actor boundary.</summary>
    public async ValueTask ValidateAsync(
        IdempotencyExecutionContext context,
        CommandEnvelope command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);
        ValidateBoundary(
            context,
            command.MessageId,
            command.CorrelationId,
            command.TenantId,
            command.Domain,
            command.AggregateId,
            command.CommandType);
        await ValidateProofAsync(context, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask ValidateProofAsync(
        IdempotencyExecutionContext context,
        CancellationToken cancellationToken)
    {
        string expected = await ComputeProofAsync(context with { Proof = string.Empty }, cancellationToken)
            .ConfigureAwait(false);
        if (!FixedTimeEquals(expected, context.Proof))
        {
            throw new InvalidOperationException("The idempotency execution fence is missing, stale, or invalid.");
        }
    }

    private static void ValidateBoundary(
        IdempotencyExecutionContext context,
        string messageId,
        string correlationId,
        string tenant,
        string domain,
        string aggregateId,
        string commandType)
    {
        if (context.SchemaVersion != IdempotencyExecutionContext.CurrentSchemaVersion
            || context.FencingToken <= 0
            || string.IsNullOrWhiteSpace(context.Proof)
            || !string.Equals(context.MessageId, messageId, StringComparison.Ordinal)
            || !string.Equals(context.CorrelationId, correlationId, StringComparison.Ordinal)
            || !string.Equals(context.Tenant, tenant, StringComparison.Ordinal)
            || !string.Equals(context.Domain, domain, StringComparison.Ordinal)
            || !string.Equals(context.AggregateId, aggregateId, StringComparison.Ordinal)
            || !string.Equals(context.CommandType, commandType, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The idempotency execution fence is missing, stale, or invalid.");
        }
    }

    private async ValueTask<string> ComputeProofAsync(
        IdempotencyExecutionContext context,
        CancellationToken cancellationToken)
    {
        using IdempotencyDigestKeyRing keyRing = await _keyProvider
            .GetKeyRingAsync(cancellationToken)
            .ConfigureAwait(false);
        byte[] key = keyRing.RentKeyMaterial(context.DigestKeyVersion);
        byte[] encoded = Encode(context);
        byte[] input = new byte[_proofDomain.Length + encoded.Length];
        try
        {
            Buffer.BlockCopy(_proofDomain, 0, input, 0, _proofDomain.Length);
            Buffer.BlockCopy(encoded, 0, input, _proofDomain.Length, encoded.Length);
            byte[] proof = HMACSHA256.HashData(key, input);
            try
            {
                return Base64Url(proof);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(proof);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(encoded);
            CryptographicOperations.ZeroMemory(input);
        }
    }

    private static byte[] Encode(IdempotencyExecutionContext context)
    {
        using var stream = new MemoryStream();
        WriteInt32(stream, context.SchemaVersion);
        WriteString(stream, context.AdmissionActorId);
        WriteInt64(stream, context.FencingToken);
        WriteString(stream, context.DigestKeyVersion);
        WriteString(stream, context.MessageId);
        WriteString(stream, context.CorrelationId);
        WriteString(stream, context.Tenant);
        WriteString(stream, context.Domain);
        WriteString(stream, context.AggregateId);
        WriteString(stream, context.CommandType);
        return stream.ToArray();
    }

    private static void WriteString(Stream stream, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        try
        {
            WriteInt32(stream, bytes.Length);
            stream.Write(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static void WriteInt32(Stream stream, int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        stream.Write(bytes);
    }

    private static void WriteInt64(Stream stream, long value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        stream.Write(bytes);
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        byte[] leftBytes = Encoding.UTF8.GetBytes(left);
        byte[] rightBytes = Encoding.UTF8.GetBytes(right);
        try
        {
            return leftBytes.Length == rightBytes.Length
                && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(leftBytes);
            CryptographicOperations.ZeroMemory(rightBytes);
        }
    }

    private static void ValidateFields(
        string admissionActorId,
        long fencingToken,
        string digestKeyVersion,
        SubmitCommand command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(admissionActorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(digestKeyVersion);
        if (fencingToken <= 0
            || string.IsNullOrWhiteSpace(command.MessageId)
            || string.IsNullOrWhiteSpace(command.CorrelationId)
            || string.IsNullOrWhiteSpace(command.Tenant)
            || string.IsNullOrWhiteSpace(command.Domain)
            || string.IsNullOrWhiteSpace(command.AggregateId)
            || string.IsNullOrWhiteSpace(command.CommandType))
        {
            throw new InvalidOperationException("The idempotency execution fence is missing, stale, or invalid.");
        }
    }

    private static string Base64Url(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
