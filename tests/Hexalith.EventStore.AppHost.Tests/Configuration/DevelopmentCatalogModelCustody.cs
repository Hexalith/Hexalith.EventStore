using System.Security.Cryptography;

using Hexalith.EventStore.Aspire;

namespace Hexalith.EventStore.AppHost.Tests.Configuration;

/// <summary>Explicit fresh owner material for model-only tests; it makes no claim about a running Redis backend.</summary>
internal static class DevelopmentCatalogModelCustody
{
    internal static DevelopmentRouteCatalogCompositionOptions Create(string directory)
        => new(Path.Combine(directory, "owner-custody"), "routing-owner", "catalog-authority", "development-v1",
            new Dictionary<string, string> { ["development-v1"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) }, "development-v1");
}
