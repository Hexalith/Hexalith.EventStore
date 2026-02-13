namespace Microsoft.Extensions.DependencyInjection;

using FluentValidation;

using Hexalith.EventStore.CommandApi.Authentication;
using Hexalith.EventStore.CommandApi.ErrorHandling;
using Hexalith.EventStore.CommandApi.Filters;
using Hexalith.EventStore.CommandApi.Pipeline;
using Hexalith.EventStore.CommandApi.Validation;
using Hexalith.EventStore.Server.Pipeline;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

public static class CommandApiServiceCollectionExtensions
{
    public static IServiceCollection AddCommandApi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddProblemDetails();
        services.AddExceptionHandler<ValidationExceptionHandler>();
        services.AddExceptionHandler<AuthorizationExceptionHandler>();
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.AddHttpContextAccessor();

        // JWT Bearer Authentication (replaces auth stubs from Story 2.1)
        services.AddOptions<EventStoreAuthenticationOptions>()
            .BindConfiguration("Authentication:JwtBearer");

        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddAuthorization();

        // Claims transformation
        services.AddTransient<IClaimsTransformation, EventStoreClaimsTransformation>();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<SubmitCommandHandler>();
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(AuthorizationBehavior<,>));
        });

        services.AddValidatorsFromAssemblyContaining<SubmitCommandRequestValidator>();

        services.AddControllers(options =>
        {
            options.Filters.Add<ValidateModelFilter>();
        });

        return services;
    }
}
