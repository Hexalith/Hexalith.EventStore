using System.Runtime.Serialization;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Queries;
using Shouldly;

namespace Hexalith.EventStore.Contracts.Tests.Queries;

public sealed class QueryCallerAuthorityTests
{
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
