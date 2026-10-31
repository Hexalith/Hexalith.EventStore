using System.Reflection;

using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.Admin.Server.Controllers;

using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Selects real SDK controller actions without starting unrelated domain dependencies.</summary>
internal sealed class TechnicalHttpControllerFeatureProvider(bool fullInventory = false, string? inventoryAssembly = null, bool includeBusiness = false) : IApplicationFeatureProvider<ControllerFeature>
{
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        TypeInfo[] selected = [typeof(AdminStorageCommandController).GetTypeInfo(), typeof(AdminCommandsQueryController).GetTypeInfo(), typeof(AdminTenantsController).GetTypeInfo()];
        if (fullInventory) selected = feature.Controllers.Where(controller => (inventoryAssembly is null || controller.Assembly.GetName().Name == inventoryAssembly) && (controller.IsDefined(typeof(Hexalith.EventStore.Contracts.Routing.EventStoreTechnicalHttpControllerAttribute), inherit: false) || controller.GetMethods().Any(method => method.IsDefined(typeof(Hexalith.EventStore.Contracts.Routing.EventStoreTechnicalHttpControllerAttribute), inherit: false)))).ToArray();
        if (includeBusiness) selected = [.. selected, typeof(CommandsController).GetTypeInfo()];
        foreach (var controller in feature.Controllers.Where(controller => !selected.Contains(controller)).ToArray()) feature.Controllers.Remove(controller);
    }
}
