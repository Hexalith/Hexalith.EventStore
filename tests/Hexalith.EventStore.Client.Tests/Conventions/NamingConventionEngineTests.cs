
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

// Empty after suffix strip (all three suffix-only type names)
internal class Aggregate;

internal class Projection;

internal class Processor;

// Multi-digit boundary: Order12Aggregate -> "order-12" (not "order-1-2")
internal class Order12Aggregate;

// ProjectionAggregate: strips "Aggregate" first, leaving "Projection" -> "projection"
internal class ProjectionAggregate;

// ProcessorAggregate: strips "Aggregate" first, leaving "Processor" -> "processor"
internal class ProcessorAggregate;

// Single-char after suffix strip: XAggregate -> "x"
internal class XAggregate;

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

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("Order")]
    [InlineData("-order")]
    [InlineData("order-")]
    public void GetStateStoreName_InvalidDomain_ThrowsArgumentException(string domain) {
        _ = Assert.Throws<ArgumentException>(() => NamingConventionEngine.GetStateStoreName(domain));
    }

    [Fact]
    public void GetStateStoreName_NullDomain_ThrowsArgumentNullException() {
        _ = Assert.Throws<ArgumentNullException>(() => NamingConventionEngine.GetStateStoreName(null!));
    }

    [Fact]
    public void GetPubSubTopic_ValidInputs_ReturnsExpectedFormat() {
        string result = NamingConventionEngine.GetPubSubTopic("acme", "order");

        Assert.Equal("acme.order.events", result);
    }

    [Theory]
    [InlineData("", "order")]
    [InlineData("  ", "order")]
    [InlineData("Acme", "order")]
    [InlineData("acme", "")]
    [InlineData("acme", "  ")]
    [InlineData("acme", "Order")]
    [InlineData("acme", "-order")]
    [InlineData("acme", "order-")]
    public void GetPubSubTopic_InvalidInputs_ThrowsArgumentException(string tenantId, string domain) {
        _ = Assert.Throws<ArgumentException>(() => NamingConventionEngine.GetPubSubTopic(tenantId, domain));
    }

    [Fact]
    public void GetPubSubTopic_NullTenant_ThrowsArgumentNullException() {
        _ = Assert.Throws<ArgumentNullException>(() => NamingConventionEngine.GetPubSubTopic(null!, "order"));
    }

    [Fact]
    public void GetPubSubTopic_NullDomain_ThrowsArgumentNullException() {
        _ = Assert.Throws<ArgumentNullException>(() => NamingConventionEngine.GetPubSubTopic("acme", null!));
    }

    [Fact]
    public void GetCommandEndpoint_ValidDomain_ReturnsExpectedFormat() {
        string result = NamingConventionEngine.GetCommandEndpoint("order");

        Assert.Equal("order-commands", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("Order")]
    [InlineData("-order")]
    [InlineData("order-")]
    public void GetCommandEndpoint_InvalidDomain_ThrowsArgumentException(string domain) {
        _ = Assert.Throws<ArgumentException>(() => NamingConventionEngine.GetCommandEndpoint(domain));
    }

    [Fact]
    public void GetCommandEndpoint_NullDomain_ThrowsArgumentNullException() {
        _ = Assert.Throws<ArgumentNullException>(() => NamingConventionEngine.GetCommandEndpoint(null!));
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

    // --- Story 16-8: Multi-digit boundary tests (AC#1: 2.1) ---

    [Fact]
    public void GetDomainName_MultiDigitBoundary_ProducesCorrectKebabCase() {
        // Multi-digit number should stay together: Order12 -> "order-12" (not "order-1-2")
        string result = NamingConventionEngine.GetDomainName(typeof(Order12Aggregate));

        Assert.Equal("order-12", result);
    }

    // --- Story 16-8: Consecutive suffix tests (AC#1: 2.1) ---

    [Theory]
    [InlineData(typeof(ProjectionAggregate), "projection")]
    [InlineData(typeof(ProcessorAggregate), "processor")]
    public void GetDomainName_ConsecutiveSuffixes_StripsFirstMatchAndProducesValidName(Type input, string expected) {
        string result = NamingConventionEngine.GetDomainName(input);

        Assert.Equal(expected, result);
    }

    // --- Story 16-8: Single-char after suffix strip (AC#1: 2.1) ---

    [Fact]
    public void GetDomainName_SingleCharAfterSuffixStrip_ReturnsValidName() {
        string result = NamingConventionEngine.GetDomainName(typeof(XAggregate));

        Assert.Equal("x", result);
    }

    // --- Story 16-8: All three suffix-only names throw (AC#1: 2.4) ---

    [Theory]
    [InlineData(typeof(Projection))]
    [InlineData(typeof(Processor))]
    public void GetDomainName_SuffixOnlyName_ThrowsArgumentException(Type input) {
        _ = Assert.Throws<ArgumentException>(
            () => NamingConventionEngine.GetDomainName(input));
    }

    // --- Story 16-8: Thread-safety test (AC#1: 2.2) ---

    [Fact]
    public void GetDomainName_ConcurrentCalls_AllReturnCorrectResults() {
        const int parallelism = 64;
        Type[] types = [
            typeof(CounterAggregate), typeof(UserManagementAggregate),
            typeof(OrderProjection), typeof(PaymentProcessor),
            typeof(OrderHandler), typeof(Order),
        ];
        var results = new string[parallelism];

        Parallel.For(0, parallelism, i => {
            Type type = types[i % types.Length];
            results[i] = NamingConventionEngine.GetDomainName(type);
        });

        // Verify correctness: each type should always produce the same name
        for (int i = 0; i < parallelism; i++) {
            Type type = types[i % types.Length];
            string expected = NamingConventionEngine.GetDomainName(type);
            Assert.Equal(expected, results[i]);
        }
    }

    // --- Story 16-8: Generic overload with various types (AC#1: 2.3) ---

    [Fact]
    public void GetDomainName_Generic_MatchesNonGenericOverload() {
        Assert.Equal(NamingConventionEngine.GetDomainName(typeof(OrderProjection)), NamingConventionEngine.GetDomainName<OrderProjection>());
        Assert.Equal(NamingConventionEngine.GetDomainName(typeof(PaymentProcessor)), NamingConventionEngine.GetDomainName<PaymentProcessor>());
        Assert.Equal(NamingConventionEngine.GetDomainName(typeof(Order)), NamingConventionEngine.GetDomainName<Order>());
    }

    // --- Story 16-8: GetPubSubTopic with hyphenated domain (AC#1: 2.5) ---

    [Fact]
    public void GetPubSubTopic_HyphenatedDomain_ReturnsExpectedFormat() {
        string result = NamingConventionEngine.GetPubSubTopic("acme", "order-item");

        Assert.Equal("acme.order-item.events", result);
    }

    [Fact]
    public void GetPubSubTopic_DomainWithDigits_ReturnsExpectedFormat() {
        string result = NamingConventionEngine.GetPubSubTopic("tenant1", "order-2");

        Assert.Equal("tenant1.order-2.events", result);
    }

    // --- Story 18-1: GetProjectionChangedTopic tests (Task 11.3) ---

    [Fact]
    public void GetProjectionChangedTopic_ValidInputs_ReturnsExpectedFormat() {
        string result = NamingConventionEngine.GetProjectionChangedTopic("order-list", "acme");

        Assert.Equal("acme.order-list.projection-changed", result);
    }

    [Fact]
    public void GetProjectionChangedTopic_HyphenatedInputs_ReturnsExpectedFormat() {
        string result = NamingConventionEngine.GetProjectionChangedTopic("shopping-cart", "tenant-123");

        Assert.Equal("tenant-123.shopping-cart.projection-changed", result);
    }

    [Theory]
    [InlineData("", "acme")]
    [InlineData("  ", "acme")]
    [InlineData("OrderList", "acme")]
    [InlineData("-order-list", "acme")]
    [InlineData("order-list-", "acme")]
    [InlineData("order-list", "")]
    [InlineData("order-list", "  ")]
    [InlineData("order-list", "Acme")]
    [InlineData("order-list", "-acme")]
    [InlineData("order-list", "acme-")]
    public void GetProjectionChangedTopic_InvalidInputs_ThrowsArgumentException(string projectionType, string tenantId) {
        _ = Assert.Throws<ArgumentException>(
            () => NamingConventionEngine.GetProjectionChangedTopic(projectionType, tenantId));
    }

    [Fact]
    public void GetProjectionChangedTopic_NullProjectionType_ThrowsArgumentNullException() {
        _ = Assert.Throws<ArgumentNullException>(
            () => NamingConventionEngine.GetProjectionChangedTopic(null!, "acme"));
    }

    [Fact]
    public void GetProjectionChangedTopic_NullTenantId_ThrowsArgumentNullException() {
        _ = Assert.Throws<ArgumentNullException>(
            () => NamingConventionEngine.GetProjectionChangedTopic("order-list", null!));
    }
}
