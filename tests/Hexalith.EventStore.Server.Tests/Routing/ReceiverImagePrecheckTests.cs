using System.Buffers;
using System.Reflection;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Actual receiver prechecks borrow only their default DI owner's open computation lifetime, preserving all original observations.</summary>
[Collection("RouteCatalogImageBufferPool")]
public sealed class ReceiverImagePrecheckTests
{
#if DEBUG
    private static bool PrivateExpected => true;
#else
    private static bool PrivateExpected => false;
#endif

    [Theory]
    [InlineData("dapr/config")]
    [InlineData("healthz")]
    public async Task DefaultFactoryAttachesOnlyWhenResolvedAndDiscoveryClosesBeforeNext(string path)
    {
        await using var fixture = new ReceiverImagePrecheckFixture();
        fixture.OnNext = _ =>
        {
            RouteCatalogImageDigestScope.Current.ShouldBeNull();
            fixture.Registrations.Scopes.ShouldAllBe(scope => scope == null || !scope.IsOpen);
        };
        (await fixture.RequestAsync(path)).Response.StatusCode.ShouldBe(204);
        fixture.StoreConstructions.ShouldBe(0);
        fixture.Registrations.Scopes.Count.ShouldBe(4);
        fixture.Registrations.Scopes.ShouldAllBe(scope => scope == null);
        var owner = fixture.ResolveOwner();
        fixture.StoreConstructions.ShouldBe(1); fixture.StateReads.ShouldBe(0);
        ReceiverImagePrecheckFixture.Memo(owner).Count.ShouldBe(0);
        fixture.Registrations.Reset();
        (await fixture.RequestAsync(path)).Response.StatusCode.ShouldBe(204);
        fixture.Registrations.Scopes.Count.ShouldBe(4);
        var cold = fixture.Registrations.Scopes[0];
        fixture.Registrations.Scopes.ShouldAllBe(scope => ReferenceEquals(scope, cold));
        fixture.Registrations.Reset();
        (await fixture.RequestAsync(path)).Response.StatusCode.ShouldBe(204);
        var warm = fixture.Registrations.Scopes[0];
        if (PrivateExpected)
        {
            cold.ShouldNotBeNull().CacheHits.ShouldBe(8);
            warm.ShouldNotBeNull().ShouldNotBeSameAs(cold); warm.CacheHits.ShouldBe(12);
            cold.IsOpen.ShouldBeFalse(); warm.IsOpen.ShouldBeFalse();
            ReceiverImagePrecheckFixture.Memo(owner).Count.ShouldBe(4);
        }
        else { cold.ShouldBeNull(); warm.ShouldBeNull(); }
        fixture.StateReads.ShouldBe(0); fixture.Trust.ReceivedCalls().ShouldBeEmpty();
        fixture.NextCalls.ShouldBe(3);
    }

    [Fact]
    public async Task NativeSequenceAndRequestFingerprintHaveSeparateClosedScopesBeforeActualAcquire()
    {
        await using var fixture = new ReceiverImagePrecheckFixture();
        fixture.CaptureNativeBaseline(); fixture.ResolveOwner();
        fixture.OnStateRead = () =>
        {
            fixture.Registrations.Scopes.Count.ShouldBe(7);
            fixture.Registrations.Scopes.ShouldAllBe(scope => scope == null || !scope.IsOpen);
            fixture.Registrations.Scopes.ShouldNotContain(scope => scope != null && ReferenceEquals(scope, fixture.StateReadImageScope));
        };
        (await fixture.RequestAsync("actors/{actorTypeName}/{actorId}/method/{methodName}")).Response.StatusCode.ShouldBe(503);
        fixture.StateReads.ShouldBe(1); fixture.NextCalls.ShouldBe(0); fixture.Trust.ReceivedCalls().ShouldBeEmpty();
        var observed = fixture.Registrations.Scopes;
        if (PrivateExpected)
        {
            var first = observed[0].ShouldNotBeNull(); var second = observed[5].ShouldNotBeNull();
            first.ShouldNotBeSameAs(second);
            observed.Take(5).ShouldAllBe(scope => ReferenceEquals(scope, first));
            observed.Skip(5).ShouldAllBe(scope => ReferenceEquals(scope, second));
            first.CacheHits.ShouldBe(8); second.CacheHits.ShouldBe(8);
            fixture.StateReadImageScope.ShouldNotBeNull().IsOpen.ShouldBeFalse();
        }
        else observed.ShouldAllBe(scope => scope == null);
        RouteCatalogImageDigestScope.Current.ShouldBeNull();
    }

