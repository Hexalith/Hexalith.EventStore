using System.Security.Cryptography;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Binds an explicit callable to exact implementation identity, file bytes and canonical declared options.</summary>
/// <remarks>
/// Retained bindings admit both implementation and options callables to exact runtime image objects and one loss scope.
/// File-only bindings remain local claims; complete dependency catalogs, framework execution and startup readiness are separate requirements.
/// </remarks>
internal sealed class EventImplementationBinding
{
    private readonly string _implementationId;
    private readonly byte[] _assemblyHash;
    private readonly byte[] _optionsHash;
    private readonly EventOptionRule[] _optionSchema;
    private readonly Func<ReadOnlyMemory<byte>>? _runtimeOptions;
    private readonly EventManagedArtifactExecutionBinding? _executionBinding;
    private readonly EventManagedArtifactExecutionBinding? _runtimeOptionsExecutionBinding;
    private readonly System.Reflection.Assembly _implementationAssembly;
    private readonly System.Reflection.Assembly? _runtimeOptionsAssembly;
    private EventEvolutionCapabilityLoss? _capabilityLoss;

    /// <summary>Admits single callables, exact optional image bindings and expanded schema hashes before callback use.</summary>
    internal EventImplementationBinding(string implementationId, Delegate implementation, ReadOnlyMemory<byte> canonicalOptions,
        IReadOnlyList<EventOptionRule> optionSchema, Func<ReadOnlyMemory<byte>>? runtimeOptions = null,
        EventManagedArtifactExecutionBinding? executionBinding = null,
        EventManagedArtifactExecutionBinding? runtimeOptionsExecutionBinding = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(implementationId);
        ArgumentNullException.ThrowIfNull(implementation);
        ArgumentNullException.ThrowIfNull(optionSchema);
        if (implementation.GetInvocationList().Length != 1)
        {
            throw new ArgumentException("A registered implementation must be one explicit callable.", nameof(implementation));
        }
        if (runtimeOptions is not null && runtimeOptions.GetInvocationList().Length != 1)
        {
            throw new ArgumentException("A runtime options source must be one explicit callable.", nameof(runtimeOptions));
        }
        if (runtimeOptions is null && runtimeOptionsExecutionBinding is not null)
        {
            throw new ArgumentException("An options execution binding requires its exact callable.", nameof(runtimeOptionsExecutionBinding));
        }
        _implementationId = implementationId;
        _optionSchema = optionSchema.ToArray();
        _optionsHash = EventOptionsManifestCodec.ComputeHash(canonicalOptions, _optionSchema);
        _runtimeOptions = runtimeOptions;
        _implementationAssembly = implementation.Method.Module.Assembly;
        _executionBinding = executionBinding;
        _runtimeOptionsAssembly = runtimeOptions?.Method.Module.Assembly;
        // A retained implementation defaults to an options source from its same exact
        // runtime image. A separately retained source needs an explicit binding.
        _runtimeOptionsExecutionBinding = runtimeOptions is null ? null : runtimeOptionsExecutionBinding ?? executionBinding;
        RequireCallableBindings();
        if (executionBinding is not null)
        {
            _assemblyHash = executionBinding.CopyHashForAssembly(_implementationAssembly);
            return;
        }

        string location = _implementationAssembly.Location;
        if (string.IsNullOrEmpty(location))
        {
            throw new ArgumentException("An implementation file must be resolvable.", nameof(implementation));
        }

        using FileStream stream = File.OpenRead(location);
        _assemblyHash = SHA256.HashData(stream);
    }

