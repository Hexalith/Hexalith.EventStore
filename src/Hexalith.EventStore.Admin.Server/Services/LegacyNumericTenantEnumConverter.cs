using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Admin.Server.Services;

/// <summary>
/// Deserializes tenant-domain enums (<c>TenantStatus</c>, <c>TenantRole</c>) emitted by the tenant
/// query pipeline, accepting BOTH wire formats those actors can produce:
/// <list type="bullet">
///   <item>string by member name (the canonical format, e.g. <c>"Active"</c> / <c>"TenantOwner"</c>);</item>
///   <item>legacy numeric values that predate the <c>Unknown = 0</c> sentinel inserted at ordinal 0.</item>
/// </list>
/// Legacy numeric payloads were zero-based over the business members only, so a wire
/// number <c>N</c> maps to the N-th business member (the defined members excluding a member named
/// <c>Unknown</c>, when present): for
/// <c>TenantStatus</c> <c>0 -&gt; Active</c>, <c>1 -&gt; Disabled</c>; for <c>TenantRole</c>
/// <c>0 -&gt; TenantOwner</c>, <c>1 -&gt; TenantContributor</c>, <c>2 -&gt; TenantReader</c>. Unparseable
/// strings and out-of-range numbers fail closed to <c>Unknown</c> when the enum defines it, otherwise
/// to the enum's default value.
/// </summary>
/// <typeparam name="TEnum">The tenant-domain enum type whose ordinal 0 member is the <c>Unknown</c> sentinel.</typeparam>
internal sealed class LegacyNumericTenantEnumConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum {
    // Business members in declaration order, excluding Unknown by name instead of ordinal.
    // Published Tenants packages may still define Active/TenantOwner at ordinal 0.
    // Index in this array == the legacy zero-based numeric wire value.
    private static readonly TEnum[] _businessMembers =
        [.. Enum.GetValues<TEnum>().Where(static v => !string.Equals(Enum.GetName(v), "Unknown", StringComparison.Ordinal))];

    private static readonly TEnum _default =
        Enum.TryParse("Unknown", ignoreCase: false, out TEnum unknown) && Enum.IsDefined(unknown)
            ? unknown
            : default;

    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        switch (reader.TokenType) {
            case JsonTokenType.String:
                string? name = reader.GetString();
                return Enum.TryParse(name, ignoreCase: false, out TEnum parsed) && Enum.IsDefined(parsed)
                    ? parsed
                    : _default;

            case JsonTokenType.Number:
                if (reader.TryGetInt32(out int wire) && wire >= 0 && wire < _businessMembers.Length) {
                    return _businessMembers[wire];
                }

                return _default;

            default:
                reader.Skip();
                return _default;
        }
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options) {
        ArgumentNullException.ThrowIfNull(writer);

        // Canonical serialization is by member name, matching the tenant-domain enum contract.
        writer.WriteStringValue(Enum.GetName(value) ?? Enum.GetName(_default));
    }
}
