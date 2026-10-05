using Hexalith.EventStore.Client.Reminders;
using Hexalith.EventStore.Contracts.Reminders;
using Hexalith.EventStore.DomainService.Tests.Fixtures;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Runtime diagnostics retain bounded reasons and exception types without exposing exception content.</summary>
public sealed class ReminderDiagnosticsTests
{
    private static readonly ReminderTarget Item = ReminderTestHarness.Target("item-1");

    /// <summary>Malformed candidate identifiers never enter mismatch or tuple-validation exception diagnostics.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MalformedCandidateIdentifierIsNotLogged(bool invalidTuple)
    {
        const string secret = "secret-tenant:secret-domain:secret-aggregate:secret-token";
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        await harness.CreateIndex().EnsureCandidateAsync(Item, actorId, CancellationToken.None);
        string key = ReminderStateKeys.TenantCandidates(harness.Options.ActorTypeName, Item.Tenant);
        var document = new ReminderTenantCandidates(Item.Tenant,
            [new ReminderCandidate(invalidTuple ? string.Empty : Item.Domain, Item.Aggregate, secret)]);
        harness.Store.SeedRaw(harness.Options.StateStoreName, key, document);
        var before = await harness.Store.GetAsync<ReminderTenantCandidates>(harness.Options.StateStoreName, key);
        var logger = new ReminderDiagnosticLogger<ReminderReconciler>();
        using var reconciler = new ReminderReconciler(harness.CreateIndex(), harness.CreateRegistrar(), harness.Status,
            Options.Create(harness.Options), harness.Time, logger);

        (await reconciler.RunPassAsync(CancellationToken.None)).Incomplete.ShouldBe(1);

        var entry = logger.Entries.Single(static entry => entry.EventId.Id == 200211);
        entry.Fields["ActorId"].ShouldBe(invalidTuple ? "candidate-invalid" : actorId);
        entry.Fields["ReasonCode"].ShouldBe(invalidTuple ? string.Empty : "actor-id-mismatch");
        entry.Fields["ExceptionType"].ShouldBe(invalidTuple ? nameof(ArgumentException) : string.Empty);
        logger.Entries.Select(static entry => entry.Message)
            .Concat(logger.Entries.SelectMany(static entry => entry.Fields.Values).Select(static value => value?.ToString() ?? string.Empty))
            .ShouldAllBe(value => !value.Contains(secret, StringComparison.Ordinal));
        var after = await harness.Store.GetAsync<ReminderTenantCandidates>(harness.Options.StateStoreName, key);
        ReminderTenantCandidates retained = after.Value.ShouldNotBeNull();
        retained.Tenant.ShouldBe(document.Tenant);
        retained.Candidates.ShouldHaveSingleItem().ShouldBe(document.Candidates.Single());
        after.ETag.ShouldBe(before.ETag);
        harness.Status.Snapshot().IncompleteScans.ShouldBe(1);
    }

    /// <summary>Unknown stored quarantine reasons are retained by digest and repaired before diagnostic emission.</summary>
    [Fact]
    public async Task RestoredSecretQuarantineReasonIsRepairedBeforeLogging()
    {
        const string secret = "secret payload and delegation token";
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        string originalDigest = actorId[4..];
        harness.SeedItemState(actorId, new ReminderItemState(Item.Tenant, Item.Domain, Item.Aggregate, 1, [],
            [new ReminderQuarantineRecord(originalDigest, secret, null, harness.Time.Now)]));
        var logger = new ReminderDiagnosticLogger<ReminderCoordinator>();
        var coordinator = new ReminderCoordinator(harness.Source, harness.CreateIndex(), harness.CoordinatorStore,
            harness.CoordinatorStore, Options.Create(harness.Options), harness.Status, harness.Time, logger,
            harness.Submitter, harness.Tokens);

        (await coordinator.ConvergeAsync(actorId, Item, harness.SchedulerFor(actorId), CancellationToken.None)).Quarantined.ShouldBe(1);

        ReminderQuarantineRecord repaired = harness.ItemState(actorId).ShouldNotBeNull().Quarantine.ShouldHaveSingleItem();
        repaired.ReasonCode.ShouldBe("stored-quarantine-invalid");
        repaired.EvidenceDigest.Length.ShouldBe(52);
        repaired.EvidenceDigest.ShouldNotBe(originalDigest);
        harness.Disposition(actorId, repaired.EvidenceDigest).ShouldNotBeNull().ReasonCode.ShouldBe("stored-quarantine-invalid");
        harness.Candidates().ShouldHaveSingleItem();
        harness.Submitter.Calls.ShouldBeEmpty();
        logger.Entries.ShouldContain(entry => entry.EventId.Id == 200207
            && Equals(entry.Fields["ReasonCode"], "stored-quarantine-invalid"));
        logger.Entries.Select(static entry => entry.Message)
            .Concat(logger.Entries.SelectMany(static entry => entry.Fields.Values).Select(static value => value?.ToString() ?? string.Empty))
            .ShouldAllBe(value => !value.Contains(secret, StringComparison.Ordinal));
    }

