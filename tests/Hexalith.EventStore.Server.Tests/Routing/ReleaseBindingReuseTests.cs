using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Hosting;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Exercises completed-lease binding reuse through actual fresh Dapr state validation and exact ETag writes.</summary>
[Collection("ReleaseBinding")]
public sealed class ReleaseBindingReuseTests(ITestOutputHelper output)
{
#if DEBUG
    private static bool PrivateExpected => true;
#else
    private static bool PrivateExpected => false;
#endif

    /// <summary>Release owns a distinct closing scope and never borrows foreign decode, binding or image contexts.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RealStoreReleaseUsesOnlyItsOwnSelectedRetainedBindingScope(bool retained)
    {
        using var fixture = await ReleaseBindingHttpFixture.CreateAsync();
        fixture.Options.RetainValidatedCatalogBytes = retained;
        var lease = await fixture.Authority.AcquireAsync(TestContext.Current.CancellationToken);
        var acquiring = fixture.Backend.Reads[0].Binding;
        using var foreignDecode = RouteCatalogDecodeScope.Enter();
        using var foreignBinding = RouteCatalogBindingScope.Enter();
        using var foreignImages = RouteCatalogImageDigestScope.Enter();
        RouteCatalogActivationBinding.ComputeDigest(fixture.Backend.State.Committed!);
        fixture.Backend.Releasing = true;
        await lease.DisposeAsync();
        var released = fixture.Backend.Reads.Single(row => row.Release);
        if (PrivateExpected && retained)
        {
            RouteCatalogBindingScope binding = released.Binding.ShouldNotBeNull();
            binding.ShouldNotBeSameAs(acquiring);
            binding.ShouldNotBeSameAs(foreignBinding);
            binding.CacheHits.ShouldBe(1);
            released.Decode.ShouldNotBeNull().ShouldNotBeSameAs(foreignDecode);
            binding.TryCompute(fixture.Backend.State.Committed!, out _).ShouldBeFalse();
        }
        else { released.Binding.ShouldBeNull(); released.Decode.ShouldBeNull(); }
        released.Image.ShouldBeNull();
        fixture.Backend.Writes.Single().Binding.ShouldBeSameAs(released.Binding);
        fixture.Backend.Writes.Single().Image.ShouldBeNull();
        fixture.Backend.State.Leases.ShouldBeEmpty();
        fixture.Backend.ReleaseReads.ShouldBe(1); fixture.Backend.ReleaseWrites.ShouldBe(1);
        RouteCatalogDecodeScope.Current.ShouldBeSameAs(foreignDecode);
        RouteCatalogBindingScope.Current.ShouldBeSameAs(foreignBinding);
        RouteCatalogImageDigestScope.Current.ShouldBeSameAs(foreignImages);
        foreignBinding.CacheHits.ShouldBe(0);
    }

    /// <summary>Every existing selector and disposed owning storage keeps completed-lease release on the original binding path.</summary>
    [Theory]
    [InlineData("scoped")]
    [InlineData("retention")]
    [InlineData("environment")]
    [InlineData("fixture")]
    [InlineData("profile")]
    [InlineData("technical")]
    [InlineData("closed")]
    public async Task DisabledOrClosedOwnerStillReadsAndReleasesFreshState(string changed)
    {
        using var fixture = await ReleaseBindingHttpFixture.CreateAsync();
        var lease = await fixture.Authority.AcquireAsync(TestContext.Current.CancellationToken);
        switch (changed)
        {
            case "scoped": fixture.Options.ScopedDecodeReuse = false; break;
            case "retention": fixture.Options.RetainValidatedCatalogBytes = false; break;
            case "environment": fixture.Environment.EnvironmentName.Returns(Environments.Production); break;
            case "fixture": fixture.Options.DevelopmentFixture = false; break;
            case "profile": fixture.Options.Profile = "Production"; break;
            case "technical": fixture.Options.RequireTechnicalHttpBinding = false; break;
            case "closed": fixture.Authority.Dispose(); break;
        }
        fixture.Backend.Releasing = true;
        await lease.DisposeAsync();
        fixture.Backend.Reads.Single(row => row.Release).Binding.ShouldBeNull();
        fixture.Backend.Reads.Single(row => row.Release).Image.ShouldBeNull();
        fixture.Backend.State.Leases.ShouldBeEmpty();
        fixture.Backend.ReleaseReads.ShouldBe(1); fixture.Backend.ReleaseWrites.ShouldBe(1);
    }

