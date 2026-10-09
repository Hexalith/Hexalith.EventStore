using System.Reflection;
using System.Text.Json;
using Dapr.Actors.Runtime;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Queries;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.DomainService.Queries;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Queries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Queries;
public sealed class DaprLogicalQueryTests
{
    [Fact]
    public async Task ActualDispatcherPinsRowsAndPreservesLegacyStore()
    {
        using var fixture = new DaprLogicalQueryFixture();
        QueryEnvelope original = fixture.Query;
        byte[] before = original.Payload.ToArray();
        QueryResult result = await fixture.ExecuteAsync(original);
        result.GetPayload().GetProperty("total").GetInt32().ShouldBe(5);
        fixture.Observed.ShouldBe([2, 3]);
        fixture.HandlerCalls.ShouldBe(1);
        fixture.ObservedEtags.ShouldBe([null, null ]);
        original.Payload.ShouldBe(before);
        fixture.Budget!.LiveBytes.ShouldBe(0);
        fixture.Captured.Count.ShouldBeGreaterThan(0);
        fixture.Captured.All(static bytes => bytes.All(static b => b == 0)).ShouldBeTrue();
        fixture.ProducedBytes!.All(static b => b == 0).ShouldBeTrue();
        foreach (var lease in fixture.Leases)
        {
            Should.Throw<ObjectDisposedException>(() => _ = lease.Length);
        }

        await Should.ThrowAsync<InvalidOperationException>(() => fixture.Store!.GetAsync<DaprLogicalQueryValue>("store", "item:a", fixture.Token));
        QueryResult ordinary = await fixture.ExecuteAsync(original with { QueryType = "legacy" });
        ordinary.GetPayload().GetProperty("Value").GetInt32().ShouldBe(19);
    }

