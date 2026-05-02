
using Hexalith.EventStore.Server.Configuration;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Configuration;

public class ProjectionOptionsTests {
    [Fact]
    public void Validate_DefaultOptions_DoesNotThrow() {
        var options = new ProjectionOptions();

        Should.NotThrow(() => options.Validate());
    }

    [Fact]
    public void Validate_EmptyCheckpointStateStoreName_Throws() {
        var options = new ProjectionOptions { CheckpointStateStoreName = " " };

        _ = Should.Throw<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void Validate_NegativeDefaultRefreshInterval_Throws() {
        var options = new ProjectionOptions { DefaultRefreshIntervalMs = -1 };

        _ = Should.Throw<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void Validate_EmptyDomainKey_Throws() {
        var options = new ProjectionOptions {
            Domains = new Dictionary<string, DomainProjectionOptions> {
                [" "] = new DomainProjectionOptions { RefreshIntervalMs = 0 },
            },
        };

        _ = Should.Throw<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void Validate_NegativeDomainRefreshInterval_Throws() {
        var options = new ProjectionOptions {
            Domains = new Dictionary<string, DomainProjectionOptions> {
                ["counter"] = new DomainProjectionOptions { RefreshIntervalMs = -1 },
            },
        };

        _ = Should.Throw<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void Validate_DomainKeysDifferingOnlyByCase_Throws() {
        // Default Dictionary<string,...> uses ordinal-case-sensitive comparison, so a
        // configuration that provides "Counter" and "counter" binds two distinct entries.
        // GetRefreshIntervalMs falls back to a non-deterministic foreach scan; reject the
        // ambiguity at validation time so misconfiguration cannot silently pick a winner.
        var options = new ProjectionOptions {
            Domains = new Dictionary<string, DomainProjectionOptions> {
                ["Counter"] = new DomainProjectionOptions { RefreshIntervalMs = 1000 },
                ["counter"] = new DomainProjectionOptions { RefreshIntervalMs = 5000 },
            },
        };

        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() => options.Validate());
        ex.Message.ShouldContain("unique");
    }

    [Fact]
    public void GetRefreshIntervalMs_DomainNotConfigured_ReturnsDefault() {
        var options = new ProjectionOptions { DefaultRefreshIntervalMs = 7000 };

        options.GetRefreshIntervalMs("counter").ShouldBe(7000);
    }

    [Fact]
    public void GetRefreshIntervalMs_DomainConfigured_ReturnsDomainOverride() {
        var options = new ProjectionOptions {
            DefaultRefreshIntervalMs = 7000,
            Domains = new Dictionary<string, DomainProjectionOptions> {
                ["counter"] = new DomainProjectionOptions { RefreshIntervalMs = 250 },
            },
        };

        options.GetRefreshIntervalMs("counter").ShouldBe(250);
    }
}
