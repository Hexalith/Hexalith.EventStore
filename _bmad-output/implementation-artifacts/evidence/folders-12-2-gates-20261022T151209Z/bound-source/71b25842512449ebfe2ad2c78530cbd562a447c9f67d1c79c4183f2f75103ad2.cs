using Hexalith.EventStore.Server.Commands;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// Validates idempotency retention against the authoritative status/archive lifetime.
/// </summary>
/// <param name="commandStatusOptions">The shared status/archive options.</param>
public sealed class ValidateIdempotencyRetentionOptions(
    IOptions<CommandStatusOptions> commandStatusOptions) : IValidateOptions<IdempotencyRetentionOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, IdempotencyRetentionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.TerminalRetentionSeconds <= 0)
        {
            return ValidateOptionsResult.Fail("Idempotency terminal retention must be greater than zero seconds.");
        }

        int statusArchiveTtl = commandStatusOptions.Value.TtlSeconds;
        return options.TerminalRetentionSeconds < statusArchiveTtl
            ? ValidateOptionsResult.Fail(
                $"Idempotency terminal retention must be at least the status/archive TTL ({statusArchiveTtl} seconds).")
            : ValidateOptionsResult.Success;
    }
}
