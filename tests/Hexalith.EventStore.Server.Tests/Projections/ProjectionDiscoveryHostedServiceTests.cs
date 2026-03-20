
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

/// <summary>
/// Story 11-4 Task 4: Tests for ProjectionDiscoveryHostedService.
/// Verifies startup discovery logging and ExtractDomain helper.
/// </summary>
public class ProjectionDiscoveryHostedServiceTests
{
    // --- ExtractDomain tests ---

    [Fact]
    public void ExtractDomain_ColonSeparatedKey_ReturnsDomain()
    {
        string result = ProjectionDiscoveryHostedService.ExtractDomain("tenant1:counter:v1");
        result.ShouldBe("counter");
    }

    [Fact]
    public void ExtractDomain_PipeSeparatedKey_ReturnsDomain()
    {
        string result = ProjectionDiscoveryHostedService.ExtractDomain("tenant1|counter|v1");
        result.ShouldBe("counter");
    }

    [Fact]
    public void ExtractDomain_SingleSegment_ReturnsEmpty()
    {
        string result = ProjectionDiscoveryHostedService.ExtractDomain("invalid");
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void ExtractDomain_FourSegmentKey_ReturnsEmpty()
    {
        // Malformed registration keys are rejected.
        string result = ProjectionDiscoveryHostedService.ExtractDomain("org:tenant:counter:v1");
        result.ShouldBe(string.Empty);
    }

    // --- StartAsync tests ---

    [Fact]
    public async Task StartAsync_NoRegistrations_LogsNoRegistrations()
    {
        // Arrange
        var entries = new List<LogEntry>();
        var dsOptions = Options.Create(new DomainServiceOptions());
        var pOptions = Options.Create(new ProjectionOptions());
        var service = new ProjectionDiscoveryHostedService(dsOptions, pOptions, new TestLogger<ProjectionDiscoveryHostedService>(entries));

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        entries.ShouldContain(e => e.EventId.Id == 1120 && e.Level == LogLevel.Information);
    }

    [Fact]
    public async Task StartAsync_WithRegistrations_LogsProjectionModesAndSummary()
    {
        // Arrange
        var entries = new List<LogEntry>();
        var dsOptions = Options.Create(new DomainServiceOptions
        {
            Registrations = new Dictionary<string, DomainServiceRegistration>
            {
                ["tenant1:counter:v1"] = new DomainServiceRegistration("counter-svc", "project", "tenant1", "counter", "v1"),
            },
        });
        var pOptions = Options.Create(new ProjectionOptions { DefaultRefreshIntervalMs = 0 });
        var service = new ProjectionDiscoveryHostedService(dsOptions, pOptions, new TestLogger<ProjectionDiscoveryHostedService>(entries));

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        entries.ShouldContain(e => e.EventId.Id == 1121 && e.Level == LogLevel.Information && e.Message.Contains("counter", StringComparison.OrdinalIgnoreCase));
        entries.ShouldContain(e => e.EventId.Id == 1124 && e.Level == LogLevel.Information);
    }

    [Fact]
    public async Task StartAsync_OrphanedDomainConfig_LogsWarning()
    {
        // Arrange - discovered domain is "counter", config also includes orphan "unknown"
        var entries = new List<LogEntry>();
        var dsOptions = Options.Create(new DomainServiceOptions
        {
            Registrations = new Dictionary<string, DomainServiceRegistration>
            {
                ["tenant1:counter:v1"] = new DomainServiceRegistration("counter-svc", "project", "tenant1", "counter", "v1"),
            },
        });
        var pOptions = Options.Create(new ProjectionOptions
        {
            Domains = new Dictionary<string, DomainProjectionOptions>
            {
                ["unknown"] = new DomainProjectionOptions { RefreshIntervalMs = 5000 },
            },
        });
        var service = new ProjectionDiscoveryHostedService(dsOptions, pOptions, new TestLogger<ProjectionDiscoveryHostedService>(entries));

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        entries.ShouldContain(e => e.EventId.Id == 1123 && e.Level == LogLevel.Warning && e.Message.Contains("unknown", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task StartAsync_MalformedRegistrationKey_LogsWarning()
    {
        // Arrange
        var entries = new List<LogEntry>();
        var dsOptions = Options.Create(new DomainServiceOptions
        {
            Registrations = new Dictionary<string, DomainServiceRegistration>
            {
                ["org:tenant:counter:v1"] = new DomainServiceRegistration("counter-svc", "project", "tenant1", "counter", "v1"),
            },
        });
        var pOptions = Options.Create(new ProjectionOptions());
        var service = new ProjectionDiscoveryHostedService(dsOptions, pOptions, new TestLogger<ProjectionDiscoveryHostedService>(entries));

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        entries.ShouldContain(e => e.EventId.Id == 1125 && e.Level == LogLevel.Warning);
    }

    private sealed class TestLogger<T>(List<LogEntry> entries) : ILogger<T>
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => entries.Add(new LogEntry(logLevel, eventId, formatter(state, exception)));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    private sealed record LogEntry(LogLevel Level, EventId EventId, string Message);
}
