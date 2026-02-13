namespace Microsoft.Extensions.DependencyInjection;

using FluentValidation;

using Hexalith.EventStore.CommandApi.ErrorHandling;
using Hexalith.EventStore.CommandApi.Filters;
using Hexalith.EventStore.CommandApi.Validation;
using Hexalith.EventStore.Server.Pipeline;

public static class CommandApiServiceCollectionExtensions
{
    public static IServiceCollection AddCommandApi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddProblemDetails();
        services.AddExceptionHandler<ValidationExceptionHandler>();
        services.AddExceptionHandler<GlobalExceptionHandler>();

        // Auth stubs for now (Story 2.4 will add full JWT)
        services.AddAuthentication();
        services.AddAuthorization();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<SubmitCommandHandler>();
        });

        services.AddValidatorsFromAssemblyContaining<SubmitCommandRequestValidator>();

        services.AddControllers(options =>
        {
            options.Filters.Add<ValidateModelFilter>();
        });

        return services;
    }
}
