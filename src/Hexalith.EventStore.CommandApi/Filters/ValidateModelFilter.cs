namespace Hexalith.EventStore.CommandApi.Filters;

using FluentValidation;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

/// <summary>
/// Action filter that validates request models using FluentValidation.
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

                var problemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Validation Failed",
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                    Detail = "One or more validation errors occurred.",
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

                context.Result = new BadRequestObjectResult(problemDetails)
                {
                    ContentTypes = { "application/problem+json" },
                };
                return;
            }
        }

        await next().ConfigureAwait(false);
    }
}
