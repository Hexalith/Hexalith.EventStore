namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// Opt-in, ETag-conditional single-key erase capability for persisted read models.
/// </summary>
/// <remarks>
/// <para>
/// This is an <b>additive</b> companion to <see cref="IReadModelStore"/>: the released
/// <see cref="IReadModelStore"/> contract (read / save / conditional-save) is unchanged, and a
/// third-party implementation that has not opted into erasure keeps binary and source compatibility.
/// Coordinated projection erasure resolves this capability before any mutation and reports
/// <c>Unsupported</c> when the registered store has not opted in.
/// </para>
/// <para>
/// The same concrete instance also implements <see cref="IReadModelStore"/>; the platform registers one
/// singleton behind both interfaces (see <c>AddEventStoreReadModelStore</c>).
/// </para>
/// </remarks>
public interface IReadModelConditionalEraser {
    /// <summary>
    /// Attempts to erase a read-model value under optimistic concurrency (first-write-wins).
    /// </summary>
    /// <param name="storeName">The DAPR state-store component name.</param>
    /// <param name="key">The state key.</param>
    /// <param name="etag">The expected ETag.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> when the value was erased or was already absent (idempotent, regardless of
    /// the supplied ETag); <see langword="false"/> when a present value has a different ETag. Never throws
    /// for either the absent-key or the ETag-conflict case.
    /// </returns>
    Task<bool> TryEraseAsync(
        string storeName,
        string key,
        string etag,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the current ETag of a read-model value without deserializing (or knowing) its value type.
    /// </summary>
    /// <remarks>
    /// The returned ETag mirrors the store's own visibility/ETag read, so it can be passed straight back to
    /// <see cref="TryEraseAsync"/> for a first-write-wins conditional erase and read-back classification. The
    /// coordinated eraser uses this to detect an absent target (skip), a matching target (erase), or a newer
    /// concurrent value (conflict) without disclosing the stored value.
    /// </remarks>
    /// <param name="storeName">The DAPR state-store component name.</param>
    /// <param name="key">The state key.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>
    /// <c>(true, etag)</c> when a value is present; <c>(false, "")</c> when the value is absent.
    /// </returns>
    Task<(bool Present, string Etag)> TryReadEtagAsync(
        string storeName,
        string key,
        CancellationToken cancellationToken = default);
}
