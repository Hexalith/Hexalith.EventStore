using Hexalith.EventStore.LoadTests.Helpers;

namespace Hexalith.EventStore.LoadTests.Tests;

public sealed class LoadTestJwtTokenGeneratorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("short")]
    public void ResolveSigningKey_MissingBlankOrWeakValue_FailsSupportSafely(string? value)
    {
        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => LoadTestJwtTokenGenerator.ResolveSigningKey(value));

        exception.Message.ShouldContain("LOAD_TEST_JWT_SIGNING_KEY");
        if (!string.IsNullOrWhiteSpace(value))
        {
            exception.Message.ShouldNotContain(value);
        }
    }

    [Fact]
    public void ResolveSigningKey_UsesUtf8ByteStrengthAndReturnsCallerValue()
    {
        string value = new('\u00e9', 16);

        string resolved = LoadTestJwtTokenGenerator.ResolveSigningKey(value);

        resolved.ShouldBe(value);
    }
}
