using System.Runtime.Serialization;
using System.Text.Json;
using System.Xml.Linq;
using Hexalith.EventStore.Contracts.Queries;
using Shouldly;

namespace Hexalith.EventStore.Contracts.Tests.Queries;

public sealed class QueryCallerAuthorityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OriginatingContextCopiesCollectionsAndSurvivesJsonAndDataContractEnvelopeRoundTrips(bool absent)
    {
        string[] scopes = ["read", "resolve"], audiences = ["gateway", "projects"];
        var origin = new OriginatingQueryContext("actor", "original-client", true, "delegation",
            absent ? null : scopes, absent ? null : audiences);
        var expected = new OriginatingQueryContext("actor", "original-client", true, "delegation",
            absent ? null : ["read", "resolve"], absent ? null : ["gateway", "projects"]);
        scopes[0] = "write"; audiences[0] = "other";
        if (!absent)
        {
            ((string[])origin.Scopes!)[0] = "admin";
            ((string[])origin.Audience!)[0] = "other";
        }
        origin.ShouldBe(expected); origin.GetHashCode().ShouldBe(expected.GetHashCode());
        var query = new QueryEnvelope("tenant", "folders", "folder", "Read", [], "corr", "actor")
        {
            OriginalActorId = "actor", AuthenticatedWorkloadId = "projects", IsDelegated = true,
            Audience = ["eventstore"], CallerAuthority = new("delegated-owner-query", [], ["read_metadata"]) { OriginatingContext = origin },
        };
        QueryEnvelope json = JsonSerializer.Deserialize<QueryEnvelope>(JsonSerializer.Serialize(query, JsonSerializerOptions.Web), JsonSerializerOptions.Web)!;
        json.CallerAuthority.ShouldBe(query.CallerAuthority);
        json.AuthenticatedWorkloadId.ShouldBe("projects"); json.Audience.ShouldBe(["eventstore"]);
        var serializer = new DataContractSerializer(typeof(QueryEnvelope));
        using var stream = new MemoryStream(); serializer.WriteObject(stream, query); stream.Position = 0;
        QueryEnvelope data = (QueryEnvelope)serializer.ReadObject(stream)!;
        data.CallerAuthority.ShouldBe(query.CallerAuthority);
        data.CallerAuthority!.OriginatingContext!.Scopes.ShouldBe(absent ? null : new[] { "read", "resolve" });
        (query with { CallerAuthority = query.CallerAuthority with { OriginatingContext = origin with { DelegationId = "other" } } }).ShouldNotBe(query);
    }

    [Fact]
    public void AbsentOriginatingContextPreservesExactLegacyAuthorityBytes()
    {
        var authority = new QueryCallerAuthority("authenticated-caller", ["reader"], ["read"]);
        JsonSerializer.Serialize(authority, JsonSerializerOptions.Web).ShouldBe(
            "{\"origin\":\"authenticated-caller\",\"roles\":[\"reader\"],\"permissions\":[\"read\"]}");
        var legacy = JsonSerializer.Deserialize<QueryCallerAuthority>("{\"origin\":\"authenticated-caller\",\"roles\":[],\"permissions\":[]}", JsonSerializerOptions.Web)!;
        legacy.OriginatingContext.ShouldBeNull();
        var serializer = new DataContractSerializer(typeof(QueryCallerAuthority));
        using var stream = new MemoryStream();
        serializer.WriteObject(stream, authority); stream.Position = 0;
        XElement xml = XElement.Load(stream);
        xml.Elements().Select(static member => member.Name.LocalName).ShouldBe(["Origin", "Permissions", "Roles"]);
        xml.Descendants().Any(static member => member.Name.LocalName == "OriginatingContext").ShouldBeFalse();
    }

    [Fact]
    public void AuthorityCopiesInputsAndExposedListsAndComparesOrdinalSequenceContent()
    {
        string[] roles = ["reader", "operator"], permissions = ["read", "inspect"];
        var authority = new QueryCallerAuthority("authenticated-caller", roles, permissions);
        var expected = new QueryCallerAuthority("authenticated-caller", ["reader", "operator"], ["read", "inspect"]);
        roles[0] = "admin"; permissions[0] = "write";
        ((string[])authority.Roles)[0] = "admin"; ((string[])authority.Permissions)[0] = "write";
        authority.ShouldBe(expected); authority.GetHashCode().ShouldBe(expected.GetHashCode());
        (authority with { Roles = ["operator", "reader"] }).ShouldNotBe(expected);
        var query = new QueryEnvelope("tenant", "projects", "project", "read", [], "corr", "actor") { CallerAuthority = authority };
        QueryEnvelope second = query with { CallerAuthority = expected };
        query.ShouldBe(second); query.GetHashCode().ShouldBe(second.GetHashCode());
        JsonSerializer.Deserialize<QueryCallerAuthority>(JsonSerializer.Serialize(authority, JsonSerializerOptions.Web), JsonSerializerOptions.Web).ShouldBe(authority);
        var serializer = new DataContractSerializer(typeof(QueryCallerAuthority));
        using var stream = new MemoryStream(); serializer.WriteObject(stream, authority); stream.Position = 0;
        ((QueryCallerAuthority)serializer.ReadObject(stream)!).ShouldBe(authority);
    }
}
