using System.Reflection;
using System.Runtime.Loader;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Composes privately admitted managed images into one owned context with explicit shared object imports.</summary>
/// <remarks>
/// This dormant prerequisite binds declared managed references to retained private images or exact
/// supplied Default-context objects. It does not establish catalog completeness, immutable executed
/// framework/native bytes, serving-peer authority, process observation coverage or readiness.
/// Explicit path/stream loads bypass normal resolution and remain subject to the trusted-code policy
/// and separate observation qualification. No production registration consumes this composition.
/// </remarks>
internal sealed class EventManagedArtifactSet : IDisposable
{
    private readonly object _gate = new();
    private readonly EventEvolutionCapabilityLoss _capabilityLoss;
    private readonly Dictionary<string, EventManagedArtifact> _images = new(StringComparer.Ordinal);
    private readonly Dictionary<EventDependencyIdentity, string> _identities = [];
    private readonly HashSet<EventDependencyIdentity> _logicalDeclarations = [];
    private readonly HashSet<string> _simpleNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Assembly> _imports = new(StringComparer.Ordinal);
    private readonly Dictionary<string, EventManagedArtifactExecutionBinding> _bindings = new(StringComparer.Ordinal);
    private readonly HashSet<string> _loadingIdentities = new(StringComparer.Ordinal);
    private readonly List<EventRegistryRow> _importDeclarations = [];
    private readonly List<EventBufferReservation> _reservations = [];
    private EventManagedArtifactLoadContext? _context;
    private bool _disposed;

