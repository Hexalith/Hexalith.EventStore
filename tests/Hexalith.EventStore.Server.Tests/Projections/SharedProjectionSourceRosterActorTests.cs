using Dapr.Actors;
using Dapr.Actors.Runtime;
using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Tests.TestUtilities;
using Hexalith.EventStore.Testing.Fakes;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed class SharedProjectionSourceRosterActorTests
{
    [Fact]
    public async Task UncertifiedInstallationAndWritesFailClosed()
    {
        var scope = new SharedProjectionSourceScope("tenant-a", "timesheets");
        var backend = new InMemoryStateManager();
        var actor = new SharedProjectionSourceRosterActor(ActorHost.CreateForTest<SharedProjectionSourceRosterActor>(
            new ActorTestOptions { ActorId = new(scope.ActorId) }));
        ActorStateManagerTestHelper.SetStateManager(actor, backend);

        (await actor.InstallAsync(new(scope, "clean", "writers", true, []))).ShouldBeFalse();
        (await actor.BeginWriteAsync(new AggregateIdentity("tenant-a", "timesheets", "owner-a"),
            "write-a", false)).ShouldBeFalse();
        (await actor.BeginCaptureAsync(scope, "capture-a")).ShouldBeNull();
        backend.CommittedState.ShouldBeEmpty();
    }

    [Fact]
    public async Task CertifiedCleanRosterCapturesTenThousandOwnersAndFencesWrites()
    {
        var scope = new SharedProjectionSourceScope("tenant-a", "timesheets");
        var authority = Substitute.For<IInstallationCoverageAuthority>();
        authority.AuthorizeInstallationAsync(Arg.Any<SharedProjectionSourceInstallation>()).Returns(true);
        authority.AuthorizeWriteAsync(Arg.Any<SharedProjectionSourceScope>(), Arg.Any<AggregateIdentity>(),
            Arg.Any<string>()).Returns(true);
        authority.AuthorizeCaptureAsync(Arg.Any<SharedProjectionSourceScope>(), Arg.Any<string>()).Returns(true);
        var backend = new InMemoryStateManager();
        long acceptedCount = 0;
        DomainSharedProjectionRebuildPhase phase = DomainSharedProjectionRebuildPhase.Accumulating;
        ISharedProjectionRebuildGateway gateway = ReceiptGateway(() => (phase, acceptedCount));
        var actor = new SharedProjectionSourceRosterActor(ActorHost.CreateForTest<SharedProjectionSourceRosterActor>(
            new ActorTestOptions { ActorId = new(scope.ActorId) }), authority, gateway);
        ActorStateManagerTestHelper.SetStateManager(actor, backend);
        (await actor.InstallAsync(new(scope, "certified-clean", "all-writers", true, []))).ShouldBeTrue();

        for (int index = 0; index < 10_000; index++)
        {
            var owner = new AggregateIdentity(scope.TenantId, scope.Domain, $"owner-{index:D5}");
            string intent = $"write-{index:D5}";
            (await actor.BeginWriteAsync(owner, intent, false)).ShouldBeTrue();
            (await actor.CompleteWriteAsync(owner, intent)).ShouldBeTrue();
        }

        long revision = (await actor.BeginCaptureAsync(scope, "capture-a")).ShouldNotBeNull();
        var captured = new List<AggregateIdentity>();
        for (int partition = 0; partition < 64; partition++)
        {
            SharedProjectionSourceRosterPage page = (await actor.ReadPageAsync(scope, "capture-a", partition))
                .ShouldNotBeNull();
            page.Revision.ShouldBe(revision);
            captured.AddRange(page.Owners);
        }
        captured.Count.ShouldBe(10_000);
        captured.Distinct().Count().ShouldBe(10_000);
        (await actor.BeginWriteAsync(new AggregateIdentity(scope.TenantId, scope.Domain, "owner-new"),
            "blocked-during-capture", false)).ShouldBeFalse();
        for (int partition = 0; partition < 64; partition++)
        {
            SharedProjectionSourceRosterPage page = (await actor.ReadPageAsync(scope, "capture-a", partition))
                .ShouldNotBeNull();
            (await actor.WriteCapturedPageAsync(scope, "capture-a", partition,
                [.. page.Owners.Select(owner => new SharedProjectionCapturedSourceOwner(owner, 0, 0, false))]))
                .ShouldBeTrue();
        }
        (await actor.SealCaptureAsync(scope, "capture-a", "fingerprint-a",
            "shared-projection", "catalog-a")).ShouldBeTrue();
        (await actor.MarkOwnersAccumulatedAsync(scope, "capture-a")).ShouldBeFalse();
        (await actor.CompleteCaptureAsync(scope, "capture-a")).ShouldBeFalse();
        (await actor.ReleaseAppendFenceAsync(scope, "capture-a")).ShouldBeTrue();
        (await actor.BeginWriteAsync(new AggregateIdentity(scope.TenantId, scope.Domain, "owner-00000"),
            "append-after-begin", false)).ShouldBeTrue();
        (await actor.CompleteWriteAsync(new AggregateIdentity(scope.TenantId, scope.Domain, "owner-00000"),
            "append-after-begin")).ShouldBeTrue();
        (await actor.BeginWriteAsync(new AggregateIdentity(scope.TenantId, scope.Domain, "owner-00000"),
            "erasure-before-accumulation", true)).ShouldBeFalse();
        acceptedCount = 10_000;
        (await actor.MarkOwnersAccumulatedAsync(scope, "capture-a")).ShouldBeTrue();
        (await actor.CompleteCaptureAsync(scope, "capture-a")).ShouldBeFalse();
        phase = DomainSharedProjectionRebuildPhase.Committed;
        (await actor.CompleteCaptureAsync(scope, "capture-a")).ShouldBeTrue();
        (await actor.BeginWriteAsync(new AggregateIdentity(scope.TenantId, scope.Domain, "owner-new"),
            "blocked-at-capacity", false)).ShouldBeFalse();
    }

    [Fact]
    public async Task OutstandingWriteIntentPreventsCaptureUntilResolved()
    {
        var scope = new SharedProjectionSourceScope("tenant-a", "timesheets");
        var authority = Substitute.For<IInstallationCoverageAuthority>();
        authority.AuthorizeInstallationAsync(Arg.Any<SharedProjectionSourceInstallation>()).Returns(true);
        authority.AuthorizeWriteAsync(Arg.Any<SharedProjectionSourceScope>(), Arg.Any<AggregateIdentity>(),
            Arg.Any<string>()).Returns(true);
        authority.AuthorizeCaptureAsync(Arg.Any<SharedProjectionSourceScope>(), Arg.Any<string>()).Returns(true);
        var actor = new SharedProjectionSourceRosterActor(ActorHost.CreateForTest<SharedProjectionSourceRosterActor>(
            new ActorTestOptions { ActorId = new(scope.ActorId) }), authority);
        ActorStateManagerTestHelper.SetStateManager(actor, new InMemoryStateManager());
        (await actor.InstallAsync(new(scope, "certified-clean", "all-writers", true, []))).ShouldBeTrue();
        var owner = new AggregateIdentity(scope.TenantId, scope.Domain, "owner-a");
        (await actor.BeginWriteAsync(owner, "write-a", false)).ShouldBeTrue();
        var pendingOwners = new List<AggregateIdentity> { owner };
        for (int index = 0; index < 20; index++)
        {
            var pendingOwner = new AggregateIdentity(scope.TenantId, scope.Domain, $"pending-{index:D2}");
            (await actor.BeginWriteAsync(pendingOwner, $"pending-write-{index:D2}", false)).ShouldBeTrue();
            pendingOwners.Add(pendingOwner);
        }
        (await actor.BeginCaptureAsync(scope, "capture-a")).ShouldBeNull();
        (await actor.ReadCaptureProgressAsync(scope, "capture-a")).ShouldNotBeNull()
            .AppendFenceHeld.ShouldBeTrue();
        for (int index = 0; index < 20; index++)
        {
            (await actor.BeginWriteAsync(new AggregateIdentity(scope.TenantId, scope.Domain, $"late-{index:D2}"),
                $"late-write-{index:D2}", false)).ShouldBeFalse();
        }
        (await actor.CompleteWriteAsync(owner, "write-a")).ShouldBeTrue();
        for (int index = 0; index < 20; index++)
            (await actor.CompleteWriteAsync(pendingOwners[index + 1], $"pending-write-{index:D2}")).ShouldBeTrue();
        (await actor.BeginCaptureAsync(scope, "capture-a")).ShouldNotBeNull();
        (await actor.ReadCaptureProgressAsync(scope, "capture-a")).ShouldNotBeNull().OwnerCount.ShouldBe(21);
    }

    [Fact]
    public async Task ThousandAndFirstHashCollisionSpillsWithoutOmittingOrDuplicatingAnOwner()
    {
        var scope = new SharedProjectionSourceScope("tenant-a", "timesheets");
        var authority = Substitute.For<IInstallationCoverageAuthority>();
        authority.AuthorizeInstallationAsync(Arg.Any<SharedProjectionSourceInstallation>()).Returns(true);
        authority.AuthorizeWriteAsync(Arg.Any<SharedProjectionSourceScope>(), Arg.Any<AggregateIdentity>(),
            Arg.Any<string>()).Returns(true);
        authority.AuthorizeCaptureAsync(Arg.Any<SharedProjectionSourceScope>(), Arg.Any<string>()).Returns(true);
        var actor = new SharedProjectionSourceRosterActor(ActorHost.CreateForTest<SharedProjectionSourceRosterActor>(
            new ActorTestOptions { ActorId = new(scope.ActorId) }), authority);
        ActorStateManagerTestHelper.SetStateManager(actor, new InMemoryStateManager());
        var colliding = new List<AggregateIdentity>();
        for (int candidate = 0; colliding.Count < 1001; candidate++)
        {
            var owner = new AggregateIdentity(scope.TenantId, scope.Domain, $"collision-{candidate:D7}");
            if (SHA256.HashData(Encoding.UTF8.GetBytes(owner.ActorId))[0] % 64 == 0)
                colliding.Add(owner);
        }

        (await actor.InstallAsync(new(scope, "legacy-census", "writers", false, colliding.Take(1000).ToArray()))).ShouldBeTrue();
        (await actor.BeginWriteAsync(colliding[1000], "spill-write", false)).ShouldBeTrue();
        (await actor.CompleteWriteAsync(colliding[1000], "spill-write")).ShouldBeTrue();
        (await actor.BeginWriteAsync(colliding[1000], "repeat-write", false)).ShouldBeTrue();
        (await actor.CompleteWriteAsync(colliding[1000], "repeat-write")).ShouldBeTrue();
        (await actor.BeginCaptureAsync(scope, "capture-collision")).ShouldNotBeNull();
        SharedProjectionSourceCaptureProgress progress = (await actor.ReadCaptureProgressAsync(
            scope, "capture-collision")).ShouldNotBeNull();
        progress.PageCount.ShouldBe(65);
        var found = new List<AggregateIdentity>();
        for (int page = 0; page < progress.PageCount; page++)
        {
            found.AddRange((await actor.ReadPageAsync(scope, "capture-collision", page))
                .ShouldNotBeNull().Owners);
        }

        found.Count.ShouldBe(1001);
        found.Distinct().Count().ShouldBe(1001);
        found.ShouldContain(colliding[1000]);
    }

    [Fact]
    public async Task UnsealedCaptureCanAbortAfterRestartAndRemovePartialPages()
    {
        var scope = new SharedProjectionSourceScope("tenant-a", "timesheets");
        var authority = Substitute.For<IInstallationCoverageAuthority>();
        authority.AuthorizeInstallationAsync(Arg.Any<SharedProjectionSourceInstallation>()).Returns(true);
        authority.AuthorizeWriteAsync(Arg.Any<SharedProjectionSourceScope>(), Arg.Any<AggregateIdentity>(),
            Arg.Any<string>()).Returns(true);
        authority.AuthorizeCaptureAsync(Arg.Any<SharedProjectionSourceScope>(), Arg.Any<string>()).Returns(true);
        var backend = new InMemoryStateManager();
        SharedProjectionSourceRosterActor CreateActor()
        {
            var actor = new SharedProjectionSourceRosterActor(
                ActorHost.CreateForTest<SharedProjectionSourceRosterActor>(new ActorTestOptions
                    { ActorId = new(scope.ActorId) }), authority);
            ActorStateManagerTestHelper.SetStateManager(actor, backend);
            return actor;
        }

        SharedProjectionSourceRosterActor first = CreateActor();
        (await first.InstallAsync(new(scope, "clean", "writers", true, []))).ShouldBeTrue();
        var owner = new AggregateIdentity(scope.TenantId, scope.Domain, "owner-before-first-event");
        (await first.BeginWriteAsync(owner, "failed-first-event", false)).ShouldBeTrue();
        (await first.BeginCaptureAsync(scope, "cut-a")).ShouldBeNull();
        (await first.ReadPendingWritesAsync(scope, "cut-a")).ShouldHaveSingleItem()
            .Identity.ShouldBe(owner);
        (await first.CompleteWriteAsync(owner, "failed-first-event")).ShouldBeTrue();
        (await first.BeginCaptureAsync(scope, "cut-a")).ShouldNotBeNull();
        for (int partition = 0; partition < 64; partition++)
        {
            SharedProjectionSourceRosterPage page = (await first.ReadPageAsync(scope, "cut-a", partition))
                .ShouldNotBeNull();
            if (page.Owners.Count != 0)
            {
                (await first.WriteCapturedPageAsync(scope, "cut-a", partition,
                    [new SharedProjectionCapturedSourceOwner(owner, 0, 0, false)])).ShouldBeTrue();
                break;
            }
        }

        SharedProjectionSourceRosterActor restarted = CreateActor();
        (await restarted.ReadCaptureProgressAsync(scope, "cut-a")).ShouldNotBeNull()
            .AppendFenceHeld.ShouldBeTrue();
        (await restarted.AbortUnsealedCaptureAsync(scope, "cut-a")).ShouldBeTrue();
        (await restarted.BeginCaptureAsync(scope, "cut-b")).ShouldNotBeNull();
        backend.CommittedState.Keys.Any(key => key.Contains("shared-source-capture-page-", StringComparison.Ordinal))
            .ShouldBeFalse();
    }

    [Fact]
    public async Task SealedRejectedBeginReleasesFenceOnlyAfterSdkAbortReceipt()
    {
        var scope = new SharedProjectionSourceScope("tenant-a", "timesheets");
        var authority = Substitute.For<IInstallationCoverageAuthority>();
        authority.AuthorizeInstallationAsync(Arg.Any<SharedProjectionSourceInstallation>()).Returns(true);
        authority.AuthorizeCaptureAsync(Arg.Any<SharedProjectionSourceScope>(), Arg.Any<string>()).Returns(true);
        var backend = new InMemoryStateManager();
        bool abortProven = false;
        ISharedProjectionRebuildGateway gateway = Substitute.For<ISharedProjectionRebuildGateway>();
        gateway.SendAsync(Arg.Any<DomainSharedProjectionRebuildRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                DomainSharedProjectionRebuildRequest request = call.ArgAt<DomainSharedProjectionRebuildRequest>(0);
                return Task.FromResult(request.Action == DomainSharedProjectionRebuildAction.Inspect
                    ? new DomainSharedProjectionRebuildResponse(DomainSharedProjectionRebuildProtocol.Version,
                        DomainSharedProjectionRebuildPhase.Accumulating, ProjectionDispatchStatus.Retryable,
                        0, DomainSharedProjectionRebuildFingerprint.EmptyInventory, null,
                        ProjectionDispatchReasonCodes.DeliveryGap)
                    : new DomainSharedProjectionRebuildResponse(DomainSharedProjectionRebuildProtocol.Version,
                        DomainSharedProjectionRebuildPhase.Aborted,
                        abortProven ? ProjectionDispatchStatus.Completed : ProjectionDispatchStatus.Retryable,
                        0, DomainSharedProjectionRebuildFingerprint.EmptyInventory, null, null));
            });
        SharedProjectionSourceRosterActor CreateActor()
        {
            var actor = new SharedProjectionSourceRosterActor(
                ActorHost.CreateForTest<SharedProjectionSourceRosterActor>(new ActorTestOptions
                    { ActorId = new(scope.ActorId) }), authority, gateway);
            ActorStateManagerTestHelper.SetStateManager(actor, backend);
            return actor;
        }

        SharedProjectionSourceRosterActor first = CreateActor();
        (await first.InstallAsync(new(scope, "clean", "writers", true, []))).ShouldBeTrue();
        (await first.BeginCaptureAsync(scope, "rejected-a")).ShouldNotBeNull();
        for (int page = 0; page < 64; page++)
            (await first.WriteCapturedPageAsync(scope, "rejected-a", page, [])).ShouldBeTrue();
        (await first.SealCaptureAsync(scope, "rejected-a", "fingerprint-a", "capability", "catalog-a"))
            .ShouldBeTrue();
        SharedProjectionSourceRosterActor restarted = CreateActor();
        (await restarted.AbortRejectedCaptureAsync(scope, "rejected-a")).ShouldBeFalse();
        (await restarted.ReadCaptureProgressAsync(scope, "rejected-a")).ShouldNotBeNull()
            .AppendFenceHeld.ShouldBeTrue();
        abortProven = true;
        (await restarted.AbortRejectedCaptureAsync(scope, "rejected-a")).ShouldBeTrue();
        (await restarted.BeginCaptureAsync(scope, "rejected-a")).ShouldBeNull();
        (await restarted.BeginCaptureAsync(scope, "next-a")).ShouldNotBeNull();
        backend.CommittedState.Keys.Any(key => key.Contains("shared-source-capture-page-", StringComparison.Ordinal))
            .ShouldBeFalse();
    }

    [Fact]
    public async Task OmittedCaptureOwnerFailsAndRestartPreservesAppendAndErasureHolds()
    {
        var scope = new SharedProjectionSourceScope("tenant-a", "timesheets");
        var authority = Substitute.For<IInstallationCoverageAuthority>();
        authority.AuthorizeInstallationAsync(Arg.Any<SharedProjectionSourceInstallation>()).Returns(true);
        authority.AuthorizeWriteAsync(Arg.Any<SharedProjectionSourceScope>(), Arg.Any<AggregateIdentity>(),
            Arg.Any<string>()).Returns(true);
        authority.AuthorizeCaptureAsync(Arg.Any<SharedProjectionSourceScope>(), Arg.Any<string>()).Returns(true);
        var backend = new InMemoryStateManager();
        long acceptedCount = 0;
        DomainSharedProjectionRebuildPhase phase = DomainSharedProjectionRebuildPhase.Accumulating;
        ISharedProjectionRebuildGateway gateway = ReceiptGateway(() => (phase, acceptedCount));
        SharedProjectionSourceRosterActor CreateActor()
        {
            var instance = new SharedProjectionSourceRosterActor(
                ActorHost.CreateForTest<SharedProjectionSourceRosterActor>(new ActorTestOptions
                    { ActorId = new(scope.ActorId) }), authority, gateway);
            ActorStateManagerTestHelper.SetStateManager(instance, backend);
            return instance;
        }
        SharedProjectionSourceRosterActor actor = CreateActor();
        (await actor.InstallAsync(new(scope, "clean", "writers", true, []))).ShouldBeTrue();
        var owner = new AggregateIdentity(scope.TenantId, scope.Domain, "owner-a");
        (await actor.BeginWriteAsync(owner, "write-a", false)).ShouldBeTrue();
        (await actor.CompleteWriteAsync(owner, "write-a")).ShouldBeTrue();
        long revision = (await actor.BeginCaptureAsync(scope, "capture-a")).ShouldNotBeNull();
        actor = CreateActor();
        (await actor.BeginCaptureAsync(scope, "capture-a")).ShouldBe(revision);
        (await actor.BeginWriteAsync(owner, "blocked-a", false)).ShouldBeFalse();
        for (int partition = 0; partition < 64; partition++)
        {
            SharedProjectionSourceRosterPage page = (await actor.ReadPageAsync(scope, "capture-a", partition))
                .ShouldNotBeNull();
            if (page.Owners.Count != 0)
                (await actor.WriteCapturedPageAsync(scope, "capture-a", partition, [])).ShouldBeFalse();
            (await actor.WriteCapturedPageAsync(scope, "capture-a", partition,
                [.. page.Owners.Select(item => new SharedProjectionCapturedSourceOwner(item, 0, 0, false))]))
                .ShouldBeTrue();
        }
        (await actor.SealCaptureAsync(scope, "capture-a", "fingerprint-a",
            "shared-projection", "catalog-a")).ShouldBeTrue();
        actor = CreateActor();
        (await actor.ReadCaptureProgressAsync(scope, "capture-a")).ShouldNotBeNull()
            .AppendFenceHeld.ShouldBeTrue();
        (await actor.BeginWriteAsync(owner, "erasure-before-begin", true)).ShouldBeFalse();
        (await actor.ReleaseAppendFenceAsync(scope, "capture-a")).ShouldBeTrue();
        actor = CreateActor();
        (await actor.BeginWriteAsync(owner, "append-after-begin", false)).ShouldBeTrue();
        (await actor.CompleteWriteAsync(owner, "append-after-begin")).ShouldBeTrue();
        var newOwner = new AggregateIdentity(scope.TenantId, scope.Domain, "owner-b");
        (await actor.BeginWriteAsync(newOwner, "new-owner-after-begin", false)).ShouldBeTrue();
        (await actor.CompleteWriteAsync(newOwner, "new-owner-after-begin")).ShouldBeTrue();
        (await actor.ReadCaptureProgressAsync(scope, "capture-a")).ShouldNotBeNull().OwnerCount.ShouldBe(1);
        (await actor.BeginWriteAsync(owner, "erasure-before-accumulation", true)).ShouldBeFalse();
        (await actor.ReadCapturedPageAsync(scope, "capture-a", 0)).ShouldNotBeNull();
        acceptedCount = 1;
        (await actor.MarkOwnersAccumulatedAsync(scope, "capture-a")).ShouldBeTrue();
        (await actor.BeginWriteAsync(owner, "erasure-after-accumulation", true)).ShouldBeTrue();
        (await actor.CompleteWriteAsync(owner, "erasure-after-accumulation")).ShouldBeTrue();
        phase = DomainSharedProjectionRebuildPhase.Committed;
        (await actor.CompleteCaptureAsync(scope, "capture-a")).ShouldBeTrue();
    }

    private static ISharedProjectionRebuildGateway ReceiptGateway(
        Func<(DomainSharedProjectionRebuildPhase Phase, long Count)> state)
    {
        ISharedProjectionRebuildGateway gateway = Substitute.For<ISharedProjectionRebuildGateway>();
        gateway.SendAsync(Arg.Any<DomainSharedProjectionRebuildRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                DomainSharedProjectionRebuildRequest request = call.ArgAt<DomainSharedProjectionRebuildRequest>(0);
                (DomainSharedProjectionRebuildPhase phase, long count) = state();
                return Task.FromResult(new DomainSharedProjectionRebuildResponse(
                    DomainSharedProjectionRebuildProtocol.Version, phase,
                    ProjectionDispatchStatus.Completed, count,
                    DomainSharedProjectionRebuildFingerprint.EmptyInventory, null, null));
            });
        return gateway;
    }
}
