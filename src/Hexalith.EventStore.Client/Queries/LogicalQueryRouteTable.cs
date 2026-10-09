using System.Collections.Frozen;
using Hexalith.EventStore.Client.Events;

namespace Hexalith.EventStore.Client.Queries;
/// <summary>Classifies supplied frozen query routes before cache or handler access.</summary>
/// <remarks>This private local table supplies no authoritative deployment or activation capability.</remarks>
internal sealed class LogicalQueryRouteTable
{
    private readonly FrozenDictionary<string, bool> _routes;
    private readonly EventEvolutionCapabilityLoss _loss;
    private readonly Action<CancellationToken> _requireCurrent;
    /// <summary>Freezes an explicit complete local route table in its observed-loss scope.</summary>
    internal LogicalQueryRouteTable(IEnumerable<(string Domain, string QueryType, bool Logical)> routes, EventEvolutionCapabilityLoss loss, Action<CancellationToken> requireCurrent)
    {
        _loss = loss ?? throw new ArgumentNullException(nameof(loss));
        _requireCurrent = requireCurrent ?? throw new ArgumentNullException(nameof(requireCurrent));
        var values = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach ((string domain, string queryType, bool logical)in routes)
        {
            if (string.IsNullOrWhiteSpace(domain) || string.IsNullOrWhiteSpace(queryType) || !values.TryAdd(domain + "\0" + queryType, logical))
            {
                throw new ArgumentException("ReadModelRouteContextRequired: ambiguous frozen route.");
            }
        }

        _routes = values.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Requires current capability and returns exact frozen classification, refusing unknown routes.</summary>
    internal bool IsLogical(string domain, string queryType, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        _loss.RequireNoObservedLoss();
        try
        {
            _requireCurrent(token);
        }
        finally
        {
            token.ThrowIfCancellationRequested();
        }

        _loss.RequireNoObservedLoss();
        if (!_routes.TryGetValue(domain + "\0" + queryType, out bool logical))
        {
            throw new InvalidOperationException("ReadModelRouteContextRequired: route classification is unavailable.");
        }

        return logical;
    }
}
