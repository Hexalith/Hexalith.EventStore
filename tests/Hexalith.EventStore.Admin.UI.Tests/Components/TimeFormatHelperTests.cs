using Hexalith.EventStore.Admin.UI.Components.Shared;

namespace Hexalith.EventStore.Admin.UI.Tests.Components;

/// <summary>
/// Unit tests for <see cref="TimeFormatHelper.FormatBytes"/>.
/// </summary>
public class TimeFormatHelperTests
{
    [Fact]
    public void FormatBytes_Null_ReturnsNA()
    {
        TimeFormatHelper.FormatBytes(null).ShouldBe("N/A");
    }

    [Fact]
    public void FormatBytes_Zero_Returns0B()
    {
        TimeFormatHelper.FormatBytes(0).ShouldBe("0.0 B");
    }

    [Fact]
    public void FormatBytes_1023_ReturnsBytes()
    {
        string result = TimeFormatHelper.FormatBytes(1023);
        result.ShouldContain("B");
        result.ShouldNotContain("KB");
    }

    [Fact]
    public void FormatBytes_1024_Returns1KB()
    {
        TimeFormatHelper.FormatBytes(1024).ShouldBe("1.0 KB");
    }

    [Fact]
    public void FormatBytes_1MB_Returns1MB()
    {
        TimeFormatHelper.FormatBytes(1_048_576).ShouldBe("1.0 MB");
    }

    [Fact]
    public void FormatBytes_1GB_Returns1GB()
    {
        TimeFormatHelper.FormatBytes(1_073_741_824).ShouldBe("1.0 GB");
    }
}
