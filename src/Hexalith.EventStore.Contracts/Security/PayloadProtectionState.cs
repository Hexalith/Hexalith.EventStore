namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Provider-neutral protection state for event payloads and snapshot state.
/// </summary>
public enum PayloadProtectionState {
    /// <summary>
    /// Payload bytes are domain plaintext. The default <see cref="IEventPayloadProtectionService"/> no-op
    /// implementation returns this state, and legacy records with no protection metadata are mapped here.
    /// </summary>
    Unprotected = 0,

    /// <summary>
    /// Payload bytes have been transformed by a protection provider that EventStore can identify by
    /// <see cref="EventStorePayloadProtectionMetadata.Scheme"/>. EventStore never inspects or interprets
    /// the protected bytes outside the registered protection service.
    /// </summary>
    Protected = 1,

    /// <summary>
    /// Payload bytes were produced by a protection provider EventStore cannot interpret. Opaque bytes
    /// must be carried verbatim and never decrypted, plain-text published, or rebuilt from outside the
    /// registered protection service.
    /// </summary>
    ProviderOpaque = 2,
}
