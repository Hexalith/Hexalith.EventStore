using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.DomainService.DurableTasks;

/// <summary>Admits one task per authorized scope and atomically consumes its optional confirmation artifact.</summary>
/// <param name="store">The EventStore conditional state-store interface.</param>
/// <param name="storeName">An isolated store component providing strong reads and first-write CAS.</param>
/// <param name="authorization">The host-owned current authority and operation policy.</param>
/// <param name="timeProvider">The UTC clock used for confirmation expiry.</param>
/// <remarks>The host constructs this service only for the authenticated admission and confirmation components. This type does not expose an HTTP route or register a runtime on its own.</remarks>
public sealed class DurableTaskAdmission(
    IReadModelStore store,
    string storeName,
    IDurableTaskAdmissionAuthorizer authorization,
    TimeProvider timeProvider)
{
    private const int MaxWrites = 8;
    private const string AdmissionKeyPrefix = "durable-task-admission-v1-";
    private const string IndexKeyPrefix = "durable-task-id-v1-";
    private static readonly TimeSpan ConfirmationLifetime = TimeSpan.FromMinutes(15);

    private readonly IReadModelStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly string _storeName = !string.IsNullOrWhiteSpace(storeName) ? storeName : throw new ArgumentException("A state component is required.", nameof(storeName));
    private readonly IDurableTaskAdmissionAuthorizer _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
    private readonly TimeProvider _time = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <summary>Issues an opaque token to a currently authorized interactive caller.</summary>
    public async Task<string?> IssueConfirmationAsync(
        DurableTaskScope scope,
        string requestSha256,
        CancellationToken cancellationToken = default)
    {
        string key = ValidateAndGetKey(scope, requestSha256);
        for (int attempt = 0; attempt < MaxWrites; attempt++)
        {
            DurableTaskAdmissionAuthority decision = await AuthorizeAsync(scope, requestSha256, cancellationToken).ConfigureAwait(false);
            if (!decision.Allowed || !decision.RequiresConfirmation || !IsSha256(decision.ConfirmationBindingSha256))
            {
                return null;
            }

            ReadModelEntry<DurableTaskAdmissionState> entry = await ReadAsync(scope, key, cancellationToken).ConfigureAwait(false);
            if (entry.Value?.Task is not null)
            {
                return null;
            }

            if (await AuthorizeAsync(scope, requestSha256, cancellationToken).ConfigureAwait(false) != decision)
            {
                return null;
            }

            byte[] token = RandomNumberGenerator.GetBytes(32);
            var confirmation = new DurableTaskConfirmationState(
                Hash(token), requestSha256, decision.ConfirmationBindingSha256!, _time.GetUtcNow() + ConfirmationLifetime);
            var next = new DurableTaskAdmissionState(scope, confirmation, null);
            if (await TrySaveAsync(key, entry, next, cancellationToken).ConfigureAwait(false))
            {
                return Convert.ToHexStringLower(token);
            }
        }

        throw new InvalidOperationException("Durable Task confirmation contention exceeded the retry budget.");
    }

    /// <summary>Admits the current intent or reauthorizes and returns its original committed task.</summary>
    public async Task<DurableTaskAdmissionResult> AdmitAsync(
        DurableTaskScope scope,
        string requestSha256,
        string? confirmationToken = null,
        CancellationToken cancellationToken = default)
    {
        string key = ValidateAndGetKey(scope, requestSha256);
        string? taskId = null;
        for (int attempt = 0; attempt < MaxWrites; attempt++)
        {
            DurableTaskAdmissionAuthority decision = await AuthorizeAsync(scope, requestSha256, cancellationToken).ConfigureAwait(false);
            if (!decision.Allowed)
            {
                return new(DurableTaskAdmissionOutcome.Denied);
            }

            ReadModelEntry<DurableTaskAdmissionState> entry = await ReadAsync(scope, key, cancellationToken).ConfigureAwait(false);
            decision = await AuthorizeAsync(scope, requestSha256, cancellationToken).ConfigureAwait(false);
            if (!decision.Allowed)
            {
                return new(DurableTaskAdmissionOutcome.Denied);
            }

            if (entry.Value?.Task is { } original)
            {
                return original.RequestSha256 == requestSha256
                    ? new(DurableTaskAdmissionOutcome.Admitted, original)
                    : new(DurableTaskAdmissionOutcome.Conflict);
            }

            DurableTaskConfirmationState? confirmation = entry.Value?.Confirmation;
            DateTimeOffset now = _time.GetUtcNow();
            if (decision.RequiresConfirmation && !Matches(confirmation, confirmationToken, requestSha256, decision.ConfirmationBindingSha256, now))
            {
                return new(DurableTaskAdmissionOutcome.RenewConfirmation);
            }

            taskId ??= await ReserveTaskIdAsync(key, cancellationToken).ConfigureAwait(false);
            var task = new CommittedDurableTaskAdmission(taskId, scope, requestSha256, now);
            var next = new DurableTaskAdmissionState(
                scope, decision.RequiresConfirmation ? confirmation! with { ConsumedBy = task.TaskId } : null, task);
            if (await TrySaveAsync(key, entry, next, cancellationToken).ConfigureAwait(false))
            {
                return new(DurableTaskAdmissionOutcome.Admitted, task);
            }
        }

        throw new InvalidOperationException("Durable Task admission contention exceeded the retry budget.");
    }

    /// <summary>Reads committed admission by exact authorized scope and normalized intent.</summary>
    public async Task<CommittedDurableTaskAdmission?> GetCommittedAsync(
        DurableTaskScope scope,
        string requestSha256,
        CancellationToken cancellationToken = default)
        => (await ReadAuthorizedAdmissionAsync(scope, requestSha256, cancellationToken).ConfigureAwait(false))?.Task;

    /// <summary>Reads a committed task's progress under current authority for the exact scope and intent.</summary>
    public async Task<DurableTaskProgress?> GetProgressAsync(
        DurableTaskScope scope,
        string requestSha256,
        CancellationToken cancellationToken = default)
    {
        DurableTaskAdmissionState? state = await ReadAuthorizedAdmissionAsync(scope, requestSha256, cancellationToken).ConfigureAwait(false);
        return state is null ? null : state.Execution?.Progress ?? DurableTaskExecutionState.InitialProgress;
    }

    /// <summary>Reads a committed task by identifier under current authority for its original scope and intent.</summary>
    /// <param name="taskId">The platform-generated task identifier.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    /// <returns>The committed task and progress, or null when the task is absent or the caller lacks current authority.</returns>
    public async Task<DurableTaskSnapshot?> GetStatusAsync(string taskId, CancellationToken cancellationToken = default)
    {
        if (await ResolveCommittedAsync(taskId, cancellationToken).ConfigureAwait(false) is not { } task)
        {
            return null;
        }

        DurableTaskAdmissionState? state = await ReadAuthorizedAdmissionAsync(task.Scope, task.RequestSha256, cancellationToken).ConfigureAwait(false);
        return state?.Task == task ? new(task, state.Execution?.Progress ?? DurableTaskExecutionState.InitialProgress) : null;
    }

    /// <summary>Cancels a task before its irreversible checkpoint under current authority for its original scope and intent.</summary>
    /// <param name="taskId">The platform-generated task identifier.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    /// <returns>The cancellation outcome. A conflict carries the committed status and changes nothing.</returns>
    /// <remarks>Cancellation clears the worker lease, so the fenced worker cannot advance. Committed receipts are retained.</remarks>
    public async Task<DurableTaskCancellationResult> CancelAsync(string taskId, CancellationToken cancellationToken = default)
    {
        if (await GetStatusAsync(taskId, cancellationToken).ConfigureAwait(false) is not { } current)
        {
            return new(DurableTaskCancellationOutcome.NotFound);
        }

        DurableTaskExecutionState next = await UpdateExecutionAsync(current.Task, static state =>
            DurableTaskExecutionState.CanCancel(state.Progress)
                ? state with
                {
                    Owner = null,
                    ExpiresAt = null,
                    Progress = state.Progress with { Status = DurableTaskStatus.Cancelled },
                }
                : state, cancellationToken).ConfigureAwait(false);
        return new(
            next.Progress.Status == DurableTaskStatus.Cancelled ? DurableTaskCancellationOutcome.Cancelled : DurableTaskCancellationOutcome.Conflict,
            new DurableTaskSnapshot(current.Task, next.Progress));
    }

    /// <summary>Resolves a reserved task identifier to its committed admission for a trusted worker.</summary>
    /// <remarks>This lookup does not authorize a caller. Owner effects reauthorize the original actor.</remarks>
    internal async Task<CommittedDurableTaskAdmission?> ResolveCommittedAsync(string taskId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        cancellationToken.ThrowIfCancellationRequested();
        ReadModelEntry<DurableTaskIndexState> index = await _store
            .GetAsync<DurableTaskIndexState>(_storeName, IndexKey(taskId), cancellationToken).ConfigureAwait(false);
        if (index.Value is not { } reservation)
        {
            return null;
        }

        if (reservation.TaskId != taskId || reservation.AdmissionKey?.StartsWith(AdmissionKeyPrefix, StringComparison.Ordinal) != true)
        {
            throw new InvalidOperationException("The persisted Durable Task index is inconsistent.");
        }

        ReadModelEntry<DurableTaskAdmissionState> entry = await _store
            .GetAsync<DurableTaskAdmissionState>(_storeName, reservation.AdmissionKey, cancellationToken).ConfigureAwait(false);
        if (entry.Value is not { } state)
        {
            return null;
        }

        EnsureConsistent(entry, state.Scope);
        if (state.Task is not { } task || task.TaskId != taskId)
        {
            return null;
        }

        return ValidateAndGetKey(state.Scope, task.RequestSha256) == reservation.AdmissionKey ? task
            : throw new InvalidOperationException("The persisted Durable Task admission is inconsistent.");
    }

    /// <summary>Updates only the execution portion of the exact committed admission using the admission CAS boundary.</summary>
    internal async Task<DurableTaskExecutionState> UpdateExecutionAsync(
        CommittedDurableTaskAdmission task,
        Func<DurableTaskExecutionState, DurableTaskExecutionState> update,
        CancellationToken cancellationToken)
    {
        string key = ValidateAndGetKey(task.Scope, task.RequestSha256);
        for (int attempt = 0; attempt < MaxWrites; attempt++)
        {
            ReadModelEntry<DurableTaskAdmissionState> entry = await ReadAsync(task.Scope, key, cancellationToken).ConfigureAwait(false);
            if (entry.Value is not { Task: { } persisted } state || persisted != task)
            {
                throw new InvalidOperationException("The Durable Task admission could not be verified.");
            }

            DurableTaskExecutionState current = state.Execution ?? DurableTaskExecutionState.Initial;
            DurableTaskExecutionState next = update(current);
            if (next == current || await TrySaveAsync(key, entry, state with { Execution = next }, cancellationToken).ConfigureAwait(false))
            {
                return next;
            }
        }

        throw new InvalidOperationException("Durable Task execution contention exceeded the retry budget.");
    }

    private async Task<DurableTaskAdmissionState?> ReadAuthorizedAdmissionAsync(
        DurableTaskScope scope,
        string requestSha256,
        CancellationToken cancellationToken)
    {
        string key = ValidateAndGetKey(scope, requestSha256);
        if (!(await AuthorizeAsync(scope, requestSha256, cancellationToken).ConfigureAwait(false)).Allowed)
        {
            return null;
        }

        ReadModelEntry<DurableTaskAdmissionState> entry = await ReadAsync(scope, key, cancellationToken).ConfigureAwait(false);
        return entry.Value?.Task is { } task && task.RequestSha256 == requestSha256
            && (await AuthorizeAsync(scope, requestSha256, cancellationToken).ConfigureAwait(false)).Allowed ? entry.Value : null;
    }

    private async ValueTask<DurableTaskAdmissionAuthority> AuthorizeAsync(
        DurableTaskScope scope, string requestSha256, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await _authorization.AuthorizeAsync(scope, requestSha256, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads and validates the shared admission and execution row.</summary>
    internal static async Task<ReadModelEntry<DurableTaskAdmissionState>> ReadAsync(
        IReadModelStore store, string storeName, DurableTaskScope scope, string key, CancellationToken cancellationToken)
    {
        ReadModelEntry<DurableTaskAdmissionState> entry = await store
            .GetAsync<DurableTaskAdmissionState>(storeName, key, cancellationToken).ConfigureAwait(false);
        EnsureConsistent(entry, scope);
        return entry;
    }

    private Task<ReadModelEntry<DurableTaskAdmissionState>> ReadAsync(
        DurableTaskScope scope, string key, CancellationToken cancellationToken)
        => ReadAsync(_store, _storeName, scope, key, cancellationToken);

    private static void EnsureConsistent(ReadModelEntry<DurableTaskAdmissionState> entry, DurableTaskScope scope)
    {
        if (entry.Value is { } value &&
            (string.IsNullOrEmpty(entry.ETag) || value.Scope is null || value.Scope != scope || value.Task is { Scope: var bound } && bound != scope
                || value.Confirmation is { ConsumedBy: var consumedBy } && consumedBy != value.Task?.TaskId
                || value.Execution is not null && (value.Task is null || !DurableTaskExecutionState.IsConsistent(value.Execution))))
        {
            throw new InvalidOperationException("The persisted Durable Task admission is inconsistent.");
        }
    }

    private async Task<string> ReserveTaskIdAsync(string admissionKey, CancellationToken cancellationToken)
    {
        string taskId = UniqueIdHelper.GenerateSortableUniqueStringId();
        return await _store.TrySaveAsync(
                _storeName, IndexKey(taskId), new DurableTaskIndexState(taskId, admissionKey), string.Empty, cancellationToken)
            .ConfigureAwait(false)
            ? taskId
            : throw new InvalidOperationException("The Durable Task identifier could not be reserved.");
    }

    private Task<bool> TrySaveAsync(string key, ReadModelEntry<DurableTaskAdmissionState> entry,
        DurableTaskAdmissionState next, CancellationToken cancellationToken)
        => _store.TrySaveAsync(_storeName, key, next, entry.ETag ?? string.Empty, cancellationToken);

    private static bool Matches(DurableTaskConfirmationState? state, string? token, string requestSha256,
        string? bindingSha256, DateTimeOffset now)
        => state is { ConsumedBy: null }
            && state.ExpiresAt > now
            && state.RequestSha256 == requestSha256
            && IsSha256(bindingSha256)
            && state.BindingSha256 == bindingSha256
            && IsSha256(token)
            && IsSha256(state.TokenSha256)
            && CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(state.TokenSha256), SHA256.HashData(Convert.FromHexString(token!)));

    /// <summary>Validates the original scope and intent and returns their admission row key.</summary>
    internal static string ValidateAndGetKey(DurableTaskScope scope, string requestSha256)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (!IsScopedValue(scope.TenantId) || !IsScopedValue(scope.ActorId)
            || !IsScopedValue(scope.Operation) || !IsScopedValue(scope.IdempotencyKey))
        {
            throw new ArgumentException("Durable Task scope values must be nonempty and at most 256 characters.", nameof(scope));
        }

        if (!IsSha256(requestSha256))
        {
            throw new ArgumentException("The normalized intent requires a lowercase SHA-256 digest.", nameof(requestSha256));
        }

        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new[] { scope.TenantId, scope.ActorId, scope.Operation, scope.IdempotencyKey });
        return AdmissionKeyPrefix + Hash(bytes);
    }

    private static string IndexKey(string taskId) => IndexKeyPrefix + Hash(Encoding.UTF8.GetBytes(taskId));

    private static bool IsScopedValue(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 256;

    /// <summary>Checks for a canonical lowercase SHA-256 digest.</summary>
    internal static bool IsSha256(string? value)
        => value is { Length: 64 } && value.All(static c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    /// <summary>Computes a canonical lowercase SHA-256 digest.</summary>
    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
