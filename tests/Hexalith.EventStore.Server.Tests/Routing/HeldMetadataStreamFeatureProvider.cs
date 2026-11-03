using System.Reflection;

using Hexalith.EventStore.Controllers;

using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Adds the actual SDK core stream controller after the shared technical-only fixture's filtering.</summary>
internal sealed class HeldMetadataStreamFeatureProvider : IApplicationFeatureProvider<ControllerFeature>
{
    /// <inheritdoc/>
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        var stream = typeof(StreamsController).GetTypeInfo();
        if (!feature.Controllers.Contains(stream)) feature.Controllers.Add(stream);
    }
}
