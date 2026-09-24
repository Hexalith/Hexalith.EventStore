using System.Security.Cryptography;

namespace Hexalith.EventStore.Admin.UI.E2E;

/// <summary>Supplies disposable development credentials for the standalone UI hosts.</summary>
internal static class E2EAuthenticationSettings
{
    public static Dictionary<string, string?> Create()
        => new()
        {
            ["EventStore:Authentication:Issuer"] = "hexalith-e2e",
            ["EventStore:Authentication:Audience"] = "hexalith-eventstore",
            ["EventStore:Authentication:SigningKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            ["EventStore:Authentication:Subject"] = "e2e-user",
            ["EventStore:Authentication:GlobalAdmin"] = "true",
            ["EventStore:Authentication:Tenants:0"] = "tenant-a",
            ["EventStore:Authentication:Domains:0"] = "counter",
            ["EventStore:Authentication:Permissions:0"] = "admin:read",
            ["EventStore:Authentication:Permissions:1"] = "admin:write",
        };
}
