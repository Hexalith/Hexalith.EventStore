using System.Security.Cryptography;

using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Owns privately verified completed state and its actual operation fence through one command invocation.</summary>
internal sealed class PrivateLogicalCommandState : IDisposable
{
    private readonly RegisteredLogicalReplayBinding _binding;
    private readonly DaprLogicalClaimTrust _trust;
    private readonly Func<CancellationToken, Task> _actualOwnerFence;
    private readonly EventBufferBudget _budget;
    private readonly ImmutablePayload _state;
    private readonly DaprLogicalVerifiedClaim<DaprLogicalCommandStateClaim> _claim;
    private readonly EventBufferReservation _graphs;
    private CommandEnvelope? _command;
    private readonly EventBufferReservation _commandCharge;
    private bool _disposed;
    private int _used;

    private PrivateLogicalCommandState(RegisteredLogicalReplayBinding binding, DaprLogicalClaimTrust trust,
        Func<CancellationToken, Task> actualOwnerFence, EventBufferBudget budget, ImmutablePayload state,
        DaprLogicalVerifiedClaim<DaprLogicalCommandStateClaim> claim, EventBufferReservation graphs, CommandEnvelope command, EventBufferReservation commandCharge)
    {
        _binding = binding;
        _trust = trust;
        _actualOwnerFence = actualOwnerFence;
        _budget = budget;
        _state = state;
        _claim = claim;
        _graphs = graphs;
        _command = command;
        _commandCharge = commandCharge;
    }

    /// <summary>Captures only after the owning replay operation has admitted every committed participant.</summary>
    internal static PrivateLogicalCommandState Capture(ReadOnlySpan<byte> state, ReadOnlySpan<byte> proof,
        RegisteredLogicalReplayBinding binding, DaprLogicalClaimTrust trust, EventBufferBudget parent,
        Func<CancellationToken, Task> actualOwnerFence, CommandEnvelope command, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        binding.RequireCurrent(token);
        EventBufferBudget budget = parent.CreatePartition(binding.GetCommandCapacity(state.Length, proof.Length, command.Payload.Length));
        ImmutablePayload? owned = null;
        DaprLogicalVerifiedClaim<DaprLogicalCommandStateClaim>? claim = null;
        EventBufferReservation? graphs = null;
        EventBufferReservation? commandCharge = null;
        CommandEnvelope? commandCopy = null;
        try
        {
            claim = DaprLogicalCommandStateProofCodec.Verify(proof, trust, budget, token);
            if (!claim.Value.ReconstructionBindingHash.Span.SequenceEqual(binding.Fingerprint.Span)
                || !claim.Value.CanonicalStateHash.Span.SequenceEqual(SHA256.HashData(state)))
            {
                throw new InvalidOperationException("ProofMismatch: private completed state or reconstruction binding changed.");
            }
            EventBufferReservation charge = budget.Reserve(state.Length);
            try
            {
                owned = new ImmutablePayload(state.ToArray(), state.Length, token, charge);
            }
            catch
            {
                charge.Dispose();
                throw;
            }
            owned.RequireJsonState(token);
            graphs = binding.ReserveCommandGraphs(budget);
            if (!DaprLogicalCommandStateCodec.CommandHash(command, budget, token).AsSpan().SequenceEqual(claim.Value.CommandHash.Span))
            {
                throw new InvalidOperationException("ProofMismatch: actual owner's command changed.");
            }
            commandCharge = budget.Reserve(checked(command.Payload.Length + 4 * 1024 * 1024));
            commandCopy = command with
            {
                Payload = command.Payload.ToArray(),
                Extensions = command.Extensions is null ? null : new Dictionary<string, string>(command.Extensions, StringComparer.Ordinal)
            };
            return new PrivateLogicalCommandState(binding, trust, actualOwnerFence, budget, owned, claim, graphs, commandCopy, commandCharge);
        }
        catch
        {
            owned?.Dispose();
            claim?.Dispose();
            graphs?.Dispose();
            if (commandCopy is not null)
            {
                CryptographicOperations.ZeroMemory(commandCopy.Payload);
            }
            commandCharge?.Dispose();
            budget.Dispose();
            throw;
        }
    }

    /// <summary>Gets the sealed private command only for the admitted internal processor invocation.</summary>
    internal CommandEnvelope Command
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _command!;
        }
    }

    /// <summary>Gets the same parent-charged budget for bounded result workspace.</summary>
    internal EventBufferBudget Budget => _budget;

    /// <summary>Admits one exact command with no caller-supplied state before decoding the private canonical state.</summary>
    internal async Task<object> AdmitAsync(DomainServiceRequest request, CancellationToken token)
    {
        await RequireCurrentAsync(token).ConfigureAwait(false);
        if (Interlocked.Exchange(ref _used, 1) != 0 || request.CurrentState is not null || request.WriterMode is not null
            || request.RegistryFingerprint is not null || request.CommandStateProof is not null || request.VerifiedEffectiveEvents is not null
            || !DaprLogicalCommandStateCodec.CommandHash(request.Command, _budget, token).AsSpan().SequenceEqual(_claim.Value.CommandHash.Span))
        {
            throw new InvalidOperationException("ProofMismatch: completed state requires its one private exact command request.");
        }
        return await _binding.ReadCommandStateAsync(_state, _budget, RequireCurrentAsync, token).ConfigureAwait(false);
    }

    /// <summary>Rejects admission-stage mutation before any later stage or processor can consume that graph.</summary>
    internal Task RequireStateGraphAsync(object state, CancellationToken token)
        => _binding.RequireCommandGraphAsync(state, _state, _budget, RequireCurrentAsync, token);

    /// <summary>Rechecks original cancellation, proof expiry, binding and every actual durable participant.</summary>
    internal async Task RequireCurrentAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);
        _trust.RequireCommandCurrent(_claim.Value, token);
        _binding.RequireCurrent(token);
        if (!DaprLogicalCommandStateCodec.CommandHash(_command!, _budget, token).AsSpan().SequenceEqual(_claim.Value.CommandHash.Span))
        {
            throw new InvalidOperationException("ProofMismatch: private command changed during invocation.");
        }
        try
        {
            await _actualOwnerFence(token).ConfigureAwait(false);
        }
        finally
        {
            token.ThrowIfCancellationRequested();
        }
        ObjectDisposedException.ThrowIf(_disposed, this);
        _trust.RequireCommandCurrent(_claim.Value, token);
        _binding.RequireCurrent(token);
        if (!DaprLogicalCommandStateCodec.CommandHash(_command!, _budget, token).AsSpan().SequenceEqual(_claim.Value.CommandHash.Span))
        {
            throw new InvalidOperationException("ProofMismatch: private command changed during the actual owner await.");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _state.Dispose();
        _claim.Dispose();
        _graphs.Dispose();
        CryptographicOperations.ZeroMemory(_command!.Payload);
        ((Dictionary<string, string>?)_command.Extensions)?.Clear();
        _command = null;
        _commandCharge.Dispose();
        _budget.Dispose();
    }
}
