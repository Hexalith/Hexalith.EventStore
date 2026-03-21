using System.ComponentModel.DataAnnotations;

namespace Hexalith.EventStore.Admin.Server.Models;

/// <summary>
/// Request body for dead-letter retry, skip, and archive operations.
/// </summary>
/// <param name="MessageIds">The message IDs to act upon.</param>
public record DeadLetterActionRequest(
    [property: Required] IReadOnlyList<string> MessageIds);
