namespace Hexalith.EventStore.DomainService.Queries;
/// <summary>Installs one platform-owned private session before any scoped handler/store construction.</summary>
internal sealed class PrivateLogicalQueryHolder
{
    private PrivateLogicalQuerySession? _session;
    /// <summary>Installs an exclusive session once in this isolated DI scope.</summary>
    internal void Install(PrivateLogicalQuerySession session)
    {
        if (Interlocked.CompareExchange(ref _session, session, null)is not null)
        {
            throw new InvalidOperationException("ReadModelRouteContextRequired: query scope is already installed.");
        }
    }

    /// <summary>Requires this exact installed and still-open session.</summary>
    internal PrivateLogicalQuerySession Session => _session ?? throw new InvalidOperationException("ReadModelRouteContextRequired: no private query scope.");
}
