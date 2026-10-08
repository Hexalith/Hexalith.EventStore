using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using System.Security.Cryptography;

namespace Hexalith.EventStore.Client.Events;

/// <summary>Owns a bounded private G-pinned managed image and its isolated direct assembly load.</summary>
/// <remarks>
/// Loading reads this privately admitted image, never a subsequently replaceable file. This proves
/// only the direct image supplied to one owned managed loader context. Transitive/framework/native
/// dependencies, complete catalog admission, serving-peer pins and runtime allocation qualification
/// remain separate. A local managed observer may admit these stream-loaded images only with their
/// exact object evidence and matching pinned graph; no prerequisite is registered or grants readiness.
/// </remarks>
internal sealed class EventManagedArtifact : IDisposable
{
    private readonly object _gate = new();
    private readonly EventEvolutionCapabilityLoss _capabilityLoss;
    private readonly string _contextId;
    private readonly byte[] _hash = [];
    private readonly string _assemblyIdentity = string.Empty;
    private EventBufferReservation? _identityReservation;
    private EventRegistryRow? _declaration;
    private EventBufferReservation? _declarationReservation;
    private byte[]? _image;
    private EventBufferReservation? _reservation;
    private AssemblyLoadContext? _context;
    private Assembly? _assembly;
    private bool _disposed;
    private int _unloaded;

