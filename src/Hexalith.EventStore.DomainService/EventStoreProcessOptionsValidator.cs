using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.DomainService;

/// <summary>Preserves individual process option failures for host startup diagnostics.</summary>
internal sealed class EventStoreProcessOptionsValidator : IValidateOptions<EventStoreProcessOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, EventStoreProcessOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        IReadOnlyList<string> failures = options.Validate();
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
