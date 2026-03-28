
using System.Reflection;

using FluentValidation;
using FluentValidation.Results;

using Hexalith.EventStore.ErrorHandling;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Hexalith.EventStore.Filters;

/// <summary>
/// Action filter that validates request models using FluentValidation.
/// Returns RFC 7807 ProblemDetails with application/problem+json content type.
/// </summary>
public class ValidateModelFilter(IServiceProvider serviceProvider) : IAsyncActionFilter {
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next) {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        foreach (object? argument in context.ActionArguments.Values) {
            if (argument == null) {
                continue;
            }

            Type argumentType = argument.GetType();
            Type validatorType = typeof(IValidator<>).MakeGenericType(argumentType);

            if (serviceProvider.GetService(validatorType) is not IValidator validator) {
                continue;
            }

            var validationContext = new ValidationContext<object>(argument);
            ValidationResult validationResult = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted).ConfigureAwait(false);

            if (!validationResult.IsValid) {
                string correlationId = context.HttpContext.Items["CorrelationId"]?.ToString() ?? "unknown";
                string? tenantId = ExtractTenantId(argument);
                int errorCount = validationResult.Errors.Count;

                ProblemDetails problemDetails = ValidationProblemDetailsFactory.Create(
                    $"The command has {errorCount} validation error(s). See 'errors' for specifics.",
                    validationResult.Errors,
                    correlationId,
                    tenantId);
                problemDetails.Instance = context.HttpContext.Request.Path;

                var result = new BadRequestObjectResult(problemDetails);
                result.ContentTypes.Add("application/problem+json");
                context.Result = result;
                return;
            }
        }

        _ = await next().ConfigureAwait(false);
    }

    private static string? ExtractTenantId(object argument) {
        // Use reflection to extract Tenant property if present
        PropertyInfo? tenantProp = argument.GetType().GetProperty("Tenant");
        if (tenantProp?.GetValue(argument) is string tenant && !string.IsNullOrEmpty(tenant)) {
            return tenant;
        }

        return null;
    }
}
