using Hexalith.EventStore.Server.Configuration;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Configuration;

public class ProjectionDeliveryIdempotencyOptionsTests {
    [Fact]
    public void Defaults_MatchFrozenDeliveryContract() {
        var options = new ProjectionDeliveryIdempotencyOptions();

        options.CompletedReceiptLimit.ShouldBe(256);
        options.ReservationLease.ShouldBe(TimeSpan.FromMinutes(5));
        options.MaxStateTransitionAttempts.ShouldBe(8);
        Should.NotThrow(options.Validate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4097)]
    public void Validate_ReceiptLimitOutsideBounds_Throws(int value) {
        var options = new ProjectionDeliveryIdempotencyOptions { CompletedReceiptLimit = value };

        _ = Should.Throw<InvalidOperationException>(options.Validate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(33)]
    public void Validate_StateTransitionAttemptsOutsideBounds_Throws(int value) {
        var options = new ProjectionDeliveryIdempotencyOptions { MaxStateTransitionAttempts = value };

        _ = Should.Throw<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void Validate_ReservationLeaseOutsideBounds_Throws() {
        _ = Should.Throw<InvalidOperationException>(
            new ProjectionDeliveryIdempotencyOptions { ReservationLease = TimeSpan.FromSeconds(29) }.Validate);
        _ = Should.Throw<InvalidOperationException>(
            new ProjectionDeliveryIdempotencyOptions { ReservationLease = TimeSpan.FromHours(24) + TimeSpan.FromTicks(1) }.Validate);
    }
}
