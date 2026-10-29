using System.Reflection;

using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Limits the test HTTP host to the exact MVC probe controller.</summary>
internal sealed class CatalogGeneratedMvcFeature : IApplicationFeatureProvider<ControllerFeature>
{
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        foreach (TypeInfo other in feature.Controllers.Where(type => type.AsType() != typeof(CatalogGeneratedMvcProbeController)).ToArray())
            feature.Controllers.Remove(other);
    }
}
