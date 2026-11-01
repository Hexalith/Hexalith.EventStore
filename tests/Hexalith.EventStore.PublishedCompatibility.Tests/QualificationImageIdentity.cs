using System.Text.Json;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Binds a daemon's observed local image identity to the approved immutable Redis reference.</summary>
internal static class QualificationImageIdentity
{
    /// <summary>Limits image inspection to actual ID and immutable repository digests, never environment/configuration secrets.</summary>
    internal const string Format = """[{"Id":{{json .Id}},"RepoDigests":{{json .RepoDigests}}}]""";

    internal static string RequireApprovedEngineId(JsonElement observation)
    {
        string digest = PublishedCluster.RedisImage.Split('@')[1];
        string[] approved = ["redis@" + digest, "library/redis@" + digest, "docker.io/library/redis@" + digest];
        if (observation.ValueKind != JsonValueKind.Array || observation.GetArrayLength() != 1)
            throw new InvalidOperationException("The selected immutable image requires its one actual daemon observation.");
        var image = observation[0]; string? id = image.GetProperty("Id").GetString();
        if (id is not { Length: 71 } || !id.StartsWith("sha256:", StringComparison.Ordinal) || !id[7..].All(char.IsAsciiHexDigitLower)
            || image.GetProperty("RepoDigests").ValueKind != JsonValueKind.Array
            || !image.GetProperty("RepoDigests").EnumerateArray().Any(value => approved.Contains(value.GetString(), StringComparer.Ordinal)))
            throw new InvalidOperationException("The actual local image is not bound to the approved immutable repository digest.");
        return id;
    }
}
