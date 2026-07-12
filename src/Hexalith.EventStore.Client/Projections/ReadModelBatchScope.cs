using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// Stable identity scope for a coordinated read-model batch.
/// </summary>
/// <remarks>
/// The scope binds a batch to a single store component and a single logical projection delivery. Its
/// components are hashed into an opaque, collision-resistant marker namespace so no raw tenant data or
/// payload content ever appears in a marker key. <paramref name="BatchId"/> is a stable, caller-supplied
/// message/ULID identity — never a value generated fresh per retry — and is validated only as a
/// non-whitespace token (consistent with the platform's aggregate-identity rules); it is never parsed
/// with <c>Guid.TryParse</c>.
/// </remarks>
/// <param name="StoreName">The DAPR state-store component name. All operations target this one store.</param>
/// <param name="TenantId">The owning tenant identifier.</param>
/// <param name="Domain">The domain name.</param>
/// <param name="AggregateId">The aggregate identifier the projection delivery is for.</param>
/// <param name="ProjectionType">The projection type discriminator.</param>
/// <param name="BatchId">The stable caller-supplied batch identity (ULID/message id).</param>
public sealed record ReadModelBatchScope(
    string StoreName,
    string TenantId,
    string Domain,
    string AggregateId,
    string ProjectionType,
    string BatchId) {

    /// <summary>The maximum accepted UTF-8 byte length of any single scope component.</summary>
    public const int MaxComponentByteLength = 512;

    /// <summary>
    /// Validates every scope component (non-whitespace and within the byte limit). Called by
    /// <see cref="ReadModelBatch"/> construction so an invalid identity fails before any state access.
    /// </summary>
    /// <exception cref="ArgumentException">A component is null, whitespace, or exceeds the byte limit.</exception>
    public void Validate() {
        ValidateComponent(StoreName, nameof(StoreName));
        ValidateComponent(TenantId, nameof(TenantId));
        ValidateComponent(Domain, nameof(Domain));
        ValidateComponent(AggregateId, nameof(AggregateId));
        ValidateComponent(ProjectionType, nameof(ProjectionType));
        ValidateComponent(BatchId, nameof(BatchId));
    }

    /// <summary>
    /// Computes the opaque, culture-invariant scope hash used to derive marker/receipt keys. The
    /// components are length-prefixed before hashing so distinct component boundaries cannot collide.
    /// </summary>
    /// <returns>A base64url-encoded SHA-256 digest of the length-delimited scope components.</returns>
    public string ComputeScopeHash() {
        var builder = new StringBuilder(256);
        AppendComponent(builder, StoreName);
        AppendComponent(builder, TenantId);
        AppendComponent(builder, Domain);
        AppendComponent(builder, AggregateId);
        AppendComponent(builder, ProjectionType);
        AppendComponent(builder, BatchId);
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Base64Url(digest);
    }

    private static void AppendComponent(StringBuilder builder, string value) =>
        builder.Append(value.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(value);

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static void ValidateComponent(string value, string name) {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, name);
        if (Encoding.UTF8.GetByteCount(value) > MaxComponentByteLength) {
            throw new ArgumentException(
                $"Read-model batch scope component '{name}' exceeds {MaxComponentByteLength} UTF-8 bytes.",
                name);
        }
    }
}
