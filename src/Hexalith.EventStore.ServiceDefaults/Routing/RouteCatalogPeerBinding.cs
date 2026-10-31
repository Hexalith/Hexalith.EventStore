using Microsoft.AspNetCore.Http;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Checks an existing peer-generation boundary, adding the complete activation identity only for opted-in v2 snapshots.</summary>
public static class RouteCatalogPeerBinding
{
    /// <summary>Requires exactly one current root/epoch and, when present in the snapshot, one complete run binding.</summary>
    public static bool Matches(IHeaderDictionary headers, CommittedRouteCatalogSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(headers); ArgumentNullException.ThrowIfNull(snapshot);
        return headers[RouteCatalogInvocationHandler.RootHeader].Count == 1 && headers[RouteCatalogInvocationHandler.RootHeader][0] == snapshot.RootDigest
            && headers[RouteCatalogInvocationHandler.EpochHeader].Count == 1
            && headers[RouteCatalogInvocationHandler.EpochHeader][0] == snapshot.Epoch.ToString(System.Globalization.CultureInfo.InvariantCulture)
            && (snapshot.ActivationBindingDigest is null || headers[RouteCatalogInvocationHandler.BindingHeader].Count == 1
                && headers[RouteCatalogInvocationHandler.BindingHeader][0] == snapshot.ActivationBindingDigest);
    }
}
