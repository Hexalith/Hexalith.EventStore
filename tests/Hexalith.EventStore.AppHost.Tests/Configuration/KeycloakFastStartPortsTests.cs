namespace Hexalith.EventStore.AppHost.Tests.Configuration;

using global::Aspire.Hosting;
using Hexalith.EventStore.Aspire;

public class KeycloakFastStartPortsTests {
    [Fact]
    public void Resolve_WhenBothUnset_ReturnsDefaults() {
        (int httpPort, int managementPort) = KeycloakFastStartPorts.Resolve(null, null);

        httpPort.ShouldBe(8180);
        managementPort.ShouldBe(8543);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_WhenBlank_ReturnsDefaults(string blank) {
        (int httpPort, int managementPort) = KeycloakFastStartPorts.Resolve(blank, blank);

        httpPort.ShouldBe(KeycloakFastStartPorts.DefaultHttpPort);
        managementPort.ShouldBe(KeycloakFastStartPorts.DefaultManagementPort);
    }

    [Fact]
    public void Resolve_WhenValidCustomPair_ReturnsThoseValues() {
        (int httpPort, int managementPort) = KeycloakFastStartPorts.Resolve("9180", "9543");

        httpPort.ShouldBe(9180);
        managementPort.ShouldBe(9543);
    }

    [Fact]
    public void Resolve_TrimsSurroundingWhitespace() {
        (int httpPort, int managementPort) = KeycloakFastStartPorts.Resolve("  9180 ", " 9543  ");

        httpPort.ShouldBe(9180);
        managementPort.ShouldBe(9543);
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("65536")]
    [InlineData("99999999999999999999")]
    public void Resolve_WhenHttpPortInvalid_ThrowsNamingKeyAndValue(string badValue) {
        DistributedApplicationException ex = Should.Throw<DistributedApplicationException>(
            () => KeycloakFastStartPorts.Resolve(badValue, "8543"));

        ex.Message.ShouldContain(KeycloakFastStartPorts.HttpPortKey);
        ex.Message.ShouldContain(badValue);
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("65536")]
    public void Resolve_WhenManagementPortInvalid_ThrowsNamingKeyAndValue(string badValue) {
        DistributedApplicationException ex = Should.Throw<DistributedApplicationException>(
            () => KeycloakFastStartPorts.Resolve("8180", badValue));

        ex.Message.ShouldContain(KeycloakFastStartPorts.ManagementPortKey);
        ex.Message.ShouldContain(badValue);
    }

    [Fact]
    public void Resolve_WhenPortsEqual_Throws() {
        DistributedApplicationException ex = Should.Throw<DistributedApplicationException>(
            () => KeycloakFastStartPorts.Resolve("8200", "8200"));

        ex.Message.ShouldContain(KeycloakFastStartPorts.HttpPortKey);
        ex.Message.ShouldContain(KeycloakFastStartPorts.ManagementPortKey);
    }

    [Fact]
    public void Resolve_WhenHttpPortEqualsEventStoreAppPort_Throws() {
        DistributedApplicationException ex = Should.Throw<DistributedApplicationException>(
            () => KeycloakFastStartPorts.Resolve("8080", "8543"));

        ex.Message.ShouldContain(KeycloakFastStartPorts.HttpPortKey);
        ex.Message.ShouldContain("8080");
    }

    [Fact]
    public void Resolve_WhenManagementPortEqualsEventStoreAppPort_Throws() {
        DistributedApplicationException ex = Should.Throw<DistributedApplicationException>(
            () => KeycloakFastStartPorts.Resolve("8180", "8080"));

        ex.Message.ShouldContain(KeycloakFastStartPorts.ManagementPortKey);
        ex.Message.ShouldContain("8080");
    }

    [Fact]
    public void Resolve_AllowsBoundaryPorts1And65535() {
        (int httpPort, int managementPort) = KeycloakFastStartPorts.Resolve("1", "65535");

        httpPort.ShouldBe(1);
        managementPort.ShouldBe(65535);
    }
}
