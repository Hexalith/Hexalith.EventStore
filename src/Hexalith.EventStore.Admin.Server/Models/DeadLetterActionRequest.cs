using System.ComponentModel.DataAnnotations;

namespace Hexalith.EventStore.Admin.Server.Models;

/// <summary>
/// Request body for dead-letter retry, skip, and archive operations.
/// </summary>
/// <remarks>
/// Modelled as an explicit class so ASP.NET Core MVC reads validation metadata directly from
/// the property. A positional record with <c>[property: Required]</c> would log an
/// InvalidOperationException at first bind because record validation metadata must sit on the
/// constructor parameter (see ASP.NET Core model-binding docs for .NET 10, record types).
/// </remarks>
public sealed class DeadLetterActionRequest : IValidatableObject
{
    /// <summary>
    /// Gets the message identifiers to act upon.
    /// </summary>
    [Required]
    public IReadOnlyList<string>? MessageIds { get; init; }

    /// <inheritdoc/>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MessageIds is null)
        {
            yield break;
        }

        if (MessageIds.Count == 0)
        {
            yield return new ValidationResult(
                "MessageIds must contain at least one message identifier.",
                [nameof(MessageIds)]);
            yield break;
        }

        for (int index = 0; index < MessageIds.Count; index++)
        {
            if (string.IsNullOrWhiteSpace(MessageIds[index]))
            {
                yield return new ValidationResult(
                    $"MessageIds[{index}] must not be null, empty, or whitespace.",
                    [nameof(MessageIds)]);
                yield break;
            }
        }
    }
}