    /// <summary>Captures every private image and admits its static managed references before creating a loader context.</summary>
    internal EventManagedArtifactSet(IReadOnlyList<EventManagedArtifactSource> sources,
        IReadOnlyList<EventManagedAssemblyImport> sharedImports, EventBufferBudget budget,
        EventEvolutionCapabilityLoss capabilityLoss, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(sharedImports);
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentNullException.ThrowIfNull(capabilityLoss);
        cancellationToken.ThrowIfCancellationRequested();
        capabilityLoss.RequireNoObservedLoss();
        _capabilityLoss = capabilityLoss;
        int sourceCount = sources.Count;
        int importCount = sharedImports.Count;
        cancellationToken.ThrowIfCancellationRequested();
        if (sourceCount is < 1 or > 65_536 || importCount > 65_536 - sourceCount)
        {
            throw new InvalidOperationException("RegistryLimit: managed composition requires bounded source and import declarations.");
        }
        try
        {
            // Snapshot input slots once before any file, metadata or caller callback can change them.
            _reservations.Add(budget.Reserve(checked((sourceCount + importCount) * 512)));
            var sourceSnapshot = new EventManagedArtifactSource[sourceCount];
            var importSnapshot = new EventManagedAssemblyImport[importCount];
            for (int i = 0; i < sourceCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                sourceSnapshot[i] = sources[i];
                ArgumentNullException.ThrowIfNull(sourceSnapshot[i]);
            }
            for (int i = 0; i < importCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                importSnapshot[i] = sharedImports[i];
                ArgumentNullException.ThrowIfNull(importSnapshot[i]);
            }

            string? contextId = null;
            string? domain = null;
            var references = new List<string[]>();
            foreach (EventManagedArtifactSource source in sourceSnapshot)
            {
                cancellationToken.ThrowIfCancellationRequested();
                RequireManagedRow(source.Dependency);
                string declaredContext = source.Dependency.GetTextField(3);
                string declaredDomain = source.Dependency.GetTextKey(0);
                contextId ??= declaredContext;
                domain ??= declaredDomain;
                if (!string.Equals(contextId, declaredContext, StringComparison.Ordinal)
                    || !string.Equals(domain, declaredDomain, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("CapabilityMismatch: private managed sources require one domain and loader context.");
                }
                var artifact = new EventManagedArtifact(source.Dependency, source.ResolvedFile, budget, capabilityLoss, cancellationToken);
                try
                {
                    artifact.InspectManagedIdentities(budget, (identity, outgoing) =>
                    {
                        int capacity = checked(256 + identity.Length * 4 + outgoing.Length * 128);
                        foreach (string reference in outgoing) { capacity = checked(capacity + reference.Length * 4); }
                        _reservations.Add(budget.Reserve(capacity));
                        var logical = new EventDependencyIdentity(source.Dependency.GetTextKey(1), "managed");
                        if (!_simpleNames.Add(new AssemblyName(identity).Name!) || !_logicalDeclarations.Add(logical)
                            || !_identities.TryAdd(logical, identity) || !_images.TryAdd(identity, artifact))
                        {
                            throw new InvalidOperationException("CapabilityMismatch: private managed source identities are ambiguous.");
                        }
                        references.Add(outgoing);
                    }, cancellationToken);
                }
                catch { artifact.Dispose(); throw; }
            }

            foreach (EventManagedAssemblyImport import in importSnapshot)
            {
                cancellationToken.ThrowIfCancellationRequested();
                RequireManagedRow(import.Dependency);
                ArgumentNullException.ThrowIfNull(import.Assembly);
                Assembly assembly = import.Assembly;
                AssemblyLoadContext? actualContext = AssemblyLoadContext.GetLoadContext(assembly);
                if (!ReferenceEquals(actualContext, AssemblyLoadContext.Default) || assembly.IsDynamic
                    || string.IsNullOrEmpty(assembly.Location) || assembly.FullName is not string identity
                    || !string.Equals(import.Dependency.GetTextKey(0), domain, StringComparison.Ordinal)
                    || !string.Equals(import.Dependency.GetTextField(3), actualContext.Name, StringComparison.Ordinal)
                    || !string.Equals(import.Dependency.GetTextField(1), assembly.GetName().Version?.ToString(), StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("CapabilityMismatch: shared managed imports require exact declared Default-context objects.");
                }
                _reservations.Add(budget.Reserve(checked(import.Dependency.Encoded.Length * 4 + identity.Length * 4 + 256)));
                var declaration = new EventRegistryRow(import.Dependency.Encoded);
                _importDeclarations.Add(declaration);
                try
                {
                    using EventBufferReservation workspace = budget.Reserve(64 * 1024 + 256);
                    EventDependencyFileVerifier.RequireExactFile(declaration, assembly.Location, cancellationToken);
                }
                catch (IOException)
                {
                    throw new InvalidOperationException("CapabilityMismatch: the declared shared managed artifact is unavailable.");
                }
                var logical = new EventDependencyIdentity(declaration.GetTextKey(1), "managed");
                if (!_simpleNames.Add(assembly.GetName().Name!) || !_logicalDeclarations.Add(logical)
                    || _images.ContainsKey(identity) || !_imports.TryAdd(identity, assembly))
                {
                    throw new InvalidOperationException("CapabilityMismatch: shared managed import identities are ambiguous.");
                }
            }

            // Full static managed references of each retained private image must be declared,
            // including framework facade references. Shared framework execution remains unqualified.
            foreach (string[] outgoing in references)
            {
                foreach (string identity in outgoing)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!_images.ContainsKey(identity) && !_imports.ContainsKey(identity))
                    {
                        throw new InvalidOperationException("CapabilityMismatch: a private managed image has an undeclared managed reference.");
                    }
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            capabilityLoss.RequireNoObservedLoss();
            _context = new EventManagedArtifactLoadContext(contextId!, Resolve, capabilityLoss);
            _context.Unloading += OnContextUnloading;
            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
        }
        catch { Clear(); throw; }
    }

    /// <summary>Loads a declared private root and returns the same retained binding on each retry.</summary>
    internal EventManagedArtifactExecutionBinding Load(EventDependencyIdentity root, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequireActive();
            if (!_identities.TryGetValue(root, out string? identity))
            {
                throw new InvalidOperationException("CapabilityMismatch: the requested managed root is not privately declared.");
            }
            // Bind the entire privately admitted set while its fresh context is still unexposed.
            // Later static resolution uses these exact objects; it never probes a changed source.
            foreach (string declared in _images.Keys) { _ = LoadPrivate(declared, cancellationToken); }
            EventManagedArtifactExecutionBinding binding = LoadPrivate(identity, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            RequireActive();
            return binding;
        }
    }

    /// <summary>Checks every returned binding remains active in this exact context and shared loss scope.</summary>
    internal void RequireActive()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _capabilityLoss.RequireNoObservedLoss();
            foreach (EventManagedArtifact image in _images.Values) { image.RequireActive(); }
            foreach (EventManagedArtifactExecutionBinding binding in _bindings.Values)
            {
                binding.RequireCapabilityScope(_capabilityLoss);
                binding.RequireBoundAssembly(binding.Assembly);
            }
            if (_context is not null)
            {
                foreach (Assembly assembly in _context.Assemblies)
                {
                    if (assembly.FullName is not string identity || !_bindings.TryGetValue(identity, out EventManagedArtifactExecutionBinding? binding)
                        || !ReferenceEquals(binding.Assembly, assembly))
                    {
                        _capabilityLoss.ObserveViolation();
                    }
                }
                _capabilityLoss.RequireNoObservedLoss();
            }
        }
    }

