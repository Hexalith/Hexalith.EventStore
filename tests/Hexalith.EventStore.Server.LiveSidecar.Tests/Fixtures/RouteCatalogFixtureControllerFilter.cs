using System.Reflection;

using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Limits the gateway qualification host to the actual query controller under test.</summary>
internal sealed class RouteCatalogFixtureControllerFilter(Type controller) : IApplicationFeatureProvider<ControllerFeature>
{
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        foreach (TypeInfo other in feature.Controllers.Where(type => type.AsType() != controller).ToArray()) feature.Controllers.Remove(other);
    }
}
