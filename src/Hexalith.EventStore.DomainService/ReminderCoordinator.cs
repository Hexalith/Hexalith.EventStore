using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Client.Effects;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Reminders;
using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Reminders;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Converges and fires one tenant item's typed reminders. Every method runs inside the item's serialized
/// reminder actor turn. Streams are authoritative for current intents and submissions. Orphaned,
/// quarantined, or structurally invalid callbacks can cancel reminders without a stream fold.
/// Persisted state holds identifiers and digests only.
/// </summary>
/// <remarks>
/// <para>
/// Callback admission runs in order after the app-channel token filter: the stored full tuple must re-derive
/// the actor identifier and the reminder name (a mismatch is quarantined), the name's kind must have a
/// configured purpose (otherwise the work is denied and retained), and the stream must still report the exact
/// witness (otherwise an audited no-op cancels the reminder).
/// </para>
/// <para>
/// A durable target receipt releases pending state after its audit record is written and the scheduler
/// reminder is successfully cancelled. The index entry goes last only after a fresh fold reports no current intents.
/// An uncertain outcome keeps the state, re-arms a backoff reminder, and counts as unresolved. A callback
/// never reports failure by throwing.
/// </para>
/// </remarks>
internal sealed class ReminderCoordinator
{
    private const string MalformedName = "malformed";
    // One tick doubled 63 times exceeds TimeSpan.MaxValue, covering every positive delay ratio.
    private const int MaxBackoffExponent = 63;

    private readonly IReminderIntentSource _source;
    private readonly ReminderIntentIndex _index;
    private readonly IReadModelStore _store;
    private readonly IReadModelConditionalEraser _eraser;
    private readonly EventStoreReminderOptions _options;
    private readonly ReminderRuntimeStatus _status;
    private readonly TimeProvider _time;
    private readonly ILogger<ReminderCoordinator> _logger;
    private readonly ITrustedEffectSubmitter? _submitter;
    private readonly IReminderDelegationTokenProvider? _delegationTokenProvider;

