using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Contracts.Queries;

/// <summary>
/// Converts projection lifecycle values using exact canonical names and fail-safe reads.
/// </summary>
public sealed class ProjectionLifecycleStateJsonConverter : JsonConverter<ProjectionLifecycleState>
{
    /// <inheritdoc />
    public override bool HandleNull => true;

    /// <inheritdoc />
    public override ProjectionLifecycleState Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
            {
                reader.Skip();
            }

            return ProjectionLifecycleState.Unknown;
        }

        return reader.GetString() switch
        {
            nameof(ProjectionLifecycleState.Current) => ProjectionLifecycleState.Current,
            nameof(ProjectionLifecycleState.Stale) => ProjectionLifecycleState.Stale,
            nameof(ProjectionLifecycleState.Rebuilding) => ProjectionLifecycleState.Rebuilding,
            nameof(ProjectionLifecycleState.Degraded) => ProjectionLifecycleState.Degraded,
            nameof(ProjectionLifecycleState.Unavailable) => ProjectionLifecycleState.Unavailable,
            nameof(ProjectionLifecycleState.LocalOnly) => ProjectionLifecycleState.LocalOnly,
            _ => ProjectionLifecycleState.Unknown,
        };
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        ProjectionLifecycleState value,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStringValue(value switch
        {
            ProjectionLifecycleState.Current => nameof(ProjectionLifecycleState.Current),
            ProjectionLifecycleState.Stale => nameof(ProjectionLifecycleState.Stale),
            ProjectionLifecycleState.Rebuilding => nameof(ProjectionLifecycleState.Rebuilding),
            ProjectionLifecycleState.Degraded => nameof(ProjectionLifecycleState.Degraded),
            ProjectionLifecycleState.Unavailable => nameof(ProjectionLifecycleState.Unavailable),
            ProjectionLifecycleState.LocalOnly => nameof(ProjectionLifecycleState.LocalOnly),
            _ => nameof(ProjectionLifecycleState.Unknown),
        });
    }
}
