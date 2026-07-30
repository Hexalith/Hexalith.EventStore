namespace Hexalith.EventStore.AppHost.Tests.Configuration;

using System.Net;
using System.Net.Sockets;

using global::Aspire.Hosting;
using Hexalith.EventStore.Aspire;

public class KeycloakFastStartPortsTests {
    /// <summary>
    /// Pins the default (non-persistent) port model the operator guides describe: the resolver
    /// *prefers* the fixed defaults and walks forward from them, rather than taking an
    /// OS-assigned ephemeral port. Occupying the preferred http port is the precondition; an
    /// environment that already holds it (a live AppHost) satisfies the same precondition, so
    /// the test neither skips nor fails on a busy machine.
    /// </summary>
    [Fact]
    public void ResolveDynamic_WhenPreferredHttpPortIsBusy_WalksForwardFromThePreferredPort() {
        TcpListener? held = TryHold(KeycloakFastStartPorts.DefaultHttpPort);
        try {
            (int httpPort, int managementPort) = KeycloakFastStartPorts.ResolveDynamic();

            httpPort.ShouldBeGreaterThan(KeycloakFastStartPorts.DefaultHttpPort);
            httpPort.ShouldBeLessThan(
                KeycloakFastStartPorts.DefaultHttpPort + 100,
                "The default topology must walk forward from its preferred port, not fall back to an ephemeral one.");
            httpPort.ShouldNotBe(KeycloakFastStartPorts.ReservedEventStoreAppPort);
            managementPort.ShouldNotBe(httpPort);
            managementPort.ShouldNotBe(KeycloakFastStartPorts.ReservedEventStoreAppPort);
        }
        finally {
            held?.Stop();
        }
    }

    /// <summary>
    /// The management port is preferred the same way, and never collides with the resolved http
    /// port or the reserved EventStore app port.
    /// </summary>
    [Fact]
    public void ResolveDynamic_WhenPreferredManagementPortIsBusy_WalksForwardFromThePreferredPort() {
        TcpListener? held = TryHold(KeycloakFastStartPorts.DefaultManagementPort);
        try {
            (int httpPort, int managementPort) = KeycloakFastStartPorts.ResolveDynamic();

            managementPort.ShouldBeGreaterThan(KeycloakFastStartPorts.DefaultManagementPort);
            managementPort.ShouldBeLessThan(
                KeycloakFastStartPorts.DefaultManagementPort + 100,
                "The default topology must walk forward from its preferred port, not fall back to an ephemeral one.");
            managementPort.ShouldNotBe(httpPort);
            managementPort.ShouldNotBe(KeycloakFastStartPorts.ReservedEventStoreAppPort);
        }
        finally {
            held?.Stop();
        }
    }

    /// <summary>
    /// Occupies a loopback port for the duration of a test, matching the resolver's own
    /// availability probe. Returns <see langword="null"/> when the port is already taken, which
    /// establishes the same precondition without failing.
    /// </summary>
    /// <param name="port">The loopback port to occupy.</param>
    /// <returns>The started listener, or <see langword="null"/> when the port was already busy.</returns>
    private static TcpListener? TryHold(int port) {
        TcpListener listener = new(IPAddress.Loopback, port);
        try {
            listener.Start();
            return listener;
        }
        catch (SocketException) {
            listener.Dispose();
            return null;
        }
    }

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
