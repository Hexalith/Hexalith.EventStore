namespace Hexalith.EventStore.CommandApi.Filters;

using FluentValidation;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

/// <summary>
/// Action filter that validates request models using FluentValidation.
/// Returns RFC 7807 ProblemDetails with application/problem+json content type.
/// </summary>
public class ValidateModelFilter(IServiceProvider serviceProvider) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument == null)
            {
                continue;
            }

            var argumentType = argument.GetType();
            var validatorType = typeof(IValidator<>).MakeGenericType(argumentType);
            var validator = serviceProvider.GetService(validatorType) as IValidator;

            if (validator == null)
            {
                continue;
            }

            var validationContext = new ValidationContext<object>(argument);
            var validationResult = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted).ConfigureAwait(false);

            if (!validationResult.IsValid)
            {
                string correlationId = context.HttpContext.Items["CorrelationId"]?.ToString() ?? "unknown";
                string? tenantId = ExtractTenantId(argument);

                var problemDetails = new ProblemDetails
                {
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

                if (tenantId is not null)
                {
                    problemDetails.Extensions["tenantId"] = tenantId;
                }

                context.HttpContext.Response.ContentType = "application/problem+json";
                context.Result = new BadRequestObjectResult(problemDetails);
                return;
            }
        }

        await next().ConfigureAwait(false);
    }

    private static string? ExtractTenantId(object argument)
    {
        // Use reflection to extract Tenant property if present
        var tenantProp = argument.GetType().GetProperty("Tenant");
        if (tenantProp?.GetValue(argument) is string tenant && !string.IsNullOrEmpty(tenant))
        {
            return tenant;
        }

        return null;
    }
}