    /// <summary>Reserves an exact private image before allocation and verifies its G hash and managed version.</summary>
    internal EventManagedArtifact(EventRegistryRow dependency, string resolvedFile, EventBufferBudget budget,
        EventEvolutionCapabilityLoss capabilityLoss, CancellationToken cancellationToken,
        int maximumArtifactBytes = 64 * 1024 * 1024)
    {
        ArgumentNullException.ThrowIfNull(dependency);
        ArgumentException.ThrowIfNullOrEmpty(resolvedFile);
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentNullException.ThrowIfNull(capabilityLoss);
        if (dependency.Tag != 0x47 || dependency.GetTextKey(2) != "managed")
        {
            throw new ArgumentException("A private managed image requires an exact managed G row.", nameof(dependency));
        }
        if (maximumArtifactBytes is < 1 or > 64 * 1024 * 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumArtifactBytes));
        }

        cancellationToken.ThrowIfCancellationRequested();
        capabilityLoss.RequireNoObservedLoss();
        _capabilityLoss = capabilityLoss;
        try
        {
            // Charge encoded bytes, decoded primary keys, context text and fixed metadata
            // before retaining the exact original declaration alongside the private image.
            _declarationReservation = budget.Reserve(checked(dependency.Encoded.Length * 4 + 128));
            _declaration = new EventRegistryRow(dependency.Encoded);
            _contextId = _declaration.GetTextField(3);
            _hash = _declaration.GetEncodedField(2).ToArray();
            using FileStream file = File.OpenRead(resolvedFile);
            long length = file.Length;
            if (length < 1 || length > maximumArtifactBytes - _declaration.Encoded.Length)
            {
                throw new InvalidOperationException("RegistryLimit: a managed image exceeds its admitted artifact capacity.");
            }

            _reservation = budget.Reserve(checked((int)length));
            _image = new byte[(int)length];
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            int offset = 0;
            while (offset < _image.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int read = file.Read(_image.AsSpan(offset, Math.Min(64 * 1024, _image.Length - offset)));
                if (read == 0)
                {
                    throw new InvalidOperationException("CapabilityMismatch: a managed artifact changed during private admission.");
                }

                hash.AppendData(_image.AsSpan(offset, read));
                offset += read;
            }

            cancellationToken.ThrowIfCancellationRequested();
            byte[] actual = hash.GetHashAndReset();
            try
            {
                if (file.ReadByte() != -1 || !actual.AsSpan().SequenceEqual(_hash))
                {
                    throw new InvalidOperationException("CapabilityMismatch: private managed image bytes disagree with the dependency row.");
                }
            }
            finally { CryptographicOperations.ZeroMemory(actual); }

            // Metadata parsing can retain at most another image-sized private workspace.
            // Keep its capacity charged while the parser is alive; no executable code is loaded.
            using (EventBufferReservation metadata = budget.Reserve(_image.Length))
            using (var image = new MemoryStream(_image, 0, _image.Length, writable: false, publiclyVisible: false))
            using (var reader = new PEReader(image))
            {
                if (!reader.HasMetadata || !reader.GetMetadataReader().IsAssembly
                    || !string.Equals(reader.GetMetadataReader().GetAssemblyDefinition().Version.ToString(),
                        _declaration.GetTextField(1), StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("CapabilityMismatch: private managed metadata disagrees with its dependency row.");
                }
                MetadataReader metadataReader = reader.GetMetadataReader();
                AssemblyDefinition definition = metadataReader.GetAssemblyDefinition();
                string identity = GetManagedIdentity(metadataReader, definition.Name, definition.Version,
                    definition.Culture, definition.PublicKey, definition.Flags);
                _identityReservation = budget.Reserve(checked(identity.Length * 4 + 128));
                _assemblyIdentity = identity;
            }

            cancellationToken.ThrowIfCancellationRequested();
            capabilityLoss.RequireNoObservedLoss();
        }
        catch (Exception error) when (error is IOException or BadImageFormatException)
        {
            ClearPrivateImage();
            throw new InvalidOperationException("CapabilityMismatch: the declared managed artifact cannot be privately admitted.");
        }
        catch
        {
            ClearPrivateImage();
            throw;
        }
    }

    /// <summary>Loads the admitted image once into a privately owned context and returns direct object evidence.</summary>
    internal EventManagedArtifactExecutionBinding Load(CancellationToken cancellationToken)
        => LoadInContext(null, cancellationToken);

    /// <summary>Loads the retained image into one composition-owned context instead of probing a file path.</summary>
    /// <remarks>The composition must admit all its managed references before calling this internal seam.</remarks>
    internal EventManagedArtifactExecutionBinding LoadInContext(EventManagedArtifactLoadContext? context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            RequireActive();
            RequirePrivateImageHash(cancellationToken);
            if (_assembly is null)
            {
                // The context is private until the initial load completes. An existing foreign
                // same-identity assembly cannot be substituted for this first supplied image.
                if (context is not null && !string.Equals(context.Name, _contextId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("CapabilityMismatch: a managed image requires its declared loader context.");
                }
                if (context is not null && context.Assemblies.Any(assembly =>
                    string.Equals(assembly.FullName, _assemblyIdentity, StringComparison.Ordinal)))
                {
                    _capabilityLoss.ObserveViolation();
                    throw new InvalidOperationException("CapabilityMismatch: a managed image cannot bind pre-existing context contents.");
                }
                _context = context ?? new AssemblyLoadContext(_contextId, isCollectible: true);
                _context.Unloading += OnContextUnloading;
                try
                {
                    using var image = new MemoryStream(_image!, 0, _image!.Length, writable: false, publiclyVisible: false);
                    _assembly = _context.LoadFromStream(image);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    _capabilityLoss.ObserveViolation();
                    _context?.Unload();
                    throw;
                }
                catch
                {
                    _capabilityLoss.ObserveViolation();
                    _context?.Unload();
                    throw new InvalidOperationException("CapabilityMismatch: the admitted managed image could not be loaded.");
                }
            }
            else if (context is not null && !ReferenceEquals(context, _context))
            {
                throw new InvalidOperationException("CapabilityMismatch: a managed image is already bound to another loader context.");
            }

            // Load observations cannot undo a completed load. Cancellation or observed loss
            // refuses the uncommitted binding; the caller retains existing effect/recovery rules.
            cancellationToken.ThrowIfCancellationRequested();
            RequireActive();
            RequirePrivateImageHash(cancellationToken);
            return new EventManagedArtifactExecutionBinding(this, _assembly);
        }
    }

    /// <summary>Reads exact managed identities from the retained private image under a bounded temporary workspace.</summary>
    /// <remarks>The returned strings are charged and retained by the composition that requested them.</remarks>
    internal void InspectManagedIdentities(EventBufferBudget budget, Action<string, string[]> inspect, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentNullException.ThrowIfNull(inspect);
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequireActive();
            RequirePrivateImageHash(cancellationToken);
            using EventBufferReservation workspace = budget.Reserve(checked(_image!.Length * 8));
            using var image = new MemoryStream(_image, 0, _image.Length, writable: false, publiclyVisible: false);
            using var reader = new PEReader(image);
            MetadataReader metadata = reader.GetMetadataReader();
            if (metadata.AssemblyReferences.Count > 65_536)
            {
                throw new InvalidOperationException("RegistryLimit: a private managed image has too many reference declarations.");
            }
            AssemblyDefinition definition = metadata.GetAssemblyDefinition();
            string identity = GetManagedIdentity(metadata, definition.Name, definition.Version, definition.Culture,
                definition.PublicKey, definition.Flags);
            using EventBufferReservation slots = budget.Reserve(checked(metadata.AssemblyReferences.Count * 128));
            var referenceCharges = new List<EventBufferReservation>();
            try
            {
                var references = new string[metadata.AssemblyReferences.Count];
                int index = 0;
                foreach (AssemblyReferenceHandle handle in metadata.AssemblyReferences)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    AssemblyReference reference = metadata.GetAssemblyReference(handle);
                    string referencedIdentity = GetManagedIdentity(metadata, reference.Name, reference.Version, reference.Culture,
                        reference.PublicKeyOrToken, reference.Flags);
                    referenceCharges.Add(budget.Reserve(checked(referencedIdentity.Length * 4 + 128)));
                    references[index++] = referencedIdentity;
                }
                cancellationToken.ThrowIfCancellationRequested();
                inspect(identity, references);
            }
            finally { foreach (EventBufferReservation reservation in referenceCharges) { reservation.Dispose(); } }
            cancellationToken.ThrowIfCancellationRequested();
            RequireActive();
        }
    }

    private static string GetManagedIdentity(MetadataReader metadata, StringHandle name, Version version,
        StringHandle culture, BlobHandle key, AssemblyFlags flags)
    {
        var identity = new AssemblyName
        {
            Name = metadata.GetString(name), Version = version, CultureName = metadata.GetString(culture),
        };
        if ((flags & AssemblyFlags.Retargetable) != 0) { identity.Flags |= AssemblyNameFlags.Retargetable; }
        if ((flags & AssemblyFlags.ContentTypeMask) != 0)
        {
            if ((flags & AssemblyFlags.ContentTypeMask) != AssemblyFlags.WindowsRuntime)
            {
                throw new InvalidOperationException("CapabilityMismatch: a managed image has an unsupported content identity.");
            }
            identity.ContentType = AssemblyContentType.WindowsRuntime;
        }
        byte[] publicKeyOrToken = metadata.GetBlobBytes(key);
        if ((flags & AssemblyFlags.PublicKey) != 0) { identity.SetPublicKey(publicKeyOrToken); }
        else { identity.SetPublicKeyToken(publicKeyOrToken); }
        return identity.FullName;
    }

    /// <summary>Refuses direct-image use after disposal, context unload or observed capability loss.</summary>
    internal void RequireActive()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (Volatile.Read(ref _unloaded) != 0) { _capabilityLoss.ObserveViolation(); }
            _capabilityLoss.RequireNoObservedLoss();
        }
    }

    /// <summary>Records loss of a binding whose identity was used for a catalog implementation.</summary>
    internal void ObserveBindingLoss() => _capabilityLoss.ObserveViolation();

    /// <summary>Requires artifact lifetime and registry work to share one sticky observed-loss boundary.</summary>
    internal void RequireCapabilityScope(EventEvolutionCapabilityLoss capabilityLoss)
    {
        RequireActive();
        if (!ReferenceEquals(_capabilityLoss, capabilityLoss))
        {
            throw new InvalidOperationException("CapabilityMismatch: managed artifact and registry use different observed-loss scopes.");
        }
    }

    /// <summary>Requires the exact originally admitted domain, dependency identity, version, hash and context row.</summary>
    internal void RequireDependencyDeclaration(EventRegistryRow declaration)
    {
        lock (_gate)
        {
            RequireActive();
            if (!_declaration!.Encoded.SequenceEqual(declaration.Encoded))
            {
                throw new InvalidOperationException("CapabilityMismatch: the private managed image was admitted under a different dependency declaration.");
            }
        }
    }

    /// <summary>Copies the retained image digest only for the direct assembly loaded by this owner.</summary>
    internal byte[] CopyLoadedHashForAssembly(Assembly assembly)
    {
        lock (_gate)
        {
            RequireActive();
            if (!ReferenceEquals(assembly, _assembly))
            {
                throw new InvalidOperationException("CapabilityMismatch: an assembly object has no direct image evidence from this owner.");
            }

            return _hash.ToArray();
        }
    }

    /// <summary>Fences use, requests context unload and clears the entire private image before releasing capacity.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) { return; }
            _disposed = true;
            _capabilityLoss.ObserveViolation();
            if (_context is not null)
            {
                _context.Unload();
                _context.Unloading -= OnContextUnloading;
                _context = null;
                _assembly = null;
            }

            ClearPrivateImage();
        }
    }

    private void OnContextUnloading(AssemblyLoadContext context)
    {
        Interlocked.Exchange(ref _unloaded, 1);
        _capabilityLoss.ObserveViolation();
    }

    private void ClearPrivateImage()
    {
        if (_image is not null)
        {
            CryptographicOperations.ZeroMemory(_image);
            _image = null;
        }

        CryptographicOperations.ZeroMemory(_hash);
        _reservation?.Dispose();
        _reservation = null;
        _identityReservation?.Dispose();
        _identityReservation = null;
        _declaration?.Dispose();
        _declaration = null;
        _declarationReservation?.Dispose();
        _declarationReservation = null;
    }

    private void RequirePrivateImageHash(CancellationToken cancellationToken)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        for (int offset = 0; offset < _image!.Length; offset += 64 * 1024)
        {
            cancellationToken.ThrowIfCancellationRequested();
            hash.AppendData(_image.AsSpan(offset, Math.Min(64 * 1024, _image.Length - offset)));
        }

        cancellationToken.ThrowIfCancellationRequested();
        byte[] current = hash.GetHashAndReset();
        try
        {
            if (!current.AsSpan().SequenceEqual(_hash))
            {
                _capabilityLoss.ObserveViolation();
                ClearPrivateImage();
                throw new InvalidOperationException("CapabilityMismatch: the retained private managed image changed before binding use.");
            }
        }
        finally { CryptographicOperations.ZeroMemory(current); }
    }
}