    /// <summary>Requires the exact adjacent ID, assembly and options fields from a decoded descriptor.</summary>
    internal void RequireFields(EventRegistryRow descriptor, int implementationField, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireDeclaredFields(descriptor, implementationField);
        if (_runtimeOptions is not null) { RequireRuntimeOptions(cancellationToken); }
        RequireDeclaredFields(descriptor, implementationField);
        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>Fences the runtime-options getter independently before parsing any returned settings.</summary>
    internal async ValueTask RequireFieldsAsync(EventRegistryRow descriptor, int implementationField,
        Func<CancellationToken, Task> sourceFence, CancellationToken token, Action? requireCurrent = null)
    {
        token.ThrowIfCancellationRequested();
        RequireDeclaredFields(descriptor, implementationField);
        if (_runtimeOptions is not null)
        {
            await sourceFence(token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            RequireDeclaredFields(descriptor, implementationField);
            ReadOnlyMemory<byte> options;
            try { options = _runtimeOptions(); }
            finally { token.ThrowIfCancellationRequested(); }
            token.ThrowIfCancellationRequested();
            requireCurrent?.Invoke();
            RequireDeclaredFields(descriptor, implementationField);
            await sourceFence(token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            RequireDeclaredFields(descriptor, implementationField);
            RequireOptionsHash(options, token);
        }

        token.ThrowIfCancellationRequested();
        RequireDeclaredFields(descriptor, implementationField);
    }

    /// <summary>Checks immutable descriptor fields without invoking the implementation's runtime options callback.</summary>
    internal void RequireDeclaredFields(EventRegistryRow descriptor, int implementationField)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        RequireCallableBindings();
        if (!string.Equals(_implementationId, descriptor.GetTextField(implementationField), StringComparison.Ordinal)
            || !_assemblyHash.AsSpan().SequenceEqual(descriptor.GetEncodedField(implementationField + 1))
            || !_optionsHash.AsSpan().SequenceEqual(descriptor.GetEncodedField(implementationField + 2)))
        {
            throw new InvalidOperationException("CapabilityMismatch: implementation bytes or options disagree with the descriptor.");
        }
    }

    /// <summary>Checks direct-image evidence shares the executing registry's sticky loss scope.</summary>
    internal void RequireCapabilityScope(EventEvolutionCapabilityLoss capabilityLoss)
    {
        ArgumentNullException.ThrowIfNull(capabilityLoss);
        capabilityLoss.RequireNoObservedLoss();
        _executionBinding?.RequireCapabilityScope(capabilityLoss);
        _runtimeOptionsExecutionBinding?.RequireCapabilityScope(capabilityLoss);
        EventEvolutionCapabilityLoss? prior = Interlocked.CompareExchange(ref _capabilityLoss, capabilityLoss, null);
        if (prior is not null && !ReferenceEquals(prior, capabilityLoss))
        {
            throw new InvalidOperationException("CapabilityMismatch: implementation options belong to another capability scope.");
        }
        RequireCallableBindings();
    }

    /// <summary>Checks current implementation-owned settings against the expanded declared options.</summary>
    internal void RequireRuntimeOptions() => RequireRuntimeOptions(CancellationToken.None);

    /// <summary>Checks settings through the original cancellation and admitted callable boundaries.</summary>
    internal void RequireRuntimeOptions(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireCallableBindings();
        if (_runtimeOptions is null)
        {
            throw new InvalidOperationException("CapabilityMismatch: no runtime settings source is bound to this implementation.");
        }

        ReadOnlyMemory<byte> options = _runtimeOptions();
        // Refuse an uncommitted route immediately after application code returns,
        // before hashing or parsing its potentially invalid returned options.
        cancellationToken.ThrowIfCancellationRequested();
        RequireCallableBindings();
        RequireOptionsHash(options, cancellationToken);
    }

    private void RequireOptionsHash(ReadOnlyMemory<byte> options, CancellationToken cancellationToken)
    {
        byte[] currentHash = EventOptionsManifestCodec.ComputeHash(options, _optionSchema);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequireCallableBindings();
            if (!_optionsHash.AsSpan().SequenceEqual(currentHash))
            {
                throw new InvalidOperationException("CapabilityMismatch: executing runtime settings disagree with the sealed options manifest.");
            }
        }
        finally { CryptographicOperations.ZeroMemory(currentHash); }
    }

    private void RequireCallableBindings()
    {
        _capabilityLoss?.RequireNoObservedLoss();
        _executionBinding?.RequireBoundAssembly(_implementationAssembly);
        if (_runtimeOptionsAssembly is not null)
        {
            _runtimeOptionsExecutionBinding?.RequireBoundAssembly(_runtimeOptionsAssembly);
        }
        _capabilityLoss?.RequireNoObservedLoss();
    }
}
