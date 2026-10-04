using System.Security.Cryptography;

using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Executes an exact F conversion with immutable input, measured bounded output and mandatory schema/semantic validation.</summary>
/// <remarks>The producer still owns complete encoded-result admission before using this local output.</remarks>
internal sealed class EventV1DownserializeExecutor(EventDomainRegistry registry, EventVersionValidator validateVersion,
    EventAliasValidator validateAlias, EventSemanticRoundTripValidator validateRoundTrip)
{
    /// <summary>Returns a charged alias-specific output only after descriptor, schema, identity and semantic checks.</summary>
    internal async ValueTask<ImmutablePayload> DownserializeAsync(string canonicalType, string writeAlias, IReadOnlyPayload source,
        RegisteredV1Downserializer binding, int measuredLegacyPayloadBytes, EventBufferBudget budget, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentNullException.ThrowIfNull(validateVersion);
        ArgumentNullException.ThrowIfNull(validateAlias);
        ArgumentNullException.ThrowIfNull(validateRoundTrip);
        cancellationToken.ThrowIfCancellationRequested();
        EventRegistryRow descriptor = registry.GetDownserializer(canonicalType, writeAlias);
        binding.RequireDescriptor(descriptor);
        (string type, int sourceVersion, string format) = registry.ResolveAlias(writeAlias);
        using ImmutablePayload input = ImmutablePayload.CopyFrom(source, budget, cancellationToken);
        Validate(input, canonicalType, registry.GetCurrentVersion(canonicalType), descriptor.GetTextField(5), cancellationToken);
        byte[] inputHash = input.ComputeSha256();
        using var writer = BoundedPayloadWriter.CreateLegacy(measuredLegacyPayloadBytes, cancellationToken, budget);
        using var scratch = new BoundedScratchAllocator(128 * 1024 * 1024, budget, cancellationToken);
        V1DownserializeResult result;
        using (var lease = new InvocationPayloadLease(input, cancellationToken))
        {
            try
            {
                result = await binding.Downserializer.DownserializeAsync(lease, writer, scratch, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                lease.Dispose();
                scratch.Dispose();
                RequireUnchanged(input, inputHash);
            }
        }

        scratch.RequireValidInvocation();
        cancellationToken.ThrowIfCancellationRequested();
        if (result is null || !string.Equals(result.Domain, registry.Domain, StringComparison.Ordinal)
            || !string.Equals(result.EventContractType, type, StringComparison.Ordinal)
            || !string.Equals(result.EventTypeName, writeAlias, StringComparison.Ordinal)
            || result.SourcePayloadVersion != sourceVersion || !string.Equals(result.SerializationFormat, format, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("DownserializeRejected: output identity disagrees with the selected alias.");
        }

        ImmutablePayload output;
        try
        {
            output = writer.TakeCompletedPayload();
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException("DownserializeRejected: output writer violated its completion contract.", exception);
        }

        try
        {
            byte[] outputHash = output.ComputeSha256();
            using (var aliasLease = new InvocationPayloadLease(output, cancellationToken))
            {
                try
                {
                    validateAlias(registry.Domain, type, writeAlias, sourceVersion, format, aliasLease, cancellationToken);
                }
                finally
                {
                    aliasLease.Dispose();
                    RequireUnchanged(output, outputHash);
                }
            }

            using (var currentLease = new InvocationPayloadLease(input, cancellationToken))
            using (var legacyLease = new InvocationPayloadLease(output, cancellationToken))
            {
                try
                {
                    validateRoundTrip(currentLease, legacyLease, cancellationToken);
                }
                finally
                {
                    currentLease.Dispose();
                    legacyLease.Dispose();
                    RequireUnchanged(input, inputHash);
                    RequireUnchanged(output, outputHash);
                }
            }

            scratch.RequireValidInvocation();
            cancellationToken.ThrowIfCancellationRequested();
            return output;
        }
        catch
        {
            output.Dispose();
            throw;
        }
    }

    private void Validate(ImmutablePayload payload, string type, int version, string format, CancellationToken cancellationToken)
    {
        byte[] before = payload.ComputeSha256();
        using var lease = new InvocationPayloadLease(payload, cancellationToken);
        try
        {
            validateVersion(registry.Domain, type, version, format, lease, cancellationToken);
        }
        finally
        {
            lease.Dispose();
            RequireUnchanged(payload, before);
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void RequireUnchanged(ImmutablePayload payload, byte[] expected)
    {
        if (!CryptographicOperations.FixedTimeEquals(expected, payload.ComputeSha256()))
        {
            throw new InvalidOperationException("DownserializeRejected: immutable conversion bytes changed.");
        }
    }
}
