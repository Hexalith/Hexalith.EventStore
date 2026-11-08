using Hexalith.EventStore.Aspire;

namespace Hexalith.EventStore.AppHost.Tests.Configuration;

/// <summary>Verifies explicit matched profile selection without infrastructure or changing legacy constructor semantics.</summary>
public sealed class DevelopmentRouteCatalogRuntimeProfileTests
{
    /// <summary>Existing constructor, deconstruction and omitted selection retain their original values.</summary>
    [Fact]
    public void OriginalConstructorAndSelectionRemainCompatible()
    {
        var options = new DevelopmentRouteCatalogTransportOptions("custody", "runtime", "127.0.0.1:17000", "127.0.0.1:17001");
        var (custody, runtime, placement, scheduler) = options;
        custody.ShouldBe("custody"); runtime.ShouldBe("runtime"); placement.ShouldBe("127.0.0.1:17000"); scheduler.ShouldBe("127.0.0.1:17001");
        options.RuntimeProfile.ShouldBeSameAs(DevelopmentRouteCatalogRuntimeProfile.Original);
        DevelopmentRouteCatalogRuntimeProfile.Select(null).ShouldBeSameAs(options.RuntimeProfile);
        options.RuntimeProfile.Version.ShouldBe("1.18.7");
        options.RuntimeProfile.CliSha256.ShouldBe("43429557ff18a7230737b945699417ec391ad41bd431244c175b79ab3c904ce5");
    }

    /// <summary>The candidate is an explicit matched tuple; arbitrary versions cannot be composed.</summary>
    [Fact]
    public void CandidateRequiresExplicitMatchedSelection()
    {
        var profile = DevelopmentRouteCatalogRuntimeProfile.Select("1.19.1");
        var options = new DevelopmentRouteCatalogTransportOptions("custody", "runtime") { RuntimeProfile = profile };
        options.RuntimeProfile.Version.ShouldBe("1.19.1");
        profile.RuntimeSha256.ShouldBe("b1ece5da76674bf6e15126aeb019fa58e444b5b8edf4d0ac59e32df895ae8fd2");
        profile.SentryImage.ShouldContain("@sha256:"); profile.PlacementImage.ShouldContain("@sha256:"); profile.SchedulerImage.ShouldContain("@sha256:");
        profile.ShouldNotBeSameAs(DevelopmentRouteCatalogRuntimeProfile.Original);
        Should.Throw<ArgumentException>(() => DevelopmentRouteCatalogRuntimeProfile.Select("latest"));
        Should.Throw<ArgumentException>(() => DevelopmentRouteCatalogRuntimeProfile.Select("1.19.0"));
    }
}
