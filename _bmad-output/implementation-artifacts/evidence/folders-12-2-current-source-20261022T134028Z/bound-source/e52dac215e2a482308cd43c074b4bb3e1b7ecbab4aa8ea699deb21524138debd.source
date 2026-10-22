using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.Server.Actors;

public partial class AggregateActor
{
    private string[] SourceErasureRoutes(AggregateIdentity identity)
    {
        INamedProjectionRouteCatalog? catalogs = serviceProvider?.GetService<INamedProjectionRouteCatalog>();
        if (catalogs is null) return [];
        return [.. catalogs.Current.Entries.Where(entry => entry.Domain == identity.Domain)
            .SelectMany(static entry => entry.ProjectionTypes).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }

    private async Task AcquireSourceErasureFencesAsync(AggregateIdentity identity, string operation, string[] routes)
    {
        if (routes.Length == 0) return;
        IProjectionLifecycleGateway lifecycle = serviceProvider!.GetRequiredService<IProjectionLifecycleGateway>();
        var held = new List<string>();
        try
        {
            foreach (string route in routes)
            {
                ProjectionEraseAdmission admission = await lifecycle.BeginEraseAsync(identity, route, operation,
                    operation, true).ConfigureAwait(false);
                if (admission.Kind is not ProjectionEraseAdmissionKind.Admitted and not ProjectionEraseAdmissionKind.Resume)
                    throw new InvalidOperationException("delivery_in_progress");
                held.Add(route);
            }
        }
        catch
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            foreach (string route in held)
                await lifecycle.CompleteEraseAsync(identity, route, operation, cleanup.Token).ConfigureAwait(false);
            throw;
        }
    }

    private async Task ReleaseSourceErasureFencesAsync(AggregateIdentity identity, string operation, string[] routes)
    {
        if (routes.Length == 0) return;
        IProjectionLifecycleGateway lifecycle = serviceProvider!.GetRequiredService<IProjectionLifecycleGateway>();
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        foreach (string route in routes)
            if (!await lifecycle.CompleteEraseAsync(identity, route, operation, cleanup.Token).ConfigureAwait(false))
                throw new InvalidOperationException("delivery_in_progress");
    }

    private static string SourceErasureOperation(AggregateIdentity identity)
        => "source-erase-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identity.ActorId)));
}
