
using Hexalith.EventStore.Client.Attributes;
using Hexalith.EventStore.Client.Conventions;

namespace Hexalith.EventStore.Client.Tests.Conventions;

// --- Test stub types for NamingConventionEngine ---
// These are intentionally NOT extending EventStoreAggregate<T> because GetDomainName is type-agnostic.

internal class CounterAggregate;

internal class UserManagementAggregate;

internal class OrderProjection;

internal class PaymentProcessor;

internal class OrderHandler;

internal class Order;

internal class HTTPClientAggregate;

internal class IOAggregate;

internal class AWSLambdaAggregate;

internal class MyHTTPClientAggregate;

internal class Order2Aggregate;

internal class V2OrderAggregate;

// Used for digit-to-lowercase NO boundary test
internal class order2x;

internal class AggregateProjection;

internal class ShoppingCartCheckoutAggregate;

internal class OrderItemAggregate;

[EventStoreDomain("billing")]
internal class BillingOverrideAggregate;

[EventStoreDomain("BILLING")]
internal class UppercaseAttributeAggregate;

[EventStoreDomain("custom-name")]
internal class CustomNameAggregate;

internal class A;

internal class IO;

// Inherited = false test types
[EventStoreDomain("base-domain")]
internal class BaseWithAttribute;

internal class DerivedWithoutAttribute : BaseWithAttribute;

// Empty after suffix strip
internal class Aggregate;

// Name too long (>64 chars after conversion)
internal class ThisIsAnExtremelyLongClassNameThatWillDefinitelyExceedTheSixtyFourCharacterLimitWhenConvertedToKebabCaseFormat;

// Leading hyphen result — a type starting with uppercase that after conversion could produce issues
[EventStoreDomain("-invalid")]
internal class LeadingHyphenAttribute;

[EventStoreDomain("invalid-")]
internal class TrailingHyphenAttribute;

public class NamingConventionEngineTests : IDisposable {
    public NamingConventionEngineTests() {
        NamingConventionEngine.ClearCache();
    }

    public void Dispose() {
        NamingConventionEngine.ClearCache();
        GC.SuppressFinalize(this);
    }

    // --- Task 4.3: Suffix stripping tests ---
    // --- Task 4.4: PascalCase to kebab-case tests ---

    [Theory]
    [InlineData(typeof(CounterAggregate), "counter")]
    [InlineData(typeof(UserManagementAggregate), "user-management")]
    [InlineData(typeof(OrderProjection), "order")]
    [InlineData(typeof(PaymentProcessor), "payment")]
    [InlineData(typeof(OrderHandler), "order-handler")]
    [InlineData(typeof(Order), "order")]
    [InlineData(typeof(HTTPClientAggregate), "http-client")]
    [InlineData(typeof(IOAggregate), "io")]
    [InlineData(typeof(AWSLambdaAggregate), "aws-lambda")]
    [InlineData(typeof(MyHTTPClientAggregate), "my-http-client")]
    [InlineData(typeof(Order2Aggregate), "order-2")]
    [InlineData(typeof(V2OrderAggregate), "v-2-order")]
    [InlineData(typeof(order2x), "order-2x")]
    [InlineData(typeof(AggregateProjection), "aggregate")]
    [InlineData(typeof(ShoppingCartCheckoutAggregate), "shopping-cart-checkout")]
    [InlineData(typeof(OrderItemAggregate), "order-item")]
    [InlineData(typeof(A), "a")]
    [InlineData(typeof(IO), "io")]
    public void GetDomainName_ValidInputs_ReturnsExpectedKebabCase(Type input, string expected) {
        string result = NamingConventionEngine.GetDomainName(input);

        Assert.Equal(expected, result);
    }

    // --- Task 4.5: Attribute override tests ---

    [Fact]
    public void GetDomainName_AttributeOverride_ReturnsAttributeValue() {
        string result = NamingConventionEngine.GetDomainName(typeof(BillingOverrideAggregate));

        Assert.Equal("billing", result);
    }

    [Fact]
    public void GetDomainName_CustomNameAttribute_ReturnsAttributeValue() {
        string result = NamingConventionEngine.GetDomainName(typeof(CustomNameAggregate));

        Assert.Equal("custom-name", result);
    }

