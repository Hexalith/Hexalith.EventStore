using System.Collections.Concurrent;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.DomainService.DurableTasks;
using Hexalith.EventStore.Testing.Fakes;

using NSubstitute;
using NSubstitute.Core;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests.DurableTasks;

/// <summary>Checks admission and confirmation against the conditional store contract.</summary>
public sealed class DurableTaskAdmissionTests
{
    private const string StoreName = "tasks-fixture";
    private static readonly string RequestHash = new('a', 64);
    private static readonly string ChangedHash = new('b', 64);
    private static readonly string ConfirmationBinding = new('c', 64);
    private static readonly DurableTaskScope Scope = new("tenant", "original-actor", "sample.provision@1", "original-key");

    /// <summary>Two replicas racing a task-only first admission return the same committed task.</summary>
    [Fact]
    public Task CompetingTaskOnlyAdmissionsConverge()
    {
        var store = new InMemoryReadModelStore();
        return VerifyCompetingAdmissionsAsync(store, store, StoreName);
    }

    /// <summary>Tests the task-only CAS and authoritative lookup through two independently connected stores.</summary>
    internal static async Task VerifyCompetingAdmissionsAsync(IReadModelStore first, IReadModelStore second, string component)
    {
        IDurableTaskAdmissionAuthorizer authority = Authorizer(new(true, false, null));
        DurableTaskAdmission[] replicas = [new(first, component, authority, TimeProvider.System), new(second, component, authority, TimeProvider.System)];

        DurableTaskAdmissionResult[] results = await Task.WhenAll(replicas.Select(replica => replica.AdmitAsync(Scope, RequestHash)));

        results[0].Outcome.ShouldBe(DurableTaskAdmissionOutcome.Admitted);
        results[1].ShouldBe(results[0]);
        (await replicas[1].GetCommittedAsync(Scope, RequestHash)).ShouldBe(results[0].Task);
        (await replicas[1].AdmitAsync(Scope, ChangedHash)).Outcome.ShouldBe(DurableTaskAdmissionOutcome.Conflict);
    }

    /// <summary>A failure before commit leaves the artifact usable and a lost committed response is replayable.</summary>
    /// <param name="commitBeforeLoss">Whether the conditional write commits before its response is lost.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ConfirmationAndAdmissionShareOneWrite(bool commitBeforeLoss)
    {
        var retained = new InMemoryReadModelStore();
        return VerifyConfirmationCommitAsync(retained, retained, StoreName, commitBeforeLoss);
    }

