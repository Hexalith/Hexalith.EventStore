using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Streams;
using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Gateway;

public sealed class BoundOwnerQueryProofTests
{
    [Theory]
    [InlineData(200, true)]
    [InlineData(201, true)]
    [InlineData(256, true)]
    [InlineData(257, false)]
    public void AggregateBoundIs256ForBothProofFamiliesWithoutTruncation(int length, bool accepted)
    {
        using RSA key = RSA.Create(2048);
        string privateKey = Convert.ToBase64String(key.ExportPkcs8PrivateKey());
        string publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var owner = Request(new string('a', length));
        var stream = new StreamReadRequest("tenant", "domain", owner.Query.AggregateId);
        if (!accepted)
        {
            Should.Throw<ArgumentException>(() => BoundOwnerQueryProof.Issue(owner, "projects", privateKey, now));
            Should.Throw<ArgumentException>(() => BoundStreamReadProof.Issue(stream, "projects", privateKey, now));
            string valid = BoundOwnerQueryProof.Issue(Request(new string('a', 256)), "projects", privateKey, now);
            BoundOwnerQueryProof.Verify(owner, valid, "projects", publicKey, now).ShouldBeFalse();
            return;
        }
        string proof = BoundOwnerQueryProof.Issue(owner, "projects", privateKey, now);
        BoundOwnerQueryProof.Verify(owner, proof, "projects", publicKey, now).ShouldBeTrue();
        BoundOwnerQueryProof.Verify(owner with { Query = owner.Query with { AggregateId = owner.Query.AggregateId + "b" } }, proof, "projects", publicKey, now).ShouldBeFalse();
        string readProof = BoundStreamReadProof.Issue(stream, "projects", privateKey, now);
        BoundStreamReadProof.Verify(readProof, stream, "projects", publicKey, now).ShouldBeTrue();
        BoundStreamReadProof.Verify(readProof, stream with { AggregateId = stream.AggregateId + "b" }, "projects", publicKey, now).ShouldBeFalse();
        BoundStreamReadProof.Verify(readProof, stream, "projects", publicKey, now.AddSeconds(30)).ShouldBeFalse();
        BoundOwnerQueryProof.Verify(owner, proof, "projects", publicKey, now.AddSeconds(30)).ShouldBeFalse();
    }

    [Theory]
    [InlineData("principal", 200, true)]
    [InlineData("principal", 201, false)]
    [InlineData("tenant", 200, true)]
    [InlineData("tenant", 201, false)]
    [InlineData("domain", 200, true)]
    [InlineData("domain", 201, false)]
    public void PrincipalTenantAndDomainProofBoundsStay200(string field, int length, bool accepted)
    {
        using RSA key = RSA.Create(2048);
        string privateKey = Convert.ToBase64String(key.ExportPkcs8PrivateKey());
        string publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
        var request = Request(new string('a', 256));
        request = field switch
        {
            "principal" => request with { PrincipalId = new string('p', length) },
            "tenant" => request with { Query = request.Query with { Tenant = new string('t', length) } },
            _ => request with { Query = request.Query with { Domain = new string('d', length) } },
        };
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (accepted)
        {
            string proof = BoundOwnerQueryProof.Issue(request, "projects", privateKey, now);
            BoundOwnerQueryProof.Verify(request, proof, "projects", publicKey, now).ShouldBeTrue();
        }
        else
        {
            Should.Throw<ArgumentException>(() => BoundOwnerQueryProof.Issue(request, "projects", privateKey, now));
            BoundOwnerQueryProof.Verify(request, "invalid", "projects", publicKey, now).ShouldBeFalse();
        }
        if (field != "principal")
        {
            var stream = new StreamReadRequest(request.Query.Tenant, request.Query.Domain, request.Query.AggregateId);
            if (accepted) BoundStreamReadProof.Verify(BoundStreamReadProof.Issue(stream, "projects", privateKey, now), stream, "projects", publicKey, now).ShouldBeTrue();
            else Should.Throw<ArgumentException>(() => BoundStreamReadProof.Issue(stream, "projects", privateKey, now));
        }
        // AggregateIdentity's independent 64-character tenant/domain limits do not inherit proof bounds.
        Should.Throw<ArgumentException>(() => new AggregateIdentity("tenant", new string('d', 65), new string('a', 256)));
    }

    private static BoundOwnerQueryRequest Request(string aggregate) => new(
        new("tenant", "domain", aggregate, "Read", Payload: JsonSerializer.SerializeToElement(new { })), "actor");
}
