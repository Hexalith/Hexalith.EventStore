using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;

using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Configuration;

public class IdempotencyRetentionOptionsTests
{
    [Fact]
    public void DefaultsToTwentyFourHours()
    {
        new IdempotencyRetentionOptions().TerminalRetentionSeconds.ShouldBe(86_400);
    }

    [Fact]
    public void Validate_WhenRetentionIsShorterThanStatusTtl_Fails()
    {
        var validator = new ValidateIdempotencyRetentionOptions(
            Options.Create(new CommandStatusOptions { TtlSeconds = 86_400 }));

        ValidateOptionsResult result = validator.Validate(
            null,
            new IdempotencyRetentionOptions { TerminalRetentionSeconds = 3_600 });

        result.Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WhenRetentionMatchesStatusTtl_Succeeds()
    {
        var validator = new ValidateIdempotencyRetentionOptions(
            Options.Create(new CommandStatusOptions { TtlSeconds = 86_400 }));

        ValidateOptionsResult result = validator.Validate(
            null,
            new IdempotencyRetentionOptions { TerminalRetentionSeconds = 86_400 });

        result.Succeeded.ShouldBeTrue();
    }
}
