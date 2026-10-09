using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Dapr.Actors.Runtime;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Queries;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.DomainService.Queries;
using Hexalith.EventStore.Server.Queries;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Hexalith.EventStore.Server.Tests.Queries;
/// <summary>Owns explicit local query evidence, actual-state substitutions and callback observations.</summary>
public sealed class DaprLogicalQueryFixture : IDisposable
{
    private static readonly AsyncLocal<DaprLogicalQueryFixture?> Active = new();
    private readonly SemaphoreSlim _gate = new(1);
    private readonly ServiceProvider _services;
    private readonly LogicalQueryDescriptor _descriptor;
    private readonly PrivateLogicalQueryCatalog _catalog;
    /// <summary>Provides the current isolated fixture callback context.</summary>
    internal static DaprLogicalQueryFixture Current => Active.Value!;

    /// <summary>Provides sticky callback authority loss.</summary>
    internal readonly EventEvolutionCapabilityLoss Loss = new();
    /// <summary>Provides the originating cancellation scope.</summary>
    internal readonly CancellationTokenSource Cancellation = new();
    /// <summary>Provides the actual actor root independently read by the owner.</summary>
    internal DaprLogicalQueryRoot Root = MakeRoot();
    /// <summary>Provides the supplied authoritative fixture clock.</summary>
    internal DateTimeOffset Utc = new(638900000000000000, TimeSpan.Zero);
    /// <summary>Provides the application callback interruption hook.</summary>
    internal Action<string>? Hook;
    /// <summary>Provides actual root readback substitutions.</summary>
    internal Action? ReadHook;
    /// <summary>Provides independent origin participant substitutions.</summary>
    internal Action? OriginHook = null;
    /// <summary>Provides actual originating-operation participant records.</summary>
    internal readonly Dictionary<string, DaprLogicalQueryOrigin> Origins = new(StringComparer.Ordinal);
    /// <summary>Provides an optional resolver key boundary override.</summary>
    internal string? PlanKey;
    /// <summary>Provides asynchronously yielding authorization substitutions.</summary>
    internal Func<Task>? AuthorizationHook;
    /// <summary>Provides the serialized decision fault mode.</summary>
    internal string FenceMode = "normal";
    /// <summary>Provides ordered application callbacks.</summary>
    internal readonly List<string> Calls = [];
    /// <summary>Provides values seen through the pinned store.</summary>
    internal readonly List<int> Observed = [];
    /// <summary>Provides public read-store ETags seen by the handler.</summary>
    internal readonly List<string?> ObservedEtags = [];
    /// <summary>Provides private payload arrays observed during callbacks.</summary>
    internal readonly List<byte[]> Captured = [];
    /// <summary>Provides payload facades retained to check expiry.</summary>
    internal readonly List<IReadOnlyPayload> Leases = [];
    /// <summary>Provides the shared operation reservation observed by acquisition.</summary>
    internal EventBufferBudget? Budget;
    /// <summary>Provides the retained private owner input.</summary>
    internal PrivateLogicalQueryInput? Input;
    /// <summary>Provides the retained closed-scope store facade.</summary>
    internal IReadModelStore? Store;
    /// <summary>Provides the private resolver/handler request.</summary>
    internal QueryEnvelope? PrivateQuery;
    /// <summary>Provides the retained handler result alias.</summary>
    internal byte[]? ProducedBytes;
    /// <summary>Provides the fixture callback counts and fault controls.</summary>
    internal bool Returned, WrongClr, WrongKey, Write, BulkBad, Bulk, DualCancellation;
    /// <summary>Provides the fixture callback counts and fault controls.</summary>
    internal int HandlerCalls, Materializations, SourceReads;
    /// <summary>Gets the token observed directly by the handler at a refused private store call.</summary>
    internal CancellationToken? StoreRefusalToken;
    /// <summary>Provides the fixture callback counts and fault controls.</summary>
    internal LogicalQueryRouteTable Routes { get; }
    /// <summary>Provides the fixture callback counts and fault controls.</summary>
    internal QueryEnvelope Query { get; } = new("tenant", "d", "a", "get-total", "{}"u8.ToArray(), "c", "u");
    /// <summary>Provides the exact originating token.</summary>
    internal CancellationToken Token => Cancellation.Token;
    /// <summary>Provides the host provider with ordinary legacy registrations.</summary>
    internal IServiceProvider Services => _services;
    /// <summary>Gets the admitted descriptor for direct preparation boundary controls.</summary>
    internal LogicalQueryDescriptor Descriptor => _descriptor;

