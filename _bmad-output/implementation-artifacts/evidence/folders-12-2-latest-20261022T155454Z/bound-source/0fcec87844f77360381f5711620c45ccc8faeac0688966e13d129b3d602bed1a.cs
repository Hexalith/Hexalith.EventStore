using Hexalith.EventStore.Operations.Configuration;

using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Operations.Tests;

/// <summary>
/// Verifies bounded operations configuration.
/// </summary>
public sealed class EventStoreOperationsOptionsValidatorTests
{
    /// <summary>Verifies action limits outside the supported range fail startup validation.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1_001)]
    public void InvalidActionLimitFailsValidation(int value)
    {
        var validator = new EventStoreOperationsOptionsValidator();

        ValidateOptionsResult result = validator.Validate(
            null,
            new EventStoreOperationsOptions { MaxActionItems = value });

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain(failure => failure.Contains("MaxActionItems", StringComparison.Ordinal));
    }
}
