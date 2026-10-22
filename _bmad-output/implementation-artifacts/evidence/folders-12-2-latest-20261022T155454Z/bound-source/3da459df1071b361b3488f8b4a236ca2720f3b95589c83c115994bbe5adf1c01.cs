using System.Collections.Concurrent;

using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Contracts.Reminders;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.DomainService.Tests.Fixtures;

/// <summary>
/// Wires the reminder runtime over durable in-memory fakes. The store, scheduler, and receipt authority are
/// shared by every coordinator the harness creates, so a new coordinator models a restarted host.
/// </summary>
internal sealed class ReminderTestHarness
{
    /// <summary>The synthetic tenant.</summary>
    public const string Tenant = "tenant-a";

    /// <summary>The synthetic domain.</summary>
    public const string Domain = "widget";

    /// <summary>The synthetic date-resume purpose.</summary>
    public const string DatePurpose = "synthetic-date-resume";

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _turns = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, FakeReminderScheduler> _schedulers = new(StringComparer.Ordinal);

    /// <summary>The synthetic reminder actor type.</summary>
    public const string ActorType = "WidgetReminderActor";

    /// <summary>Initializes a new instance of the <see cref="ReminderTestHarness"/> class.</summary>
    public ReminderTestHarness()
    {
        CoordinatorStore = new DispositionFailingReadModelStore(Store);
        Options.Purposes[EffectKindCatalog.DateResume] = DatePurpose;
        Options.Purposes[EffectKindCatalog.Expiry] = "synthetic-expiry";
    }

    /// <summary>Gets the durable state store.</summary>
    public InMemoryReadModelStore Store { get; } = new();

    /// <summary>Gets the store the runtime writes through; it can fail audit disposition writes.</summary>
    public DispositionFailingReadModelStore CoordinatorStore { get; }

    /// <summary>Gets the synthetic domain intent source.</summary>
    public FakeReminderIntentSource Source { get; } = new();

    /// <summary>Gets the synthetic receipt authority.</summary>
    public FakeTrustedEffectSubmitter Submitter { get; } = new();

    /// <summary>Gets the synthetic delegation issuer.</summary>
    public FakeReminderDelegationTokenProvider Tokens { get; } = new();

