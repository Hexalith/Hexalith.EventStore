
using System.Reflection;

using FluentValidation;
using FluentValidation.Results;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Hexalith.EventStore.CommandApi.Filters;
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

                var problemDetails = new ProblemDetails {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Validation Failed",
                    Type = "https://tools.ietf.org/html/rfc9457#section-3",
                    Detail = "One or more validation errors occurred.",
                    Instance = context.HttpContext.Request.Path,
                    Extensions =
                    {
                        ["correlationId"] = correlationId,
                        ["validationErrors"] = validationResult.Errors.Select(e => new
                        {
                            field = e.PropertyName,
                            message = e.ErrorMessage,
                        }).ToArray(),
                    },
                };

                if (tenantId is not null) {
                    problemDetails.Extensions["tenantId"] = tenantId;
                }

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