    /// <summary>Warm computation never substitutes a captured activation for freshly read changed or malformed backend bytes.</summary>
    [Theory]
    [InlineData("owner")]
    [InlineData("root")]
    [InlineData("catalog")]
    [InlineData("host")]
    public async Task ChangedFreshBackendInputsCannotInheritTheEarlierBinding(string changed)
    {
        using var fixture = await ReleaseBindingHttpFixture.CreateAsync();
        var lease = await fixture.Authority.AcquireAsync(TestContext.Current.CancellationToken);
        RouteCatalogActivationState original = fixture.Backend.State;
        RouteCatalogActivation activation = original.Committed!;
        byte[] canonical = activation.CanonicalCatalog.ToArray();
        fixture.Backend.State = original with { Committed = changed switch
        {
            "owner" => activation with { Owner = "different-valid-binding-owner" },
            "root" => activation with { RootDigest = new string('0', 64) },
            "host" => activation with { RequiredHosts = [activation.RequiredHosts[0] with { InventoryDigest = "invalid" }, .. activation.RequiredHosts.Skip(1)] },
            _ => activation,
        } };
        if (changed == "catalog") activation.CanonicalCatalog[^1] = (byte)'!';
        fixture.Backend.Releasing = true;
        if (changed == "owner")
        {
            await lease.DisposeAsync();
            fixture.Backend.State.Committed!.Owner.ShouldBe("different-valid-binding-owner");
            fixture.Backend.State.Leases.ShouldBeEmpty();
        }
        else
        {
            await Should.ThrowAsync<RouteCatalogUnavailableException>(async () => await lease.DisposeAsync());
            fixture.Backend.State.Leases.Count.ShouldBe(1);
            fixture.Backend.ReleaseWrites.ShouldBe(0);
            canonical.CopyTo(activation.CanonicalCatalog, 0);
            fixture.Backend.State = original;
            await lease.DisposeAsync();
            fixture.Backend.State.Leases.ShouldBeEmpty();
            fixture.Backend.ReleaseReads.ShouldBe(2);
        }
        if (PrivateExpected) fixture.Backend.Reads.First(row => row.Release).Binding!.CacheHits.ShouldBe(0);
        RouteCatalogBindingScope.Current.ShouldBeNull();
    }

    /// <summary>Conflicts, real 500 readback and ambiguous responses preserve exact fresh ETags, retries and the original durable fence.</summary>
    [Theory]
    [InlineData("conflict")]
    [InlineData("500-readback")]
    [InlineData("500-no-change")]
    [InlineData("loss-before")]
    [InlineData("loss-after")]
    public async Task RealStoreReadbackAndResponseLossKeepFreshReleaseRecovery(string fault)
    {
        using var fixture = await ReleaseBindingHttpFixture.CreateAsync();
        var lease = await fixture.Authority.AcquireAsync(TestContext.Current.CancellationToken);
        fixture.Backend.Releasing = true; fixture.Backend.Fault = fault;
        if (fault is "500-no-change" or "loss-before" or "loss-after")
        {
            await Should.ThrowAsync<RouteCatalogUnavailableException>(async () => await lease.DisposeAsync());
            fixture.Backend.State.Leases.Count.ShouldBe(fault == "loss-after" ? 0 : 1);
            await lease.DisposeAsync();
        }
        else await lease.DisposeAsync();
        fixture.Backend.State.Leases.ShouldBeEmpty();
        fixture.Backend.ReleaseWrites.ShouldBe(fault == "loss-after" ? 1 : 2);
        fixture.Backend.ReleaseReads.ShouldBe(fault is "500-readback" or "500-no-change" ? 3 : 2);
        fixture.Backend.Writes[0].ETag.ShouldBe("2");
        if (fault is "conflict" or "500-readback") fixture.Backend.Writes[1].ETag.ShouldBe("3");
        foreach (var read in fixture.Backend.Reads.Where(row => row.Release))
        {
            read.Image.ShouldBeNull();
            if (PrivateExpected) read.Binding.ShouldNotBeNull().CacheHits.ShouldBeGreaterThanOrEqualTo(1);
            else read.Binding.ShouldBeNull();
        }
        RouteCatalogBindingScope.Current.ShouldBeNull();
    }