    /// <summary>Provides the admitted local descriptor, owner and isolated providers.</summary>
    internal DaprLogicalQueryFixture(string factoryMode = "normal", string manifestChange = "none", ServiceLifetime lifetime = ServiceLifetime.Scoped, int graphBytes = 8192)
    {
        Active.Value = this;
        Action<IReadOnlyPayload, CancellationToken> request = (payload, token) =>
        {
            Capture(payload);
            Callback("request");
            Validate(payload, token);
        };
        Action<IReadOnlyPayload, CancellationToken> response = (payload, token) =>
        {
            Capture(payload);
            Callback("response");
            Validate(payload, token);
        };
        Action<IReadOnlyPayload, CancellationToken> validate = (payload, token) =>
        {
            Capture(payload);
            Callback("input-validation");
            Validate(payload, token);
        };
        Func<IReadOnlyPayload, CancellationToken, object> read = (payload, token) =>
        {
            Capture(payload);
            Callback("input-read");
            Materializations++;
            byte[] bytes = new byte[payload.Length];
            payload.CopyTo(0, bytes);
            try
            {
                return JsonSerializer.Deserialize<DaprLogicalQueryValue>(bytes)!;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
            }
        };
        LogicalQueryCallable validator = Bind("value-validator", validate), materializer = Bind("value-reader", read);
        var mapping = new LogicalQueryInputMapping("value", "value", typeof(DaprLogicalQueryValue), graphBytes, validate, read, validator, materializer);
        byte[] options = LogicalQueryDescriptor.EncodeOptions([mapping], 4096, 1024 * 1024);
        byte[] optionsHash = EventOptionsManifestCodec.ComputeHash(options, LogicalQueryDescriptor.OptionSchema);
        Func<IServiceProvider, IDomainQueryHandler> factory = provider =>
        {
            Callback("factory");
            if (factoryMode == "bypass")
            {
                _ = provider.GetRequiredService<Dapr.Client.DaprClient>();
            }

            return provider.GetRequiredService<DaprLogicalQueryHandler>();
        };
        Func<QueryEnvelope, CancellationToken, IReadOnlyList<LogicalQueryReadPlanEntry>> resolve = (query, token) =>
        {
            PrivateQuery = query;
            Callback("resolver");
            return[new("store", PlanKey ?? "item:a", "value"), new("store", "item:b", "value")];
        };
        LogicalQueryCallable registration = Bind("factory", factory, options, LogicalQueryDescriptor.OptionSchema);
        LogicalQueryCallable resolver = Bind("resolver", resolve), requestBinding = Bind("request", request), responseBinding = Bind("response", response);
        byte[] row = BuildRow(optionsHash, registration, resolver, requestBinding, responseBinding, manifestChange);
        if (manifestChange == "fields")
        {
            row[20] ^= 1;
        }

        if (manifestChange == "mapping")
        {
            mapping = new LogicalQueryInputMapping("value", "foreign", typeof(DaprLogicalQueryValue), graphBytes, validate, read, validator, materializer);
        }

        if (manifestChange == "delegate")
        {
            request = static (_, _) =>
            {
            };
        }

        _descriptor = new LogicalQueryDescriptor(row, typeof(DaprLogicalQueryHandler), "test/query", factory, resolve, request, response, registration, resolver, requestBinding, responseBinding, [mapping], 4096, 1024 * 1024, token =>
        {
            token.ThrowIfCancellationRequested();
            Loss.RequireNoObservedLoss();
        }, [BuildInputRoute(), BuildDeclaration()], [BuildDependency()]);
        Routes = new LogicalQueryRouteTable([("d", "get-total", true), ("d", "legacy", false)], Loss, _descriptor.RequireCurrent);
        RefreshOrigins();
        IActorStateManager state = Substitute.For<IActorStateManager>();
        state.TryGetStateAsync<DaprLogicalQueryRoot>("logical-root", Arg.Any<CancellationToken>()).Returns(call =>
        {
            SourceReads++;
            ReadHook?.Invoke();
            return Task.FromResult(new ConditionalValue<DaprLogicalQueryRoot>(true, Root));
        });
        state.TryGetStateAsync<DaprLogicalQueryOrigin>(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            OriginHook?.Invoke();
            return Task.FromResult(Origins.TryGetValue(call.ArgAt<string>(0), out DaprLogicalQueryOrigin? origin) ? new ConditionalValue<DaprLogicalQueryOrigin>(true, origin) : new ConditionalValue<DaprLogicalQueryOrigin>(false, default!));
        });
        var owner = new DaprLogicalQueryRootOwner(state, "logical-root", "tenant", "d", "get-total", "u", _descriptor.CompatibilityHash.Span, "route", "space", "store", "backend"u8, 64 * 1024, async (query, plan, token) =>
        {
            Callback("authorization");
            if (AuthorizationHook is not null)
            {
                await AuthorizationHook();
            }

            token.ThrowIfCancellationRequested();
            return Utc;
        }, async (decision, token) =>
        {
            await _gate.WaitAsync(token);
            try
            {
                if (FenceMode == "skip")
                {
                    return;
                }

                if (FenceMode == "foreign")
                {
                    await decision(CancellationToken.None);
                    return;
                }

                await decision(token);
                if (FenceMode == "repeat")
                {
                    await decision(token);
                }
            }
            finally
            {
                _gate.Release();
            }
        }, () =>
        {
            Callback("utc");
            return Utc;
        }, Loss);
        _catalog = new PrivateLogicalQueryCatalog(Routes, [_descriptor], async (query, plan, budget, token) =>
        {
            Budget = budget;
            Input = await owner.AcquireAsync(query, plan, budget, token);
            Captured.AddRange(Input.Root.Rows.Where(static row => row.Payload is not null).Select(static row => row.Payload!));
            return Input;
        });
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton(Substitute.For<Dapr.Client.DaprClient>());
        services.Add(new ServiceDescriptor(typeof(DaprLogicalQueryHandler), typeof(DaprLogicalQueryHandler), lifetime));
        IReadModelStore ordinary = Substitute.For<IReadModelStore>();
        ordinary.GetAsync<DaprLogicalQueryValue>("legacy", "physical:legacy", Arg.Any<CancellationToken>()).Returns(new ReadModelEntry<DaprLogicalQueryValue>(new(19), "etag"));
        services.AddSingleton(ordinary);
        services.AddScoped<IDomainQueryHandler, DaprLogicalQueryLegacyHandler>();
        _catalog.Install(services);
        _services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    /// <summary>Publishes independent fixture participants only at explicit legitimate setup boundaries.</summary>
    internal void RefreshOrigins()
    {
        Origins.Clear();
        foreach (DaprLogicalQueryRow row in Root.Rows)
        {
            Origins.Add(DaprLogicalQueryRootOwner.GetOriginKey("logical-root", row.OriginOperationId, row.Key), new(row.OriginOperationId, Root.Tenant, Root.Domain, Root.StoreName, row.Key, row.ValueTypeName, row.Payload is null ? null : SHA256.HashData(row.Payload), row.ExpiresAt));
        }
    }