    /// <summary>Fences dependent work, unloads the owned context and clears images before releasing composed charges.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) { return; }
            _disposed = true;
            _capabilityLoss.ObserveViolation();
            Clear();
        }
    }

    private Assembly Resolve(AssemblyName name)
    {
        lock (_gate)
        {
            RequireActive();
            string identity = name.FullName;
            if (_images.ContainsKey(identity)) { return LoadPrivate(identity, CancellationToken.None).Assembly; }
            if (_imports.TryGetValue(identity, out Assembly? shared)) { return shared; }
            _capabilityLoss.ObserveViolation();
            // Null and FileNotFoundException both continue resolution through Default/Resolving.
            throw new InvalidOperationException("CapabilityMismatch: undeclared managed resolution cannot use the Default fallback.");
        }
    }

    private EventManagedArtifactExecutionBinding LoadPrivate(string identity, CancellationToken cancellationToken)
    {
        if (_bindings.TryGetValue(identity, out EventManagedArtifactExecutionBinding? existing)) { return existing; }
        _loadingIdentities.Add(identity);
        try
        {
            EventManagedArtifactExecutionBinding binding = _images[identity].LoadInContext(_context!, cancellationToken);
            _bindings.Add(identity, binding);
            return binding;
        }
        finally { _loadingIdentities.Remove(identity); }
    }

    private void OnAssemblyLoad(object? sender, AssemblyLoadEventArgs arguments)
    {
        if (!ReferenceEquals(AssemblyLoadContext.GetLoadContext(arguments.LoadedAssembly), _context)) { return; }
        lock (_gate)
        {
            if (_disposed) { return; }
            Assembly assembly = arguments.LoadedAssembly;
            if (assembly.IsDynamic || assembly.FullName is not string identity
                || (!_loadingIdentities.Contains(identity)
                    && (!_bindings.TryGetValue(identity, out EventManagedArtifactExecutionBinding? binding)
                        || !ReferenceEquals(binding.Assembly, assembly))))
            {
                // Observe only: the explicit assembly load has already occurred.
                _capabilityLoss.ObserveViolation();
            }
        }
    }

    private void OnContextUnloading(AssemblyLoadContext context) => _capabilityLoss.ObserveViolation();

    private void Clear()
    {
        AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
        if (_context is not null)
        {
            _context.Unload();
            _context.Unloading -= OnContextUnloading;
            _context = null;
        }
        foreach (EventManagedArtifactExecutionBinding binding in _bindings.Values) { binding.Dispose(); }
        _bindings.Clear();
        _loadingIdentities.Clear();
        foreach (EventManagedArtifact artifact in _images.Values) { artifact.Dispose(); }
        _images.Clear();
        _identities.Clear();
        _logicalDeclarations.Clear();
        _simpleNames.Clear();
        _imports.Clear();
        foreach (EventRegistryRow row in _importDeclarations) { row.Dispose(); }
        _importDeclarations.Clear();
        foreach (EventBufferReservation reservation in _reservations) { reservation.Dispose(); }
        _reservations.Clear();
    }

    private static void RequireManagedRow(EventRegistryRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.Tag != 0x47 || row.GetTextKey(2) != "managed")
        {
            throw new InvalidOperationException("CapabilityMismatch: managed compositions require exact managed G declarations.");
        }
    }
}
