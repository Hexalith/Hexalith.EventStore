using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Tests.Actors;
using Hexalith.EventStore.Server.Tests.TestUtilities;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Configuration;

/// <summary>Proves the actor startup diagnostic follows final deployment enrollment.</summary>
public sealed class FoldedSnapshotQualificationStartupTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActorHostWarnsAtStartupOnlyWhenQualificationRegistryIsAbsent(bool enrolled)
    {
        // HX-FS-V1 §§5.2/12, V15: absent capture eligibility leaves commands supported.
        var services = new ServiceCollection();
        List<LogEntry> entries = [];
        ILogger<FoldedSnapshotQualificationHostedService> logger =
            new TestLogger<FoldedSnapshotQualificationHostedService>(entries);
        services.AddSingleton(logger);
        services.AddEventStoreServer(new ConfigurationBuilder().Build());
        if (enrolled)
        {
            services.AddEventStoreFoldedSnapshotActorQualification(
                "orders", "OrderAggregate", "order-profile", "order-parity", SnapshotTestLimits.ForState());
        }
        services.Count(descriptor => descriptor.ServiceType == typeof(IHostedService)
            && descriptor.ImplementationType == typeof(FoldedSnapshotQualificationHostedService)).ShouldBe(1);
        using ServiceProvider provider = services.BuildServiceProvider();
        var startup = ActivatorUtilities.CreateInstance<FoldedSnapshotQualificationHostedService>(provider);

        await startup.StartAsync(CancellationToken.None);

        entries.Count.ShouldBe(enrolled ? 0 : 1);
        if (!enrolled)
        {
            entries[0].Level.ShouldBe(LogLevel.Warning);
            entries[0].EventId.Id.ShouldBe(7105);
            string message = entries[0].Message;
            message.ShouldContain("Stage=FoldedSnapshotQualificationAbsent");
            message.ShouldContain("FailureClass=SnapshotCaptureUnsupported");
        }
        else
        {
            FoldedSnapshotQualification qualification = provider.GetRequiredService<FoldedSnapshotQualificationRegistry>()
                .Find("orders", "OrderAggregate").ShouldNotBeNull();
            qualification.ProfileId.ShouldBe("order-profile");
            qualification.ParityEvidenceId.ShouldBe("order-parity");
            qualification.Limits.ShouldBe(SnapshotTestLimits.ForState());
            qualification.StateType.ShouldBe(typeof(object));
        }
    }
}
