using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Detects managed-load policy violations in explicitly inventoried local loader contexts.</summary>
/// <remarks>
/// This dormant observer checks supplied graph/file declarations and observed assembly identities,
/// contexts, locations and current file bytes. A matching observation does not bind the loaded image
/// to an immutable artifact, establish graph completeness, cover unlisted contexts or native loads,
/// or confer readiness. It cannot prevent or undo effects executed before an observation. The host
/// must separately qualify complete process coverage and immutable execution binding before use.
/// </remarks>
internal sealed class EventEvolutionManagedLoadObserver : IDisposable
{
    private const long MaximumManifestBytes = 64L * 1024 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly object _gate = new();
    private readonly EventEvolutionCapabilityLoss _capabilityLoss;
    private readonly AssemblyLoadContext[] _contexts;
    private readonly HashSet<AssemblyLoadContext> _contextSet;
    private readonly Dictionary<(AssemblyLoadContext Context, string FullName), (string File, EventRegistryRow Declaration)> _bindings = [];
    private readonly Dictionary<Assembly, EventManagedArtifactExecutionBinding> _privateImages = [];
    private bool _disposed;

    /// <summary>Checks supplied local dependencies, attaches observations, then reconciles existing loads.</summary>
    internal EventEvolutionManagedLoadObserver(
        EventDomainRegistry registry,
        IReadOnlyList<EventResolvedDependency> resolvedGraph,
        IReadOnlyList<EventDependencyIdentity> executedRoots,
        IReadOnlyList<EventManagedLoadContext> loaderContexts,
        CancellationToken cancellationToken,
        IReadOnlyList<EventManagedArtifactExecutionBinding>? privateImages = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(resolvedGraph);
        ArgumentNullException.ThrowIfNull(executedRoots);
        ArgumentNullException.ThrowIfNull(loaderContexts);
        cancellationToken.ThrowIfCancellationRequested();
        registry.CapabilityLoss.RequireNoObservedLoss();
        if (loaderContexts.Count is < 1 or > 65_536
            || resolvedGraph.Count is < 1 or > 65_536 || executedRoots.Count is < 1 or > 65_536
            || privateImages?.Count > 65_536)
        {
            throw new InvalidOperationException("RegistryLimit: managed observations require bounded loader contexts.");
        }

        _capabilityLoss = registry.CapabilityLoss;
        EventResolvedDependency[] graph = resolvedGraph.ToArray();
        EventDependencyIdentity[] roots = executedRoots.ToArray();
        EventManagedLoadContext[] suppliedContexts = loaderContexts.ToArray();
        EventManagedArtifactExecutionBinding[] images = privateImages?.ToArray() ?? [];
        EventDependencyClosureVerifier.RequireResolvedGraph(registry, graph, roots, cancellationToken);
        var contexts = new Dictionary<string, AssemblyLoadContext>(StringComparer.Ordinal);
        var runtimeContexts = new HashSet<AssemblyLoadContext>();
        long accounted = 64 * 1024; // One serialized observation's hashing workspace.
        foreach (EventManagedLoadContext entry in suppliedContexts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(entry);
            ArgumentException.ThrowIfNullOrEmpty(entry.LoaderContextId);
            ArgumentNullException.ThrowIfNull(entry.Context);
            Account(ref accounted, 128L + StrictUtf8.GetByteCount(entry.LoaderContextId) * 4L);
            if (!contexts.TryAdd(entry.LoaderContextId, entry.Context) || !runtimeContexts.Add(entry.Context))
            {
                throw new InvalidOperationException("CapabilityMismatch: managed loader contexts must have unique identities and runtime instances.");
            }
        }

        _contexts = runtimeContexts.ToArray();
        _contextSet = runtimeContexts;
        var dependencies = registry.Rows.Where(static row => row.Tag == 0x47)
            .ToDictionary(static row => new EventDependencyIdentity(row.GetTextKey(1), row.GetTextKey(2)));
        var usedContexts = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            foreach (EventResolvedDependency node in graph.Where(static node => node.Identity.Kind == "managed"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!contexts.TryGetValue(node.LoaderContextId, out AssemblyLoadContext? context))
                {
                    throw new InvalidOperationException("CapabilityMismatch: a managed dependency has no observed runtime context.");
                }

                EventRegistryRow row = dependencies[node.Identity];
                string file = Path.GetFullPath(node.ResolvedFile);
                AssemblyName name = AssemblyName.GetAssemblyName(file);
                string fullName = name.FullName;
                if (!string.Equals(name.Version?.ToString(), node.ResolvedVersionOrAbiIdentity, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("CapabilityMismatch: managed artifact version disagrees with its dependency row.");
                }

                // Recheck after metadata inspection. This detects local replacement during this
                // check; immutable execution binding still belongs to deployment qualification.
                EventDependencyFileVerifier.RequireExactFile(row, file, cancellationToken);
                Account(ref accounted, 384L + row.Encoded.Length * 4L
                    + StrictUtf8.GetByteCount(fullName) * 4L + StrictUtf8.GetByteCount(file) * 4L);
                var declaration = new EventRegistryRow(row.Encoded);
                if (!_bindings.TryAdd((context, fullName), (file, declaration)))
                {
                    declaration.Dispose();
                    throw new InvalidOperationException("CapabilityMismatch: managed assembly identity is ambiguous in its observed context.");
                }

                usedContexts.Add(node.LoaderContextId);
            }

            if (_bindings.Count == 0 || usedContexts.Count != contexts.Count)
            {
                throw new InvalidOperationException("CapabilityMismatch: every observed context requires a declared managed dependency.");
            }

            foreach (EventManagedArtifactExecutionBinding image in images)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ArgumentNullException.ThrowIfNull(image);
                image.RequireCapabilityScope(registry.CapabilityLoss);
                Assembly assembly = image.Assembly;
                AssemblyLoadContext? context = AssemblyLoadContext.GetLoadContext(assembly);
                Account(ref accounted, 128);
                if (context is null || assembly.FullName is not string fullName
                    || !_bindings.TryGetValue((context, fullName), out (string File, EventRegistryRow Declaration) declared)
                    || !_privateImages.TryAdd(assembly, image))
                {
                    throw new InvalidOperationException("CapabilityMismatch: a private managed image is missing, duplicated or outside its declared context.");
                }

                image.RequireDependencyDeclaration(declared.Declaration);
                byte[] actualHash = image.CopyHashForAssembly(assembly);
                try
                {
                    if (!actualHash.AsSpan().SequenceEqual(declared.Declaration.GetEncodedField(2)))
                    {
                        throw new InvalidOperationException("CapabilityMismatch: a private managed image disagrees with the supplied pinned graph.");
                    }
                }
                finally { CryptographicOperations.ZeroMemory(actualHash); }
            }

            // Subscribe before enumeration: existing loads are reconciled and later loads cannot
            // fall into a gap between the baseline snapshot and event subscription.
            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
            foreach (AssemblyLoadContext context in _contexts) { context.Unloading += OnContextUnloading; }
            ReconcileCurrentLoads(cancellationToken);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>Rechecks currently observed managed loads without claiming immutable execution binding.</summary>
    internal void ReconcileCurrentLoads(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _capabilityLoss.RequireNoObservedLoss();
            RequirePresentContexts();
            foreach (AssemblyLoadContext context in _contexts)
            {
                foreach (Assembly assembly in context.Assemblies)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ObserveAssembly(assembly, cancellationToken);
                    _capabilityLoss.RequireNoObservedLoss();
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            RequirePresentContexts();
            _capabilityLoss.RequireNoObservedLoss();
        }
    }

    /// <summary>Ends local observation and fences subsequent capability after coverage is lost.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) { return; }
            _disposed = true;
            _capabilityLoss.ObserveViolation();
            AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
            foreach (AssemblyLoadContext context in _contexts) { context.Unloading -= OnContextUnloading; }
            foreach ((string _, EventRegistryRow declaration) in _bindings.Values) { declaration.Dispose(); }
            _bindings.Clear();
            _privateImages.Clear();
        }
    }

    private void OnAssemblyLoad(object? sender, AssemblyLoadEventArgs arguments)
    {
        AssemblyLoadContext? context = AssemblyLoadContext.GetLoadContext(arguments.LoadedAssembly);
        if (context is null || !_contextSet.Contains(context)) { return; }
        lock (_gate)
        {
            if (_disposed) { return; }
            ObserveAssembly(arguments.LoadedAssembly, CancellationToken.None);
        }
    }

    private void ObserveAssembly(Assembly assembly, CancellationToken cancellationToken)
    {
        try
        {
            AssemblyLoadContext? context = AssemblyLoadContext.GetLoadContext(assembly);
            if (context is null || !_contextSet.Contains(context)) { return; }
            if (assembly.IsDynamic || assembly.FullName is not string fullName
                || !_bindings.TryGetValue((context, fullName), out (string File, EventRegistryRow Declaration) binding))
            {
                _capabilityLoss.ObserveViolation();
                return;
            }

            if (_privateImages.TryGetValue(assembly, out EventManagedArtifactExecutionBinding? image))
            {
                image.RequireCapabilityScope(_capabilityLoss);
                image.RequireDependencyDeclaration(binding.Declaration);
                byte[] actualHash = image.CopyHashForAssembly(assembly);
                try
                {
                    if (!actualHash.AsSpan().SequenceEqual(binding.Declaration.GetEncodedField(2))) { _capabilityLoss.ObserveViolation(); }
                }
                finally { CryptographicOperations.ZeroMemory(actualHash); }
                return;
            }

            if (string.IsNullOrEmpty(assembly.Location)
                || !string.Equals(Path.GetFullPath(assembly.Location), binding.File,
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            {
                _capabilityLoss.ObserveViolation();
                return;
            }

            if (!CurrentFileMatches(assembly.Location, binding.Declaration.GetEncodedField(2), cancellationToken))
            {
                _capabilityLoss.ObserveViolation();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Runtime load callbacks must not throw, disclose paths or claim prevention.
            _capabilityLoss.ObserveViolation();
        }
    }

    private void OnContextUnloading(AssemblyLoadContext context) => _capabilityLoss.ObserveViolation();

    private static bool CurrentFileMatches(string location, ReadOnlySpan<byte> expectedHash, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using FileStream stream = File.OpenRead(location);
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[64 * 1024];
        byte[]? digest = null;
        try
        {
            int read;
            while ((read = stream.Read(buffer)) != 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                hash.AppendData(buffer.AsSpan(0, read));
            }

            cancellationToken.ThrowIfCancellationRequested();
            digest = hash.GetHashAndReset();
            return digest.AsSpan().SequenceEqual(expectedHash);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffer);
            if (digest is not null) { CryptographicOperations.ZeroMemory(digest); }
        }
    }

    private void RequirePresentContexts()
    {
        foreach (AssemblyLoadContext context in _contexts)
        {
            if (!AssemblyLoadContext.All.Contains(context)) { _capabilityLoss.ObserveViolation(); }
        }

        _capabilityLoss.RequireNoObservedLoss();
    }

    private static void Account(ref long accounted, long capacity)
    {
        accounted = checked(accounted + capacity);
        if (accounted > MaximumManifestBytes)
        {
            throw new InvalidOperationException("RegistryLimit: managed observation declarations exceed 64 MiB.");
        }
    }
}
