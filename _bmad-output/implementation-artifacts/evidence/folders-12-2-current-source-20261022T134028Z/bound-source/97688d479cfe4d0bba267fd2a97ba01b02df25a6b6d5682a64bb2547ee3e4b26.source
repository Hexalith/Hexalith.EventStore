using System.Text.Json;
using System.Text.Json.Serialization;

using Hexalith.Tenants.Contracts.Enums;

namespace Hexalith.Tenants.Contracts.Serialization;

public sealed class TenantStatusJsonConverter : JsonConverter<TenantStatus> {
    public override TenantStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if (reader.TokenType == JsonTokenType.String) {
            string? value = reader.GetString();
            return Enum.TryParse(value, ignoreCase: false, out TenantStatus status) && Enum.IsDefined(status)
                ? status
                : TenantStatus.Unknown;
        }

        reader.Skip();
        return TenantStatus.Unknown;
    }

    public override void Write(Utf8JsonWriter writer, TenantStatus value, JsonSerializerOptions options) {
        ArgumentNullException.ThrowIfNull(writer);
        string name = Enum.GetName(value) ?? nameof(TenantStatus.Unknown);
        writer.WriteStringValue(name);
    }
}