    /// <summary>Initializes a new instance of the <see cref="ReminderCoordinator"/> class.</summary>
    /// <param name="source">The domain intent source.</param>
    /// <param name="index">The discovery index.</param>
    /// <param name="store">The durable compare-and-swap store.</param>
    /// <param name="eraser">The conditional eraser of the same store.</param>
    /// <param name="options">The reminder options.</param>
    /// <param name="status">The host-local readiness view.</param>
    /// <param name="timeProvider">The time provider; only this edge reads a clock.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="submitter">The trusted-effect submitter, or <see langword="null"/> to fail closed.</param>
    /// <param name="delegationTokenProvider">The delegation provider, or <see langword="null"/> to fail closed.</param>
    public ReminderCoordinator(
        IReminderIntentSource source,
        ReminderIntentIndex index,
        IReadModelStore store,
        IReadModelConditionalEraser eraser,
        IOptions<EventStoreReminderOptions> options,
        ReminderRuntimeStatus status,
        TimeProvider timeProvider,
        ILogger<ReminderCoordinator> logger,
        ITrustedEffectSubmitter? submitter,
        IReminderDelegationTokenProvider? delegationTokenProvider)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _index = index ?? throw new ArgumentNullException(nameof(index));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _eraser = eraser ?? throw new ArgumentNullException(nameof(eraser));
        _options = (options ?? throw new ArgumentNullException(nameof(options))).Value;
        _status = status ?? throw new ArgumentNullException(nameof(status));
        _time = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _submitter = submitter;
        _delegationTokenProvider = delegationTokenProvider;
    }

    /// <summary>Validates a target and derives its reminder actor identifier.</summary>
    /// <param name="target">The target stream.</param>
    /// <returns>The <c>wra-</c> actor identifier.</returns>
    /// <exception cref="ArgumentException">The target is not canonical.</exception>
    public static string ComputeActorId(ReminderTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (string.IsNullOrWhiteSpace(target.Domain))
        {
            throw new ArgumentException("The target domain is required.", nameof(target));
        }

        var identity = new AggregateIdentity(target.Tenant, target.Domain, target.Aggregate);
        if (!string.Equals(identity.TenantId, target.Tenant, StringComparison.Ordinal)
            || !string.Equals(identity.Domain, target.Domain, StringComparison.Ordinal))
        {
            throw new ArgumentException("Reminder target tenant and domain must be canonical lowercase.", nameof(target));
        }

        return ReminderIdentityCodec.ComputeActorId(target.Tenant, target.Aggregate);
    }

    /// <summary>
    /// Converges the item with the stream's current intents: new witnesses are indexed and persisted before
    /// scheduling, future ones are armed, due ones are submitted, and obsolete ones are cancelled.
    /// </summary>
    /// <param name="actorId">The actor identifier that owns this turn.</param>
    /// <param name="target">The target stream; it must derive <paramref name="actorId"/>.</param>
    /// <param name="scheduler">The actor's scheduler operations.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>Actions taken plus the item's retained unresolved and quarantined totals.</returns>
    public async Task<ReminderConvergenceResult> ConvergeAsync(
        string actorId,
        ReminderTarget target,
        IReminderScheduler scheduler,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(scheduler);
        if (string.IsNullOrWhiteSpace(target.Domain))
        {
            throw new ArgumentException("The target domain is required.", nameof(target));
        }

        if (!string.Equals(ComputeActorId(target), actorId, StringComparison.Ordinal))
        {
            throw new ArgumentException("The target does not derive this reminder actor.", nameof(target));
        }

        string key = ReminderStateKeys.Item(_options.ActorTypeName, actorId);
        (ReminderItemState? stored, string? etag) = await LoadAsync(key, actorId, cancellationToken).ConfigureAwait(false);
        try
        {
            DateTimeOffset now = _time.GetUtcNow();
            if (stored is not null && !IsSameTarget(stored, target))
            {
                return await QuarantineActorCollisionAsync(actorId, key, stored, etag, target, now, cancellationToken)
                    .ConfigureAwait(false);
            }

            IReadOnlyList<ReminderIntent>? intents = await _source
                .GetCurrentIntentsAsync(target, cancellationToken)
                .ConfigureAwait(false);
            if (intents is null)
            {
                // A null fold is not an empty stream. Treating it as empty would cancel stored reminders.
                throw new ReminderFailClosedException("source-unavailable");
            }

            return await ConvergeCoreAsync(actorId, key, stored, etag, target, intents, now, scheduler, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ReminderFailClosedException exception)
        {
            ReminderLog.FailedClosed(_logger, actorId, exception.ReasonCode, exception.GetType().Name);
            (ReminderItemState? current, _) = await LoadAsync(key, actorId, cancellationToken).ConfigureAwait(false);
            if (current is null || !HasWork(current))
            {
                // Nothing durable holds this item yet, so no index entry or pass can rediscover it: surface the
                // failure so the caller's at-least-once delivery retries the convergence.
                throw;
            }

            // A failure after an earlier successful state write must be judged from the durable state now, not
            // the snapshot loaded at the start of this turn. Re-ensure discovery before acknowledging it.
            var currentTarget = new ReminderTarget(current.Tenant, current.Domain, current.Aggregate);
            await _index.EnsureCandidateAsync(currentTarget, actorId, cancellationToken).ConfigureAwait(false);
            int unresolved = Math.Max(current.Entries.Count(static e => e.Status != ReminderEntryStatus.Quarantined), 1);
            int quarantined = CountQuarantined(current);
            _status.RecordItem(actorId, unresolved, quarantined);
            return new ReminderConvergenceResult(0, 0, 0, unresolved, quarantined);
        }
    }

    /// <summary>Admits and executes one scheduler callback. It never throws for an admission or submission failure.</summary>
    /// <param name="actorId">The actor identifier that received the callback.</param>
    /// <param name="reminderName">The reminder name that fired.</param>
    /// <param name="scheduler">The actor's scheduler operations.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The applied disposition, or <see langword="null"/> when no witness exists.</returns>
    public async Task<ReminderDisposition?> HandleCallbackAsync(
        string actorId,
        string reminderName,
        IReminderScheduler scheduler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        if (string.IsNullOrWhiteSpace(actorId) || string.IsNullOrWhiteSpace(reminderName))
        {
            return null;
        }

        string loggedName = ReminderIdentityCodec.TryParseReminderName(reminderName, out _, out _)
            ? reminderName
            : MalformedName;
        try
        {
            return await HandleCallbackCoreAsync(actorId, reminderName, loggedName, scheduler, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (ReminderFailClosedException exception)
        {
            ReminderLog.FailedClosed(_logger, actorId, exception.ReasonCode, exception.GetType().Name);
            return ReminderDisposition.Retrying;
        }
        catch (Exception exception)
        {
            // The witness, if any, is untouched and stays discoverable; the next firing or pass retries it.
            ReminderLog.CallbackFailed(_logger, actorId, loggedName, exception.GetType().Name);
            return ReminderDisposition.Retrying;
        }
    }

    private static bool IsSameTarget(ReminderItemState state, ReminderTarget target)
        => string.Equals(state.Tenant, target.Tenant, StringComparison.Ordinal)
            && string.Equals(state.Domain, target.Domain, StringComparison.Ordinal)
            && string.Equals(state.Aggregate, target.Aggregate, StringComparison.Ordinal);

    private static int CountUnresolved(ReminderItemState? state)
        => state?.Entries.Count(static e => e.Status is ReminderEntryStatus.Pending or ReminderEntryStatus.Retrying) ?? 0;

    private static int CountQuarantined(ReminderItemState? state)
        => state is null
            ? 0
            : state.Entries.Count(static e => e.Status == ReminderEntryStatus.Quarantined) + state.Quarantine.Count;

    private static bool HasWork(ReminderItemState state) => state.Entries.Count > 0 || state.Quarantine.Count > 0;

    private static string? ValidateIntent(ReminderIntent? intent, ReminderTarget target)
    {
        if (intent is null)
        {
            return "intent-missing";
        }

        if (!string.Equals(intent.Tenant, target.Tenant, StringComparison.Ordinal)
            || !string.Equals(intent.TargetDomain, target.Domain, StringComparison.Ordinal)
            || !string.Equals(intent.TargetAggregate, target.Aggregate, StringComparison.Ordinal))
        {
            return "target-mismatch";
        }

        if (!ReminderIdentityCodec.IsSupportedKind(intent.Kind))
        {
            return "kind-unsupported";
        }

        if (intent.DueUtc.Offset != TimeSpan.Zero)
        {
            return "due-not-utc";
        }

        if (intent.ScheduleRevision < 0)
        {
            return "revision-invalid";
        }

        if (intent.SourceSequence <= 0)
        {
            return "source-sequence-invalid";
        }

        if (string.IsNullOrWhiteSpace(intent.PayloadType) || intent.Payload is null)
        {
            return "payload-invalid";
        }

        try
        {
            _ = ReminderIdentityCodec.ComputeReminderName(intent);
            _ = EffectIdentityCodec.Encode(CreateEffectIdentity(intent.Tenant, intent.TargetDomain, intent.TargetAggregate, intent));
        }
        catch (ArgumentException)
        {
            return "identity-invalid";
        }

        return null;
    }

    private static EffectIdentity CreateEffectIdentity(string tenant, string targetDomain, string targetAggregate, ReminderIntent intent)
        => new(
            tenant,
            intent.SourceDomain,
            intent.SourceAggregate,
            intent.SourceSequence,
            intent.Kind,
            targetDomain,
            targetAggregate,
            EffectKindCatalog.PrimaryTargetOrdinal);

    private static ReminderEntry CreateWitness(ReminderIntent intent, DateTimeOffset now)
    {
        string token = ReminderIdentityCodec.ComputeScheduleToken(intent);
        return new ReminderEntry(
            ReminderIdentityCodec.ComputeReminderName(intent.Kind, token),
            token,
            intent.Kind,
            intent.DueUtc,
            intent.ScheduleRevision,
            intent.SourceDomain,
            intent.SourceAggregate,
            intent.SourceSequence,
            intent.PayloadType,
            EffectIdentityCodec.RenderDigest(SHA256.HashData(intent.Payload)),
            ReminderEntryStatus.Pending,
            0,
            null,
            now);
    }

    private static bool SameWitness(ReminderEntry left, ReminderEntry right)
        => string.Equals(left.ReminderName, right.ReminderName, StringComparison.Ordinal)
            && string.Equals(left.ScheduleToken, right.ScheduleToken, StringComparison.Ordinal)
            && string.Equals(left.Kind, right.Kind, StringComparison.Ordinal)
            && left.DueUtc.UtcTicks == right.DueUtc.UtcTicks
            && left.ScheduleRevision == right.ScheduleRevision
            && string.Equals(left.SourceDomain, right.SourceDomain, StringComparison.Ordinal)
            && string.Equals(left.SourceAggregate, right.SourceAggregate, StringComparison.Ordinal)
            && left.SourceSequence == right.SourceSequence
            && string.Equals(left.PayloadType, right.PayloadType, StringComparison.Ordinal)
            && string.Equals(left.PayloadDigest, right.PayloadDigest, StringComparison.Ordinal);

    private static ReminderEntry Quarantine(ReminderEntry entry, string reasonCode, DateTimeOffset now)
        => entry with { Status = ReminderEntryStatus.Quarantined, LastReasonCode = reasonCode, UpdatedAt = now };

    private static bool AddQuarantine(
        List<ReminderQuarantineRecord> quarantine,
        string evidenceDigest,
        string reasonCode,
        string? reminderName,
        DateTimeOffset now)
    {
        if (quarantine.Exists(record => string.Equals(record.EvidenceDigest, evidenceDigest, StringComparison.Ordinal)))
        {
            return false;
        }

        quarantine.Add(new ReminderQuarantineRecord(evidenceDigest, reasonCode, reminderName, now));
        return true;
    }

    private static ReminderItemState WithEntries(ReminderItemState state, IEnumerable<ReminderEntry> entries, IReadOnlyList<ReminderQuarantineRecord> quarantine)
        => state with
        {
            Entries = [.. entries.OrderBy(static e => e.ReminderName, StringComparer.Ordinal)],
            Quarantine = [.. quarantine.OrderBy(static q => q.EvidenceDigest, StringComparer.Ordinal)],
        };

    private static string EvidenceDigest(ReminderIntent? intent)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendText(hash, "reminder-intent-evidence-v1");
        if (intent is not null)
        {
            AppendText(hash, intent.Tenant);
            AppendText(hash, intent.TargetDomain);
            AppendText(hash, intent.TargetAggregate);
            AppendLong(hash, intent.DueUtc.UtcTicks);
            AppendLong(hash, intent.DueUtc.Offset.Ticks);
            AppendText(hash, intent.Kind);
            AppendText(hash, intent.PayloadType);
            AppendBytes(hash, intent.Payload);
            AppendText(hash, intent.SourceDomain);
            AppendText(hash, intent.SourceAggregate);
            AppendLong(hash, intent.SourceSequence);
            AppendLong(hash, intent.ScheduleRevision);
        }

        return EffectIdentityCodec.RenderDigest(hash.GetHashAndReset());
    }

    private static string EvidenceDigest(ReminderTarget target)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendText(hash, "reminder-target-evidence-v1");
        AppendText(hash, target.Tenant);
        AppendText(hash, target.Domain);
        AppendText(hash, target.Aggregate);
        return EffectIdentityCodec.RenderDigest(hash.GetHashAndReset());
    }

    private static string EvidenceDigest(ReminderEntry? entry, int ordinal)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendText(hash, "persisted-reminder-entry-v1");
        AppendLong(hash, ordinal);
        if (entry is not null)
        {
            AppendText(hash, entry.ReminderName);
            AppendText(hash, entry.ScheduleToken);
            AppendText(hash, entry.Kind);
            AppendLong(hash, entry.DueUtc.UtcTicks);
            AppendLong(hash, entry.DueUtc.Offset.Ticks);
            AppendLong(hash, entry.ScheduleRevision);
            AppendText(hash, entry.SourceDomain);
            AppendText(hash, entry.SourceAggregate);
            AppendLong(hash, entry.SourceSequence);
            AppendText(hash, entry.PayloadType);
            AppendText(hash, entry.PayloadDigest);
            AppendLong(hash, (int)entry.Status);
            AppendLong(hash, entry.Attempts);
            AppendText(hash, entry.LastReasonCode);
            AppendLong(hash, entry.UpdatedAt.UtcTicks);
            AppendLong(hash, entry.UpdatedAt.Offset.Ticks);
        }

        return EffectIdentityCodec.RenderDigest(hash.GetHashAndReset());
    }

    private static string EvidenceDigest(ReminderQuarantineRecord? record, int ordinal)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendText(hash, "persisted-reminder-quarantine-v1");
        AppendLong(hash, ordinal);
        if (record is not null)
        {
            AppendText(hash, record.EvidenceDigest);
            AppendText(hash, record.ReasonCode);
            AppendText(hash, record.ReminderName);
            AppendLong(hash, record.RecordedAt.UtcTicks);
            AppendLong(hash, record.RecordedAt.Offset.Ticks);
        }

        return EffectIdentityCodec.RenderDigest(hash.GetHashAndReset());
    }

    private static bool IsDigest(string? value)
    {
        const string alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        return value is { Length: 52 } && value.All(character => alphabet.Contains(character, StringComparison.Ordinal));
    }

    private static bool TryValidatePersistedEntry(
        ReminderItemState state,
        string actorId,
        ReminderEntry? entry)
    {
        if (entry is null
            || !Enum.IsDefined(entry.Status)
            || entry.DueUtc.Offset != TimeSpan.Zero
            || entry.UpdatedAt.Offset != TimeSpan.Zero
            || entry.ScheduleRevision < 0
            || entry.SourceSequence <= 0
            || entry.Attempts < 0
            || string.IsNullOrWhiteSpace(entry.SourceDomain)
            || string.IsNullOrWhiteSpace(entry.SourceAggregate)
            || string.IsNullOrWhiteSpace(entry.PayloadType)
            || !IsDigest(entry.PayloadDigest)
            || !ReminderIdentityCodec.TryParseReminderName(entry.ReminderName, out string parsedKind, out string parsedToken)
            || !string.Equals(parsedKind, entry.Kind, StringComparison.Ordinal)
            || !string.Equals(parsedToken, entry.ScheduleToken, StringComparison.Ordinal)
            || !ReminderIdentityCodec.Rederives(
                state.Tenant,
                state.Aggregate,
                entry.Kind,
                entry.DueUtc,
                entry.ScheduleRevision,
                actorId,
                entry.ReminderName))
        {
            return false;
        }

        try
        {
            if (string.IsNullOrWhiteSpace(state.Domain))
            {
                // Effect identity cannot be derived. Leave the entry in place so the callback
                // quarantines it as domain-invalid instead of repairing it into a nameless drop.
                return true;
            }

            _ = EffectIdentityCodec.Encode(new EffectIdentity(
                state.Tenant,
                entry.SourceDomain,
                entry.SourceAggregate,
                entry.SourceSequence,
                entry.Kind,
                state.Domain,
                state.Aggregate,
                EffectKindCatalog.PrimaryTargetOrdinal));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool IsValidPersistedQuarantine(ReminderQuarantineRecord? record)
        => record is not null
            && IsDigest(record.EvidenceDigest)
            && !string.IsNullOrWhiteSpace(record.ReasonCode)
            && record.RecordedAt.Offset == TimeSpan.Zero
            && (record.ReminderName is null || ReminderIdentityCodec.TryParseReminderName(record.ReminderName, out _, out _));

    private static void AppendText(IncrementalHash hash, string? value)
        => AppendBytes(hash, value is null ? null : Encoding.UTF8.GetBytes(value));

    private static void AppendBytes(IncrementalHash hash, byte[]? value)
    {
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(length, value?.Length ?? -1);
        hash.AppendData(length);
        if (value is not null)
        {
            hash.AppendData(value);
        }
    }

    private static void AppendLong(IncrementalHash hash, long value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        hash.AppendData(bytes);
    }

    private TimeSpan Backoff(int attempts)
    {
        int exponent = Math.Clamp(attempts - 1, 0, MaxBackoffExponent);
        double ticks = _options.RetryInitialDelay.Ticks * Math.Pow(2, exponent);
        return ticks >= _options.RetryMaxDelay.Ticks ? _options.RetryMaxDelay : TimeSpan.FromTicks((long)ticks);
    }

    private bool TryGetPurpose(string kind, out string purpose)
    {
        if (_options.Purposes.TryGetValue(kind, out string? configured) && !string.IsNullOrWhiteSpace(configured))
        {
            purpose = configured;
            return true;
        }

        purpose = string.Empty;
        return false;
    }

    private async Task<ReminderConvergenceResult> ConvergeCoreAsync(
        string actorId,
        string key,
        ReminderItemState? stored,
        string? etag,
        ReminderTarget target,
        IReadOnlyList<ReminderIntent> intents,
        DateTimeOffset now,
        IReminderScheduler scheduler,
        CancellationToken cancellationToken,
        IReadOnlySet<string>? retainedReminderNames = null)
    {
        ReminderItemState state = stored ?? new ReminderItemState(target.Tenant, target.Domain, target.Aggregate, 0, [], []);
        var quarantine = new List<ReminderQuarantineRecord>(state.Quarantine);
        var valid = new List<(ReminderIntent Intent, ReminderEntry Witness)>();
        var desired = new Dictionary<string, (ReminderIntent Intent, ReminderEntry Witness)>(StringComparer.Ordinal);
        var collided = new Dictionary<string, string>(StringComparer.Ordinal);

        // Classify the stream's intents. Malformed evidence is never dropped: it is quarantined by digest.
        foreach (ReminderIntent? intent in intents)
        {
            string? reason = ValidateIntent(intent, target);
            if (reason is not null)
            {
                string digest = EvidenceDigest(intent);
                _ = AddQuarantine(quarantine, digest, reason, null, now);

                continue;
            }

            valid.Add((intent!, CreateWitness(intent!, now)));
        }

        // Classify effect overlap before collapsing names: either same-name witness may share its effect
        // with a third name, and discarding either source would let that third witness execute.
        foreach (IGrouping<string, string> shared in valid
            .GroupBy(pair => EffectIdentityCodec.ComputeEffectId(
                CreateEffectIdentity(target.Tenant, target.Domain, target.Aggregate, pair.Intent)), pair => pair.Witness.ReminderName)
            .Where(static group => group.Distinct(StringComparer.Ordinal).Skip(1).Any()))
        {
            foreach (string name in shared)
            {
                collided[name] = "effect-collision";
            }
        }

        foreach ((ReminderIntent intent, ReminderEntry witness) in valid)
        {
            if (desired.TryGetValue(witness.ReminderName, out (ReminderIntent Intent, ReminderEntry Witness) known))
            {
                if (!SameWitness(known.Witness, witness))
                {
                    collided[witness.ReminderName] = "witness-collision";
                    string digest = EvidenceDigest(intent);
                    _ = AddQuarantine(quarantine, digest, "witness-collision", witness.ReminderName, now);
                }

                continue;
            }

            desired[witness.ReminderName] = (intent, witness);
        }

        foreach ((string name, string reasonCode) in collided)
        {
            string digest = EvidenceDigest(desired[name].Intent);
            _ = AddQuarantine(quarantine, digest, reasonCode, name, now);

            _ = desired.Remove(name);
        }

        // A persisted entry that normalized into quarantine must not be recreated from the stream in this
        // turn. Operator-visible evidence remains authoritative until it is explicitly disposed of.
        foreach (ReminderQuarantineRecord record in quarantine)
        {
            if (record.ReminderName is not null
                && record.ReasonCode is "stored-entry-invalid" or "stored-entry-duplicate")
            {
                _ = desired.Remove(record.ReminderName);
            }
        }

        // Merge with persisted witnesses.
        var entries = new List<ReminderEntry>();
        var obsolete = new List<ReminderEntry>();
        var newlyQuarantined = new List<(ReminderEntry Original, ReminderEntry Quarantined)>();
        var storedNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (ReminderEntry entry in state.Entries)
        {
            _ = storedNames.Add(entry.ReminderName);
            if (entry.Status == ReminderEntryStatus.Quarantined)
            {
                entries.Add(entry);
                _ = desired.Remove(entry.ReminderName);
            }
            else if (collided.TryGetValue(entry.ReminderName, out string? collisionReason))
            {
                ReminderEntry quarantined = Quarantine(entry, collisionReason, now);
                entries.Add(entry);
                newlyQuarantined.Add((entry, quarantined));
            }
            else if (!desired.ContainsKey(entry.ReminderName)
                && retainedReminderNames?.Contains(entry.ReminderName) == true)
            {
                // The caller still owns this witness. Do not cancel it until that caller's later step succeeds.
                entries.Add(entry);
            }
            else if (!desired.TryGetValue(entry.ReminderName, out (ReminderIntent Intent, ReminderEntry Witness) current))
            {
                entries.Add(entry);
                obsolete.Add(entry);
            }
            else if (!SameWitness(entry, current.Witness))
            {
                ReminderEntry quarantined = Quarantine(entry, "witness-collision", now);
                entries.Add(entry);
                newlyQuarantined.Add((entry, quarantined));
                string digest = EvidenceDigest(current.Intent);
                _ = AddQuarantine(quarantine, digest, "witness-collision", entry.ReminderName, now);

                _ = desired.Remove(entry.ReminderName);
            }
            else
            {
                entries.Add(entry);
            }
        }

        bool added = false;
        foreach (KeyValuePair<string, (ReminderIntent Intent, ReminderEntry Witness)> pair in desired)
        {
            if (!storedNames.Contains(pair.Key))
            {
                entries.Add(pair.Value.Witness);
                added = true;
            }
        }

        ReminderItemState next = WithEntries(state, entries, quarantine);

        // The index is written before anything is scheduled, and re-written whenever the item holds work, so an
        // index restored from an older backup regains the candidate. It does not write when the entry exists.
        if (HasWork(next))
        {
            await _index.EnsureCandidateAsync(target, actorId, cancellationToken).ConfigureAwait(false);
        }

        // Audit before releasing or recording. Obsolete witnesses are retained until both the audit and
        // Scheduler cancellation succeed. Quarantine transitions do not cancel the reminder when audit is
        // unavailable, so the next convergence can retry without losing evidence.
        var entryUpdates = new Dictionary<string, ReminderEntry?>(StringComparer.Ordinal);
        int cancelled = 0;
        foreach (ReminderEntry entry in obsolete)
        {
            bool audited = await TryWriteDispositionAsync(
                state, actorId, entry.ReminderName, ReminderDisposition.Cancelled, "obsolete", null, null, entry.Attempts, now, cancellationToken)
                .ConfigureAwait(false);
            if (audited
                && await TryCancelAsync(scheduler, actorId, entry.ReminderName, entry.ReminderName, cancellationToken).ConfigureAwait(false))
            {
                entryUpdates[entry.ReminderName] = null;
                cancelled++;
                ReminderLog.Cancelled(_logger, actorId, entry.ReminderName);
            }
            else
            {
                string reasonCode = audited ? "cancel-failed" : "audit-unavailable";
                entryUpdates[entry.ReminderName] = entry with
                {
                    Status = ReminderEntryStatus.Retrying,
                    Attempts = entry.Attempts + 1,
                    LastReasonCode = reasonCode,
                    UpdatedAt = now,
                };
            }
        }

        foreach ((ReminderEntry original, ReminderEntry quarantined) in newlyQuarantined)
        {
            string reasonCode = quarantined.LastReasonCode ?? "witness-collision";
            bool audited = await TryWriteDispositionAsync(
                state, actorId, original.ReminderName, ReminderDisposition.Quarantined, reasonCode, null, null, original.Attempts, now, cancellationToken)
                .ConfigureAwait(false);
            if (audited)
            {
                ReminderLog.Quarantined(_logger, actorId, original.ReminderName, reasonCode);
                entryUpdates[original.ReminderName] = quarantined;
                _ = await TryCancelAsync(scheduler, actorId, original.ReminderName, original.ReminderName, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                entryUpdates[original.ReminderName] = original with
                {
                    Status = ReminderEntryStatus.Retrying,
                    Attempts = original.Attempts + 1,
                    LastReasonCode = "audit-unavailable",
                    UpdatedAt = now,
                };
            }
        }

        foreach (ReminderQuarantineRecord record in quarantine)
        {
            ReminderLog.Quarantined(_logger, actorId, record.EvidenceDigest, record.ReasonCode);
            bool audited = await TryWriteDispositionAsync(
                state,
                actorId,
                record.EvidenceDigest,
                ReminderDisposition.Quarantined,
                record.ReasonCode,
                null,
                null,
                0,
                now,
                cancellationToken).ConfigureAwait(false);
            if (audited
                && record.ReminderName is not null
                && !entries.Exists(entry => string.Equals(entry.ReminderName, record.ReminderName, StringComparison.Ordinal)))
            {
                _ = await TryCancelAsync(scheduler, actorId, record.ReminderName, record.ReminderName, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        entries = [.. entries
            .Select(entry => entryUpdates.TryGetValue(entry.ReminderName, out ReminderEntry? updated) ? updated : entry)
            .OfType<ReminderEntry>()];
        next = WithEntries(state, entries, quarantine);
        bool changed = added
            || entryUpdates.Count > 0
            || quarantine.Count != state.Quarantine.Count
            || (stored is not null && !HasWork(next));

        ReminderItemState? persisted = stored;
        if (changed)
        {
            (persisted, etag) = await PersistAsync(key, next, etag, cancellationToken).ConfigureAwait(false);
        }

        var toCancel = new List<string>();

        // Arm future witnesses and submit due ones through the same path a callback uses.
        int armed = 0;
        int submitted = 0;
        var toRearm = new List<(string Name, TimeSpan DueTime)>();
        var settledEntries = new Dictionary<string, ReminderEntry?>(StringComparer.Ordinal);
        ReminderItemState working = persisted ?? next;
        foreach (ReminderEntry entry in working.Entries)
        {
            if (entry.Status == ReminderEntryStatus.Quarantined
                || !desired.TryGetValue(entry.ReminderName, out (ReminderIntent Intent, ReminderEntry Witness) current))
            {
                continue;
            }

            DateTimeOffset operationNow = _time.GetUtcNow();
            if (current.Intent.DueUtc <= operationNow)
            {
                if (entry.Status == ReminderEntryStatus.Retrying && entry.UpdatedAt + Backoff(entry.Attempts) > operationNow)
                {
                    // Inside its backoff window replicas must not submit, but reconciliation still repairs a
                    // reminder the Scheduler lost so the work is not stranded until a later full pass.
                    if (!await IsHeldAsync(scheduler, actorId, entry.ReminderName, cancellationToken).ConfigureAwait(false))
                    {
                        TimeSpan retryDue = entry.UpdatedAt + Backoff(entry.Attempts) - operationNow;
                        try
                        {
                            await scheduler.ArmAsync(entry.ReminderName, retryDue, _options.RetryMaxDelay, cancellationToken)
                                .ConfigureAwait(false);
                            armed++;
                            ReminderLog.Armed(_logger, actorId, entry.ReminderName);
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception exception)
                        {
                            ReminderLog.ArmFailed(_logger, actorId, entry.ReminderName, exception.GetType().Name);
                        }
                    }

                    continue;
                }

                ReminderSubmissionOutcome outcome = await SubmitAsync(working, entry, current.Intent, cancellationToken)
                    .ConfigureAwait(false);
                (ReminderEntry? settled, ReminderDisposition applied) = await SettleAsync(
                    working, actorId, entry, outcome, operationNow, scheduler, toCancel, toRearm, cancellationToken)
                    .ConfigureAwait(false);
                settledEntries[entry.ReminderName] = settled;
                if (applied == ReminderDisposition.Submitted)
                {
                    submitted++;
                }

                continue;
            }

            if (entry.Status == ReminderEntryStatus.Armed
                && await IsHeldAsync(scheduler, actorId, entry.ReminderName, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            try
            {
                await scheduler.ArmAsync(entry.ReminderName, current.Intent.DueUtc - operationNow, _options.RetryMaxDelay, cancellationToken)
                    .ConfigureAwait(false);
                armed++;
                ReminderLog.Armed(_logger, actorId, entry.ReminderName);
                if (entry.Status != ReminderEntryStatus.Armed)
                {
                    bool audited = await TryWriteDispositionAsync(
                        working,
                        actorId,
                        entry.ReminderName,
                        ReminderDisposition.Registered,
                        "armed",
                        null,
                        null,
                        entry.Attempts,
                        operationNow,
                        cancellationToken)
                        .ConfigureAwait(false);
                    settledEntries[entry.ReminderName] = audited
                        ? entry with { Status = ReminderEntryStatus.Armed, LastReasonCode = null, UpdatedAt = operationNow }
                        : entry with
                        {
                            Status = ReminderEntryStatus.Pending,
                            Attempts = entry.Attempts + 1,
                            LastReasonCode = "audit-unavailable",
                            UpdatedAt = operationNow,
                        };
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // Pending state survives a scheduler failure; the next pass re-arms it.
                ReminderLog.ArmFailed(_logger, actorId, entry.ReminderName, exception.GetType().Name);
                if (entry.Status != ReminderEntryStatus.Pending
                    || !string.Equals(entry.LastReasonCode, "arm-failed", StringComparison.Ordinal))
                {
                    settledEntries[entry.ReminderName] = entry with { Status = ReminderEntryStatus.Pending, LastReasonCode = "arm-failed", UpdatedAt = operationNow };
                }
            }
        }

        if (settledEntries.Count > 0)
        {
            ReminderItemState settledState = WithEntries(
                working,
                working.Entries
                    .Select(e => settledEntries.TryGetValue(e.ReminderName, out ReminderEntry? settled) ? settled : e)
                    .OfType<ReminderEntry>(),
                quarantine);
            (persisted, etag) = await PersistAsync(key, settledState, etag, cancellationToken).ConfigureAwait(false);
        }

        await RunSchedulerAsync(scheduler, actorId, toCancel, toRearm, cancellationToken).ConfigureAwait(false);

        int unresolved = CountUnresolved(persisted);
        if (persisted is null || !HasWork(persisted))
        {
            unresolved += await ReleaseDiscoveryIfIdleAsync(actorId, target, cancellationToken).ConfigureAwait(false);
            persisted = null;
        }

        int quarantinedCount = CountQuarantined(persisted);
        _status.RecordItem(actorId, unresolved, quarantinedCount);
        return new ReminderConvergenceResult(armed, submitted, cancelled, unresolved, quarantinedCount);
    }

    private async Task<ReminderConvergenceResult> QuarantineActorCollisionAsync(
        string actorId,
        string key,
        ReminderItemState stored,
        string? etag,
        ReminderTarget target,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Another target derived this actor identifier. Its witnesses are left untouched and the colliding
        // target's evidence is retained by digest for operator disposition.
        var quarantine = new List<ReminderQuarantineRecord>(stored.Quarantine);
        string digest = EvidenceDigest(target);
        ReminderItemState persisted = stored;
        if (AddQuarantine(quarantine, digest, "actor-collision", null, now))
        {
            ReminderLog.Quarantined(_logger, actorId, digest, "actor-collision");
            (ReminderItemState? saved, _) = await PersistAsync(key, WithEntries(stored, stored.Entries, quarantine), etag, cancellationToken)
                .ConfigureAwait(false);
            persisted = saved ?? stored;
        }

        _ = await TryWriteDispositionAsync(
            persisted,
            actorId,
            digest,
            ReminderDisposition.Quarantined,
            "actor-collision",
            null,
            null,
            0,
            now,
            cancellationToken).ConfigureAwait(false);

        if (HasWork(persisted))
        {
            await _index.EnsureCandidateAsync(
                new ReminderTarget(persisted.Tenant, persisted.Domain, persisted.Aggregate),
                actorId,
                cancellationToken).ConfigureAwait(false);
        }

        int unresolved = CountUnresolved(persisted);
        int quarantined = CountQuarantined(persisted);
        _status.RecordItem(actorId, unresolved, quarantined);
        return new ReminderConvergenceResult(0, 0, 0, unresolved, quarantined);
    }

    private async Task<ReminderDisposition?> HandleCallbackCoreAsync(
        string actorId,
        string reminderName,
        string loggedName,
        IReminderScheduler scheduler,
        CancellationToken cancellationToken)
    {
        string key = ReminderStateKeys.Item(_options.ActorTypeName, actorId);
        (ReminderItemState? state, string? etag) = await LoadAsync(key, actorId, cancellationToken).ConfigureAwait(false);
        ReminderEntry? entry = state?.Entries.FirstOrDefault(
            e => string.Equals(e.ReminderName, reminderName, StringComparison.Ordinal));
        if (state is null || entry is null)
        {
            ReminderQuarantineRecord? repaired = state?.Quarantine.FirstOrDefault(
                record => string.Equals(record.ReminderName, reminderName, StringComparison.Ordinal));
            if (state is not null && repaired is not null)
            {
                bool audited = await TryWriteDispositionAsync(
                    state,
                    actorId,
                    repaired.EvidenceDigest,
                    ReminderDisposition.Quarantined,
                    repaired.ReasonCode,
                    null,
                    null,
                    0,
                    _time.GetUtcNow(),
                    cancellationToken).ConfigureAwait(false);
                if (audited)
                {
                    _ = await TryCancelAsync(scheduler, actorId, reminderName, loggedName, cancellationToken).ConfigureAwait(false);
                }

                _status.RecordItem(actorId, CountUnresolved(state), CountQuarantined(state));
                return audited ? ReminderDisposition.Quarantined : ReminderDisposition.Retrying;
            }

            // No witness: nothing is disclosed or mutated, and the scheduler stops firing it.
            ReminderLog.Orphan(_logger, actorId, loggedName);
            _ = await TryCancelAsync(scheduler, actorId, reminderName, loggedName, cancellationToken).ConfigureAwait(false);
            if (state is not null)
            {
                _status.RecordItem(actorId, CountUnresolved(state), CountQuarantined(state));
            }

            return null;
        }

        DateTimeOffset now = _time.GetUtcNow();
        if (entry.Status == ReminderEntryStatus.Quarantined)
        {
            _ = await TryCancelAsync(scheduler, actorId, reminderName, loggedName, cancellationToken).ConfigureAwait(false);
            _status.RecordItem(actorId, CountUnresolved(state), CountQuarantined(state));
            return ReminderDisposition.Quarantined;
        }

        if (string.IsNullOrWhiteSpace(state.Domain))
        {
            // A blank domain cannot name a stream. Retiring the witness as stale would cancel a reminder
            // the real stream may still hold.
            return await SettleCallbackAsync(key, state, etag, actorId, entry, Outcome(ReminderDisposition.Quarantined, "domain-invalid"), now, scheduler, cancellationToken)
                .ConfigureAwait(false);
        }

        // Step 2 is defense in depth: LoadAsync already validates and re-derives the stored full tuple,
        // quarantining a mismatch as stored-entry-invalid before it can reach this branch.
        if (!ReminderIdentityCodec.Rederives(state.Tenant, state.Aggregate, entry.Kind, entry.DueUtc, entry.ScheduleRevision, actorId, reminderName)
            || !reminderName.EndsWith(entry.ScheduleToken, StringComparison.Ordinal))
        {
            return await SettleCallbackAsync(key, state, etag, actorId, entry, Outcome(ReminderDisposition.Quarantined, "tuple-mismatch"), now, scheduler, cancellationToken)
                .ConfigureAwait(false);
        }

        // Step 3: the name prefix matched the stored kind above; that kind must also have a configured purpose.
        if (!TryGetPurpose(entry.Kind, out _))
        {
            return await SettleCallbackAsync(key, state, etag, actorId, entry, Outcome(ReminderDisposition.Denied, "purpose-unconfigured"), now, scheduler, cancellationToken)
                .ConfigureAwait(false);
        }

        // Step 4: the stream must still report this exact witness.
        var target = new ReminderTarget(state.Tenant, state.Domain, state.Aggregate);
        IReadOnlyList<ReminderIntent>? current;
        try
        {
            current = await _source.GetCurrentIntentsAsync(target, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return await SettleCallbackAsync(key, state, etag, actorId, entry, Outcome(ReminderDisposition.Retrying, "source-unavailable"), now, scheduler, cancellationToken)
                .ConfigureAwait(false);
        }

        if (current is null)
        {
            return await SettleCallbackAsync(key, state, etag, actorId, entry, Outcome(ReminderDisposition.Retrying, "source-unavailable"), now, scheduler, cancellationToken)
                .ConfigureAwait(false);
        }

        List<ReminderIntent> valid = [.. current.Where(intent => ValidateIntent(intent, target) is null)];
        List<ReminderIntent> matches = [.. valid.Where(intent => string.Equals(
            ReminderIdentityCodec.ComputeReminderName(intent), reminderName, StringComparison.Ordinal))];

        // Every matching intent must carry the stored witness; convergence would quarantine any disagreement.
        if (matches.Exists(intent => !SameWitness(entry, CreateWitness(intent, now))))
        {
            return await SettleCallbackAsync(key, state, etag, actorId, entry, Outcome(ReminderDisposition.Quarantined, "witness-collision"), now, scheduler, cancellationToken)
                .ConfigureAwait(false);
        }

        ReminderIntent? match = matches.Count > 0 ? matches[0] : null;
        if (match is not null)
        {
            string effectId = EffectIdentityCodec.ComputeEffectId(CreateEffectIdentity(state.Tenant, state.Domain, state.Aggregate, match));
            if (valid.Exists(intent => !string.Equals(ReminderIdentityCodec.ComputeReminderName(intent), reminderName, StringComparison.Ordinal)
                && string.Equals(EffectIdentityCodec.ComputeEffectId(CreateEffectIdentity(state.Tenant, state.Domain, state.Aggregate, intent)), effectId, StringComparison.Ordinal)))
            {
                return await SettleCallbackAsync(key, state, etag, actorId, entry, Outcome(ReminderDisposition.Quarantined, "effect-collision"), now, scheduler, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        if (match is null)
        {
            return await RetireStaleAsync(key, state, etag, actorId, entry, target, current, now, scheduler, cancellationToken)
                .ConfigureAwait(false);
        }

        if (entry.Status == ReminderEntryStatus.Retrying && entry.UpdatedAt + Backoff(entry.Attempts) > now)
        {
            _status.RecordItem(actorId, CountUnresolved(state), CountQuarantined(state));
            return ReminderDisposition.Retrying;
        }

        ReminderSubmissionOutcome outcome = await SubmitAsync(state, entry, match, cancellationToken).ConfigureAwait(false);
        return await SettleCallbackAsync(key, state, etag, actorId, entry, outcome, now, scheduler, cancellationToken)
            .ConfigureAwait(false);
    }

    private static ReminderSubmissionOutcome Outcome(ReminderDisposition disposition, string reasonCode)
        => new(disposition, reasonCode, null, null);

    private async Task<ReminderDisposition> RetireStaleAsync(
        string key,
        ReminderItemState state,
        string? etag,
        string actorId,
        ReminderEntry entry,
        ReminderTarget target,
        IReadOnlyList<ReminderIntent> current,
        DateTimeOffset now,
        IReminderScheduler scheduler,
        CancellationToken cancellationToken)
    {
        // The stale witness is an audited no-op. Replacement convergence may submit other current witnesses
        // that are due. Without a durable audit record this witness stays for a later firing to retry retirement.
        if (!await TryWriteDispositionAsync(state, actorId, entry.ReminderName, ReminderDisposition.Stale, "witness-not-current", null, null, entry.Attempts, now, cancellationToken)
            .ConfigureAwait(false))
        {
            ReminderItemState retained = WithEntries(
                state,
                state.Entries.Select(candidate => string.Equals(candidate.ReminderName, entry.ReminderName, StringComparison.Ordinal)
                    ? candidate with
                    {
                        Status = ReminderEntryStatus.Retrying,
                        Attempts = candidate.Attempts + 1,
                        LastReasonCode = "audit-unavailable",
                        UpdatedAt = now,
                    }
                    : candidate),
                state.Quarantine);
            (ReminderItemState? persistedRetry, _) = await PersistAsync(key, retained, etag, cancellationToken).ConfigureAwait(false);
            _status.RecordItem(actorId, CountUnresolved(persistedRetry), CountQuarantined(persistedRetry));
            return ReminderDisposition.Retrying;
        }

        // Index and persist the replacement while this witness and its scheduler reminder are still held.
        // A fail-closed index or state write then leaves the firing in place for another callback.
        _ = await ConvergeCoreAsync(
            actorId,
            key,
            state,
            etag,
            target,
            current,
            now,
            scheduler,
            cancellationToken,
            new HashSet<string>(StringComparer.Ordinal) { entry.ReminderName }).ConfigureAwait(false);

        if (!await TryCancelAsync(scheduler, actorId, entry.ReminderName, entry.ReminderName, cancellationToken).ConfigureAwait(false))
        {
            (ReminderItemState? held, string? heldETag) = await LoadAsync(key, actorId, cancellationToken).ConfigureAwait(false);
            if (held is null)
            {
                return ReminderDisposition.Retrying;
            }

            ReminderItemState retained = WithEntries(
                held,
                held.Entries.Select(candidate => string.Equals(candidate.ReminderName, entry.ReminderName, StringComparison.Ordinal)
                    ? candidate with
                    {
                        Status = ReminderEntryStatus.Retrying,
                        Attempts = candidate.Attempts + 1,
                        LastReasonCode = "cancel-failed",
                        UpdatedAt = now,
                    }
                    : candidate),
                held.Quarantine);
            (ReminderItemState? persistedRetry, _) = await PersistAsync(key, retained, heldETag, cancellationToken).ConfigureAwait(false);
            _status.RecordItem(actorId, CountUnresolved(persistedRetry), CountQuarantined(persistedRetry));
            return ReminderDisposition.Retrying;
        }

        ReminderLog.Stale(_logger, actorId, entry.ReminderName);
        (ReminderItemState? latest, string? latestETag) = await LoadAsync(key, actorId, cancellationToken).ConfigureAwait(false);
        if (latest is null)
        {
            return ReminderDisposition.Stale;
        }

        ReminderItemState next = WithEntries(
            latest,
            latest.Entries.Where(e => !string.Equals(e.ReminderName, entry.ReminderName, StringComparison.Ordinal)),
            latest.Quarantine);
        (ReminderItemState? persisted, _) = await PersistAsync(key, next, latestETag, cancellationToken).ConfigureAwait(false);
        int unresolved = CountUnresolved(persisted);
        if (persisted is null || !HasWork(persisted))
        {
            unresolved += await ReleaseDiscoveryIfIdleAsync(actorId, target, cancellationToken).ConfigureAwait(false);
        }

        _status.RecordItem(actorId, unresolved, CountQuarantined(persisted));
        return ReminderDisposition.Stale;
    }

    private async Task<ReminderDisposition> SettleCallbackAsync(
        string key,
        ReminderItemState state,
        string? etag,
        string actorId,
        ReminderEntry entry,
        ReminderSubmissionOutcome outcome,
        DateTimeOffset now,
        IReminderScheduler scheduler,
        CancellationToken cancellationToken)
    {
        var toCancel = new List<string>();
        var toRearm = new List<(string Name, TimeSpan DueTime)>();
        (ReminderEntry? settled, ReminderDisposition applied) = await SettleAsync(
            state, actorId, entry, outcome, now, scheduler, toCancel, toRearm, cancellationToken)
            .ConfigureAwait(false);
        ReminderItemState next = WithEntries(
            state,
            state.Entries
                .Select(e => string.Equals(e.ReminderName, entry.ReminderName, StringComparison.Ordinal) ? settled : e)
                .OfType<ReminderEntry>(),
            state.Quarantine);
        (ReminderItemState? persisted, _) = await PersistAsync(key, next, etag, cancellationToken).ConfigureAwait(false);
        await RunSchedulerAsync(scheduler, actorId, toCancel, toRearm, cancellationToken).ConfigureAwait(false);
        int unresolved = CountUnresolved(persisted);
        if (persisted is null || !HasWork(persisted))
        {
            unresolved += await ReleaseDiscoveryIfIdleAsync(actorId, new ReminderTarget(state.Tenant, state.Domain, state.Aggregate), cancellationToken)
                .ConfigureAwait(false);
        }

        _status.RecordItem(actorId, unresolved, CountQuarantined(persisted));
        return applied;
    }

    private async Task<(ReminderEntry? Entry, ReminderDisposition Applied)> SettleAsync(
        ReminderItemState state,
        string actorId,
        ReminderEntry entry,
        ReminderSubmissionOutcome outcome,
        DateTimeOffset now,
        IReminderScheduler scheduler,
        List<string> toCancel,
        List<(string Name, TimeSpan DueTime)> toRearm,
        CancellationToken cancellationToken)
    {
        switch (outcome.Disposition)
        {
            case ReminderDisposition.Submitted:
                // Audit before release: without a durable disposition the witness stays and is retried; the
                // deterministic effect identity makes the retry replay the same target receipt.
                if (await TryWriteDispositionAsync(state, actorId, entry.ReminderName, ReminderDisposition.Submitted, outcome.ReasonCode, outcome.EffectId, outcome.Receipt, entry.Attempts, now, cancellationToken)
                    .ConfigureAwait(false))
                {
                    ReminderLog.Submitted(
                        _logger,
                        actorId,
                        entry.ReminderName,
                        outcome.EffectId ?? string.Empty,
                        outcome.Receipt?.Disposition.ToString() ?? string.Empty,
                        outcome.Receipt?.Replayed ?? false);
                    if (await TryCancelAsync(scheduler, actorId, entry.ReminderName, entry.ReminderName, cancellationToken)
                        .ConfigureAwait(false))
                    {
                        return (null, ReminderDisposition.Submitted);
                    }

                    return Retry(entry, "cancel-failed", now, toRearm, actorId);
                }

                return Retry(entry, "audit-unavailable", now, toRearm, actorId);

            case ReminderDisposition.Quarantined:
                if (await TryWriteDispositionAsync(state, actorId, entry.ReminderName, ReminderDisposition.Quarantined, outcome.ReasonCode, outcome.EffectId, null, entry.Attempts, now, cancellationToken)
                    .ConfigureAwait(false))
                {
                    ReminderLog.Quarantined(_logger, actorId, entry.ReminderName, outcome.ReasonCode);
                    toCancel.Add(entry.ReminderName);
                    return (Quarantine(entry, outcome.ReasonCode, now), ReminderDisposition.Quarantined);
                }

                return Retry(entry, "audit-unavailable", now, toRearm, actorId);

            default:
                int attempts = entry.Attempts + 1;
                _ = await TryWriteDispositionAsync(state, actorId, entry.ReminderName, outcome.Disposition, outcome.ReasonCode, outcome.EffectId, null, attempts, now, cancellationToken)
                    .ConfigureAwait(false);
                if (outcome.Disposition == ReminderDisposition.Denied)
                {
                    ReminderLog.Denied(_logger, actorId, entry.ReminderName, outcome.ReasonCode);
                }

                (ReminderEntry? retried, _) = Retry(entry, outcome.ReasonCode, now, toRearm, actorId);
                return (retried, outcome.Disposition);
        }
    }

    private (ReminderEntry? Entry, ReminderDisposition Applied) Retry(
        ReminderEntry entry,
        string reasonCode,
        DateTimeOffset now,
        List<(string Name, TimeSpan DueTime)> toRearm,
        string actorId)
    {
        int attempts = entry.Attempts + 1;
        ReminderLog.Retrying(_logger, actorId, entry.ReminderName, reasonCode, attempts);
        toRearm.Add((entry.ReminderName, Backoff(attempts)));
        return (
            entry with { Status = ReminderEntryStatus.Retrying, Attempts = attempts, LastReasonCode = reasonCode, UpdatedAt = now },
            ReminderDisposition.Retrying);
    }

    private async Task<ReminderSubmissionOutcome> SubmitAsync(
        ReminderItemState state,
        ReminderEntry entry,
        ReminderIntent intent,
        CancellationToken cancellationToken)
    {
        if (!TryGetPurpose(entry.Kind, out string purpose))
        {
            return Outcome(ReminderDisposition.Denied, "purpose-unconfigured");
        }

        if (string.IsNullOrWhiteSpace(_options.Workload))
        {
            return Outcome(ReminderDisposition.Denied, "workload-unconfigured");
        }

        ReminderCommand? command;
        try
        {
            command = _source.TranslateDueIntent(intent);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Translation is pure; a failure is malformed evidence and is retained, not dropped or retried hot.
            return Outcome(ReminderDisposition.Quarantined, "translation-failed");
        }

        if (command is null || string.IsNullOrWhiteSpace(command.CommandType) || command.Payload is null)
        {
            return Outcome(ReminderDisposition.Quarantined, "translation-invalid");
        }

        EffectIdentity identity = CreateEffectIdentity(state.Tenant, state.Domain, state.Aggregate, intent);
        string effectId;
        try
        {
            effectId = EffectIdentityCodec.ComputeEffectId(identity);
        }
        catch (ArgumentException)
        {
            return Outcome(ReminderDisposition.Quarantined, "effect-identity-invalid");
        }

        if (_submitter is null)
        {
            return new ReminderSubmissionOutcome(ReminderDisposition.Retrying, "submitter-unavailable", effectId, null);
        }

        string messageId = "wrk-" + effectId;
        // Causation follows the logical effect, not its replaceable schedule witness, so a committed
        // receipt still matches after a same-source reschedule or an uncertain response.
        var submission = new TrustedEffectSubmission(identity, command.CommandType, command.Payload, messageId, messageId);
        string? delegation = null;
        if (_delegationTokenProvider is not null)
        {
            try
            {
                delegation = await _delegationTokenProvider
                    .GetDelegationTokenAsync(
                        new ReminderDelegationRequest(submission, _options.Workload, purpose, messageId),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                return new ReminderSubmissionOutcome(ReminderDisposition.Retrying, "delegation-failed", effectId, null);
            }
        }

        if (string.IsNullOrWhiteSpace(delegation))
        {
            return new ReminderSubmissionOutcome(ReminderDisposition.Retrying, "delegation-unavailable", effectId, null);
        }

        TrustedEffectResult? receipt;
        try
        {
            receipt = await _submitter
                .SubmitAsync(submission, new TrustedEffectContext(_options.Workload, purpose, messageId, delegation), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // The target may or may not hold a receipt. Retrying with the same effect identity replays it.
            ReminderLog.SubmissionUncertain(
                _logger,
                ReminderIdentityCodec.ComputeActorId(state.Tenant, state.Aggregate),
                entry.ReminderName,
                exception.GetType().Name);
            return new ReminderSubmissionOutcome(ReminderDisposition.Retrying, "submission-uncertain", effectId, null);
        }

        if (receipt is null
            || !string.Equals(receipt.EffectId, effectId, StringComparison.Ordinal)
            || !Enum.IsDefined(receipt.Disposition))
        {
            return new ReminderSubmissionOutcome(ReminderDisposition.Retrying, "receipt-mismatch", effectId, null);
        }

        return new ReminderSubmissionOutcome(ReminderDisposition.Submitted, "receipt", effectId, receipt);
    }

    /// <summary>
    /// Drops the discovery candidate only when a fresh fold reports no current intents. A null or failed fold,
    /// or a stream that still reports work, keeps the candidate so reconciliation can converge it again.
    /// </summary>
    /// <returns>One unresolved item when the fold is unavailable or discovery cannot be ensured; zero otherwise.</returns>
    private async Task<int> ReleaseDiscoveryIfIdleAsync(string actorId, ReminderTarget target, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(target.Domain))
        {
            return 1;
        }

        IReadOnlyList<ReminderIntent>? intents;
        try
        {
            intents = await _source.GetCurrentIntentsAsync(target, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            ReminderLog.FailedClosed(_logger, actorId, "source-unavailable", exception.GetType().Name);
            return 1;
        }

        if (intents is null || intents.Count > 0)
        {
            try
            {
                await _index.EnsureCandidateAsync(target, actorId, cancellationToken).ConfigureAwait(false);
            }
            catch (ReminderFailClosedException exception)
            {
                ReminderLog.FailedClosed(_logger, actorId, exception.ReasonCode, exception.GetType().Name);
                return 1;
            }

            return intents is null ? 1 : 0;
        }

        await _index.RemoveCandidateAsync(target, actorId, cancellationToken).ConfigureAwait(false);
        return 0;
    }

    private async Task<(ReminderItemState? State, string? ETag)> LoadAsync(
        string key,
        string actorId,
        CancellationToken cancellationToken)
    {
        ReadModelEntry<ReminderItemState> entry = await _store
            .GetAsync<ReminderItemState>(_options.StateStoreName, key, cancellationToken)
            .ConfigureAwait(false);
        if (entry.Value is null)
        {
            return (null, null);
        }

        ReminderItemState loaded = entry.Value;
        DateTimeOffset now = _time.GetUtcNow();
        var candidateEntries = new List<(ReminderEntry Entry, int Ordinal)>();
        var validQuarantine = new List<ReminderQuarantineRecord>();
        bool repaired = loaded.Entries is null || loaded.Quarantine is null;

        int ordinal = 0;
        foreach (ReminderEntry? candidate in loaded.Entries ?? [])
        {
            if (TryValidatePersistedEntry(loaded, actorId, candidate))
            {
                candidateEntries.Add((candidate!, ordinal));
            }
            else
            {
                repaired = true;
                string digest = EvidenceDigest(candidate, ordinal);
                validQuarantine.Add(new ReminderQuarantineRecord(
                    digest,
                    "stored-entry-invalid",
                    candidate is not null && ReminderIdentityCodec.TryParseReminderName(candidate.ReminderName, out _, out _)
                        ? candidate.ReminderName
                        : null,
                    now));
            }

            ordinal++;
        }

        var duplicateOrdinals = new HashSet<int>(candidateEntries
            .GroupBy(static candidate => candidate.Entry.ReminderName, StringComparer.Ordinal)
            .Where(static group => group.Count() > 1)
            .SelectMany(static group => group.Select(static candidate => candidate.Ordinal)));

        var validEntries = new List<ReminderEntry>();
        foreach ((ReminderEntry candidate, int candidateOrdinal) in candidateEntries)
        {
            if (!duplicateOrdinals.Contains(candidateOrdinal))
            {
                validEntries.Add(candidate);
                continue;
            }

            repaired = true;
            validQuarantine.Add(new ReminderQuarantineRecord(
                EvidenceDigest(candidate, candidateOrdinal),
                "stored-entry-duplicate",
                candidate.ReminderName,
                now));
        }

        ordinal = 0;
        foreach (ReminderQuarantineRecord? candidate in loaded.Quarantine ?? [])
        {
            if (IsValidPersistedQuarantine(candidate))
            {
                validQuarantine.Add(candidate!);
            }
            else
            {
                repaired = true;
                validQuarantine.Add(new ReminderQuarantineRecord(
                    EvidenceDigest(candidate, ordinal),
                    "stored-quarantine-invalid",
                    null,
                    now));
            }

            ordinal++;
        }

        ReminderItemState normalized = WithEntries(loaded, validEntries, validQuarantine);
        return repaired
            ? await PersistAsync(key, normalized, entry.ETag, cancellationToken).ConfigureAwait(false)
            : (normalized, entry.ETag);
    }

    private async Task<(ReminderItemState? State, string? ETag)> PersistAsync(
        string key,
        ReminderItemState state,
        string? etag,
        CancellationToken cancellationToken)
    {
        if (!HasWork(state))
        {
            if (etag is not null
                && !await _eraser.TryEraseAsync(_options.StateStoreName, key, etag, cancellationToken).ConfigureAwait(false))
            {
                throw new ReminderFailClosedException("state-conflict");
            }

            return (null, null);
        }

        ReminderItemState versioned = state with { Version = state.Version + 1 };
        if (!await _store
            .TrySaveAsync(_options.StateStoreName, key, versioned, etag ?? string.Empty, cancellationToken)
            .ConfigureAwait(false))
        {
            throw new ReminderFailClosedException("state-conflict");
        }

        // The store returns no ETag from a write; re-read it and prove the write was ours before chaining.
        ReadModelEntry<ReminderItemState> reread = await _store
            .GetAsync<ReminderItemState>(_options.StateStoreName, key, cancellationToken)
            .ConfigureAwait(false);
        if (reread.Value is null || reread.Value.Version != versioned.Version || reread.ETag is null)
        {
            throw new ReminderFailClosedException("state-conflict");
        }

        return (versioned, reread.ETag);
    }

    private async Task<bool> TryWriteDispositionAsync(
        ReminderItemState state,
        string actorId,
        string subject,
        ReminderDisposition disposition,
        string reasonCode,
        string? effectId,
        TrustedEffectResult? receipt,
        int attempts,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var record = new ReminderDispositionRecord(
            state.Tenant,
            actorId,
            subject,
            disposition,
            reasonCode,
            effectId,
            receipt?.Disposition,
            receipt?.Replayed ?? false,
            attempts,
            now);
        try
        {
            await _store
                .SaveAsync(_options.StateStoreName, ReminderStateKeys.Disposition(_options.ActorTypeName, actorId, subject), record, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            ReminderLog.AuditWriteFailed(_logger, actorId, subject, exception.GetType().Name);
            return false;
        }
    }

    private async Task RunSchedulerAsync(
        IReminderScheduler scheduler,
        string actorId,
        List<string> toCancel,
        List<(string Name, TimeSpan DueTime)> toRearm,
        CancellationToken cancellationToken)
    {
        foreach (string name in toCancel)
        {
            await TryCancelAsync(scheduler, actorId, name, name, cancellationToken).ConfigureAwait(false);
        }

        foreach ((string name, TimeSpan dueTime) in toRearm)
        {
            try
            {
                await scheduler.ArmAsync(name, dueTime, _options.RetryMaxDelay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // The retained witness keeps the work discoverable; the next pass reissues it.
                ReminderLog.ArmFailed(_logger, actorId, name, exception.GetType().Name);
            }
        }
    }

    private async Task<bool> IsHeldAsync(IReminderScheduler scheduler, string actorId, string reminderName, CancellationToken cancellationToken)
    {
        try
        {
            return await scheduler.IsArmedAsync(reminderName, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // An unanswerable lookup re-arms: registering the same deterministic name again is idempotent.
            ReminderLog.LookupFailed(_logger, actorId, reminderName, exception.GetType().Name);
            return false;
        }
    }

    private async Task<bool> TryCancelAsync(
        IReminderScheduler scheduler,
        string actorId,
        string reminderName,
        string loggedName,
        CancellationToken cancellationToken)
    {
        try
        {
            await scheduler.CancelAsync(reminderName, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            ReminderLog.CancelFailed(_logger, actorId, loggedName, exception.GetType().Name);
            return false;
        }
    }
}
