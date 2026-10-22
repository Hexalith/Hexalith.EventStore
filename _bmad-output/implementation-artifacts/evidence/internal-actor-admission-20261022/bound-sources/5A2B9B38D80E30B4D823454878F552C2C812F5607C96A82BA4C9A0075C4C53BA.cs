using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.EventStore.Contracts.Queries;

using Microsoft.AspNetCore.DataProtection;

namespace Hexalith.EventStore.Client.Queries;

/// <summary>
/// Data Protection backed implementation of <see cref="IQueryCursorCodec"/>.
/// </summary>
/// <remarks>
/// The purpose isolates one domain's cursors from another's: cursors are sealed with a protector derived
/// from it, so a cursor minted under one purpose cannot be unprotected under another. Register one codec
/// per domain via <c>AddEventStoreQueryCursorCodec</c> with a stable, domain-unique purpose (changing it
/// later invalidates all outstanding cursors, which is a safe failure).
/// </remarks>
public sealed class QueryCursorCodec : IQueryCursorCodec {
    private const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions s_jsonOptions = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IDataProtector _protector;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryCursorCodec"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">Data Protection provider used to create the cursor protector.</param>
    /// <param name="purpose">Stable, domain-unique Data Protection purpose for cursor isolation.</param>
    public QueryCursorCodec(IDataProtectionProvider dataProtectionProvider, string purpose) {
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        _protector = dataProtectionProvider.CreateProtector(purpose);
    }

    /// <inheritdoc/>
    public string Encode(string queryType, string scope, string position) {
        ArgumentException.ThrowIfNullOrWhiteSpace(queryType);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(position);

        var payload = new QueryCursorPayload(
            CurrentVersion,
            queryType,
            scope,
            position,
            DateTimeOffset.UtcNow);

        return _protector.Protect(JsonSerializer.Serialize(payload, s_jsonOptions));
    }

    /// <inheritdoc/>
    public bool TryDecode(string? cursor, string queryType, string scope, out string? position, out string? failureReason) {
        ArgumentException.ThrowIfNullOrWhiteSpace(queryType);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        position = null;
        failureReason = null;
        if (string.IsNullOrWhiteSpace(cursor)) {
            return true;
        }

        if (cursor.Length > QueryPolicyLimits.MaxCursorLength) {
            failureReason = "too-large";
            return false;
        }

        try {
            string json = _protector.Unprotect(cursor);
            QueryCursorPayload? payload = JsonSerializer.Deserialize<QueryCursorPayload>(json, s_jsonOptions);
            if (payload is null) {
                failureReason = "malformed";
                return false;
            }

            if (payload.Version != CurrentVersion) {
                failureReason = "wrong-version";
                return false;
            }

            // Cursor v1 intentionally has no wall-clock lifetime. It remains valid while the Data
            // Protection key can unprotect it; tampering or key rotation is the safe invalidation path.

            if (!string.Equals(payload.QueryType, queryType, StringComparison.Ordinal)) {
                failureReason = "wrong-query-type";
                return false;
            }

            if (!string.Equals(payload.Scope, scope, StringComparison.Ordinal)) {
                failureReason = "wrong-scope";
                return false;
            }

            if (string.IsNullOrWhiteSpace(payload.Position)) {
                failureReason = "empty-position";
                return false;
            }

            position = payload.Position;
            return true;
        }
        catch (CryptographicException) {
            // Unprotect failure: payload was tampered with, produced for a different protector, or signed with a rotated-out key.
            failureReason = "tamper-or-key-rotation";
            return false;
        }
        catch (JsonException) {
            failureReason = "malformed";
            return false;
        }
    }

    private sealed record QueryCursorPayload(
        int Version,
        string QueryType,
        string Scope,
        string Position,
        DateTimeOffset IssuedAt);
}
