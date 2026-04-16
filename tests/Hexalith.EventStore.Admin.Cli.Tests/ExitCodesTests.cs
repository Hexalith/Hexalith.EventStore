namespace Hexalith.EventStore.Admin.Cli.Tests;

public class ExitCodesTests {
    [Fact]
    public void ExitCodes_Values_MatchConvention() {
        ExitCodes.Success.ShouldBe(0);
        ExitCodes.Degraded.ShouldBe(1);
        ExitCodes.Error.ShouldBe(2);
    }
}