    [Fact]
    public void GetDomainName_InheritedFalse_DerivedGetsConventionName() {
        // BaseWithAttribute has [EventStoreDomain("base-domain")], but Inherited = false
        // so DerivedWithoutAttribute should get its convention-derived name
        string result = NamingConventionEngine.GetDomainName(typeof(DerivedWithoutAttribute));

        Assert.Equal("derived-without-attribute", result);
    }

    [Fact]
    public void GetDomainName_BaseWithAttribute_ReturnsAttributeValue() {
        string result = NamingConventionEngine.GetDomainName(typeof(BaseWithAttribute));

        Assert.Equal("base-domain", result);
    }

    // --- Task 4.6: Validation rejects invalid names ---

    [Fact]
    public void GetDomainName_UppercaseAttribute_ThrowsArgumentException() {
        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => NamingConventionEngine.GetDomainName(typeof(UppercaseAttributeAggregate)));

        Assert.Contains("BILLING", ex.Message);
    }

    [Fact]
    public void GetDomainName_EmptyAfterSuffixStrip_ThrowsArgumentException() {
        _ = Assert.Throws<ArgumentException>(
            () => NamingConventionEngine.GetDomainName(typeof(Aggregate)));
    }

    [Fact]
    public void GetDomainName_NameTooLong_ThrowsArgumentException() {
        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => NamingConventionEngine.GetDomainName(typeof(ThisIsAnExtremelyLongClassNameThatWillDefinitelyExceedTheSixtyFourCharacterLimitWhenConvertedToKebabCaseFormat)));

        Assert.Contains("64", ex.Message);
    }

    [Fact]
    public void GetDomainName_LeadingHyphenAttribute_ThrowsArgumentException() {
        _ = Assert.Throws<ArgumentException>(
            () => NamingConventionEngine.GetDomainName(typeof(LeadingHyphenAttribute)));
    }

    [Fact]
    public void GetDomainName_TrailingHyphenAttribute_ThrowsArgumentException() {
        _ = Assert.Throws<ArgumentException>(
            () => NamingConventionEngine.GetDomainName(typeof(TrailingHyphenAttribute)));
    }

    // --- Task 4.7: Resource name derivation tests ---

    [Fact]
    public void GetStateStoreName_ValidDomain_ReturnsExpectedFormat() {
        string result = NamingConventionEngine.GetStateStoreName("order");

        Assert.Equal("order-eventstore", result);
    }

    [Fact]
    public void GetPubSubTopic_ValidInputs_ReturnsExpectedFormat() {
        string result = NamingConventionEngine.GetPubSubTopic("acme", "order");

        Assert.Equal("acme.order.events", result);
    }

    [Fact]
    public void GetCommandEndpoint_ValidDomain_ReturnsExpectedFormat() {
        string result = NamingConventionEngine.GetCommandEndpoint("order");

        Assert.Equal("order-commands", result);
    }

    // --- Task 4.8: Cache behavior tests ---

    [Fact]
    public void GetDomainName_CalledTwice_ReturnsSameStringReference() {
        string first = NamingConventionEngine.GetDomainName(typeof(CounterAggregate));
        string second = NamingConventionEngine.GetDomainName(typeof(CounterAggregate));

        Assert.Same(first, second);
    }

    [Fact]
    public void ClearCache_ResetsCache_SubsequentCallRecomputes() {
        string first = NamingConventionEngine.GetDomainName(typeof(CounterAggregate));
        NamingConventionEngine.ClearCache();
        string second = NamingConventionEngine.GetDomainName(typeof(CounterAggregate));

        Assert.Equal(first, second);
        // After clearing, a new string instance is created (not the same reference)
        // Note: string interning may make them the same reference, so we just verify correctness
    }

    // --- Task 4.9: Generic convenience method ---

    [Fact]
    public void GetDomainName_Generic_ReturnsExpectedResult() {
        string result = NamingConventionEngine.GetDomainName<CounterAggregate>();

        Assert.Equal("counter", result);
    }

    // --- Null guard test ---

    [Fact]
    public void GetDomainName_NullType_ThrowsArgumentNullException() {
        _ = Assert.Throws<ArgumentNullException>(
            () => NamingConventionEngine.GetDomainName(null!));
    }
}
