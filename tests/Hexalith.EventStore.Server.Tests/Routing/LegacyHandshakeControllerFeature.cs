using Hexalith.EventStore.Controllers;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Keeps the actual SDK callback controller without unrelated gateway or test actions.</summary>
internal sealed class LegacyHandshakeControllerFeature : IApplicationFeatureProvider<ControllerFeature>
{
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        foreach (var other in feature.Controllers.Where(type => type.AsType() != typeof(ProjectionNotificationController)).ToArray())
            feature.Controllers.Remove(other);
    }
}
