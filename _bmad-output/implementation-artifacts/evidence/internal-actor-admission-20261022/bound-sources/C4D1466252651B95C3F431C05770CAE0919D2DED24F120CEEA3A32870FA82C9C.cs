using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Reminders;
using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Reminders;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>
/// Story 4.11 R6 proof against Redis, the Dapr sidecar, placement, and the Scheduler: a registration persists
/// before arming, a directly invoked callback submits one Story 4.13 target receipt, a restart with Redis kept
/// replays that receipt under the same effect identity, and the reconciler re-arms a deleted Scheduler reminder.
/// All data is synthetic, as AD-28 requires before owner approval and a restore drill.
/// </summary>
[Collection("DaprTestContainer")]
[Trait("Category", "LiveSidecar")]
public sealed class ReminderRecoveryLiveSidecarTests(DaprTestContainerFixture fixture)
{
    private const string Tenant = "tenant-a";
    private const string Domain = "counter";
    private static readonly TimeSpan PlacementBudget = TimeSpan.FromSeconds(60);

    /// <summary>Registration, callback, restart replay, and Scheduler re-arm each leave the expected persisted end state.</summary>
    [Fact]
    public async Task RegistrationCallbackRestartReplayAndRearmPersistInRedis()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        fixture.ThrowIfHostStopped();
        fixture.SetupCounterDomain();
        string item = "reminder-" + Guid.NewGuid().ToString("N");
        var target = new ReminderTarget(Tenant, Domain, item);
        string actorId = ReminderIdentityCodec.ComputeActorId(Tenant, item);
        ReminderIntent intent = Intent(item, DateTimeOffset.UtcNow.AddHours(1), revision: 1, sequence: 1);
        string name = ReminderIdentityCodec.ComputeReminderName(intent);
        fixture.ReminderIntents.Set(target, intent);

        // 1. Registration persists the witness and the index, then arms the Scheduler reminder.
        DateTimeOffset registrationStarted = DateTimeOffset.UtcNow;
        ReminderConvergenceResult registered = await ConvergeAsync(target, cancellationToken);
        DateTimeOffset registrationCompleted = DateTimeOffset.UtcNow;

        registered.Armed.ShouldBe(1);
        registered.Unresolved.ShouldBe(0);
        ReminderItemState state = (await ReadAsync<ReminderItemState>(ItemKey(actorId), cancellationToken)).ShouldNotBeNull();
        ReminderEntry witness = state.Entries.ShouldHaveSingleItem();
        witness.ReminderName.ShouldBe(name);
        witness.Status.ShouldBe(ReminderEntryStatus.Armed);
        (await fixture.GetGenericStateJsonAsync(ItemKey(actorId))).ShouldContain(name);
        (await ReadAsync<ReminderTenantCandidates>(CandidatesKey(), cancellationToken)).ShouldNotBeNull()
            .Candidates.ShouldContain(candidate => candidate.ActorId == actorId);
        await AssertSchedulerTimingAsync(actorId, name, intent.DueUtc, registrationStarted, registrationCompleted, cancellationToken);

        // 2. A callback without the app-channel token, or with a forged one, is refused and mutates nothing.
        (await InvokeCallbackAsync(actorId, name, token: null, cancellationToken)).ShouldBe(HttpStatusCode.Unauthorized);
        (await InvokeCallbackAsync(actorId, name, "forged-token", cancellationToken)).ShouldBe(HttpStatusCode.Unauthorized);
        (await ReadAsync<ReminderItemState>(ItemKey(actorId), cancellationToken)).ShouldNotBeNull().Version.ShouldBe(state.Version);

        // 3. The callback, invoked directly rather than by waiting for the timer, persists one target receipt.
        (await InvokeCallbackAsync(actorId, name, fixture.AppApiToken, cancellationToken)).ShouldBe(HttpStatusCode.OK);