    /// <summary>Owner disposal during a fresh backend read closes reuse without accepting stale validation or preventing safe fence removal.</summary>
    [Fact]
    public async Task DisposalDuringRealReadFallsBackWithoutRetainingNewComputation()
    {
        using var fixture = await ReleaseBindingHttpFixture.CreateAsync();
        var lease = await fixture.Authority.AcquireAsync(TestContext.Current.CancellationToken);
        fixture.Backend.Releasing = true;
        fixture.Backend.BeforeReleaseRead = fixture.Authority.Dispose;
        await lease.DisposeAsync();
        var scope = fixture.Backend.Reads.Single(row => row.Release).Binding;
        if (PrivateExpected) scope.ShouldNotBeNull().CacheHits.ShouldBe(0);
        else scope.ShouldBeNull();
        fixture.Backend.State.Leases.ShouldBeEmpty();
    }

    /// <summary>Measures actual fresh HTTP state decode and binding validation, including both private scope lifetimes and disposal.</summary>
    [Fact]
    public async Task MeasureCompleteRepresentativeRealStoreReadWithOwnedReleaseBinding()
    {
        using var fixture = await ReleaseBindingHttpFixture.CreateAsync();
        byte[] bytes = NativeReadDecodeReuseTests.RepresentativeCatalog();
        string root = Convert.ToHexStringLower(SHA256.HashData(bytes));
        RouteCatalogActivation activation = fixture.Backend.State.Committed! with { CanonicalCatalog = bytes, RootDigest = root };
        fixture.Backend.State = fixture.Backend.State with { Committed = activation };
        fixture.Backend.RecordScopes = false;
        using var decodeMemo = new RouteCatalogValidatedBytesMemo();
        using var bindingMemo = new RouteCatalogBindingMemo();
        async Task<(double Elapsed, long Allocated, int Hits)> Read(bool binding)
        {
            long before = GC.GetAllocatedBytesForCurrentThread(), started = Stopwatch.GetTimestamp();
            int hits;
            using (RouteCatalogDecodeScope.Suppress())
            using (RouteCatalogBindingScope.Suppress())
            using (RouteCatalogImageDigestScope.Suppress())
            using (var decoding = RouteCatalogDecodeScope.EnterWithMemo(decodeMemo))
            using (var computation = binding ? RouteCatalogBindingScope.Enter(bindingMemo) : null)
            {
                var observed = await fixture.Store.ReadAsync(TestContext.Current.CancellationToken);
                observed.State.Committed!.RootDigest.ShouldBe(root);
                hits = computation?.CacheHits ?? 0;
            }
            return (Stopwatch.GetElapsedTime(started).TotalMilliseconds, GC.GetAllocatedBytesForCurrentThread() - before, hits);
        }
        var cold = await Read(true);
        for (int warmup = 0; warmup < 12; warmup++) { await Read(false); await Read(true); }
        var samples = new List<object>();
        for (int sample = 0; sample < 7; sample++)
        {
            double plainMs = 0, ownedMs = 0; long plainBytes = 0, ownedBytes = 0;
            for (int iteration = 0; iteration < 24; iteration++)
            {
                var first = await Read(iteration % 2 == 0);
                var second = await Read(iteration % 2 != 0);
                var owned = iteration % 2 == 0 ? first : second;
                var plain = iteration % 2 == 0 ? second : first;
                owned.Hits.ShouldBe(1); plain.Hits.ShouldBe(0);
                plainMs += plain.Elapsed; ownedMs += owned.Elapsed; plainBytes += plain.Allocated; ownedBytes += owned.Allocated;
            }
            samples.Add(new { sample, originalReleaseReadMs = plainMs / 24, ownedBindingReleaseReadMs = ownedMs / 24,
                originalAllocatedBytes = plainBytes / 24, ownedAllocatedBytes = ownedBytes / 24 });
        }
        output.WriteLine(JsonSerializer.Serialize(new { representativeOnly = true, catalogBytes = bytes.Length,
            realDaprStoreHttpSerializationFreshDecodeAndBinding = true, allScopesAndDisposalIncluded = true,
            firstOwnedMs = cold.Elapsed, firstOwnedAllocatedBytes = cold.Allocated, measurements = samples }));
    }
}