    /// <summary>Provides one query through the actual domain dispatcher.</summary>
    internal async Task<QueryResult> ExecuteAsync(QueryEnvelope? query = null)
    {
        using IServiceScope scope = _services.CreateScope();
        return await DomainQueryDispatcher.ExecuteAsync(scope.ServiceProvider, query ?? Query, Token);
    }

    /// <summary>Provides one ordered callback and its interruption hook.</summary>
    internal void Callback(string point)
    {
        Calls.Add(point);
        Hook?.Invoke(point);
    }

    private void Capture(IReadOnlyPayload payload)
    {
        Leases.Add(payload);
        object? inner = payload.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic).FirstOrDefault(static field => typeof(IReadOnlyPayload).IsAssignableFrom(field.FieldType))?.GetValue(payload);
        byte[]? owner = inner?.GetType().GetField("_owner", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(inner) as byte[];
        if (owner is not null)
        {
            Captured.Add(owner);
        }
    }

    private static void Validate(IReadOnlyPayload payload, CancellationToken token)
    {
        byte[] bytes = new byte[payload.Length];
        payload.CopyTo(0, bytes);
        try
        {
            var reader = new Utf8JsonReader(bytes);
            while (reader.Read())
            {
                token.ThrowIfCancellationRequested();
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private LogicalQueryCallable Bind(string id, Delegate callback, byte[]? options = null, IReadOnlyList<EventOptionRule>? schema = null)
    {
        options ??= "{}\n"u8.ToArray();
        byte[] hash = EventOptionsManifestCodec.ComputeHash(options, schema ?? []);
        return new LogicalQueryCallable(id, callback, AssemblyHash(), hash, options, Loss, runtimeOptions: id is "request" or "resolver" ? () =>
        {
            Callback(id + "-options");
            return options;
        } : null, optionSchema: schema, schema: "json"u8.ToArray());
    }

    /// <summary>Provides the canonical two-row actor-root fixture.</summary>
    internal static DaprLogicalQueryRoot MakeRoot() => new("dapr-actor-logical-v1", "tenant", "d", "route", "space", "store", "backend"u8.ToArray(), 1, [new("item:a", "value", "{\"Value\":2}"u8.ToArray(), null, "op-1"), new("item:b", "value", "{\"Value\":3}"u8.ToArray(), null, "op-2")]);
    /// <summary>Provides the exact fixture assembly bytes.</summary>
    internal static byte[] AssemblyHash()
    {
        using FileStream file = File.OpenRead(typeof(DaprLogicalQueryHandler).Assembly.Location);
        return SHA256.HashData(file);
    }

    private static byte[] BuildRow(byte[] options, LogicalQueryCallable registration, LogicalQueryCallable resolver, LogicalQueryCallable request, LogicalQueryCallable response, string change)
    {
        byte[] hash = AssemblyHash(), empty = EventOptionsManifestCodec.ComputeHash("{}\n"u8.ToArray(), []);
        byte[] requestSchema = request.EncodeSchema(), responseSchema = response.EncodeSchema();
        using var bindings = new EventEvolutionBinaryWriter(1024);
        using var binding = new EventEvolutionBinaryWriter(1024);
        binding.WriteString("route");
        binding.WriteString("space");
        binding.WriteString("shared");
        binding.WriteString("store");
        binding.WriteBytes("backend"u8);
        binding.WriteString("");
        bindings.WriteUInt32(1);
        bindings.WriteBytes(binding.CopyEncodedBytes());
        using var row = new EventEvolutionBinaryWriter(8192);
        row.WriteByte(0x5b);
        row.WriteString("d");
        row.WriteString("get-total");
        row.WriteUInt16(15);
        row.WriteByte(1);
        row.WriteString(typeof(DaprLogicalQueryHandler).AssemblyQualifiedName!);
        row.WriteByte(2);
        row.WriteHash(hash);
        row.WriteByte(3);
        row.WriteHash(change == "query-options" ? new byte[32] : options);
        row.WriteByte(4);
        row.WriteString(change == "registration-id" ? "foreign" : registration.Id);
        row.WriteByte(5);
        row.WriteHash(change == "registration-image" ? new byte[32] : hash);
        row.WriteByte(6);
        row.WriteHash(change == "registration-options" ? new byte[32] : options);
        row.WriteByte(7);
        row.WriteString(change == "endpoint" ? "foreign" : "test/query");
        row.WriteByte(8);
        row.WriteString(change == "request-id" ? "foreign" : request.Id);
        row.WriteByte(9);
        row.WriteHash(change == "request-schema" ? new byte[32] : SHA256.HashData(requestSchema));
        row.WriteByte(10);
        row.WriteString(change == "response-id" ? "foreign" : response.Id);
        row.WriteByte(11);
        row.WriteHash(change == "response-schema" ? new byte[32] : SHA256.HashData(responseSchema));
        row.WriteByte(12);
        row.WriteString(change == "resolver-id" ? "foreign" : resolver.Id);
        row.WriteByte(13);
        row.WriteHash(change == "resolver-image" ? new byte[32] : hash);
        row.WriteByte(14);
        row.WriteHash(change == "resolver-options" ? new byte[32] : empty);
        row.WriteByte(15);
        row.WriteBytes(bindings.CopyEncodedBytes());
        return row.CopyEncodedBytes();
    }

    /// <summary>Provides the selected 52 route row.</summary>
    internal static byte[] BuildInputRoute()
    {
        using var row = new EventEvolutionBinaryWriter(4096);
        byte[] hash = AssemblyHash();
        row.WriteByte(0x52);
        row.WriteString("d");
        row.WriteString("route");
        row.WriteUInt16(18);
        string fields = "UUUUHHUUHIBUHHUHHU";
        for (int i = 0; i < fields.Length; i++)
        {
            row.WriteByte(checked((byte)(i + 1)));
            if (fields[i] == 'U')
            {
                row.WriteString(i == 17 ? "store" : "fixture");
            }
            else if (fields[i] == 'H')
            {
                row.WriteHash(hash);
            }
            else if (fields[i] == 'I')
            {
                row.WriteInt32(1);
            }
            else
            {
                row.WriteBytes("schema"u8);
            }
        }

        return row.CopyEncodedBytes();
    }

    /// <summary>Provides the selected 59 key-space row.</summary>
    internal static byte[] BuildDeclaration()
    {
        using var row = new EventEvolutionBinaryWriter(4096);
        row.WriteByte(0x59);
        row.WriteString("d");
        row.WriteString("route");
        row.WriteString("space");
        row.WriteUInt16(5);
        row.WriteByte(1);
        row.WriteString("item:");
        row.WriteByte(2);
        row.WriteString("shared");
        row.WriteByte(3);
        row.WriteString("store");
        row.WriteByte(4);
        row.WriteBytes("backend"u8);
        row.WriteByte(5);
        row.WriteHash(AssemblyHash());
        return row.CopyEncodedBytes();
    }

    /// <summary>Provides the selected G row.</summary>
    internal static byte[] BuildDependency()
    {
        using var row = new EventEvolutionBinaryWriter(4096);
        row.WriteByte(0x47);
        row.WriteString("d");
        row.WriteString("dep");
        row.WriteString("managed");
        row.WriteUInt16(3);
        row.WriteByte(1);
        row.WriteString("1.0");
        row.WriteByte(2);
        row.WriteHash(AssemblyHash());
        row.WriteByte(3);
        row.WriteString("framework");
        return row.CopyEncodedBytes();
    }

    public void Dispose()
    {
        _services.Dispose();
        _catalog.Dispose();
        _descriptor.Dispose();
        Cancellation.Dispose();
        _gate.Dispose();
        Active.Value = null;
    }
}