    [Fact]
    public async Task RootAdvanceBetweenReadsKeepsR1AndRefusesWholeResponse()
    {
        using var fixture = new DaprLogicalQueryFixture();
        fixture.Hook = point =>
        {
            if (point == "between-reads")
            {
                fixture.Root = DaprLogicalQueryFixture.MakeRoot()with
                {
                    Generation = 2
                };
            }
        };
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.ExecuteAsync());
        fixture.Observed.ShouldBe([2, 3]);
        fixture.HandlerCalls.ShouldBe(1);
        fixture.Budget!.LiveBytes.ShouldBe(0);
        fixture.ProducedBytes!.All(static b => b == 0).ShouldBeTrue();
        fixture.Captured.All(static bytes => bytes.All(static b => b == 0)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("skip")]
    [InlineData("repeat")]
    [InlineData("foreign")]
    public async Task OwnerDecisionMustExecuteOnceWithOriginalToken(string mode)
    {
        using var fixture = new DaprLogicalQueryFixture();
        fixture.FenceMode = mode;
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.ExecuteAsync());
        fixture.HandlerCalls.ShouldBe(0);
        fixture.Materializations.ShouldBe(0);
        fixture.Budget!.LiveBytes.ShouldBe(0);
    }

    [Fact]
    public async Task FreshUtcAfterFinalActualReadbackRefusesExpiredInput()
    {
        using var fixture = new DaprLogicalQueryFixture();
        fixture.Root = fixture.Root with
        {
            Rows = fixture.Root.Rows.Select(row => row with { ExpiresAt = fixture.Utc.AddSeconds(1) }).ToArray()
        };
        fixture.RefreshOrigins();
        fixture.ReadHook = () =>
        {
            if (fixture.Returned)
            {
                fixture.Utc = fixture.Utc.AddSeconds(2);
            }
        };
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.ExecuteAsync());
        fixture.Observed.ShouldBe([2, 3]);
        fixture.ProducedBytes!.All(static b => b == 0).ShouldBeTrue();
        fixture.Budget!.LiveBytes.ShouldBe(0);
    }

    [Fact]
    public async Task LastAuthorizationAwaitCannotChangeRetainedRootOrResult()
    {
        foreach (string substitution in new[]
        {
            "root",
            "result",
            "request"
        }

        )
        {
            using var fixture = new DaprLogicalQueryFixture();
            bool substituted = false;
            fixture.AuthorizationHook = async () =>
            {
                if (!fixture.Returned || substituted)
                {
                    return;
                }

                substituted = true;
                await Task.Yield();
                if (substitution == "root")
                {
                    fixture.Input!.Root.Rows[0].Payload![0] ^= 1;
                }
                else if (substitution == "result")
                {
                    fixture.ProducedBytes![0] ^= 1;
                }
                else
                {
                    fixture.PrivateQuery!.Payload[0] ^= 1;
                }
            };
            await Should.ThrowAsync<InvalidOperationException>(() => fixture.ExecuteAsync());
            fixture.HandlerCalls.ShouldBe(1);
            fixture.Budget!.LiveBytes.ShouldBe(0);
            fixture.ProducedBytes!.All(static b => b == 0).ShouldBeTrue();
            fixture.Captured.All(static bytes => bytes.All(static b => b == 0)).ShouldBeTrue();
        }
    }

    public static IEnumerable<object[]> CallbackCases()
    {
        foreach (string point in new[]
        {
            "request-options",
            "request",
            "resolver-options",
            "resolver",
            "factory",
            "domain",
            "query-type",
            "handler",
            "bulk-count",
            "bulk-index",
            "input-validation",
            "input-read",
            "response",
            "authorization",
            "utc"
        }

        )
            foreach (string loss in new[]
            {
                "loss",
                "cancel",
                "cancel-throw",
                "foreign-oce"
            }

            )
            {
                yield return[point, loss];
            }
    }

    [Theory]
    [MemberData(nameof(CallbackCases))]
    public async Task EveryApplicationCallbackRefusesBeforeLaterCallbacks(string point, string loss)
    {
        using var fixture = new DaprLogicalQueryFixture();
        ArgumentNullException.ThrowIfNull(point);
        fixture.Bulk = point.StartsWith("bulk", StringComparison.Ordinal);
        int stopped = 0;
        fixture.Hook = current =>
        {
            if (current != point || stopped != 0)
            {
                return;
            }

            stopped = fixture.Calls.Count;
            if (loss == "loss")
            {
                fixture.Loss.ObserveViolation();
                return;
            }

            fixture.Cancellation.Cancel();
            if (loss == "cancel-throw")
            {
                throw new FormatException("callback refused");
            }

            if (loss == "foreign-oce")
            {
                throw new OperationCanceledException(CancellationToken.None);
            }
        };
        if (loss == "loss")
        {
            await Should.ThrowAsync<InvalidOperationException>(() => fixture.ExecuteAsync());
        }
        else
        {
            OperationCanceledException refusal = await Should.ThrowAsync<OperationCanceledException>(() => fixture.ExecuteAsync());
            refusal.CancellationToken.ShouldBe(fixture.Token);
        }

        stopped.ShouldBeGreaterThan(0);
        fixture.Calls.Count.ShouldBe(stopped);
        if (fixture.Budget is not null)
        {
            fixture.Budget.LiveBytes.ShouldBe(0);
        }

        if (point != "request-options")
        {
            fixture.Captured.Count.ShouldBeGreaterThan(0);
        }

        fixture.Captured.All(static bytes => bytes.All(static b => b == 0)).ShouldBeTrue();
        if (fixture.ProducedBytes is not null)
        {
            fixture.ProducedBytes.All(static b => b == 0).ShouldBeTrue();
        }

        fixture.Root.Generation.ShouldBe(1);
    }

    [Theory]
    [InlineData("clr")]
    [InlineData("key")]
    [InlineData("origin")]
    [InlineData("write")]
    [InlineData("bulk")]
    public async Task ExactKeyTypeOriginAndReadOnlyAdmission(string refusal)
    {
        using var fixture = new DaprLogicalQueryFixture();
        fixture.WrongClr = refusal == "clr";
        fixture.WrongKey = refusal == "key";
        fixture.Write = refusal == "write";
        fixture.BulkBad = refusal == "bulk";
        if (refusal == "origin")
        {
            fixture.Root = fixture.Root with
            {
                Rows = fixture.Root.Rows.Select(row => row with { ValueTypeName = "foreign" }).ToArray()
            };
        }

        await Should.ThrowAsync<InvalidOperationException>(() => fixture.ExecuteAsync());
        fixture.Materializations.ShouldBe(0);
        fixture.Budget!.LiveBytes.ShouldBe(0);
        fixture.Calls.ShouldNotContain("input-validation");
        fixture.Root.Generation.ShouldBe(1);
    }

    [Theory]
    [InlineData("mapping")]
    [InlineData("delegate")]
    [InlineData("fields")]
    [InlineData("query-options")]
    [InlineData("registration-id")]
    [InlineData("registration-image")]
    [InlineData("registration-options")]
    [InlineData("endpoint")]
    [InlineData("request-id")]
    [InlineData("request-schema")]
    [InlineData("response-id")]
    [InlineData("response-schema")]
    [InlineData("resolver-id")]
    [InlineData("resolver-image")]
    [InlineData("resolver-options")]
    public async Task ExactManifestAndDelegateSubstitutionRefusesBeforeCallbacks(string mode)
    {
        DaprLogicalQueryFixture? fixture;
        try
        {
            fixture = new DaprLogicalQueryFixture(manifestChange: mode);
        }
        catch (Exception exception)when (exception is InvalidOperationException or ArgumentException)
        {
            DaprLogicalQueryFixture.Current.Calls.ShouldBeEmpty();
            DaprLogicalQueryFixture.Current.HandlerCalls.ShouldBe(0);
            return;
        }

        using (fixture)
        {
            QueryResult? result = null;
            try
            {
                result = await fixture.ExecuteAsync();
            }
            catch (InvalidOperationException)
            {
            }

            fixture.HandlerCalls.ShouldBe(0);
            result.ShouldBeNull();
        }
    }

    [Fact]
    public void CaptiveRegistrationRefusesBeforeConstruction()
    {
        Should.Throw<InvalidOperationException>(() => new DaprLogicalQueryFixture(lifetime: ServiceLifetime.Singleton));
    }

    [Fact]
    public async Task PrivateFactoryCannotResolveUndeclaredPhysicalProvider()
    {
        using var fixture = new DaprLogicalQueryFixture(factoryMode: "bypass");
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.ExecuteAsync());
        fixture.HandlerCalls.ShouldBe(0);
        fixture.Materializations.ShouldBe(0);
        fixture.Budget!.LiveBytes.ShouldBe(0);
    }

    [Fact]
    public async Task DerivedRequestAndEmptyScopeFloodRefuseBeforeRootOrHandler()
    {
        using var fixture = new DaprLogicalQueryFixture();
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.ExecuteAsync(new DaprLogicalQueryDerivedEnvelope()));
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.ExecuteAsync(fixture.Query with { Scopes = Enumerable.Repeat("", 257).ToArray() }));
        fixture.SourceReads.ShouldBe(0);
        fixture.HandlerCalls.ShouldBe(0);
        fixture.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task LogicalActorAndRouterBypassLegacyCacheAndInvoker()
    {
        using var fixture = new DaprLogicalQueryFixture();
        IETagService etag = Substitute.For<IETagService>();
        var actor = new DaprLogicalQueryCachingActor(ActorHost.CreateForTest<DaprLogicalQueryCachingActor>(), etag, fixture.Routes, (query, token) => DomainQueryDispatcher.ExecuteAsync(fixture.Services, query, token));
        _ = await actor.QueryAsync(fixture.Query, fixture.Token);
        _ = await actor.QueryAsync(fixture.Query, fixture.Token);
        fixture.HandlerCalls.ShouldBe(2);
        etag.ReceivedCalls().ShouldBeEmpty();
        var cache = (System.Collections.IDictionary)typeof(CachingProjectionActor).GetField("_payloadCache", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(actor)!;
        cache.Count.ShouldBe(0);
        IProjectionActorInvoker invoker = Substitute.For<IProjectionActorInvoker>();
        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance, fixture.Routes, (query, token) => DomainQueryDispatcher.ExecuteAsync(fixture.Services, query, token));
        var submit = new Hexalith.EventStore.Server.Pipeline.Queries.SubmitQuery("tenant", "d", "a", "get-total", "{}"u8.ToArray(), "c", "u");
        QueryRouterResult routed = await router.RouteQueryAsync(submit, fixture.Token);
        routed.Success.ShouldBeTrue();
        invoker.ReceivedCalls().ShouldBeEmpty();
        cache.Count.ShouldBe(0);
    }

    [Fact]
    public void IndependentCatalogAndRootVectorsHaveExactCapacityAndHistoricalSeparation()
    {
        using JsonDocument vectors = JsonDocument.Parse(File.ReadAllText(FindVectors()));
        byte[] Read(string name) => Convert.FromHexString(vectors.RootElement.GetProperty(name).GetProperty("hex").GetString()!);
        using var budget = new EventBufferBudget();
        byte[] row = Read("query-row");
        LogicalQueryCatalogCodec.Compute(row, [Read("input-route"), Read("input-declaration")], [Read("dependency")], budget).ShouldBe(Convert.FromHexString(vectors.RootElement.GetProperty("query-compatibility").GetProperty("sha256").GetString()!));
        int catalogCapacity = vectors.RootElement.GetProperty("query-compatibility").GetProperty("bytes").GetInt32();
        using (var exact = new EventBufferBudget(catalogCapacity))
        {
            _ = LogicalQueryCatalogCodec.Compute(row, [Read("input-route"), Read("input-declaration")], [Read("dependency")], exact);
            exact.LiveBytes.ShouldBe(0);
        }

        using (var shortBudget = new EventBufferBudget(catalogCapacity - 1))
        {
            Should.Throw<InvalidOperationException>(() => LogicalQueryCatalogCodec.Compute(row, [Read("input-route"), Read("input-declaration")], [Read("dependency")], shortBudget));
            shortBudget.LiveBytes.ShouldBe(0);
        }

        Should.Throw<ArgumentException>(() => EventRegistryFingerprintCodec.Compute("d", [row]));
        DaprLogicalQueryRoot root = new("dapr-actor-logical-v1", "tenant", "d", "route", "space", "store", "backend"u8.ToArray(), 1, [new("item:a", "value", "{\"value\":2}"u8.ToArray(), null, "op-1")]);
        int size = DaprLogicalQueryRootCodec.Measure(root, 4096);
        size.ShouldBe(vectors.RootElement.GetProperty("root-present").GetProperty("bytes").GetInt32());
        DaprLogicalQueryRootCodec.Compute(root, size, budget).ShouldBe(Convert.FromHexString(vectors.RootElement.GetProperty("root-present").GetProperty("sha256").GetString()!));
        Should.Throw<InvalidOperationException>(() => DaprLogicalQueryRootCodec.Compute(root, size - 1, budget));
        Should.Throw<ArgumentException>(() => LogicalQueryCatalogCodec.Compute(row, [Read("input-declaration"), Read("input-route")], [Read("dependency")], budget));
        Should.Throw<ArgumentException>(() => LogicalQueryCatalogCodec.Compute(row, [Read("input-route"), Read("input-route")], [Read("dependency")], budget));
        Should.Throw<ArgumentException>(() => LogicalQueryCatalogCodec.Compute(row, [Read("input-route"), Read("input-declaration")], [Read("dependency"), Read("dependency")], budget));
        budget.LiveBytes.ShouldBe(0);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("principal")]
    public async Task ForeignScopeRefusesBeforeActualRootLookup(string scope)
    {
        using var fixture = new DaprLogicalQueryFixture();
        QueryEnvelope query = scope == "tenant" ? fixture.Query with
        {
            TenantId = "foreign"
        }

        : fixture.Query with
        {
            UserId = "foreign"
        };
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.ExecuteAsync(query));
        fixture.SourceReads.ShouldBe(0);
        fixture.HandlerCalls.ShouldBe(0);
        fixture.Budget!.LiveBytes.ShouldBe(0);
        fixture.Calls.ShouldNotContain("authorization");
    }

    [Theory]
    [InlineData(513, false)]
    [InlineData(256, true)]
    [InlineData(255, true)]
    public async Task StrictUtf8KeyBoundaryRefusesBeforeOwner(int count, bool multibyte)
    {
        using var fixture = new DaprLogicalQueryFixture();
        fixture.PlanKey = multibyte ? "item:" + new string ('é', count) : new string ('a', count);
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.ExecuteAsync());
        fixture.SourceReads.ShouldBe(0);
        fixture.HandlerCalls.ShouldBe(0);
        fixture.Calls.ShouldNotContain("authorization");
    }

    [Fact]
    public async Task Exact512ByteMultibyteKeyReachesOwnerAdmission()
    {
        using var fixture = new DaprLogicalQueryFixture();
        fixture.PlanKey = "item:" + new string ('é', 253) + "x";
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.ExecuteAsync());
        fixture.Calls.ShouldContain("authorization");
        fixture.SourceReads.ShouldBe(1);
        fixture.HandlerCalls.ShouldBe(0);
        fixture.Budget!.LiveBytes.ShouldBe(0);
    }

    [Fact]
    public async Task OriginParticipantChangeUnderUnchangedRootRefusesFinalRelease()
    {
        using var fixture = new DaprLogicalQueryFixture();
        fixture.Hook = point =>
        {
            if (point != "between-reads")
            {
                return;
            }

            string key = fixture.Origins.Keys.First();
            fixture.Origins[key] = fixture.Origins[key] with
            {
                OperationId = "substituted"
            };
        };
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.ExecuteAsync());
        fixture.Root.Generation.ShouldBe(1);
        fixture.Observed.ShouldBe([2, 3]);
        fixture.ProducedBytes!.All(static b => b == 0).ShouldBeTrue();
        fixture.Budget!.LiveBytes.ShouldBe(0);
    }

    [Fact]
    public async Task RepeatedGraphAdmissionUsesOneComposedBudget()
    {
        using var fixture = new DaprLogicalQueryFixture(graphBytes: 64 * 1024 * 1024);
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.ExecuteAsync());
        fixture.Materializations.ShouldBe(1);
        fixture.Observed.ShouldBe([2]);
        fixture.ProducedBytes.ShouldBeNull();
        fixture.Budget!.LiveBytes.ShouldBe(0);
        fixture.Captured.All(static a => a.All(static b => b == 0)).ShouldBeTrue();
    }

    [Fact]
    public async Task LargeAdmittedRequestKeepsCallerBytesAndRestoresCharge()
    {
        using var fixture = new DaprLogicalQueryFixture();
        byte[] bytes = Enumerable.Repeat((byte)' ', 16 * 1024 * 1024).ToArray();
        bytes[0] = (byte)'{';
        bytes[1] = (byte)'}';
        QueryResult result = await fixture.ExecuteAsync(fixture.Query with { Payload = bytes });
        result.Success.ShouldBeTrue();
        bytes[0].ShouldBe((byte)'{');
        bytes[^1].ShouldBe((byte)' ');
        fixture.HandlerCalls.ShouldBe(1);
        fixture.Budget!.LiveBytes.ShouldBe(0);
    }

    [Fact]
    public async Task HistoricalMatchingCacheEntryRefusesWithoutClearingBorrowedBytes()
    {
        using var fixture = new DaprLogicalQueryFixture();
        IETagService etag = Substitute.For<IETagService>();
        var actor = new DaprLogicalQueryCachingActor(ActorHost.CreateForTest<DaprLogicalQueryCachingActor>(), etag, fixture.Routes, (query, token) => DomainQueryDispatcher.ExecuteAsync(fixture.Services, query, token));
        var cache = (System.Collections.IDictionary)typeof(CachingProjectionActor).GetField("_payloadCache", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(actor)!;
        Type[] types = cache.GetType().GetGenericArguments();
        byte[] borrowed = "legacy"u8.ToArray();
        cache.Add(Activator.CreateInstance(types[0], "GET-TOTAL", "sum", null, "u")!, Activator.CreateInstance(types[1], borrowed, null)!);
        await Should.ThrowAsync<InvalidOperationException>(() => actor.QueryAsync(fixture.Query, fixture.Token));
        borrowed.ShouldBe("legacy"u8.ToArray());
        cache.Count.ShouldBe(1);
        fixture.HandlerCalls.ShouldBe(0);
        etag.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public void CanonicalRootBindingsUseDecodedComponentsAndRejectNonAdjacentDuplicates()
    {
        using JsonDocument vectors = JsonDocument.Parse(File.ReadAllText(FindVectors()));
        byte[] original = Convert.FromHexString(vectors.RootElement.GetProperty("query-row").GetProperty("hex").GetString()!);
        byte[] Rewrite(params string[] routes)
        {
            using var decoded = new EventRegistryRow(original, allowCatalog: true);
            using var row = new EventEvolutionBinaryWriter(8192);
            row.WriteByte(0x5b);
            row.WriteString("d");
            row.WriteString("get-total");
            row.WriteUInt16(15);
            for (byte field = 1; field < 15; field++)
            {
                row.WriteByte(field);
                row.WriteRaw(decoded.GetEncodedField(field));
            }

            using var bindings = new EventEvolutionBinaryWriter(4096);
            bindings.WriteUInt32(checked((uint)routes.Length));
            foreach (string route in routes)
            {
                using var item = new EventEvolutionBinaryWriter(1024);
                item.WriteString(route);
                item.WriteString("space");
                item.WriteString("shared");
                item.WriteString("store");
                item.WriteBytes("backend"u8);
                item.WriteString("");
                bindings.WriteBytes(item.CopyEncodedBytes());
            }

            row.WriteByte(15);
            row.WriteBytes(bindings.CopyEncodedBytes());
            return row.CopyEncodedBytes();
        }

        using var accepted = new EventRegistryRow(Rewrite("aa", "z"), allowCatalog: true);
        LogicalQueryCatalogCodec.RequireBindings(accepted);
        foreach (string[] routes in new[]
        {
            new[]
            {
                "z",
                "aa"
            },
            new[]
            {
                "aa",
                "z",
                "aa"
            }
        }

        )
        {
            using var refused = new EventRegistryRow(Rewrite(routes), allowCatalog: true);
            Should.Throw<ArgumentException>(() => LogicalQueryCatalogCodec.RequireBindings(refused));
        }
    }

    [Fact]
    public void EveryIndependentRootAndRequestVectorHasExactCapacity()
    {
        using JsonDocument vectors = JsonDocument.Parse(File.ReadAllText(FindVectors()));
        using var budget = new EventBufferBudget();
        DaprLogicalQueryRoot root = new("dapr-actor-logical-v1", "tenant", "d", "route", "space", "store", "backend"u8.ToArray(), 1, []);
        DaprLogicalQueryRow present = new("item:a", "value", "{\"value\":2}"u8.ToArray(), null, "op-1");
        foreach (string name in new[]
        {
            "root-empty",
            "root-present",
            "root-absent",
            "root-ttl",
            "root-two",
            "root-generation"
        }

        )
        {
            DaprLogicalQueryRoot current = root with
            {
                Rows = name switch
                {
                    "root-empty" => [],
                    "root-absent" => [new("item:a", "", null, null, "op-2")],
                    "root-ttl" => [present with
                    {
                        ExpiresAt = new DateTimeOffset(638900000000000000, TimeSpan.Zero)
                    }

                    ],
                    "root-two" => [present, new("item:b", "value", "{\"value\":3}"u8.ToArray(), null, "op-2")],
                    _ => [present]
                },
                Generation = name == "root-generation" ? 2 : 1
            };
            int size = vectors.RootElement.GetProperty(name).GetProperty("bytes").GetInt32();
            DaprLogicalQueryRootCodec.Measure(current, size).ShouldBe(size);
            DaprLogicalQueryRootCodec.Compute(current, size, budget).ShouldBe(Convert.FromHexString(vectors.RootElement.GetProperty(name).GetProperty("sha256").GetString()!));
            Should.Throw<InvalidOperationException>(() => DaprLogicalQueryRootCodec.Compute(current, size - 1, budget));
        }

        QueryEnvelope basic = new("tenant", "d", "a", "get-total", "{}"u8.ToArray(), "c", "u");
        QueryEnvelope full = basic with
        {
            EntityId = "entity",
            IsGlobalAdmin = true,
            Paging = new(10, 2, "cursor"),
            OriginalActorId = "actor",
            AuthenticatedWorkloadId = "workload",
            IsDelegated = true,
            Scopes = ["s1", "s2"],
            Audience = ["aud"],
            DelegationId = "delegation",
            IdentityAdmissionProof = "proof"
        };
        LogicalQueryRequestCodec.Compute(basic, budget, CancellationToken.None).ShouldBe(Convert.FromHexString(vectors.RootElement.GetProperty("request-basic").GetProperty("sha256").GetString()!));
        LogicalQueryRequestCodec.Compute(full, budget, CancellationToken.None).ShouldBe(Convert.FromHexString(vectors.RootElement.GetProperty("request-full").GetProperty("sha256").GetString()!));
        budget.LiveBytes.ShouldBe(0);
    }

    [Fact]
    public async Task YieldingFinalFenceRetainsResponseChargeAndClearsFullBuffersOnRefusal()
    {
        using var fixture = new DaprLogicalQueryFixture();
        bool reached = false;
        fixture.AuthorizationHook = async () =>
        {
            if (!fixture.Returned || reached)
            {
                return;
            }

            reached = true;
            int metadata = JsonSerializer.SerializeToUtf8Bytes(fixture.Query with { Payload = [] }).Length;
            int expected = 3 * fixture.Query.Payload.Length + 1024 * 1024 + metadata * 16 + 1024 * 1024 + 3 * 4096 + 64 * 1024 + 1024 * 1024 + 2 * (8192 + 256);
            fixture.Budget!.LiveBytes.ShouldBe(expected);
            await Task.Yield();
            fixture.Loss.ObserveViolation();
        };
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.ExecuteAsync());
        reached.ShouldBeTrue();
        fixture.HandlerCalls.ShouldBe(1);
        fixture.Budget!.LiveBytes.ShouldBe(0);
        fixture.ProducedBytes!.All(static b => b == 0).ShouldBeTrue();
        fixture.Captured.Count.ShouldBeGreaterThan(0);
        fixture.Captured.All(static a => a.All(static b => b == 0)).ShouldBeTrue();
    }

    [Fact]
    public void MalformedCompleteRootRefusesOrderingAbsenceAndGeneration()
    {
        using var budget = new EventBufferBudget();
        DaprLogicalQueryRoot root = DaprLogicalQueryFixture.MakeRoot();
        foreach (DaprLogicalQueryRoot bad in new[]
        {
            root with
            {
                Generation = 0
            },
            root with
            {
                Rows = [root.Rows[1], root.Rows[0]]
            },
            root with
            {
                Rows = [root.Rows[0], root.Rows[0]]
            },
            root with
            {
                Rows = [new("item:a", "value", null, null, "op-1")]
            }
        }

        )
        {
            Should.Throw<InvalidOperationException>(() => DaprLogicalQueryRootCodec.Compute(bad, 4096, budget));
        }

        budget.LiveBytes.ShouldBe(0);
    }

    [Fact]
    public async Task PreparationOptionsMutationStopsBeforeRequestValidatorAndResolver()
    {
        using var fixture = new DaprLogicalQueryFixture();
        using var budget = new EventBufferBudget();
        QueryEnvelope privateQuery = fixture.Query with
        {
            Payload = fixture.Query.Payload.ToArray()
        };
        byte[] hash = LogicalQueryRequestCodec.Compute(privateQuery, budget, fixture.Token);
        fixture.Hook = point =>
        {
            if (point == "request-options")
            {
                privateQuery.Payload[0] ^= 1;
            }
        };
        Task Fence(CancellationToken token)
        {
            byte[] actual = LogicalQueryRequestCodec.Compute(privateQuery, budget, token);
            if (!actual.AsSpan().SequenceEqual(hash))
            {
                throw new InvalidOperationException("changed request");
            }

            return Task.CompletedTask;
        }

        using var payload = new ImmutablePayload(privateQuery.Payload.ToArray(), privateQuery.Payload.Length, fixture.Token);
        Exception? refusal = null;
        try
        {
            _ = await fixture.Descriptor.PrepareAsync(privateQuery, payload, budget, fixture.Token, Fence);
        }
        catch (InvalidOperationException exception)
        {
            refusal = exception;
        }

        fixture.Calls.ShouldBe(["request-options"]);
        refusal.ShouldNotBeNull();
        fixture.HandlerCalls.ShouldBe(0);
        fixture.SourceReads.ShouldBe(0);
        budget.LiveBytes.ShouldBe(0);
    }

    [Fact]
    public async Task PrivateMetadataArrayReferencesClearWhileCallerArraysRemain()
    {
        using var fixture = new DaprLogicalQueryFixture();
        QueryEnvelope request = fixture.Query with
        {
            Scopes = ["read:x"],
            Audience = ["domain"]
        };
        QueryResult result = await fixture.ExecuteAsync(request);
        result.Success.ShouldBeTrue();
        request.Scopes.ShouldBe(["read:x"]);
        request.Audience.ShouldBe(["domain"]);
        fixture.PrivateQuery!.Scopes!.Count.ShouldBe(1);
        fixture.PrivateQuery.Scopes[0].ShouldBeNull();
        fixture.PrivateQuery.Audience!.Count.ShouldBe(1);
        fixture.PrivateQuery.Audience[0].ShouldBeNull();
        fixture.PrivateQuery.Payload.All(static b => b == 0).ShouldBeTrue();
        fixture.Budget!.LiveBytes.ShouldBe(0);
    }

    [Fact]
    public async Task CurrentCallableFilePinRefusesBeforeAnyCallback()
    {
        using var fixture = new DaprLogicalQueryFixture();
        object binding = typeof(LogicalQueryDescriptor).GetField("_request", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture.Descriptor)!;
        byte[] expectedImage = (byte[])typeof(LogicalQueryCallable).GetField("_assemblyHash", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(binding)!;
        expectedImage[0] ^= 1;
        Exception? refusal = null;
        try
        {
            _ = await fixture.ExecuteAsync();
        }
        catch (InvalidOperationException exception)
        {
            refusal = exception;
        }

        fixture.HandlerCalls.ShouldBe(0);
        fixture.Calls.ShouldBeEmpty();
        fixture.SourceReads.ShouldBe(0);
        refusal.ShouldNotBeNull();
    }

    [Fact]
    public async Task PrivateMetadataRefusalClearsOnlyPrivateReferences()
    {
        using var fixture = new DaprLogicalQueryFixture();
        QueryEnvelope request = fixture.Query with
        {
            Scopes = ["read:x"],
            Audience = ["domain"]
        };
        fixture.Hook = point =>
        {
            if (point == "response")
            {
                fixture.Loss.ObserveViolation();
            }
        };
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.ExecuteAsync(request));
        request.Scopes.ShouldBe(["read:x"]);
        request.Audience.ShouldBe(["domain"]);
        fixture.PrivateQuery!.Scopes![0].ShouldBeNull();
        fixture.PrivateQuery.Audience![0].ShouldBeNull();
        fixture.Budget!.LiveBytes.ShouldBe(0);
    }

    [Fact]
    public async Task OriginalCancellationWinsDirectStoreCallWithBothTokensCancelled()
    {
        using var fixture = new DaprLogicalQueryFixture();
        fixture.DualCancellation = true;
        OperationCanceledException refusal = await Should.ThrowAsync<OperationCanceledException>(() => fixture.ExecuteAsync());
        refusal.CancellationToken.ShouldBe(fixture.Token);
        fixture.StoreRefusalToken.ShouldBe(fixture.Token);
        fixture.Calls.ShouldNotContain("input-validation");
        fixture.Materializations.ShouldBe(0);
        fixture.Budget!.LiveBytes.ShouldBe(0);
    }

    private static string FindVectors()
    {
        DirectoryInfo? path = new(Environment.CurrentDirectory);
        while (path is not null)
        {
            string candidate = Path.Combine(path.FullName, "_bmad-output/implementation-artifacts/evidence/story-6-6/dapr-logical-query-2026-10-08/vectors.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            path = path.Parent;
        }

        throw new FileNotFoundException("Independent query vectors are missing.");
    }
}