    [Theory]
    [InlineData("scoped")]
    [InlineData("retention")]
    [InlineData("fixture")]
    [InlineData("technical")]
    [InlineData("profile")]
    [InlineData("environment")]
    [InlineData("closed")]
    [InlineData("custom")]
    [InlineData("missing")]
    public async Task IneligibleOrUnattachedOwnersSuppressForeignComputations(string mode)
    {
        await using var fixture = new ReceiverImagePrecheckFixture(mode == "custom");
        RouteCatalogAuthority? owner = mode is "custom" or "missing" ? null : fixture.ResolveOwner();
        switch (mode)
        {
            case "scoped": fixture.Options.ScopedDecodeReuse = false; break;
            case "retention": fixture.Options.RetainValidatedCatalogBytes = false; break;
            case "fixture": fixture.Options.DevelopmentFixture = false; break;
            case "technical": fixture.Options.RequireTechnicalHttpBinding = false; break;
            case "profile": fixture.Options.Profile = "Production"; break;
            case "environment": fixture.App.Services.GetRequiredService<IHostEnvironment>().EnvironmentName = "Production"; break;
            case "closed": owner!.Dispose(); break;
        }
        using var foreignMemo = new RouteCatalogImageDigestMemo();
        using var foreign = RouteCatalogImageDigestScope.Enter(foreignMemo);
        fixture.OnNext = _ => RouteCatalogImageDigestScope.Current.ShouldBeSameAs(foreign);
        (await fixture.RequestAsync()).Response.StatusCode.ShouldBe(204);
        fixture.Registrations.Scopes.Count.ShouldBe(4);
        fixture.Registrations.Scopes.ShouldAllBe(scope => scope == null);
        foreignMemo.Count.ShouldBe(0); foreign.CacheHits.ShouldBe(0);
        if (owner is not null) ReceiverImagePrecheckFixture.Memo(owner).Count.ShouldBe(0);
        fixture.StateReads.ShouldBe(0); fixture.Trust.ReceivedCalls().ShouldBeEmpty();
        if (owner is null) fixture.StoreConstructions.ShouldBe(0);
    }

    [Fact]
    public async Task ForeignScopeIsNotBorrowedAndOnlyTheActualDefaultOwnerBecomesWarm()
    {
        await using var fixture = new ReceiverImagePrecheckFixture(); var owner = fixture.ResolveOwner();
        using var foreignMemo = new RouteCatalogImageDigestMemo(); using var foreign = RouteCatalogImageDigestScope.Enter(foreignMemo);
        fixture.OnNext = _ => RouteCatalogImageDigestScope.Current.ShouldBeSameAs(foreign);
        (await fixture.RequestAsync()).Response.StatusCode.ShouldBe(204);
        fixture.Registrations.Reset();
        (await fixture.RequestAsync()).Response.StatusCode.ShouldBe(204);
        foreignMemo.Count.ShouldBe(0); foreign.CacheHits.ShouldBe(0);
        ReceiverImagePrecheckFixture.Memo(owner).Count.ShouldBe(PrivateExpected ? 4 : 0);
        fixture.Registrations.Scopes.ShouldAllBe(scope => scope == null || !scope.IsOpen);
        fixture.Registrations.Scopes.ShouldNotContain(scope => ReferenceEquals(scope, foreign));
        if (PrivateExpected) fixture.Registrations.Scopes[0]!.CacheHits.ShouldBe(12);
    }