    /// <summary>A quarantined intent emits event 200207 with only its actor, evidence subject, and bounded reason.</summary>
    [Fact]
    public async Task QuarantineEmitsStructuredEvent()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        harness.Source.Set(Item, ReminderTestHarness.Intent(Item, harness.Time.Now) with { Payload = null! });
        var logger = new ReminderDiagnosticLogger<ReminderCoordinator>();
        var coordinator = new ReminderCoordinator(harness.Source, harness.CreateIndex(), harness.CoordinatorStore,
            harness.CoordinatorStore, Options.Create(harness.Options), harness.Status, harness.Time, logger,
            harness.Submitter, harness.Tokens);

        (await coordinator.ConvergeAsync(actorId, Item, harness.SchedulerFor(actorId), CancellationToken.None)).Quarantined.ShouldBe(1);

        ReminderQuarantineRecord record = harness.ItemState(actorId).ShouldNotBeNull().Quarantine.ShouldHaveSingleItem();
        var entry = logger.Entries.Single(static entry => entry.EventId.Id == 200207);
        entry.Level.ShouldBe(LogLevel.Error);
        entry.Fields.Keys.Order().ShouldBe(new[] { "{OriginalFormat}", "ActorId", "Subject", "ReasonCode" }.Order());
        entry.Fields["ActorId"].ShouldBe(actorId);
        entry.Fields["Subject"].ShouldBe(record.EvidenceDigest);
        entry.Fields["ReasonCode"].ShouldBe("payload-invalid");
        entry.Exception.ShouldBeNull();
        entry.Message.ShouldBe($"Reminder evidence quarantined: ActorId={actorId}, Subject={record.EvidenceDigest}, ReasonCode=payload-invalid");
        entry.Message.ShouldNotContain(Item.Tenant);
        entry.Message.ShouldNotContain(Item.Aggregate);
    }

    /// <summary>An unreadable registry emits its fail-closed reason separately from the exception type.</summary>
    [Fact]
    public async Task InvalidRegistryEmitsStructuredScanFailure()
    {
        var harness = new ReminderTestHarness();
        harness.Store.SeedRaw(harness.Options.StateStoreName,
            ReminderStateKeys.TenantRegistry(harness.Options.ActorTypeName),
            new ReminderTenantRegistry(null!));
        var logger = new ReminderDiagnosticLogger<ReminderReconciler>();
        using var reconciler = new ReminderReconciler(harness.CreateIndex(), harness.CreateRegistrar(), harness.Status,
            Options.Create(harness.Options), harness.Time, logger);

        ReminderReconciliationPass pass = await reconciler.RunPassAsync(CancellationToken.None);

        pass.Incomplete.ShouldBe(1);
        var entry = logger.Entries.ShouldHaveSingleItem();
        entry.EventId.Id.ShouldBe(200212);
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Fields.Keys.Order().ShouldBe(new[] { "{OriginalFormat}", "ExceptionType", "ReasonCode", "Scope" }.Order());
        entry.Fields["Scope"].ShouldBe("tenant-registry");
        entry.Fields["ReasonCode"].ShouldBe("index-registry-invalid");
        entry.Fields["ExceptionType"].ShouldBe(nameof(ReminderFailClosedException));
        entry.Exception.ShouldBeNull();
        entry.Message.ShouldBe("Reminder reconciliation scan is incomplete: Scope=tenant-registry, ReasonCode=index-registry-invalid, ExceptionType=ReminderFailClosedException");
    }

    /// <summary>Candidate failures emit a bounded reason only for fail-closed exceptions and never their messages.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FailedCandidateEmitsStructuredReasonAndType(bool failClosed)
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        await harness.CreateIndex().EnsureCandidateAsync(Item, actorId, CancellationToken.None);
        IReminderRegistrar registrar = Substitute.For<IReminderRegistrar>();
        Exception failure = failClosed
            ? new ReminderFailClosedException("state-conflict")
            : new InvalidOperationException("secret payload and token");
        registrar.ConvergeAsync(Item, Arg.Any<CancellationToken>()).ThrowsAsync(failure);
        var logger = new ReminderDiagnosticLogger<ReminderReconciler>();
        using var reconciler = new ReminderReconciler(harness.CreateIndex(), registrar, harness.Status,
            Options.Create(harness.Options), harness.Time, logger);

        ReminderReconciliationPass pass = await reconciler.RunPassAsync(CancellationToken.None);

        pass.Incomplete.ShouldBe(1);
        var entry = logger.Entries.Single(static entry => entry.EventId.Id == 200211);
        string reason = failClosed ? "state-conflict" : string.Empty;
        string exceptionType = failure.GetType().Name;
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Fields.Keys.Order().ShouldBe(new[] { "{OriginalFormat}", "ActorId", "ExceptionType", "ReasonCode" }.Order());
        entry.Fields["ActorId"].ShouldBe(actorId);
        entry.Fields["ReasonCode"].ShouldBe(reason);
        entry.Fields["ExceptionType"].ShouldBe(exceptionType);
        entry.Exception.ShouldBeNull();
        entry.Message.ShouldBe($"Reminder reconciliation could not converge a candidate: ActorId={actorId}, ReasonCode={reason}, ExceptionType={exceptionType}");
        entry.Message.ShouldNotContain("secret payload and token");
    }

    /// <summary>A cleanup fold failure after witness release retains discovery and emits bounded failure metadata.</summary>
    [Fact]
    public async Task FailedCleanupEmitsStructuredReasonAndType()
    {
        var harness = new ReminderTestHarness();
        string actorId = ReminderTestHarness.ActorId(Item);
        harness.Source.Set(Item, ReminderTestHarness.Intent(Item, harness.Time.Now));
        harness.Source.OnRead = reads =>
        {
            if (reads == 2)
            {
                harness.Source.Failing.Add(Item);
            }
        };
        var logger = new ReminderDiagnosticLogger<ReminderCoordinator>();
        var coordinator = new ReminderCoordinator(harness.Source, harness.CreateIndex(), harness.CoordinatorStore,
            harness.CoordinatorStore, Options.Create(harness.Options), harness.Status, harness.Time, logger,
            harness.Submitter, harness.Tokens);

        ReminderConvergenceResult result = await coordinator.ConvergeAsync(actorId, Item, harness.SchedulerFor(actorId), CancellationToken.None);

        result.Submitted.ShouldBe(1);
        result.Unresolved.ShouldBe(1);
        harness.ItemState(actorId).ShouldBeNull();
        harness.Candidates().ShouldHaveSingleItem().ActorId.ShouldBe(actorId);
        harness.Submitter.Receipts.ShouldHaveSingleItem();
        var entry = logger.Entries.Single(static entry => entry.EventId.Id == 200214);
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Fields.Keys.Order().ShouldBe(new[] { "{OriginalFormat}", "ActorId", "ExceptionType", "ReasonCode" }.Order());
        entry.Fields["ActorId"].ShouldBe(actorId);
        entry.Fields["ReasonCode"].ShouldBe("source-unavailable");
        entry.Fields["ExceptionType"].ShouldBe(nameof(InvalidOperationException));
        entry.Exception.ShouldBeNull();
        entry.Message.ShouldBe($"Reminder state change failed closed: ActorId={actorId}, ReasonCode=source-unavailable, ExceptionType=InvalidOperationException");
        entry.Message.ShouldNotContain("Synthetic stream read failure.");
        logger.Entries.ShouldNotContain(static entry => entry.EventId.Id == 200216);
    }
}
