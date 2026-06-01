using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Configuration;

/// <summary>
/// Configuration options for aggregate command optimistic-concurrency handling.
/// Bound to configuration section "EventStore:CommandConcurrency".
/// </summary>
public record CommandConcurrencyOptions {
    public const int DefaultMaxPersistenceConflictRetries = 1;

    /// <summary>
    /// Gets the number of automatic retries after a state-store conflict before the command is rejected.
    /// </summary>
    public int MaxPersistenceConflictRetries { get; init; } = DefaultMaxPersistenceConflictRetries;
}

/// <summary>
/// Validates command concurrency options at startup.
/// </summary>
public class ValidateCommandConcurrencyOptions : IValidateOptions<CommandConcurrencyOptions> {
    public ValidateOptionsResult Validate(string? name, CommandConcurrencyOptions options) {
        ArgumentNullException.ThrowIfNull(options);

        if (options.MaxPersistenceConflictRetries < 0) {
            return ValidateOptionsResult.Fail(
                "EventStore:CommandConcurrency:MaxPersistenceConflictRetries must be greater than or equal to 0.");
        }

        if (options.MaxPersistenceConflictRetries > 10) {
            return ValidateOptionsResult.Fail(
                "EventStore:CommandConcurrency:MaxPersistenceConflictRetries must be less than or equal to 10.");
        }

        return ValidateOptionsResult.Success;
    }
}
