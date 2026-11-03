using System.Security.Cryptography;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Streams;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Streams;

/// <summary>Records which authority the existing resource proof can and cannot restrict.</summary>
public sealed class NativeSourceMetadataScopeContractTests
{
    /// <summary>The existing proof authorizes one stream, rather than a metadata-only observation of that stream.</summary>
    [Theory]
    [InlineData("range")]
    [InlineData("page-size")]
    [InlineData("native-proof")]
    public void ExistingResourceProofCannotEstablishMetadataOnlyEnrollment(string change)
    {
        var metadata = new StreamReadRequest("tenant-a", "folders", "organization-a",
            FromSequence: 0, ToSequence: 0, PageSize: 1) { RequireNativeProof = true };
        StreamReadRequest changed = change switch
        {
            "range" => metadata with { ToSequence = 10 },
            "page-size" => metadata with { PageSize = 100 },
            _ => metadata with { RequireNativeProof = false },
        };
        using RSA key = RSA.Create(2048);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string proof = BoundStreamReadProof.Issue(metadata, "folders-source",
            Convert.ToBase64String(key.ExportPkcs8PrivateKey()), now);
        string publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());

        BoundStreamReadProof.Verify(proof, changed, "folders-source", publicKey, now).ShouldBeTrue();
        BoundStreamReadProof.Verify(proof, changed with { AggregateId = "other-aggregate" },
            "folders-source", publicKey, now).ShouldBeFalse();
        BoundStreamReadProof.Verify(proof, changed, "other-caller", publicKey, now).ShouldBeFalse();
    }
}
