#if FLOOR
using Hexalith.EventStore.Client.Queries;
using Microsoft.AspNetCore.DataProtection;

namespace PublishedConsumer;

/// <summary>Executes the actual cursor codec with all authoritative scope coordinates and negative bindings.</summary>
public static class CursorFixture
{
    public static object Execute()
    {
        var codec = new QueryCursorCodec(new EphemeralDataProtectionProvider(), "p1r-counter");
        string scope = Scope(987);
        string cursor = codec.Encode("get-counter", scope, "987");
        bool decoded = codec.TryDecode(cursor, "get-counter", scope, out string? position, out _);
        bool changed = !codec.TryDecode(cursor, "get-counter", Scope(988), out _, out _);
        bool foreignCaller = !codec.TryDecode(cursor, "get-counter", Scope(987, caller: "other"), out _, out _);
        bool foreignTenant = !codec.TryDecode(cursor, "get-counter", Scope(987, tenant: "tenant-b"), out _, out _);
        bool foreignDomain = !codec.TryDecode(cursor, "get-counter", Scope(987, domain: "other"), out _, out _);
        bool changedFilter = !codec.TryDecode(cursor, "get-counter", Scope(987, filter: "other"), out _, out _);
        bool changedQuery = !codec.TryDecode(cursor, "other-query", scope, out _, out _);
        string tampered = cursor[..10] + (cursor[10] == 'A' ? 'B' : 'A') + cursor[11..];
        bool tamperDenied = !codec.TryDecode(tampered, "get-counter", scope, out _, out _);
        int unknownDenied = 0;
        foreach (long? watermark in new long?[] { null, 0, -1 })
        {
            try { _ = QueryCursorScope.Create().AddProjectionWatermark(watermark); }
            catch (ArgumentOutOfRangeException) { unknownDenied++; }
        }
        return new { supported = true, decoded, position, changed, foreignCaller, foreignTenant, foreignDomain, changedFilter,
            changedQuery, tamperDenied, unknownDenied, identities = ConsumerIdentity.Read() };
    }

    private static string Scope(long watermark, string caller = "actor", string tenant = "tenant-a", string domain = "counter", string filter = "active")
        => QueryCursorScope.Create().Add("tenant", tenant).Add("domain", domain).Add("caller", caller).Add("filter", filter)
            .Add("query", "get-counter").AddProjectionWatermark(watermark).Build();
}
#endif