    /// <summary>Exercises the same interruption cases through independently connected replicas.</summary>
    internal static async Task VerifyConfirmationCommitAsync(
        IReadModelStore retained, IReadModelStore replica, string component, bool commitBeforeLoss)
    {
        var scope = Scope with { IdempotencyKey = $"confirmation-fixture-{commitBeforeLoss}" };
        IDurableTaskAdmissionAuthorizer authority = Authorizer(new(true, true, ConfirmationBinding));
        var initial = new DurableTaskAdmission(retained, component, authority, TimeProvider.System);
        string token = (await initial.IssueConfirmationAsync(scope, RequestHash)).ShouldNotBeNull();
        var stateKeys = new ConcurrentQueue<string>();
        IReadModelStore fault = Substitute.For<IReadModelStore>();
        fault.GetAsync<DurableTaskAdmissionState>(component, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => retained.GetAsync<DurableTaskAdmissionState>(component, call.ArgAt<string>(1), call.Arg<CancellationToken>()));
        fault.TrySaveAsync(component, Arg.Any<string>(), Arg.Any<DurableTaskAdmissionState>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(WriteThenLoseAsync);

        var failing = new DurableTaskAdmission(fault, component, authority, TimeProvider.System);
        await Should.ThrowAsync<IOException>(() => failing.AdmitAsync(scope, RequestHash, token));
        string key = stateKeys.ShouldHaveSingleItem();
        DurableTaskAdmissionState observed = (await replica.GetAsync<DurableTaskAdmissionState>(component, key)).Value.ShouldNotBeNull();
        (observed.Task is not null).ShouldBe(commitBeforeLoss);
        observed.Confirmation.ShouldNotBeNull().ConsumedBy.ShouldBe(observed.Task?.TaskId);
        observed.Confirmation!.TokenSha256.ShouldNotBe(token);

        var restarted = new DurableTaskAdmission(replica, component, authority, TimeProvider.System);
        if (commitBeforeLoss)
        {
            // An authorized retry can recover its task without refreshing already-spent evidence.
            SetDecision(authority, new(true, true, null));
        }

        DurableTaskAdmissionResult retry = await restarted.AdmitAsync(scope, RequestHash, token);
        retry.Outcome.ShouldBe(DurableTaskAdmissionOutcome.Admitted);
        CommittedDurableTaskAdmission result = retry.Task.ShouldNotBeNull();
        if (commitBeforeLoss)
        {
            result.ShouldBe(observed.Task);
        }

        DurableTaskAdmissionState final = (await retained.GetAsync<DurableTaskAdmissionState>(component, key)).Value.ShouldNotBeNull();
        final.Task.ShouldBe(result);
        final.Confirmation.ShouldNotBeNull().ConsumedBy.ShouldBe(result.TaskId);
        SetDecision(authority, new(false, true, null));
        (await restarted.AdmitAsync(scope, RequestHash, token)).ShouldBe(new(DurableTaskAdmissionOutcome.Denied));
        (await restarted.GetCommittedAsync(scope, RequestHash)).ShouldBeNull();

        async Task<bool> WriteThenLoseAsync(CallInfo call)
        {
            string writtenKey = call.ArgAt<string>(1);
            stateKeys.Enqueue(writtenKey);
            if (commitBeforeLoss)
            {
                bool committed = await retained.TrySaveAsync(component, writtenKey, call.Arg<DurableTaskAdmissionState>(), call.ArgAt<string>(3), call.Arg<CancellationToken>());
                committed.ShouldBeTrue();
            }

            throw new IOException("Simulated lost state-store response.");
        }
    }

    /// <summary>Only the supplied scope and freshly authorized intent can consume the artifact.</summary>
    [Fact]
    public async Task ChangedBindingDoesNotConsumeConfirmation()
    {
        var store = new InMemoryReadModelStore();
        IDurableTaskAdmissionAuthorizer authority = Authorizer(new(true, true, ConfirmationBinding));
        var admission = new DurableTaskAdmission(store, StoreName, authority, TimeProvider.System);
        string token = (await admission.IssueConfirmationAsync(Scope, RequestHash)).ShouldNotBeNull();
        SetDecision(authority, new(true, true, ChangedHash));
        (await admission.AdmitAsync(Scope, RequestHash, token)).Outcome.ShouldBe(DurableTaskAdmissionOutcome.RenewConfirmation);
        (await admission.GetCommittedAsync(Scope, RequestHash)).ShouldBeNull();

        SetDecision(authority, new(true, true, ConfirmationBinding));
        (await admission.AdmitAsync(Scope with { ActorId = "other-actor" }, RequestHash, token)).Outcome.ShouldBe(DurableTaskAdmissionOutcome.RenewConfirmation);
        (await admission.AdmitAsync(Scope, ChangedHash, token)).Outcome.ShouldBe(DurableTaskAdmissionOutcome.RenewConfirmation);
        (await admission.AdmitAsync(Scope, RequestHash, token)).Outcome.ShouldBe(DurableTaskAdmissionOutcome.Admitted);
    }

    private static IDurableTaskAdmissionAuthorizer Authorizer(DurableTaskAdmissionAuthority decision)
    {
        IDurableTaskAdmissionAuthorizer authorizer = Substitute.For<IDurableTaskAdmissionAuthorizer>();
        SetDecision(authorizer, decision);
        return authorizer;
    }

    private static void SetDecision(IDurableTaskAdmissionAuthorizer authorizer, DurableTaskAdmissionAuthority decision)
        => authorizer.AuthorizeAsync(Arg.Any<DurableTaskScope>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(decision);
}
