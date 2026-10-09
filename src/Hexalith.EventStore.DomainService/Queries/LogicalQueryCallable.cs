using System.Security.Cryptography;
using Hexalith.EventStore.Client.Events;

namespace Hexalith.EventStore.DomainService.Queries;
/// <summary>Pins a supplied immutable local callable and canonical options to exact catalog fields.</summary>
/// <remarks>File-only construction grants no sealed dependency closure or production readiness.</remarks>
internal sealed class LogicalQueryCallable
{
    private readonly Delegate _callable;
    private readonly string _id;
    private readonly byte[] _assemblyHash;
    private readonly EventOptionRule[] _optionSchema;
    private readonly byte[] _schema;
    private readonly byte[] _optionsHash;
    private readonly Func<ReadOnlyMemory<byte>>? _runtimeOptions;
    private readonly EventEvolutionCapabilityLoss _loss;
    /// <summary>Admits exact identity, file hash and expanded option bytes before any callback.</summary>
    internal LogicalQueryCallable(string id, Delegate callable, ReadOnlySpan<byte> assemblyHash, ReadOnlySpan<byte> optionsHash, ReadOnlyMemory<byte> canonicalOptions, EventEvolutionCapabilityLoss loss, Func<ReadOnlyMemory<byte>>? runtimeOptions = null, IReadOnlyList<EventOptionRule>? optionSchema = null, ReadOnlyMemory<byte> schema = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (callable.GetInvocationList().Length != 1 || callable.Method.Module.Assembly.Location.Length == 0)
        {
            throw new ArgumentException("QueryCapabilityChanged: unresolved callable.");
        }

        _id = id;
        _callable = callable;
        _loss = loss;
        _runtimeOptions = runtimeOptions;
        _assemblyHash = assemblyHash.ToArray();
        _optionSchema = optionSchema?.ToArray() ?? [];
        _schema = schema.ToArray();
        using FileStream file = File.OpenRead(callable.Method.Module.Assembly.Location);
        byte[] actual = SHA256.HashData(file);
        _optionsHash = EventOptionsManifestCodec.ComputeHash(canonicalOptions, _optionSchema);
        try
        {
            if (!actual.AsSpan().SequenceEqual(assemblyHash) || !_optionsHash.AsSpan().SequenceEqual(optionsHash))
            {
                throw new InvalidOperationException("QueryCapabilityChanged: exact callable declaration mismatch.");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(actual);
        }
    }

    /// <summary>Gets the exact immutable shared observed-loss scope.</summary>
    internal EventEvolutionCapabilityLoss CapabilityLoss => _loss;
    /// <summary>Gets the exact admitted implementation identity.</summary>
    internal string Id => _id;

    /// <summary>Requires the exact retained delegate that will actually be invoked.</summary>
    internal void RequireCallable(Delegate callable)
    {
        if (!_callable.Equals(callable))
        {
            throw new InvalidOperationException("QueryCapabilityChanged: callback differs from its immutable binding.");
        }
    }

    /// <summary>Requires exact adjacent implementation/file/options catalog fields.</summary>
    internal void RequireFields(EventRegistryRow row, int field)
    {
        if (row.GetTextField(field) != _id || !row.GetEncodedField(field + 1).SequenceEqual(_assemblyHash) || !row.GetEncodedField(field + 2).SequenceEqual(_optionsHash))
        {
            throw new InvalidOperationException("QueryCapabilityChanged: callable catalog fields disagree.");
        }
    }

    /// <summary>Encodes the exact bounded serializer/validator schema descriptor for this local codec.</summary>
    internal byte[] EncodeSchema()
    {
        using var writer = new EventEvolutionBinaryWriter(checked(2 * System.Text.Encoding.UTF8.GetByteCount(_id) + 4 * 32 + _schema.Length + 12));
        writer.WriteString(_id);
        writer.WriteHash(_assemblyHash);
        writer.WriteHash(_optionsHash);
        writer.WriteBytes(_schema);
        writer.WriteString(_id);
        writer.WriteHash(_assemblyHash);
        writer.WriteHash(_optionsHash);
        return writer.CopyEncodedBytes();
    }

    /// <summary>Requires exact codec identity and retained schema descriptor in the query row.</summary>
    internal void RequireSchema(EventRegistryRow row, int field)
    {
        byte[] schema = EncodeSchema();
        byte[] digest = SHA256.HashData(schema);
        try
        {
            if (row.GetTextField(field) != _id || !row.GetEncodedField(field + 1).SequenceEqual(digest))
            {
                throw new InvalidOperationException("QueryCapabilityChanged: codec/schema catalog fields disagree.");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(schema);
            CryptographicOperations.ZeroMemory(digest);
        }
    }

    /// <summary>Fences an optional options getter independently with original-token precedence.</summary>
    internal async Task RequireAsync(Func<CancellationToken, Task> fence, EventBufferBudget budget, CancellationToken token)
    {
        RequireActive(token);
        await fence(token).ConfigureAwait(false);
        RequireActive(token);
        if (_runtimeOptions is not null)
        {
            using EventBufferReservation charge = budget.Reserve(64 * 1024 * 160 + 4096);
            ReadOnlyMemory<byte> options;
            try
            {
                options = _runtimeOptions();
            }
            finally
            {
                token.ThrowIfCancellationRequested();
            }

            RequireActive(token);
            if (options.Length > 64 * 1024)
            {
                throw new InvalidOperationException("ReadModelQueryLimit: query options exceed their bound.");
            }

            byte[] privateOptions = options.ToArray();
            byte[]? current = null;
            try
            {
                await fence(token).ConfigureAwait(false);
                RequireActive(token);
                current = EventOptionsManifestCodec.ComputeHash(privateOptions, _optionSchema);
                try
                {
                    if (!current.AsSpan().SequenceEqual(_optionsHash))
                    {
                        throw new InvalidOperationException("QueryCapabilityChanged: query options changed.");
                    }
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(current);
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(privateOptions);
            }

            await fence(token).ConfigureAwait(false);
            RequireActive(token);
        }
    }

    /// <summary>Checks the admitted local method image and sticky execution-loss scope.</summary>
    internal void RequireActive(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        _loss.RequireNoObservedLoss();
        using FileStream file = File.OpenRead(_callable.Method.Module.Assembly.Location);
        byte[] actual = SHA256.HashData(file);
        try
        {
            if (!actual.AsSpan().SequenceEqual(_assemblyHash))
            {
                throw new InvalidOperationException("QueryCapabilityChanged: callable image bytes changed.");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(actual);
        }

        token.ThrowIfCancellationRequested();
        _loss.RequireNoObservedLoss();
    }
}
