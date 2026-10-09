using Hexalith.EventStore.Contracts.Events;

using Shouldly;

namespace Hexalith.EventStore.Contracts.Tests.Events;

public sealed class EventIdentityValidatorTests
{
    private const string Ulid = "01ARZ3NDEKTSV4RRFFQ69G5FAV";

    [Theory]
    [InlineData("TenantId", "bad:tenant")]
    [InlineData("Domain", "bad_domain")]
    [InlineData("AggregateId", "bad:id")]
    [InlineData("AggregateType", " ")]
    [InlineData("EventTypeName", " ")]
    [InlineData("MessageId", "not-an-ulid")]
    [InlineData("CorrelationId", "bad_id")]
    [InlineData("CausationId", "bad/id")]
    [InlineData("SequenceNumber", "0")]
    public void Write_ReportsMalformedComponent(string component, string invalid)
    {
        string tenant = component == "TenantId" ? invalid : "tenant-a";
        string domain = component == "Domain" ? invalid : "orders";
        string aggregate = component == "AggregateId" ? invalid : "order-1";
        string aggregateType = component == "AggregateType" ? invalid : "Order";
        string eventType = component == "EventTypeName" ? invalid : "OrderChanged";
        string message = component == "MessageId" ? invalid : Ulid;
        string correlation = component == "CorrelationId" ? invalid : "trace-1";
        string causation = component == "CausationId" ? invalid : "cause-1";
        long sequence = component == "SequenceNumber" ? 0 : 1;

        EventIdentityValidationException failure = Should.Throw<EventIdentityValidationException>(() =>
            EventIdentityValidator.ValidateForWrite(tenant, domain, aggregate, aggregateType,
                eventType, message, correlation, causation, sequence));

        failure.ComponentName.ShouldBe(component);
        failure.Message.ShouldNotContain("payload");
    }

    [Fact]
    public void Read_AcceptsHistoricalGuidMessageIdOnly()
    {
        string historicalGuid = "9f387b68-1111-4111-8111-bbef048fe9cb";
        EventIdentityValidator.ValidateForRead("tenant-a", "orders", "order-1", "Order",
            "OrderChanged", historicalGuid, "trace-1", "cause-1", 1);

        Should.Throw<EventIdentityValidationException>(() =>
            EventIdentityValidator.ValidateForWrite("tenant-a", "orders", "order-1", "Order",
                "OrderChanged", historicalGuid, "trace-1", "cause-1", 1))
            .ComponentName.ShouldBe("MessageId");
    }

    [Fact]
    public void Subscription_ValidatesOptionalFieldsWhenPresent()
    {
        EventIdentityValidator.ValidateSubscription("tenant-a", null, "order-1", null,
            "OrderChanged", Ulid, "trace-1", null, 1);

        Should.Throw<EventIdentityValidationException>(() =>
            EventIdentityValidator.ValidateSubscription("tenant-a", "bad_domain", "order-1", null,
                "OrderChanged", Ulid, "trace-1", null, 1))
            .ComponentName.ShouldBe("Domain");
    }
}
