using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.Server.Identity;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using MediatR;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.ProviderVerification;

/// <summary>
/// Composes the query controller through the Gateway's isolated-protocol seam, because contract verification
/// substitutes the routers and serves no committed route catalog. Every other controller is activated normally.
/// </summary>
internal sealed class ProviderVerificationControllerActivator : IControllerActivator
{
    public object Create(ControllerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        IServiceProvider services = context.HttpContext.RequestServices;
        Type controllerType = context.ActionDescriptor.ControllerTypeInfo.AsType();
        if (controllerType != typeof(QueriesController))
        {
            return ActivatorUtilities.CreateInstance(services, controllerType);
        }

        return new QueriesController(
            services.GetRequiredService<IMediator>(),
            services.GetRequiredService<IETagService>(),
            services.GetRequiredService<ITenantValidator>(),
            services.GetRequiredService<IRbacValidator>(),
            services.GetRequiredService<ILogger<QueriesController>>(),
            services.GetService<IIdentityGatewayAdmission>(),
            services.GetService<IConfiguration>(),
            services.GetService<WorkloadAssertionValidator>(),
            services.GetService<IOptionsMonitor<WorkloadAuthenticationOptions>>());
    }

    public void Release(ControllerContext context, object controller)
    {
        if (controller is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