        string expectedEffectId = EffectIdentityCodec.ComputeEffectId(
            new EffectIdentity(Tenant, Domain, item, 1, EffectKindCatalog.DateResume, Domain, item, 0));
        ReminderDispositionRecord first = (await ReadAsync<ReminderDispositionRecord>(DispositionKey(actorId, name), cancellationToken))
            .ShouldNotBeNull();
        first.Disposition.ShouldBe(ReminderDisposition.Submitted);
        first.EffectId.ShouldBe(expectedEffectId);
        first.TargetDisposition.ShouldBe(TrustedEffectDisposition.Success);
        first.Replayed.ShouldBeFalse();
        string targetActorId = new AggregateIdentity(Tenant, Domain, item).ActorId;
        (await fixture.GetActorStateJsonAsync(fixture.AggregateActorTypeName, targetActorId, "effect_receipt_" + expectedEffectId))
            .ShouldContain(expectedEffectId);
        (await ReadAsync<ReminderItemState>(ItemKey(actorId), cancellationToken)).ShouldBeNull();

        // 4. Restart the host and sidecar with Redis kept: the still-current witness replays the same receipt.
        await fixture.RestartHostAndSidecarAsync();
        fixture.SetupCounterDomain();
        (await ConvergeAsync(target, cancellationToken)).Armed.ShouldBe(1);
        (await InvokeCallbackAsync(actorId, name, fixture.AppApiToken, cancellationToken)).ShouldBe(HttpStatusCode.OK);

        ReminderDispositionRecord replay = (await ReadAsync<ReminderDispositionRecord>(DispositionKey(actorId, name), cancellationToken))
            .ShouldNotBeNull();
        replay.Disposition.ShouldBe(ReminderDisposition.Submitted);
        replay.EffectId.ShouldBe(expectedEffectId);
        replay.Replayed.ShouldBeTrue();
        (await ReadAsync<ReminderItemState>(ItemKey(actorId), cancellationToken)).ShouldBeNull();

        // 5. A new schedule witness for the same logical effect replays the real target receipt.
        ReminderIntent sameSource = intent with { DueUtc = DateTimeOffset.UtcNow.AddHours(2), ScheduleRevision = 2 };
        string sameSourceName = ReminderIdentityCodec.ComputeReminderName(sameSource);
        sameSourceName.ShouldNotBe(name);
        fixture.ReminderIntents.Set(target, sameSource);
        (await ConvergeAsync(target, cancellationToken)).Armed.ShouldBe(1);
        (await InvokeCallbackAsync(actorId, sameSourceName, fixture.AppApiToken, cancellationToken)).ShouldBe(HttpStatusCode.OK);
        ReminderDispositionRecord rescheduleReplay = (await ReadAsync<ReminderDispositionRecord>(
            DispositionKey(actorId, sameSourceName), cancellationToken)).ShouldNotBeNull();
        rescheduleReplay.Disposition.ShouldBe(ReminderDisposition.Submitted);
        rescheduleReplay.EffectId.ShouldBe(expectedEffectId);
        rescheduleReplay.Replayed.ShouldBeTrue();
        string receiptJson = await fixture.GetActorStateJsonAsync(
            fixture.AggregateActorTypeName, targetActorId, "effect_receipt_" + expectedEffectId);
        JsonSerializer.Deserialize<EffectReceipt>(receiptJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .ShouldNotBeNull().CausationId.ShouldBe("wrk-" + expectedEffectId);
        (await ReadAsync<ReminderItemState>(ItemKey(actorId), cancellationToken)).ShouldBeNull();

        // 6. A Scheduler reminder deleted behind the runtime's back is re-armed by the reconciler.
        ReminderIntent rescheduled = Intent(item, DateTimeOffset.UtcNow.AddHours(2), revision: 3, sequence: 2);
        string rescheduledName = ReminderIdentityCodec.ComputeReminderName(rescheduled);
        fixture.ReminderIntents.Set(target, rescheduled);
        (await ConvergeAsync(target, cancellationToken)).Armed.ShouldBe(1);
        (await SchedulerHoldsAsync(actorId, rescheduledName, cancellationToken)).ShouldBeTrue();
        await DeleteSchedulerReminderAsync(actorId, rescheduledName, cancellationToken);
        (await SchedulerHoldsAsync(actorId, rescheduledName, cancellationToken)).ShouldBeFalse();

        DateTimeOffset reconciliationStarted = DateTimeOffset.UtcNow;
        ReminderReconciliationPass pass = await fixture.Services.GetRequiredService<ReminderReconciler>()
            .RunPassAsync(cancellationToken);
        DateTimeOffset reconciliationCompleted = DateTimeOffset.UtcNow;

        pass.Incomplete.ShouldBe(0);
        pass.Armed.ShouldBeGreaterThanOrEqualTo(1);
        await AssertSchedulerTimingAsync(
            actorId, rescheduledName, rescheduled.DueUtc, reconciliationStarted, reconciliationCompleted, cancellationToken);
        (await ReadAsync<ReminderItemState>(ItemKey(actorId), cancellationToken)).ShouldNotBeNull()
            .Entries.ShouldHaveSingleItem().Status.ShouldBe(ReminderEntryStatus.Armed);

        // Leave nothing armed in the shared Scheduler: the stream no longer holds the intent.
        fixture.ReminderIntents.Set(target);
        (await ConvergeAsync(target, cancellationToken)).Cancelled.ShouldBe(1);
        (await SchedulerHoldsAsync(actorId, rescheduledName, cancellationToken)).ShouldBeFalse();
        (await ReadAsync<ReminderItemState>(ItemKey(actorId), cancellationToken)).ShouldBeNull();
    }

