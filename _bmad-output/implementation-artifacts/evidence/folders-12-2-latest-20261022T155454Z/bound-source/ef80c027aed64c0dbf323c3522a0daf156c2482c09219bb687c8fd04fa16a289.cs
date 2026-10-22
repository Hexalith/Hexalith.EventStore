using Hexalith.EventStore.Client.Queries;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Queries;

public class QueryCursorScopeTests {
    [Fact]
    public void Build_reproduces_simple_single_field_scope() {
        QueryCursorScope.Create().Add("user", "user-1").Build().ShouldBe("user:user-1");
        QueryCursorScope.Create().Add("tenant", "tenant-1").Build().ShouldBe("tenant:tenant-1");
    }

    [Fact]
    public void Build_joins_multiple_fields_with_pipe_separator() {
        string scope = QueryCursorScope.Create()
            .Add("requester", "user-1")
            .Add("target-user", "user-2")
            .Build();

        scope.ShouldBe("requester:user-1|target-user:user-2");
    }

    [Fact]
    public void Build_formats_instant_fields_as_round_trip_utc_and_renders_nulls_as_empty() {
        DateTimeOffset from = new(2026, 5, 14, 10, 0, 0, TimeSpan.Zero);
        DateTimeOffset to = from.AddHours(1);

        string scope = QueryCursorScope.Create()
            .Add("tenant", "tenant-1")
            .Add("from", from)
            .Add("to", to)
            .Add("category", "Administrative")
            .Build();

        scope.ShouldBe("tenant:tenant-1|from:2026-05-14T10:00:00.0000000Z|to:2026-05-14T11:00:00.0000000Z|category:Administrative");
    }

    [Fact]
    public void Build_renders_null_value_and_null_instant_as_empty_segments() {
        string scope = QueryCursorScope.Create()
            .Add("tenant", "tenant-1")
            .Add("from", (DateTimeOffset?)null)
            .Add("category", (string?)null)
            .Build();

        scope.ShouldBe("tenant:tenant-1|from:|category:");
    }

    [Fact]
    public void AddProjectionWatermark_BindsCanonicalPositiveInvariantDecimalSegment() {
        string scope = QueryCursorScope.Create()
            .Add("tenant", "tenant-1")
            .AddProjectionWatermark(9_223_372_036_854_775_000)
            .Build();

        scope.ShouldBe("tenant:tenant-1|watermark:9223372036854775000");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void AddProjectionWatermark_RejectsUnknownOrNonPositiveValues(long? watermark) =>
        Should.Throw<ArgumentOutOfRangeException>(() =>
            QueryCursorScope.Create().AddProjectionWatermark(watermark));

    [Fact]
    public void Build_escapes_user_controlled_values_once_to_prevent_scope_collisions() {
        // An attacker-controlled value injecting the separators must not collide with a structurally
        // different scope built from clean values.
        string escaped = QueryCursorScope.Create().Add("user", @"user\1|target-user:admin").Build();
        string clean = QueryCursorScope.Create()
            .Add("requester", @"user\1")
            .Add("target-user", "admin")
            .Build();

        escaped.ShouldBe(@"user:user\\1\ptarget-user\cadmin");
        clean.ShouldBe(@"requester:user\\1|target-user:admin");
        escaped.ShouldNotBe(clean);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Add_rejects_missing_key(string? key)
        => Should.Throw<ArgumentException>(() => QueryCursorScope.Create().Add(key!, "value"));
}
