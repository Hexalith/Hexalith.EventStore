using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Hexalith.EventStore.Server.Tests.Routing;

internal sealed class CatalogV3MvcFeature : IApplicationFeatureProvider<ControllerFeature>
{
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        Type[] allowed = [typeof(Controllers.CommandsController), typeof(Controllers.QueriesController),
            typeof(Controllers.RoutingCatalogMetadataController), typeof(CatalogGeneratedMvcProbeController)];
        foreach (var other in feature.Controllers.Where(type => !allowed.Contains(type.AsType())).ToArray()) feature.Controllers.Remove(other);
    }
}
