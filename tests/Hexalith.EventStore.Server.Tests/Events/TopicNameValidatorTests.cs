
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Events;

using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Events;
/// <summary>
/// Story 4.2 Task 4: TopicNameValidator unit tests.
/// Verifies D6 topic name validation and compatibility with DAPR pub/sub backends (AC: #5).
/// </summary>
public class TopicNameValidatorTests {
    private static TopicNameValidator CreateValidator() {
        ILogger<TopicNameValidator> logger = Substitute.For<ILogger<TopicNameValidator>>();
        return new TopicNameValidator(logger);
    }

    // --- Task 4.2: AC #5 ---

    [Fact]
    public void IsValidTopicName_ValidD6Pattern_ReturnsTrue() {
        // Arrange
        TopicNameValidator validator = CreateValidator();

        // Act & Assert
        validator.IsValidTopicName("acme.orders.events").ShouldBeTrue();
    }

    // --- Task 4.3: AC #5 ---

    [Fact]
    public void IsValidTopicName_HyphenatedSegments_ReturnsTrue() {
        // Arrange
        TopicNameValidator validator = CreateValidator();

        // Act & Assert
        validator.IsValidTopicName("acme-corp.order-service.events").ShouldBeTrue();
    }

    // --- Task 4.4 ---

    [Fact]
    public void IsValidTopicName_EmptyString_ReturnsFalse() {
        // Arrange
        TopicNameValidator validator = CreateValidator();

        // Act & Assert
        validator.IsValidTopicName(string.Empty).ShouldBeFalse();
    }

    // --- Task 4.5 ---

    [Fact]
    public void IsValidTopicName_MissingSuffix_ReturnsFalse() {
        // Arrange
        TopicNameValidator validator = CreateValidator();

        // Act & Assert
        validator.IsValidTopicName("acme.orders").ShouldBeFalse();
    }

    // --- Task 4.6 ---

    [Fact]
    public void IsValidTopicName_SpecialCharacters_ReturnsFalse() {
        // Arrange
        TopicNameValidator validator = CreateValidator();

        // Act & Assert
        validator.IsValidTopicName("acme.orders!.events").ShouldBeFalse();
    }

    // --- Task 4.7 ---

    [Fact]
    public void IsValidTopicName_UppercaseSegments_ReturnsFalse() {
        // Arrange
        TopicNameValidator validator = CreateValidator();

        // Act & Assert
        validator.IsValidTopicName("Acme.Orders.events").ShouldBeFalse();
    }

    // --- Task 4.8 ---

    [Fact]
    public void IsValidTopicName_MaxLengthTopic_ReturnsTrue() {
        // Arrange - 64-char tenant + 64-char domain + ".events" = 136 chars (64+1+64+1+6), within all limits
        TopicNameValidator validator = CreateValidator();
        string longTenant = new('a', 64);
        string longDomain = new('b', 64);
        string topic = $"{longTenant}.{longDomain}.events";
        topic.Length.ShouldBe(136);

        // Act & Assert
        validator.IsValidTopicName(topic).ShouldBeTrue();
    }

    // --- Task 4.9 ---

    [Fact]
    public void DeriveTopicName_ValidIdentity_MatchesPubSubTopic() {
        // Arrange
        TopicNameValidator validator = CreateValidator();
        var identity = new AggregateIdentity("acme", "orders", "order-1");

        // Act
        string derivedTopic = validator.DeriveTopicName(identity);

        // Assert
        derivedTopic.ShouldBe(identity.PubSubTopic);
        derivedTopic.ShouldBe("acme.orders.events");
    }

    // --- Task 4.10 ---

    [Fact]
    public void DeriveTopicName_IdenticalIdentities_ProduceDeterministicTopics() {
        // Arrange
        TopicNameValidator validator = CreateValidator();
        var identity1 = new AggregateIdentity("acme", "orders", "order-1");
        var identity2 = new AggregateIdentity("acme", "orders", "order-2");
        var identity3 = new AggregateIdentity("acme", "orders", "order-3");

        // Act
        string topic1 = validator.DeriveTopicName(identity1);
        string topic2 = validator.DeriveTopicName(identity2);
        string topic3 = validator.DeriveTopicName(identity3);

        // Assert - same tenant+domain always produces same topic regardless of aggregate ID
        topic1.ShouldBe(topic2);
        topic2.ShouldBe(topic3);
        topic1.ShouldBe("acme.orders.events");
    }

    // --- Additional validation edge cases ---

    [Fact]
    public void IsValidTopicName_NullInput_ThrowsArgumentNullException() {
        // Arrange
        TopicNameValidator validator = CreateValidator();

        // Act & Assert
        _ = Should.Throw<ArgumentNullException>(() => validator.IsValidTopicName(null!));
    }

    [Fact]
    public void IsValidTopicName_WhitespaceOnly_ReturnsFalse() {
        // Arrange
        TopicNameValidator validator = CreateValidator();

        // Act & Assert
        validator.IsValidTopicName("   ").ShouldBeFalse();
    }

    [Fact]
    public void IsValidTopicName_ExceedsKafkaMaxLength_ReturnsFalse() {
        // Arrange - topic > 249 chars
        TopicNameValidator validator = CreateValidator();
        string longTenant = new('a', 200);
        string longDomain = new('b', 200);
        string topic = $"{longTenant}.{longDomain}.events";

        // Act & Assert
        validator.IsValidTopicName(topic).ShouldBeFalse();
    }

    [Fact]
    public void IsValidTopicName_TopicApproachingLengthLimit_ReturnsTrue_LogsWarning() {
        // Arrange - topic > 200 chars but < 249 chars
        ILogger<TopicNameValidator> logger = Substitute.For<ILogger<TopicNameValidator>>();
        var validator = new TopicNameValidator(logger);
        string longTenant = new('a', 100);
        string longDomain = new('b', 100);
        string topic = $"{longTenant}.{longDomain}.events";
        topic.Length.ShouldBe(208);

        // Act
        bool result = validator.IsValidTopicName(topic);

        // Assert
        result.ShouldBeTrue();
        logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("approaching")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void DeriveTopicName_NullIdentity_ThrowsArgumentNullException() {
        // Arrange
        TopicNameValidator validator = CreateValidator();

        // Act & Assert
        _ = Should.Throw<ArgumentNullException>(() => validator.DeriveTopicName(null!));
    }
}
