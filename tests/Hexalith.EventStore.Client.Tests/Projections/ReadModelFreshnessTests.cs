using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Testing.Fakes;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections;

public class ReadModelFreshnessTests {
    private const string StoreName = "statestore";

    private static readonly DateTimeOffset Now = new(2026, 6, 21, 12, 0, 0, TimeSpan.Zero);

    private static readonly ReadModelFreshnessThresholds Thresholds =
        ReadModelFreshnessThresholds.Create(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));

    // Freshness-aware, JSON-serializable persisted read model.
    public sealed class FreshReadModel : IReadModelFreshness {
        public int Value { get; set; }

        public DateTimeOffset? ProjectedAt { get; set; }

        public string? ProjectionVersion { get; set; }
    }

    [Fact]
    public void Classify_NullTimestamp_IsUnknown() =>
        ReadModelFreshness.Classify((DateTimeOffset?)null, Thresholds, Now)
            .ShouldBe(ReadModelFreshnessState.Unknown);

    [Fact]
    public void Classify_NullReadModel_IsUnknown() =>
        ReadModelFreshness.Classify((IReadModelFreshness?)null, Thresholds, Now)
            .ShouldBe(ReadModelFreshnessState.Unknown);

    [Fact]
    public void Classify_WithinAgingThreshold_IsCurrent() =>
        ReadModelFreshness.Classify(Now.AddSeconds(-30), Thresholds, Now)
            .ShouldBe(ReadModelFreshnessState.Current);

    [Fact]
    public void Classify_ExactlyAtAgingBoundary_IsCurrent() =>
        // Boundary is inclusive of Current (age == Aging threshold).
        ReadModelFreshness.Classify(Now.AddMinutes(-1), Thresholds, Now)
            .ShouldBe(ReadModelFreshnessState.Current);

    [Fact]
    public void Classify_JustPastAgingThreshold_IsAging() =>
        ReadModelFreshness.Classify(Now.AddMinutes(-1).AddSeconds(-1), Thresholds, Now)
            .ShouldBe(ReadModelFreshnessState.Aging);

    [Fact]
    public void Classify_ExactlyAtStaleBoundary_IsAging() =>
        // Boundary is inclusive of Aging (age == Stale threshold).
        ReadModelFreshness.Classify(Now.AddMinutes(-5), Thresholds, Now)
            .ShouldBe(ReadModelFreshnessState.Aging);

    [Fact]
    public void Classify_PastStaleThreshold_IsStale() =>
        ReadModelFreshness.Classify(Now.AddMinutes(-6), Thresholds, Now)
            .ShouldBe(ReadModelFreshnessState.Stale);

    [Fact]
    public void Classify_FutureTimestamp_IsCurrent() =>
        // Negative age from clock skew is treated as current, never stale.
        ReadModelFreshness.Classify(Now.AddMinutes(10), Thresholds, Now)
            .ShouldBe(ReadModelFreshnessState.Current);

    [Fact]
    public void Classify_ReadModelTimestamp_DelegatesToTimestampOverload() {
        var model = new FreshReadModel { ProjectedAt = Now.AddMinutes(-6) };

        ReadModelFreshness.Classify(model, Thresholds, Now).ShouldBe(ReadModelFreshnessState.Stale);
    }

    [Fact]
    public void Classify_ReadModelWithNullProjectedAt_IsUnknown() {
        var model = new FreshReadModel { ProjectedAt = null };

        ReadModelFreshness.Classify(model, Thresholds, Now).ShouldBe(ReadModelFreshnessState.Unknown);
    }

    [Fact]
    public void Age_NullTimestamp_IsNull() =>
        ReadModelFreshness.Age(null, Now).ShouldBeNull();

    [Fact]
    public void Age_PastTimestamp_ReturnsElapsed() =>
        ReadModelFreshness.Age(Now.AddMinutes(-3), Now).ShouldBe(TimeSpan.FromMinutes(3));

    [Fact]
    public void Age_FutureTimestamp_ClampsToZero() =>
        ReadModelFreshness.Age(Now.AddMinutes(2), Now).ShouldBe(TimeSpan.Zero);

    [Fact]
    public void Thresholds_Create_RejectsNegativeAging() =>
        Should.Throw<ArgumentOutOfRangeException>(() =>
            ReadModelFreshnessThresholds.Create(TimeSpan.FromMinutes(-1), TimeSpan.FromMinutes(5)));

    [Fact]
    public void Thresholds_Create_RejectsStaleBeforeAging() =>
        Should.Throw<ArgumentOutOfRangeException>(() =>
            ReadModelFreshnessThresholds.Create(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1)));

    [Fact]
    public void Thresholds_Create_AllowsEqualAgingAndStale() {
        ReadModelFreshnessThresholds thresholds =
            ReadModelFreshnessThresholds.Create(TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(2));

        thresholds.Aging.ShouldBe(TimeSpan.FromMinutes(2));
        thresholds.Stale.ShouldBe(TimeSpan.FromMinutes(2));
    }

    [Fact]
    public async Task GetWithFreshnessAsync_AbsentKey_IsUnknown() {
        var store = new InMemoryReadModelStore();

        ReadModelFreshnessResult<FreshReadModel> result =
            await store.GetWithFreshnessAsync<FreshReadModel>(StoreName, "missing", Thresholds, Now);

        result.Value.ShouldBeNull();
        result.ETag.ShouldBeNull();
        result.Freshness.ShouldBe(ReadModelFreshnessState.Unknown);
    }

    [Fact]
    public async Task GetWithFreshnessAsync_StalePersistedTimestamp_IsStale() {
        var store = new InMemoryReadModelStore();
        store.SeedRaw(StoreName, "k", new FreshReadModel {
            Value = 7,
            ProjectedAt = Now.AddMinutes(-10),
            ProjectionVersion = "v42",
        });

        ReadModelFreshnessResult<FreshReadModel> result =
            await store.GetWithFreshnessAsync<FreshReadModel>(StoreName, "k", Thresholds, Now);

        result.Value!.Value.ShouldBe(7);
        result.ETag.ShouldNotBeNull();
        result.Freshness.ShouldBe(ReadModelFreshnessState.Stale);
    }

    [Fact]
    public async Task GetWithFreshnessAsync_FreshPersistedTimestamp_IsCurrent() {
        var store = new InMemoryReadModelStore();
        store.SeedRaw(StoreName, "k", new FreshReadModel {
            Value = 1,
            ProjectedAt = Now.AddSeconds(-10),
        });

        ReadModelFreshnessResult<FreshReadModel> result =
            await store.GetWithFreshnessAsync<FreshReadModel>(StoreName, "k", Thresholds, Now);

        result.Freshness.ShouldBe(ReadModelFreshnessState.Current);
    }

    [Fact]
    public void ToQueryResponseMetadata_StaleModel_SetsIsStaleTrueAndVersion() {
        var model = new FreshReadModel {
            ProjectedAt = Now.AddMinutes(-10),
            ProjectionVersion = "v99",
        };

        QueryResponseMetadata metadata = model.ToQueryResponseMetadata(Thresholds, Now, eTag: "\"abc\"");

        metadata.IsStale.ShouldBe(true);
        metadata.ProjectionVersion.ShouldBe("v99");
        metadata.ETag.ShouldBe("\"abc\"");
        metadata.ServedAt.ShouldBe(Now);
    }

    [Fact]
    public void ToQueryResponseMetadata_CurrentModel_SetsIsStaleFalse() {
        var model = new FreshReadModel { ProjectedAt = Now.AddSeconds(-5) };

        QueryResponseMetadata metadata = model.ToQueryResponseMetadata(Thresholds, Now);

        metadata.IsStale.ShouldBe(false);
    }

    [Fact]
    public void ToQueryResponseMetadata_AgingModel_SetsIsStaleFalse() {
        var model = new FreshReadModel { ProjectedAt = Now.AddMinutes(-3) };

        QueryResponseMetadata metadata = model.ToQueryResponseMetadata(Thresholds, Now);

        metadata.IsStale.ShouldBe(false);
    }

    [Fact]
    public void ToQueryResponseMetadata_UnknownModel_LeavesIsStaleNull() {
        QueryResponseMetadata metadata =
            ((IReadModelFreshness?)null).ToQueryResponseMetadata(Thresholds, Now);

        metadata.IsStale.ShouldBeNull();
        metadata.ProjectionVersion.ShouldBeNull();
    }
}