    [Fact]
    public async Task TwoDefaultProvidersSharingAReceiverPermanentlyFallBackAfterAmbiguousAttachment()
    {
        await using var fixture = new ReceiverImagePrecheckFixture(); var first = fixture.ResolveOwner();
        fixture.Receiver.AttachPrivateImageOwner(first); // Same-owner attachment is idempotent.
        (await fixture.RequestAsync()).Response.StatusCode.ShouldBe(204);
        int retained = ReceiverImagePrecheckFixture.Memo(first).Count;
        retained.ShouldBe(PrivateExpected ? 4 : 0);
        var services = new ServiceCollection();
        services.AddSingleton(fixture.Receiver);
        services.AddSingleton<IRouteCatalogStateStore>(Substitute.For<IRouteCatalogStateStore>());
        services.AddSingleton<IRouteCatalogTrustValidator>(Substitute.For<IRouteCatalogTrustValidator>());
        services.AddSingleton<IHostEnvironment>(fixture.App.Environment);
        services.AddEventStoreRouteCatalog(fixture.App.Configuration);
        using var provider = services.BuildServiceProvider();
        var second = (RouteCatalogAuthority)provider.GetRequiredService<IRouteCatalogAuthority>();
        second.ShouldNotBeSameAs(first);
        fixture.Receiver.AttachPrivateImageOwner(first); // Ambiguity cannot be undone by rebinding.
        fixture.Registrations.Reset();
        (await fixture.RequestAsync()).Response.StatusCode.ShouldBe(204);
        fixture.Registrations.Scopes.ShouldAllBe(scope => scope == null);
        ReceiverImagePrecheckFixture.Memo(first).Count.ShouldBe(retained);
        ReceiverImagePrecheckFixture.Memo(second).Count.ShouldBe(0);
    }

