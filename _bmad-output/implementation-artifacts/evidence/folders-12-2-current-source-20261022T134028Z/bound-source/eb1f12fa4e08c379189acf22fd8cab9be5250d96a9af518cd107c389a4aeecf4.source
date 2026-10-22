using System.Text.Json;

namespace Hexalith.EventStore.Contracts.Serialization;

/// <summary>
/// Single shared payload-binding serializer options for every EventStore reader path.
/// The command, rehydrate, projection and pub/sub paths all bind persisted or wire payloads
/// through <see cref="Options"/> so casing or converter drift cannot silently produce an
/// empty, default-constructed payload on one path while the others bind correctly.
/// </summary>
/// <remarks>
/// <para>
/// No persisted or wire payload <em>writer</em> uses these options: the two payload writers deliberately
/// keep their current behaviour, so PascalCase payload bytes are already at rest and must stay readable.
/// <see cref="JsonSerializerDefaults.Web"/> sets <c>PropertyNameCaseInsensitive = true</c>, which accepts
/// both the PascalCase bytes at rest and the camelCase bytes a normal API client submits. Widening a
/// reader like this is always safe; narrowing it is not.
/// </para>
/// <para>
/// Besides payload binding, the same instance backs the serialization halves of in-process round trips
/// that must agree with those readers — the rehydrator's arbitrary-snapshot <c>SerializeToElement</c> pass
/// and the replay engine's <c>StateJson</c> rendering. Both are diagnostic or in-memory shapes, never
/// persisted event bytes.
/// </para>
/// <para>
/// <see cref="Options"/> is made read-only during initialization so no consumer can mutate the
/// shared instance and silently change binding behaviour for every other path.
/// </para>
/// </remarks>
public static class EventStorePayloadSerialization {
    /// <summary>
    /// Gets the read-only serializer options every EventStore payload reader binds through.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = CreateReadOnlyOptions();

    private static JsonSerializerOptions CreateReadOnlyOptions() {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web);

        // populateMissingResolver: true installs the same default reflection-based resolver the options
        // would have acquired on first use. Without it, MakeReadOnly() rejects options that have no
        // resolver yet. Reflection-based dispatch is load-bearing here, so AOT/trimming is out of scope.
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
