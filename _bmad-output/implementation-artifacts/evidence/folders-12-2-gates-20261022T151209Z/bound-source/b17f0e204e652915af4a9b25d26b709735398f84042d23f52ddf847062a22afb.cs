using Hexalith.EventStore.Operations.Models;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Operations.Configuration;

/// <summary>
/// Validates <see cref="EventStoreOperationsOptions"/> at startup.
/// </summary>
internal sealed class EventStoreOperationsOptionsValidator : IValidateOptions<EventStoreOperationsOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, EventStoreOperationsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];
        ValidateRequired(options.PubSubName, nameof(options.PubSubName), failures);
        ValidateRequired(options.TopicName, nameof(options.TopicName), failures);
        ValidateRequired(options.CaptureRoute, nameof(options.CaptureRoute), failures);
        ValidateRequired(options.AdminCallerAppId, nameof(options.AdminCallerAppId), failures);
        ValidateRequired(options.ReplayAppId, nameof(options.ReplayAppId), failures);
        ValidateRequired(options.ReplayMethodName, nameof(options.ReplayMethodName), failures);
        if (options.MaxBodyBytes is < 1 or > 10_485_760)
        {
            failures.Add($"{nameof(options.MaxBodyBytes)} must be between 1 and 10485760.");
        }

        if (options.MaxActionItems is < 1 or > 1_000)
        {
            failures.Add($"{nameof(options.MaxActionItems)} must be between 1 and 1000.");
        }

        if (options.MaxListItems is < 1 or > 1_000)
        {
            failures.Add($"{nameof(options.MaxListItems)} must be between 1 and 1000.");
        }

        if (options.ReplayReminderPeriodSeconds is < 1 or > 86_400)
        {
            failures.Add($"{nameof(options.ReplayReminderPeriodSeconds)} must be between 1 and 86400.");
        }

        if (options.MaxReplayAttempts is < 1 or > 1_000)
        {
            failures.Add($"{nameof(options.MaxReplayAttempts)} must be between 1 and 1000.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateRequired(string value, string propertyName, ICollection<string> failures)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            failures.Add($"{propertyName} is required.");
        }
        else if (value.Length > DeadLetterSafeIdentity.MaxValueLength)
        {
            failures.Add($"{propertyName} cannot exceed {DeadLetterSafeIdentity.MaxValueLength} characters.");
        }
    }
}