    private static ReminderIntent Intent(string item, DateTimeOffset due, long revision, long sequence)
        => new(
            Tenant,
            Domain,
            item,
            new DateTimeOffset(due.UtcTicks, TimeSpan.Zero),
            EffectKindCatalog.DateResume,
            "counter.synthetic-resume.v1",
            [123, 125],
            Domain,
            item,
            sequence,
            revision);

    private string ItemKey(string actorId) => ReminderStateKeys.Item(fixture.ReminderActorTypeName, actorId);

    private string CandidatesKey() => ReminderStateKeys.TenantCandidates(fixture.ReminderActorTypeName, Tenant);

    private string DispositionKey(string actorId, string subject)
        => ReminderStateKeys.Disposition(fixture.ReminderActorTypeName, actorId, subject);

    private async Task<T?> ReadAsync<T>(string key, CancellationToken cancellationToken)
        where T : class
        => (await fixture.Services.GetRequiredService<IReadModelStore>()
            .GetAsync<T>("statestore", key, cancellationToken)).Value;

    /// <summary>Converges through the Dapr actor runtime, tolerating placement dissemination after a (re)start.</summary>
    private async Task<ReminderConvergenceResult> ConvergeAsync(ReminderTarget target, CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + PlacementBudget;
        while (true)
        {
            try
            {
                return await fixture.Services.GetRequiredService<IReminderRegistrar>().ConvergeAsync(target, cancellationToken);
            }
            catch (Exception) when (DateTimeOffset.UtcNow < deadline)
            {
                fixture.ThrowIfHostStopped();
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
    }

    private async Task<HttpStatusCode> InvokeCallbackAsync(
        string actorId,
        string reminderName,
        string? token,
        CancellationToken cancellationToken)
    {
        using var client = new HttpClient { BaseAddress = new Uri(fixture.AppHttpEndpoint), Timeout = TimeSpan.FromSeconds(60) };
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/actors/{fixture.ReminderActorTypeName}/{actorId}/method/remind/{reminderName}")
        {
            // The body mirrors what the sidecar sends; Dapr ignores a callback whose body names no reminder field.
            Content = new StringContent("{\"data\":null,\"dueTime\":\"0h0m0s0ms\"}", Encoding.UTF8, "application/json"),
        };
        if (token is not null)
        {
            request.Headers.Add(ReminderCallbackTokenFilter.HeaderName, token);
        }

        using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);
        return response.StatusCode;
    }

    private async Task<bool> SchedulerHoldsAsync(string actorId, string reminderName, CancellationToken cancellationToken)
    {
        using var client = new HttpClient { BaseAddress = new Uri(fixture.DaprHttpEndpoint), Timeout = TimeSpan.FromSeconds(30) };
        using HttpResponseMessage response = await client.GetAsync(
            $"/v1.0/actors/{fixture.ReminderActorTypeName}/{actorId}/reminders/{reminderName}",
            cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        return response.StatusCode == HttpStatusCode.OK && body.Contains("dueTime", StringComparison.OrdinalIgnoreCase);
    }

    private async Task AssertSchedulerTimingAsync(
        string actorId,
        string reminderName,
        DateTimeOffset dueUtc,
        DateTimeOffset schedulingStarted,
        DateTimeOffset schedulingCompleted,
        CancellationToken cancellationToken)
    {
        using var client = new HttpClient { BaseAddress = new Uri(fixture.DaprHttpEndpoint), Timeout = TimeSpan.FromSeconds(30) };
        using HttpResponseMessage response = await client.GetAsync(
            $"/v1.0/actors/{fixture.ReminderActorTypeName}/{actorId}/reminders/{reminderName}",
            cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using JsonDocument reminder = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        TimeSpan actualDelay = ParseSchedulerDuration(reminder.RootElement.GetProperty("dueTime").GetString().ShouldNotBeNull());
        TimeSpan actualPeriod = ParseSchedulerDuration(reminder.RootElement.GetProperty("period").GetString().ShouldNotBeNull());
        TimeSpan period = fixture.Services.GetRequiredService<IOptions<EventStoreReminderOptions>>().Value.RetryMaxDelay;

        // The coordinator reads its clock inside the operation. Bound that interval and allow only two
        // seconds for duration serialization and the host/sidecar clock difference.
        (schedulingCompleted - schedulingStarted).ShouldBeInRange(TimeSpan.Zero, PlacementBudget + TimeSpan.FromSeconds(30));
        TimeSpan tolerance = TimeSpan.FromSeconds(2);
        actualDelay.ShouldBeInRange(dueUtc - schedulingCompleted - tolerance, dueUtc - schedulingStarted + tolerance);
        actualPeriod.ShouldBe(period);
    }

    private static TimeSpan ParseSchedulerDuration(string duration)
    {
        // Scheduler stores repeating intervals as "@every <Go duration>" and may normalize
        // zero-valued components. Parse each component and reject any unparsed text.
        if (duration.StartsWith("@every ", StringComparison.Ordinal))
        {
            duration = duration["@every ".Length..];
        }

        MatchCollection components = Regex.Matches(duration, @"(\d+(?:\.\d+)?)(ms|h|m|s)", RegexOptions.CultureInvariant);
        string.Concat(components.Select(static component => component.Value)).ShouldBe(duration);
        components.Count.ShouldBeGreaterThan(0);
        double milliseconds = 0;
        foreach (Match component in components)
        {
            double value = double.Parse(component.Groups[1].Value, CultureInfo.InvariantCulture);
            milliseconds += value * (component.Groups[2].Value switch
            {
                "h" => 3_600_000,
                "m" => 60_000,
                "s" => 1_000,
                _ => 1,
            });
        }

        return TimeSpan.FromMilliseconds(milliseconds);
    }

    private async Task DeleteSchedulerReminderAsync(string actorId, string reminderName, CancellationToken cancellationToken)
    {
        using var client = new HttpClient { BaseAddress = new Uri(fixture.DaprHttpEndpoint), Timeout = TimeSpan.FromSeconds(30) };
        using HttpResponseMessage response = await client.DeleteAsync(
            $"/v1.0/actors/{fixture.ReminderActorTypeName}/{actorId}/reminders/{reminderName}",
            cancellationToken);
        response.IsSuccessStatusCode.ShouldBeTrue($"Deleting the Scheduler reminder returned {(int)response.StatusCode}.");
    }
}
