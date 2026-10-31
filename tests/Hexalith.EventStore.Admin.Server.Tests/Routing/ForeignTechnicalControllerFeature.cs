using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using System.Reflection;

namespace Hexalith.EventStore.Admin.Server.Tests.Routing;

/// <summary>Installs the real foreign MVC action only in its provenance regression host.</summary>
internal sealed class ForeignTechnicalControllerFeature : IApplicationFeatureProvider<ControllerFeature>
{
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        ArgumentNullException.ThrowIfNull(feature);
        feature.Controllers.Add(typeof(ForeignTechnicalController).GetTypeInfo());
    }
}