    [Fact]
    public async Task NativeRegistrationDriftStillRefusesCompleteAndClosesBorrowedScope()
    {
        await using var fixture = new ReceiverImagePrecheckFixture(); fixture.ResolveOwner();
        fixture.Registrations.OnRead = ordinal =>
        {
            if (ordinal == 3)
            {
                var actors = fixture.App.Services.GetRequiredService<IOptions<ActorRuntimeOptions>>().Value.Actors;
                actors.Remove(actors.Single(actor => actor.Type.ImplementationType == typeof(ETagActor)).Type);
            }
        };
        var failure = await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.RequestAsync());
        failure.Message.ShouldBe("Native SDK runtime inventory changed during observation.");
        fixture.Registrations.Scopes.Count.ShouldBe(3);
        fixture.Registrations.Scopes.ShouldAllBe(scope => scope == null || !scope.IsOpen);
        fixture.StateReads.ShouldBe(0); fixture.NextCalls.ShouldBe(0);
    }

    [Fact]
    public async Task SelectorDriftStartsNoSecondBorrowAndOriginalFreshAuthorityReadStillRuns()
    {
        await using var fixture = new ReceiverImagePrecheckFixture(); fixture.CaptureNativeBaseline(); fixture.ResolveOwner();
        fixture.Registrations.OnRead = ordinal => { if (ordinal == 2) fixture.Options.RetainValidatedCatalogBytes = false; };
        (await fixture.RequestAsync("actors/{actorTypeName}/{actorId}/method/{methodName}")).Response.StatusCode.ShouldBe(503);
        fixture.Registrations.Scopes.Count.ShouldBe(7);
        fixture.Registrations.Scopes.Skip(5).ShouldAllBe(scope => scope == null);
        fixture.Registrations.Scopes.Take(5).ShouldAllBe(scope => scope == null || !scope.IsOpen);
        if (PrivateExpected) fixture.Registrations.Scopes[0].ShouldNotBeNull();
        fixture.StateReads.ShouldBe(1);
    }

    [Fact]
    public async Task InvalidChannelStillStopsBeforeAnyNativeObservationOrOwnerResolution()
    {
        await using var fixture = new ReceiverImagePrecheckFixture();
        (await fixture.RequestAsync(validChannel: false)).Response.StatusCode.ShouldBe(401);
        fixture.Registrations.Scopes.ShouldBeEmpty(); fixture.StoreConstructions.ShouldBe(0); fixture.NextCalls.ShouldBe(0);
    }

    [Fact]
    public async Task EscapedContextCannotUseTheClosedReceiverBorrow()
    {
        await using var fixture = new ReceiverImagePrecheckFixture(); var owner = fixture.ResolveOwner();
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? escaped = null; RouteCatalogImageDigestScope? borrowed = null;
        fixture.Registrations.OnRead = ordinal =>
        {
            if (ordinal != 1) return;
            borrowed = RouteCatalogImageDigestScope.Current;
            escaped = Task.Run(async () =>
            {
                await resume.Task;
                RouteCatalogImageDigestScope.Current.ShouldBeSameAs(borrowed);
                (borrowed?.IsOpen ?? false).ShouldBeFalse();
                var ordinary = ArrayPool<byte>.Create(4096, 1);
                RouteCatalogImageBuffers.Select(ordinary).ShouldBeSameAs(ordinary);
                int hits = borrowed?.CacheHits ?? 0;
                TechnicalHttpActionObservation.AssemblyIdentity(typeof(RouteCatalogAuthority).Assembly).Length.ShouldBeGreaterThan(64);
                (borrowed?.CacheHits ?? 0).ShouldBe(hits);
            }, TestContext.Current.CancellationToken);
        };
        try { (await fixture.RequestAsync()).Response.StatusCode.ShouldBe(204); }
        finally { resume.SetResult(); }
        await escaped.ShouldNotBeNull().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        ReceiverImagePrecheckFixture.Memo(owner).Count.ShouldBe(PrivateExpected ? 4 : 0);
    }

    [Theory]
    [InlineData("same-length")]
    [InlineData("invalid-pe")]
    public async Task BorrowedOwnerMemoStillReadsChangedBytesAndRechecksPeAndComplete(string mutation)
    {
        await using var fixture = new ReceiverImagePrecheckFixture(); var owner = fixture.ResolveOwner();
        string directory = Directory.CreateTempSubdirectory("receiver-image-mutation-").FullName;
        try
        {
            var actual = typeof(RouteCatalogAuthority).Assembly;
            string path = Path.Combine(directory, "image.dll"); File.Copy(actual.Location, path);
            using (var file = new FileStream(path, FileMode.Append, FileAccess.Write)) file.Write(new byte[128]);
            var image = new RouteCatalogDiskImageAssembly(actual, path);
            using var boundary = RouteCatalogImageDigestScope.Suppress();
            using var images = owner.BeginPrivateReceiverImageScope();
            var pass = new TechnicalHttpObservationPass(fixture.App.Services);
            string expected = pass.AssemblyIdentity(image);
            TechnicalHttpActionObservation.AssemblyIdentity(image).ShouldBe(expected);
            if (mutation == "same-length")
            {
                long length = new FileInfo(path).Length;
                using (var file = new FileStream(path, FileMode.Open, FileAccess.Write)) { file.Position = length - 1; file.WriteByte(0x7b); }
                new FileInfo(path).Length.ShouldBe(length);
                TechnicalHttpActionObservation.AssemblyIdentity(image).ShouldNotBe(expected);
                Should.Throw<RouteCatalogUnavailableException>(pass.Complete).Message.ShouldBe("Technical HTTP loaded image changed during observation.");
            }
            else
            {
                File.WriteAllBytes(path, [1, 2, 3]);
                Should.Throw<BadImageFormatException>(() => TechnicalHttpActionObservation.AssemblyIdentity(image));
                Should.Throw<BadImageFormatException>(pass.Complete);
            }
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