    /// <summary>Gets the controllable clock.</summary>
    public ManualTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));

    /// <summary>Gets the reminder options shared by every created component.</summary>
    public EventStoreReminderOptions Options { get; } = new() { ActorTypeName = ActorType, Workload = "widget-service" };

    /// <summary>Gets the default host-local readiness view.</summary>
    public ReminderRuntimeStatus Status { get; } = new();

    /// <summary>Gets or sets a value indicating whether coordinators receive the submitter.</summary>
    public bool WithSubmitter { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether coordinators receive the delegation issuer.</summary>
    public bool WithTokens { get; set; } = true;

    /// <summary>Creates a target for an item of the synthetic tenant and domain.</summary>
    /// <param name="item">The aggregate identifier.</param>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The target.</returns>
    public static ReminderTarget Target(string item, string tenant = Tenant) => new(tenant, Domain, item);

    /// <summary>Creates a self-targeted date-resume intent.</summary>
    /// <param name="target">The target.</param>
    /// <param name="due">The due instant.</param>
    /// <param name="revision">The schedule revision.</param>
    /// <param name="sequence">The source envelope sequence.</param>
    /// <param name="kind">The reminder kind.</param>
    /// <returns>The intent.</returns>
    public static ReminderIntent Intent(
        ReminderTarget target,
        DateTimeOffset due,
        long revision = 1,
        long sequence = 3,
        string kind = EffectKindCatalog.DateResume)
        => new(
            target.Tenant,
            target.Domain,
            target.Aggregate,
            due,
            kind,
            "widget.resume-payload.v1",
            [9, 9, 9],
            target.Domain,
            target.Aggregate,
            sequence,
            revision);

    /// <summary>Gets the actor identifier of a target.</summary>
    /// <param name="target">The target.</param>
    /// <returns>The <c>wra-</c> identifier.</returns>
    public static string ActorId(ReminderTarget target) => ReminderIdentityCodec.ComputeActorId(target.Tenant, target.Aggregate);

    /// <summary>Gets the reminder name of an intent.</summary>
    /// <param name="intent">The intent.</param>
    /// <returns>The reminder name.</returns>
    public static string Name(ReminderIntent intent) => ReminderIdentityCodec.ComputeReminderName(intent);

    /// <summary>Gets the durable scheduler of one actor.</summary>
    /// <param name="actorId">The actor identifier.</param>
    /// <returns>The scheduler.</returns>
    public FakeReminderScheduler SchedulerFor(string actorId) => _schedulers.GetOrAdd(actorId, static _ => new FakeReminderScheduler());

    /// <summary>Creates a coordinator, as a freshly started host would.</summary>
    /// <param name="status">The host-local readiness view; defaults to <see cref="Status"/>.</param>
    /// <returns>The coordinator.</returns>
    public ReminderCoordinator CreateCoordinator(ReminderRuntimeStatus? status = null)
        => new(
            Source,
            CreateIndex(),
            CoordinatorStore,
            CoordinatorStore,
            Microsoft.Extensions.Options.Options.Create(Options),
            status ?? Status,
            Time,
            NullLogger<ReminderCoordinator>.Instance,
            WithSubmitter ? Submitter : null,
            WithTokens ? Tokens : null);

    /// <summary>Creates the discovery index.</summary>
    /// <returns>The index.</returns>
    public ReminderIntentIndex CreateIndex() => new(Store, Microsoft.Extensions.Options.Options.Create(Options));

    /// <summary>Creates a registrar that converges through the serialized in-process actor turn.</summary>
    /// <param name="status">The host-local readiness view; defaults to <see cref="Status"/>.</param>
    /// <returns>The registrar.</returns>
    public ReminderRegistrar CreateRegistrar(ReminderRuntimeStatus? status = null)
        => new(new InProcessReminderActorInvoker(this, status ?? Status), status ?? Status);

    /// <summary>Creates a reconciler for one host.</summary>
    /// <param name="status">The host-local readiness view; defaults to <see cref="Status"/>.</param>
    /// <param name="timeProvider">The timer provider; defaults to the controllable test clock.</param>
    /// <returns>The reconciler.</returns>
    public ReminderReconciler CreateReconciler(ReminderRuntimeStatus? status = null, TimeProvider? timeProvider = null)
        => new(
            CreateIndex(),
            CreateRegistrar(status),
            status ?? Status,
            Microsoft.Extensions.Options.Options.Create(Options),
            timeProvider ?? Time,
            NullLogger<ReminderReconciler>.Instance);

    /// <summary>Delivers a scheduler callback inside the actor's serialized turn.</summary>
    /// <param name="actorId">The actor identifier.</param>
    /// <param name="reminderName">The reminder name.</param>
    /// <param name="status">The host-local readiness view; defaults to <see cref="Status"/>.</param>
    /// <returns>The applied disposition.</returns>
    public Task<ReminderDisposition?> FireAsync(string actorId, string reminderName, ReminderRuntimeStatus? status = null)
        => InTurnAsync(
            actorId,
            () => CreateCoordinator(status).HandleCallbackAsync(actorId, reminderName, SchedulerFor(actorId), CancellationToken.None));

    /// <summary>Runs work inside one actor's serialized turn.</summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="actorId">The actor identifier.</param>
    /// <param name="work">The turn's work.</param>
    /// <returns>The work's result.</returns>
    public async Task<T> InTurnAsync<T>(string actorId, Func<Task<T>> work)
    {
        SemaphoreSlim turn = _turns.GetOrAdd(actorId, static _ => new SemaphoreSlim(1, 1));
        await turn.WaitAsync();
        try
        {
            return await work();
        }
        finally
        {
            _ = turn.Release();
        }
    }

    /// <summary>Reads the persisted item state.</summary>
    /// <param name="actorId">The actor identifier.</param>
    /// <returns>The state, or <see langword="null"/>.</returns>
    public ReminderItemState? ItemState(string actorId)
        => Store.Snapshot<ReminderItemState>(Options.StateStoreName, ReminderStateKeys.Item(Options.ActorTypeName, actorId));

    /// <summary>Reads a persisted audit disposition.</summary>
    /// <param name="actorId">The actor identifier.</param>
    /// <param name="subject">The reminder name or evidence digest.</param>
    /// <returns>The disposition, or <see langword="null"/>.</returns>
    public ReminderDispositionRecord? Disposition(string actorId, string subject)
        => Store.Snapshot<ReminderDispositionRecord>(Options.StateStoreName, ReminderStateKeys.Disposition(Options.ActorTypeName, actorId, subject));

    /// <summary>Reads a tenant's persisted discovery candidates.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The candidates, empty when none.</returns>
    public IReadOnlyList<ReminderCandidate> Candidates(string tenant = Tenant)
        => Store.Snapshot<ReminderTenantCandidates>(Options.StateStoreName, ReminderStateKeys.TenantCandidates(Options.ActorTypeName, tenant))?.Candidates ?? [];

    /// <summary>Reads the persisted tenant registry.</summary>
    /// <returns>The tenants, empty when none.</returns>
    public IReadOnlyList<string> Tenants()
        => Store.Snapshot<ReminderTenantRegistry>(Options.StateStoreName, ReminderStateKeys.TenantRegistry(Options.ActorTypeName))?.Tenants ?? [];

    /// <summary>Overwrites the persisted item state, as a restore or a foreign writer would.</summary>
    /// <param name="actorId">The actor identifier.</param>
    /// <param name="state">The state to seed.</param>
    public void SeedItemState(string actorId, ReminderItemState state)
        => Store.SeedRaw(Options.StateStoreName, ReminderStateKeys.Item(Options.ActorTypeName, actorId), state);
}
