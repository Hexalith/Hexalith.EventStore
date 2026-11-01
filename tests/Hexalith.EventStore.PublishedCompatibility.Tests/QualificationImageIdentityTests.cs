using System.Text.Json;
using System.Text.Json.Nodes;

using Shouldly;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>Separates actual daemon image identity from its immutable repository reference.</summary>
public sealed class QualificationImageIdentityTests
{
    /// <summary>Both configuration-addressed and containerd descriptor-addressed stores retain their genuine inspected ID.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ApprovedImmutableReferenceRetainsActualDaemonIdentity(bool descriptorId)
    {
        string id = descriptorId ? PublishedCluster.RedisImage.Split('@')[1] : "sha256:" + new string('a', 64);
        var actual = JsonSerializer.SerializeToElement(new[] { new { Id = id, RepoDigests = new[] { "docker.io/library/" + PublishedCluster.RedisImage } } });
        QualificationImageIdentity.RequireApprovedEngineId(actual).ShouldBe(id);
    }

    /// <summary>No tag-only, foreign repository, malformed ID or unrelated manifest may satisfy the immutable selection.</summary>
    [Theory]
    [InlineData("id")]
    [InlineData("repository")]
    [InlineData("digest")]
    [InlineData("absent-repository")]
    [InlineData("multiple-images")]
    public void UnboundImageObservationsRefuse(string mutation)
    {
        var image = JsonSerializer.SerializeToNode(new[] { new { Id = "sha256:" + new string('a', 64), RepoDigests = new[] { PublishedCluster.RedisImage } } })!.AsArray();
        if (mutation == "id") image[0]!["Id"] = "unqualified";
        if (mutation == "repository") image[0]!["RepoDigests"]![0] = "foreign.example/redis@" + PublishedCluster.RedisImage.Split('@')[1];
        if (mutation == "digest") image[0]!["RepoDigests"]![0] = "redis@sha256:" + new string('b', 64);
        if (mutation == "absent-repository") image[0]!["RepoDigests"] = new JsonArray();
        if (mutation == "multiple-images") image.Add(image[0]!.DeepClone());
        Should.Throw<InvalidOperationException>(() => QualificationImageIdentity.RequireApprovedEngineId(JsonSerializer.SerializeToElement(image)));
    }
}
