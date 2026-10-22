using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Client.Streams;

/// <summary>Bounded private atomic append/lookup adapter, disabled without real qualified owner transaction/current credentials; no target append implementation or production registration is fabricated.</summary>
public sealed class DirectoryAtomicAppendClient(TimeProvider clock, IAtomicDirectoryAppendOwner? owner = null, IDirectoryAtomicAppendAuthority? authority = null)
{
    /// <summary>Attempts only an exact independently admitted owner transaction; caller token has precedence over all failures.</summary>
    public Task<DirectoryAtomicAppendOutcome> AppendAsync(DirectoryAtomicAppendRequest request, CancellationToken cancellationToken = default)
        => ExecuteAsync(request, false, cancellationToken);
    /// <summary>Looks up exact original immutable outcome without a new transaction; retained outcome read requires current private lookup authority.</summary>
    public Task<DirectoryAtomicAppendOutcome> LookupAsync(DirectoryAtomicAppendRequest request, CancellationToken cancellationToken = default)
        => ExecuteAsync(request, true, cancellationToken);
    private async Task<DirectoryAtomicAppendOutcome> ExecuteAsync(DirectoryAtomicAppendRequest request, bool lookup, CancellationToken caller)
    {
        caller.ThrowIfCancellationRequested();
        using var deadline = new AuthoritativeStreamReadDeadline(TimeSpan.FromSeconds(30), clock, caller, clock.GetTimestamp());
        using var lifetime = new DirectoryAtomicAppendPayloadLifetime();
        var owned = Capture(request, deadline, lifetime); string id = owned.Command.MessageId; string digest = RequestDigest(owned);
        var unavailable = new DirectoryAtomicAppendOutcome(id, digest, DirectoryAtomicAppendState.Unavailable, 0, 0, 0, null);
        try
        {
            if (owner is null || authority is null) { deadline.ThrowIfCancellationRequested(); return unavailable; }
            string method = lookup ? "LookupDirectoryAtomicAppend" : "CommitDirectoryAtomicAppend";
            if (!await deadline.ReadAsync(token => authority.AuthorizeAsync(owned, method, digest, token), lifetime.BorrowUntil).ConfigureAwait(false)) { deadline.ThrowIfCancellationRequested(); return unavailable; }
            var outcome = await deadline.ReadAsync(token => lookup ? owner.LookupAsync(owned, digest, token) : owner.TryAppendAsync(owned, digest, token), lifetime.BorrowUntil).ConfigureAwait(false);
            deadline.ThrowIfCancellationRequested();
            if (outcome is null || outcome.OperationId != id || outcome.RequestDigest != digest || !Enum.IsDefined(outcome.State)
                || outcome.State == DirectoryAtomicAppendState.Accepted && (outcome.CommittedTargetRevision <= owned.ExpectedStreamRevision
                    || outcome.AcceptedAtAdmissionFenceOrdinal < 0 || outcome.AcceptedAtGuardHighWater <= 0)
                || outcome.State != DirectoryAtomicAppendState.Accepted && (outcome.CommittedTargetRevision != 0 || outcome.AcceptedAtAdmissionFenceOrdinal != 0 || outcome.AcceptedAtGuardHighWater != 0)
                || outcome.State is DirectoryAtomicAppendState.Accepted or DirectoryAtomicAppendState.Rejected && !ValidText(outcome.AuthenticatedReceiptId)) { return unavailable; }
            if (!await deadline.ReadAsync(token => authority.VerifyOutcomeAsync(owned, digest, outcome, token), lifetime.BorrowUntil).ConfigureAwait(false)
                || !await deadline.ReadAsync(token => authority.AuthorizeAsync(owned, method, digest, token), lifetime.BorrowUntil).ConfigureAwait(false)) { deadline.ThrowIfCancellationRequested(); return unavailable; }
            deadline.ThrowIfCancellationRequested(); return outcome;
        }
        catch (OperationCanceledException) { caller.ThrowIfCancellationRequested(); return unavailable; }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or ArgumentException or JsonException)
        { caller.ThrowIfCancellationRequested(); return unavailable; }
        catch (Exception) { caller.ThrowIfCancellationRequested(); throw; }
    }
    /// <summary>Hashes only exact safe identity fields and the retained tenant-keyed content fingerprint; never unkeyed command content.</summary>
    public static string RequestDigest(DirectoryAtomicAppendRequest request)
    {
        ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(request.Command);
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {
        request.Command.MessageId, request.Command.TenantId, request.Command.Domain, request.Command.AggregateId, request.Command.CommandType,
        request.EpochId, request.Kind, request.ExpectedStreamRevision, request.PermitOwner, request.SourceConversationId, request.PermitId,
        request.DirectoryCapabilityId, request.CapabilityOwnerRevision, request.ContentIntentHmac, request.DigestKeyVersion })));
    }
    internal static DirectoryAtomicAppendRequest Capture(DirectoryAtomicAppendRequest request, AuthoritativeStreamReadDeadline deadline, DirectoryAtomicAppendPayloadLifetime lifetime)
    {
        ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(request.Command); ArgumentNullException.ThrowIfNull(request.PermitOwner);
        var command = request.Command;
        foreach (var field in new[] { command.MessageId, command.TenantId, command.Domain, command.AggregateId, command.CommandType, command.CorrelationId, command.UserId,
            request.EpochId, request.SourceConversationId, request.PermitId, request.DirectoryCapabilityId, request.DigestKeyVersion }) { if (!ValidText(field)) { throw new ArgumentException("Malformed private directory append identity."); } }
        if (command.CausationId is not null && !ValidText(command.CausationId)) { throw new ArgumentException("Malformed private directory append causation."); }
        if (request.ExpectedStreamRevision < 0 || request.CapabilityOwnerRevision <= 0 || request.PermitOwner.TenantId != command.TenantId || !Enum.IsDefined(request.Kind)
            || request.ContentIntentHmac is not { Length: 64 } || request.ContentIntentHmac.Any(c => c is not (>= '0' and <= '9' or >= 'A' and <= 'F'))
            || command.Payload is null || command.Payload.Length > 8 * 1024 * 1024 || command.Extensions?.Count > 64) { throw new ArgumentException("Malformed private directory append."); }
        byte[] payload = lifetime.Capture(command.Payload); deadline.ThrowIfCancellationRequested(); Dictionary<string, string>? extensions = null;
        if (command.Extensions is not null)
        {
            extensions = new(StringComparer.Ordinal);
            foreach (var pair in command.Extensions)
            { deadline.ThrowIfCancellationRequested(); if (extensions.Count >= 64 || !ValidText(pair.Key) || !ValidText(pair.Value) || !extensions.TryAdd(pair.Key, pair.Value)) { throw new ArgumentException("Malformed private directory extensions."); } }
        }
        deadline.ThrowIfCancellationRequested(); return request with { Command = command with { Payload = payload, Extensions = extensions } };
    }
    private static bool ValidText(string? s)
    { try { return !string.IsNullOrWhiteSpace(s) && s.Length <= 2048 && new UTF8Encoding(false, true).GetByteCount(s) <= 2048; } catch (EncoderFallbackException) { return false; } }
}
