using System.Collections.Frozen;
using System.Security.Cryptography;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Client.Queries;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.EventStore.DomainService.Queries;
/// <summary>Retains one exact local query row, scoped registration, codecs and pure input resolver.</summary>
internal sealed class LogicalQueryDescriptor : IDisposable
{
    private readonly EventRegistryRow _row;
    private readonly Func<IServiceProvider, IDomainQueryHandler> _factory;
    private readonly Func<QueryEnvelope, CancellationToken, IReadOnlyList<LogicalQueryReadPlanEntry>> _resolve;
    private readonly Action<IReadOnlyPayload, CancellationToken> _requestValidator;
    private readonly Action<IReadOnlyPayload, CancellationToken> _responseValidator;
    private readonly LogicalQueryCallable _registration;
    private readonly LogicalQueryCallable _resolver;
    private readonly LogicalQueryCallable _request;
    private readonly LogicalQueryCallable _response;
    private readonly Action<CancellationToken> _current;
    private readonly FrozenDictionary<string, LogicalQueryInputMapping> _mappings;
    private readonly EventRegistryRow[] _inputs;
    private readonly byte[] _compatibilityHash;
    private int _disposed;
    /// <summary>Admits exact row fields against supplied handlers, manifest-bound callables and input mappings.</summary>
    internal LogicalQueryDescriptor(ReadOnlySpan<byte> row, Type handlerType, string endpoint, Func<IServiceProvider, IDomainQueryHandler> factory, Func<QueryEnvelope, CancellationToken, IReadOnlyList<LogicalQueryReadPlanEntry>> resolve, Action<IReadOnlyPayload, CancellationToken> requestValidator, Action<IReadOnlyPayload, CancellationToken> responseValidator, LogicalQueryCallable registration, LogicalQueryCallable resolver, LogicalQueryCallable request, LogicalQueryCallable response, IReadOnlyList<LogicalQueryInputMapping> mappings, int maximumResponseBytes, int workingBytes, Action<CancellationToken> current, IReadOnlyList<ReadOnlyMemory<byte>> inputRows, IReadOnlyList<ReadOnlyMemory<byte>> dependencyRows)
    {
        _row = new EventRegistryRow(row, allowCatalog: true);
        using var catalogBudget = new EventBufferBudget();
        _compatibilityHash = LogicalQueryCatalogCodec.Compute(row, inputRows, dependencyRows, catalogBudget);
        _inputs = inputRows.Select(static x => new EventRegistryRow(x.Span, allowCatalog: true)).ToArray();
        if (_inputs.Length != 2 || _inputs[0].Tag != 0x52 || _inputs[1].Tag != 0x59 || _inputs[0].GetTextKey(1) != _inputs[1].GetTextKey(1) || _inputs[1].GetTextField(2) != "shared" || _inputs[0].GetTextField(18) != _inputs[1].GetTextField(3))
        {
            throw new InvalidOperationException("ReadModelQueryConsistencyHold: local intake requires one declared shared root.");
        }

        var bindingField = new EventEvolutionBinaryReader(_row.GetEncodedField(15));
        var bindings = new EventEvolutionBinaryReader(bindingField.ReadBytes(64 * 1024));
        if (bindings.ReadUInt32() != 1)
        {
            throw new InvalidOperationException("ReadModelQueryConsistencyHold: multiple roots are unsupported.");
        }

        var binding = new EventEvolutionBinaryReader(bindings.ReadBytes(64 * 1024));
        if (binding.ReadString(1024) != _inputs[1].GetTextKey(1) || binding.ReadString(1024) != _inputs[1].GetTextKey(2) || binding.ReadString(32) != "shared" || binding.ReadString(1024) != _inputs[1].GetTextField(3) || !binding.ReadBytes(4096).SequenceEqual(ReadBackend(_inputs[1])) || binding.ReadString(1024).Length != 0)
        {
            throw new InvalidOperationException("ReadModelRouteContextRequired: root template disagrees with declared input rows.");
        }

        binding.RequireEnd();
        bindings.RequireEnd();
        bindingField.RequireEnd();
        LogicalQueryCatalogCodec.RequireBindings(_row);
        if (handlerType.AssemblyQualifiedName != _row.GetTextField(1) || endpoint != _row.GetTextField(7) || !typeof(IDomainQueryHandler).IsAssignableFrom(handlerType) || maximumResponseBytes is < 1 or > 64 * 1024 * 1024 || workingBytes is < 1 or > 64 * 1024 * 1024)
        {
            _row.Dispose();
            throw new InvalidOperationException("QueryCapabilityChanged: descriptor mapping mismatch.");
        }

        using FileStream file = File.OpenRead(handlerType.Assembly.Location);
        byte[] actual = SHA256.HashData(file);
        try
        {
            if (!actual.AsSpan().SequenceEqual(_row.GetEncodedField(2)))
            {
                throw new InvalidOperationException("QueryCapabilityChanged: handler image mismatch.");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(actual);
        }

        HandlerType = handlerType;
        Domain = _row.Domain;
        QueryType = _row.GetTextKey(1);
        MaximumResponseBytes = maximumResponseBytes;
        WorkingBytes = workingBytes;
        _factory = factory;
        _resolve = resolve;
        _requestValidator = requestValidator;
        _responseValidator = responseValidator;
        _registration = registration;
        _resolver = resolver;
        _request = request;
        _response = response;
        _current = current;
        if (!ReferenceEquals(registration.CapabilityLoss, resolver.CapabilityLoss) || !ReferenceEquals(registration.CapabilityLoss, request.CapabilityLoss) || !ReferenceEquals(registration.CapabilityLoss, response.CapabilityLoss) || mappings.Any(mapping => !ReferenceEquals(registration.CapabilityLoss, mapping.CapabilityLoss)))
        {
            throw new InvalidOperationException("QueryCapabilityChanged: callbacks have foreign observed-loss scopes.");
        }

        registration.RequireCallable(factory);
        resolver.RequireCallable(resolve);
        request.RequireCallable(requestValidator);
        response.RequireCallable(responseValidator);
        registration.RequireFields(_row, 4);
        resolver.RequireFields(_row, 12);
        request.RequireSchema(_row, 8);
        response.RequireSchema(_row, 10);
        byte[] options = EncodeOptions(mappings, maximumResponseBytes, workingBytes);
        if (options.Length > 64 * 1024 || workingBytes < checked(options.Length * 160 + 4096))
        {
            throw new InvalidOperationException("ReadModelQueryLimit: immutable manifest workspace exceeds admitted working capacity.");
        }

        byte[] optionsHash = EventOptionsManifestCodec.ComputeHash(options, OptionSchema);
        try
        {
            if (!_row.GetEncodedField(3).SequenceEqual(optionsHash) || !_row.GetEncodedField(6).SequenceEqual(optionsHash))
            {
                throw new InvalidOperationException("QueryCapabilityChanged: input mappings or capacity options disagree.");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(options);
            CryptographicOperations.ZeroMemory(optionsHash);
        }

        _mappings = mappings.ToFrozenDictionary(static x => x.Id, StringComparer.Ordinal);
        if (_mappings.Count == 0)
        {
            throw new InvalidOperationException("ReadModelQueryConsistencyHold: no registered input mapping.");
        }
    }

    /// <summary>Gets the frozen fully expanded query/registration option schema.</summary>
    internal static IReadOnlyList<EventOptionRule> OptionSchema { get; } = Array.AsReadOnly<EventOptionRule>([new("inputMappings", System.Text.Json.JsonValueKind.Array, true), new("maximumResponseBytes", System.Text.Json.JsonValueKind.Number, true, IntegerOnly: true), new("workingBytes", System.Text.Json.JsonValueKind.Number, true, IntegerOnly: true)]);

    /// <summary>Encodes exact immutable mappings and actual composed capacity inputs as canonical options.</summary>
    internal static byte[] EncodeOptions(IReadOnlyList<LogicalQueryInputMapping> mappings, int maximumResponseBytes, int workingBytes)
    {
        string[] values = mappings.OrderBy(static x => x.Id, StringComparer.Ordinal).Select(static mapping =>
        {
            byte[] bytes = mapping.EncodeManifest();
            try
            {
                return Convert.ToBase64String(bytes);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
            }
        }).ToArray();
        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(new { inputMappings = values, maximumResponseBytes, workingBytes }));
        return EventCanonicalJsonValueCodec.EncodeText(document.RootElement);
    }

    /// <summary>Requires the actual private input root and every planned key to match its exact declaration.</summary>
    internal void RequireRoot(PrivateLogicalQueryInput input, QueryEnvelope query, IReadOnlyList<LogicalQueryReadPlanEntry> plan, EventBufferBudget budget, CancellationToken token)
    {
        input.RequireScope(query, _compatibilityHash, budget, token);
        DaprLogicalQueryRoot root = input.Root;
        EventRegistryRow declaration = _inputs[1];
        if (!ReferenceEquals(input.CapabilityLoss, _registration.CapabilityLoss) || root.Tenant != query.TenantId || root.Domain != Domain || root.HandlerRoute != declaration.GetTextKey(1) || root.KeySpace != declaration.GetTextKey(2) || root.StoreName != declaration.GetTextField(3) || !root.BackendDescriptor.AsSpan().SequenceEqual(ReadBackend(declaration)) || plan.Any(x => x.StoreName != root.StoreName || !x.LogicalKey.StartsWith(declaration.GetTextField(1), StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("ReadModelRouteContextRequired: pinned root/plan is outside its exact declaration.");
        }
    }

    private static ReadOnlySpan<byte> ReadBackend(EventRegistryRow declaration)
    {
        var reader = new EventEvolutionBinaryReader(declaration.GetEncodedField(4));
        return reader.ReadBytes(4096);
    }

    /// <summary>Gets the exact retained compatibility digest for actual owner/session scope comparison.</summary>
    internal ReadOnlyMemory<byte> CompatibilityHash => _compatibilityHash;
    /// <summary>Gets the exact admitted concrete scoped handler type.</summary>
    internal Type HandlerType { get; }
    /// <summary>Gets the canonical domain.</summary>
    internal string Domain { get; }
    /// <summary>Gets the canonical query discriminator.</summary>
    internal string QueryType { get; }
    /// <summary>Gets the encoded output ceiling admitted before handler allocation.</summary>
    internal int MaximumResponseBytes { get; }
    /// <summary>Gets the declared request/aggregation/schema workspace admitted before callbacks.</summary>
    internal int WorkingBytes { get; }

    /// <summary>Checks exact current local capability before and after every actual owner boundary.</summary>
    internal void RequireCurrent(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        try
        {
            _current(token);
        }
        finally
        {
            token.ThrowIfCancellationRequested();
        }

        _registration.RequireActive(token);
        _resolver.RequireActive(token);
        _request.RequireActive(token);
        _response.RequireActive(token);
        byte[] options = EncodeOptions(_mappings.Values.ToArray(), MaximumResponseBytes, WorkingBytes);
        byte[] expected = EventOptionsManifestCodec.ComputeHash(options, OptionSchema);
        try
        {
            if (!_row.GetEncodedField(3).SequenceEqual(expected))
            {
                throw new InvalidOperationException("QueryCapabilityChanged: input mapping implementation changed.");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(options);
            CryptographicOperations.ZeroMemory(expected);
        }

        token.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
    }

    /// <summary>Validates immutable request bytes then resolves and privately captures the complete bounded plan.</summary>
    internal async Task<LogicalQueryReadPlanEntry[]> PrepareAsync(QueryEnvelope query, ImmutablePayload payload, EventBufferBudget budget, CancellationToken token, Func<CancellationToken, Task> requestFence)
    {
        Task Fence(CancellationToken cancellation)
        {
            RequireCurrent(cancellation);
            return requestFence(cancellation);
        }

        await _request.RequireAsync(Fence, budget, token).ConfigureAwait(false);
        using (var lease = new InvocationPayloadLease(payload, token))
        {
            try
            {
                _requestValidator(lease, token);
            }
            finally
            {
                token.ThrowIfCancellationRequested();
            }
        }

        await Fence(token).ConfigureAwait(false);
        await _resolver.RequireAsync(Fence, budget, token).ConfigureAwait(false);
        IReadOnlyList<LogicalQueryReadPlanEntry> supplied;
        try
        {
            supplied = _resolve(query, token);
        }
        finally
        {
            token.ThrowIfCancellationRequested();
        }

        await Fence(token).ConfigureAwait(false);
        int count;
        try
        {
            count = supplied.Count;
        }
        finally
        {
            token.ThrowIfCancellationRequested();
        }

        await Fence(token).ConfigureAwait(false);
        if (count is < 1 or > 256)
        {
            throw new InvalidOperationException("ReadModelQueryLimit: plan count exceeds its bound.");
        }

        var utf8 = new System.Text.UTF8Encoding(false, true);
        int encodedBytes = 4;
        var admitted = new List<LogicalQueryReadPlanEntry>(count);
        var unique = new HashSet<(string, string)>();
        for (int index = 0; index < count; index++)
        {
            LogicalQueryReadPlanEntry entry;
            try
            {
                entry = supplied[index];
            }
            finally
            {
                token.ThrowIfCancellationRequested();
            }

            await Fence(token).ConfigureAwait(false);
            int keyBytes = utf8.GetByteCount(entry.LogicalKey);
            int storeBytes = utf8.GetByteCount(entry.StoreName), mappingBytes = utf8.GetByteCount(entry.MappingId);
            encodedBytes = checked(encodedBytes + 12 + keyBytes + storeBytes + mappingBytes);
            if (keyBytes is < 1 or > 512 || storeBytes is < 1 or > 1024 || mappingBytes is < 1 or > 1024 || encodedBytes > 512 * 1024 || !unique.Add((entry.StoreName, entry.LogicalKey)) || !_mappings.ContainsKey(entry.MappingId))
            {
                throw new InvalidOperationException("ReadModelQueryConsistencyHold: undeclared or oversized query plan.");
            }

            admitted.Add(entry);
        }

        return admitted.ToArray();
    }

    /// <summary>Resolves exactly the admitted handler inside the already installed private scope.</summary>
    internal async Task<IDomainQueryHandler> CreateAsync(IServiceProvider provider, Func<CancellationToken, Task> fence, EventBufferBudget budget, CancellationToken token)
    {
        await _registration.RequireAsync(fence, budget, token).ConfigureAwait(false);
        IDomainQueryHandler handler;
        try
        {
            handler = _factory(provider);
        }
        finally
        {
            token.ThrowIfCancellationRequested();
        }

        await fence(token).ConfigureAwait(false);
        if (!ReferenceEquals(handler, Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService(provider, HandlerType)) || handler.GetType() != HandlerType)
        {
            throw new InvalidOperationException("QueryCapabilityChanged: registration returned another handler.");
        }

        string domain;
        try
        {
            domain = handler.Domain;
        }
        finally
        {
            token.ThrowIfCancellationRequested();
        }

        await fence(token).ConfigureAwait(false);
        if (!string.Equals(domain, Domain, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("QueryCapabilityChanged: handler domain differs.");
        }

        string queryType;
        try
        {
            queryType = handler.QueryType;
        }
        finally
        {
            token.ThrowIfCancellationRequested();
        }

        await fence(token).ConfigureAwait(false);
        if (!string.Equals(queryType, QueryType, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("QueryCapabilityChanged: handler query type differs.");
        }

        return handler;
    }

    /// <summary>Returns only an explicitly declared exact input mapping.</summary>
    internal LogicalQueryInputMapping GetMapping(string id) => _mappings.TryGetValue(id, out LogicalQueryInputMapping? mapping) ? mapping : throw new InvalidOperationException("ReadModelQueryConsistencyHold: unknown input mapping.");
    /// <summary>Validates private response bytes before the final actual root/TTL fence.</summary>
    internal async Task ValidateResponseAsync(ImmutablePayload payload, Func<CancellationToken, Task> fence, EventBufferBudget budget, CancellationToken token)
    {
        await _response.RequireAsync(fence, budget, token).ConfigureAwait(false);
        using (var lease = new InvocationPayloadLease(payload, token))
        {
            try
            {
                _responseValidator(lease, token);
            }
            finally
            {
                token.ThrowIfCancellationRequested();
            }
        }

        await fence(token).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _row.Dispose();
            foreach (EventRegistryRow input in _inputs)
            {
                input.Dispose();
            }

            CryptographicOperations.ZeroMemory(_compatibilityHash);
        }
    }
}
