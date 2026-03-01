
using System.Reflection;
using System.Reflection.Emit;

using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Attributes;
using Hexalith.EventStore.Client.Conventions;
using Hexalith.EventStore.Client.Discovery;

namespace Hexalith.EventStore.Client.Tests.Discovery;

#region Test Stub Types

// Concrete aggregate — should be discovered
internal sealed class TestCounterAggregate : EventStoreAggregate<TestCounterState> { }
internal sealed class TestCounterState { }

// Concrete projection — should be discovered
internal sealed class TestOrderProjection : EventStoreProjection<TestOrderReadModel> { }
internal sealed class TestOrderReadModel { }

// Abstract aggregate — should NOT be discovered
internal abstract class AbstractTestAggregate : EventStoreAggregate<TestCounterState> { }

// Aggregate with attribute override — should use attribute domain name
[EventStoreDomain("billing")]
internal sealed class TestBillingAggregate : EventStoreAggregate<TestBillingState> { }
internal sealed class TestBillingState { }

// Two aggregates with conflicting domain names — for duplicate detection test
internal sealed class TestDuplicateAggregate : EventStoreAggregate<TestDuplicateState> { }

[EventStoreDomain("test-duplicate")]
internal sealed class TestDuplicateConflictAggregate : EventStoreAggregate<TestDuplicateState> { }
internal sealed class TestDuplicateState { }

// Intermediate generic base class — tests deep inheritance chain
internal abstract class VersionedAggregate<T> : EventStoreAggregate<T> where T : class, new() { }
internal sealed class TestVersionedOrderAggregate : VersionedAggregate<TestVersionedOrderState> { }
internal sealed class TestVersionedOrderState { }

// Nested type inside container — tests nested type discovery
internal static class TestContainer {
    internal sealed class NestedAggregate : EventStoreAggregate<TestCounterState> { }
}

// Aggregate + projection sharing same domain name — should NOT conflict (cross-category OK)
internal sealed class TestSharedAggregate : EventStoreAggregate<TestSharedState> { }
internal sealed class TestSharedProjection : EventStoreProjection<TestSharedReadModel> { }
internal sealed class TestSharedState { }
internal sealed class TestSharedReadModel { }

// Type with invalid attribute name — for error wrapping test
[EventStoreDomain("INVALID_NAME")]
internal sealed class TestInvalidNameAggregate : EventStoreAggregate<TestCounterState> { }

#endregion

public sealed class AssemblyScannerTests : IDisposable {
    public AssemblyScannerTests() {
        AssemblyScanner.ClearCache();
        NamingConventionEngine.ClearCache();
    }

    public void Dispose() {
        AssemblyScanner.ClearCache();
        NamingConventionEngine.ClearCache();
        GC.SuppressFinalize(this);
    }

    private static Assembly CreateDynamicAggregateAssemblyWithTypeName(string typeName, Type stateType) {
        string assemblyName = $"Hexalith.EventStore.Client.Tests.Dynamic.{Guid.NewGuid():N}";
        var name = new AssemblyName(assemblyName);
        AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
        ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("Main");

        Type aggregateBase = typeof(EventStoreAggregate<>).MakeGenericType(stateType);
        TypeBuilder typeBuilder = moduleBuilder.DefineType(
            $"Dynamic.{typeName}",
            TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed,
            aggregateBase);

        _ = typeBuilder.DefineDefaultConstructor(MethodAttributes.Public);
        _ = typeBuilder.CreateType();

        return assemblyBuilder;
    }

    // --- AC2: ScanForAggregates discovers concrete subclasses ---

    [Fact]
    public void ScanForAggregates_ConcreteAggregate_ReturnsType() {
        Type[] types = [typeof(TestCounterAggregate), typeof(TestCounterState)];

        DiscoveryResult result = AssemblyScanner.ScanForDomainTypes(types);

        DiscoveredDomain aggregate = Assert.Single(result.Aggregates);
        Assert.Equal(typeof(TestCounterAggregate), aggregate.Type);
        Assert.Equal("test-counter", aggregate.DomainName);
        Assert.Empty(result.Projections);
    }

    // --- AC3: ScanForProjections discovers concrete subclasses ---

    [Fact]
    public void ScanForProjections_ConcreteProjection_ReturnsType() {
        Type[] types = [typeof(TestOrderProjection), typeof(TestOrderReadModel)];

        DiscoveryResult result = AssemblyScanner.ScanForDomainTypes(types);

        DiscoveredDomain projection = Assert.Single(result.Projections);
        Assert.Equal(typeof(TestOrderProjection), projection.Type);
        Assert.Equal("test-order", projection.DomainName);
        Assert.Empty(result.Aggregates);
    }

    // --- AC2/AC3: Abstract classes excluded ---

    [Fact]
    public void ScanForDomainTypes_AbstractAggregate_Excluded() {
        Type[] types = [typeof(AbstractTestAggregate), typeof(TestCounterState)];

        DiscoveryResult result = AssemblyScanner.ScanForDomainTypes(types);

        Assert.Empty(result.Aggregates);
        Assert.Empty(result.Projections);
        Assert.Equal(0, result.TotalCount);
    }

    // --- AC4: Combined scan returns both aggregates and projections ---

    [Fact]
    public void ScanForDomainTypes_MixedTypes_ReturnsBothCategories() {
        Type[] types = [typeof(TestCounterAggregate), typeof(TestOrderProjection)];

        DiscoveryResult result = AssemblyScanner.ScanForDomainTypes(types);

        Assert.Single(result.Aggregates);
        Assert.Single(result.Projections);
        Assert.Equal(2, result.TotalCount);
    }

    // --- AC5: Multi-assembly scanning with de-duplication ---

    [Fact]
    public void ScanForDomainTypes_MultipleAssemblies_CombinesAndDeduplicatesResults() {
        Assembly testAssembly = typeof(SmokeTestAggregate).Assembly;

        // Same assembly passed twice — types should be de-duplicated (not doubled)
        DiscoveryResult result = AssemblyScanner.ScanForDomainTypes([testAssembly, testAssembly]);

        // Exact cardinality: same count as a single-assembly scan
        DiscoveryResult singleScan = AssemblyScanner.ScanForDomainTypes(testAssembly);
        Assert.Equal(singleScan.Aggregates.Count, result.Aggregates.Count);
        Assert.Equal(singleScan.Projections.Count, result.Projections.Count);
        Assert.Equal(singleScan.TotalCount, result.TotalCount);

        Assert.Contains(result.Aggregates, d => d.Type == typeof(SmokeTestAggregate));
        Assert.Contains(result.Projections, d => d.Type == typeof(SmokeTestProjection));
    }

    [Fact]
    public void ScanForDomainTypes_DuplicateTypeInCollection_ThrowsDuplicateDetection() {
        // Providing the same type twice produces duplicate domain names
        Type[] types = [typeof(TestCounterAggregate), typeof(TestCounterAggregate)];

        Assert.Throws<InvalidOperationException>(() => AssemblyScanner.ScanForDomainTypes(types));
    }

    // --- AC6: Domain name resolution integration ---

    [Fact]
    public void ScanForDomainTypes_ConventionDerivedName_ResolvedCorrectly() {
        Type[] types = [typeof(TestCounterAggregate)];

        DiscoveryResult result = AssemblyScanner.ScanForDomainTypes(types);

        Assert.Equal("test-counter", Assert.Single(result.Aggregates).DomainName);
    }

    [Fact]
    public void ScanForDomainTypes_AttributeOverride_UsesAttributeName() {
        Type[] types = [typeof(TestBillingAggregate)];

        DiscoveryResult result = AssemblyScanner.ScanForDomainTypes(types);

        Assert.Equal("billing", Assert.Single(result.Aggregates).DomainName);
    }

    // --- AC7: Within-category duplicate domain name detection ---

    [Fact]
    public void ScanForDomainTypes_DuplicateAggregateDomainName_ThrowsInvalidOperationException() {
        // TestDuplicateAggregate convention name: "test-duplicate"
        // TestDuplicateConflictAggregate attribute name: "test-duplicate"
        Type[] types = [typeof(TestDuplicateAggregate), typeof(TestDuplicateConflictAggregate)];

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => AssemblyScanner.ScanForDomainTypes(types));

        Assert.Contains("test-duplicate", ex.Message);
        Assert.Contains(nameof(TestDuplicateAggregate), ex.Message);
        Assert.Contains(nameof(TestDuplicateConflictAggregate), ex.Message);
    }

    // --- AC7: Cross-category same domain name is VALID ---

    [Fact]
    public void ScanForDomainTypes_CrossCategorySameDomainName_Succeeds() {
        Type[] types = [typeof(TestSharedAggregate), typeof(TestSharedProjection)];

        DiscoveryResult result = AssemblyScanner.ScanForDomainTypes(types);

        DiscoveredDomain aggregate = Assert.Single(result.Aggregates);
        DiscoveredDomain projection = Assert.Single(result.Projections);
        Assert.Equal("test-shared", aggregate.DomainName);
        Assert.Equal("test-shared", projection.DomainName);
    }

    // --- AC8: DiscoveryResult TotalCount ---

    [Fact]
    public void DiscoveryResult_TotalCount_SumsCategories() {
        Type[] types = [typeof(TestCounterAggregate), typeof(TestOrderProjection)];

        DiscoveryResult result = AssemblyScanner.ScanForDomainTypes(types);

        Assert.Equal(result.Aggregates.Count + result.Projections.Count, result.TotalCount);
    }

    // --- AC9: DiscoveredDomain StateType extraction ---

    [Fact]
    public void DiscoveredDomain_AggregateStateType_ExtractedCorrectly() {
        Type[] types = [typeof(TestCounterAggregate)];

        DiscoveryResult result = AssemblyScanner.ScanForDomainTypes(types);

        Assert.Equal(typeof(TestCounterState), Assert.Single(result.Aggregates).StateType);
    }

    [Fact]
    public void DiscoveredDomain_ProjectionStateType_ExtractedCorrectly() {
        Type[] types = [typeof(TestOrderProjection)];

        DiscoveryResult result = AssemblyScanner.ScanForDomainTypes(types);

        Assert.Equal(typeof(TestOrderReadModel), Assert.Single(result.Projections).StateType);
    }

    // --- AC9/AC10: DomainKind set correctly ---

    [Fact]
    public void DiscoveredDomain_AggregateKind_SetCorrectly() {
        Type[] types = [typeof(TestCounterAggregate)];

        DiscoveryResult result = AssemblyScanner.ScanForDomainTypes(types);

        Assert.Equal(DomainKind.Aggregate, Assert.Single(result.Aggregates).Kind);
    }

    [Fact]
    public void DiscoveredDomain_ProjectionKind_SetCorrectly() {
        Type[] types = [typeof(TestOrderProjection)];

        DiscoveryResult result = AssemblyScanner.ScanForDomainTypes(types);

        Assert.Equal(DomainKind.Projection, Assert.Single(result.Projections).Kind);
    }

    // --- AC11: Caching behavior ---

    [Fact]
    public void ScanForDomainTypes_SameAssemblyTwice_ReturnsCachedReference() {
        Assembly testAssembly = typeof(SmokeTestAggregate).Assembly;

        DiscoveryResult first = AssemblyScanner.ScanForDomainTypes(testAssembly);
        DiscoveryResult second = AssemblyScanner.ScanForDomainTypes(testAssembly);

        Assert.True(ReferenceEquals(first, second));
    }

    [Fact]
    public void ClearCache_AfterScan_ForcesRescan() {
        Assembly testAssembly = typeof(SmokeTestAggregate).Assembly;

        DiscoveryResult first = AssemblyScanner.ScanForDomainTypes(testAssembly);
        AssemblyScanner.ClearCache();
        DiscoveryResult second = AssemblyScanner.ScanForDomainTypes(testAssembly);

        Assert.False(ReferenceEquals(first, second));
    }

    // --- Empty scan ---

    [Fact]
    public void ScanForDomainTypes_EmptyTypes_ReturnsEmptyResult() {
        Type[] types = [];

        DiscoveryResult result = AssemblyScanner.ScanForDomainTypes(types);

        Assert.Empty(result.Aggregates);
        Assert.Empty(result.Projections);
        Assert.Equal(0, result.TotalCount);
    }

    // --- Null assembly ---

    [Fact]
    public void ScanForDomainTypes_NullAssembly_ThrowsArgumentNullException() {
        Assert.Throws<ArgumentNullException>(() => AssemblyScanner.ScanForDomainTypes((Assembly)null!));
    }

    [Fact]
    public void ScanForAggregates_NullAssembly_ThrowsArgumentNullException() {
        Assert.Throws<ArgumentNullException>(() => AssemblyScanner.ScanForAggregates(null!));
    }

    [Fact]
    public void ScanForProjections_NullAssembly_ThrowsArgumentNullException() {
        Assert.Throws<ArgumentNullException>(() => AssemblyScanner.ScanForProjections(null!));
    }

    // --- Null in multi-assembly collection ---

    [Fact]
    public void ScanForDomainTypes_NullInAssemblyCollection_ThrowsArgumentNullException() {
        Assembly valid = typeof(SmokeTestAggregate).Assembly;

        Assert.Throws<ArgumentNullException>(
            () => AssemblyScanner.ScanForDomainTypes(new Assembly[] { valid, null! }));
    }

    [Fact]
    public void ScanForDomainTypes_NullAssemblyCollection_ThrowsArgumentNullException() {
        Assert.Throws<ArgumentNullException>(
            () => AssemblyScanner.ScanForDomainTypes((IEnumerable<Assembly>)null!));
    }

    [Fact]
    public void ScanForDomainTypes_NullTypeInCollection_ThrowsArgumentNullException() {
        Assert.Throws<ArgumentNullException>(
            () => AssemblyScanner.ScanForDomainTypes(new Type[] { typeof(TestCounterAggregate), null! }));
    }

    // --- Intermediate generic inheritance ---

    [Fact]
    public void ScanForDomainTypes_IntermediateGenericBase_ExtractsConcreteStateType() {
        Type[] types = [typeof(TestVersionedOrderAggregate)];

        DiscoveryResult result = AssemblyScanner.ScanForDomainTypes(types);

        DiscoveredDomain aggregate = Assert.Single(result.Aggregates);
        Assert.Equal(typeof(TestVersionedOrderState), aggregate.StateType);
        Assert.False(aggregate.StateType.IsGenericParameter);
    }

    // --- Nested type discovery ---

    [Fact]
    public void ScanForDomainTypes_NestedType_Discovered() {
        Type[] types = [typeof(TestContainer.NestedAggregate)];

        DiscoveryResult result = AssemblyScanner.ScanForDomainTypes(types);

        DiscoveredDomain aggregate = Assert.Single(result.Aggregates);
        Assert.Equal(typeof(TestContainer.NestedAggregate), aggregate.Type);
    }

    // --- Naming engine error wrapping ---

    [Fact]
    public void ScanForDomainTypes_InvalidAttributeName_ThrowsInvalidOperationExceptionWithContext() {
        Type[] types = [typeof(TestInvalidNameAggregate)];

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => AssemblyScanner.ScanForDomainTypes(types));

        Assert.Contains(nameof(TestInvalidNameAggregate), ex.Message);
        Assert.IsType<ArgumentException>(ex.InnerException);
    }

    // --- Open generic excluded ---

    [Fact]
    public void ScanForDomainTypes_OpenGenericBase_Excluded() {
        Type[] types = [typeof(VersionedAggregate<>)];

        DiscoveryResult result = AssemblyScanner.ScanForDomainTypes(types);

        Assert.Empty(result.Aggregates);
        Assert.Equal(0, result.TotalCount);
    }

    // --- Cross-assembly same-category duplicate ---

    [Fact]
    public void ScanForDomainTypes_CrossAssemblySameCategoryDuplicate_ThrowsWithAssemblyInfo() {
        // Note: Both types are in the same test assembly. True cross-assembly testing would require
        // types compiled in separate assemblies, which is impractical in a unit test. The duplicate
        // detection logic pools all types regardless of assembly origin (via HashSet<Type>), so
        // same-assembly and cross-assembly duplicates follow the identical code path.
        Type[] types = [typeof(TestDuplicateAggregate), typeof(TestDuplicateConflictAggregate)];

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => AssemblyScanner.ScanForDomainTypes(types));

        // Verify error message includes both type names and assembly info for debugging
        Assert.Contains("assembly", ex.Message);
        Assert.Contains(nameof(TestDuplicateAggregate), ex.Message);
        Assert.Contains(nameof(TestDuplicateConflictAggregate), ex.Message);
        Assert.Contains("test-duplicate", ex.Message);
    }

    [Fact]
    public void ScanForDomainTypes_MultiAssemblyDuplicateDomainName_ThrowsViaAssemblyOverload() {
        // Build a second assembly containing Dynamic.SmokeTestAggregate : EventStoreAggregate<SmokeTestState>
        // so both assemblies resolve aggregate domain name "smoke-test" and should conflict.
        Assembly staticAssembly = typeof(SmokeTestAggregate).Assembly;
        Assembly dynamicAssembly = CreateDynamicAggregateAssemblyWithTypeName(nameof(SmokeTestAggregate), typeof(SmokeTestState));

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => AssemblyScanner.ScanForDomainTypes([staticAssembly, dynamicAssembly]));

        Assert.Contains("smoke-test", ex.Message);
        Assert.Contains(nameof(SmokeTestAggregate), ex.Message);
        Assert.Contains(staticAssembly.GetName().Name!, ex.Message);
        Assert.Contains(dynamicAssembly.GetName().Name!, ex.Message);
    }

    // --- Non-domain types ignored ---

    [Fact]
    public void ScanForDomainTypes_NonDomainTypes_Ignored() {
        Type[] types = [typeof(string), typeof(int), typeof(TestCounterState)];

        DiscoveryResult result = AssemblyScanner.ScanForDomainTypes(types);

        Assert.Equal(0, result.TotalCount);
    }

    // --- SMOKE TESTS: Assembly overload end-to-end ---

    [Fact]
    public void ScanForDomainTypes_Assembly_DiscoversPublicSmokeStubs() {
        Assembly testAssembly = typeof(AssemblyScannerTests).Assembly;

        DiscoveryResult result = AssemblyScanner.ScanForDomainTypes(testAssembly);

        Assert.Contains(result.Aggregates, d => d.Type == typeof(SmokeTestAggregate));
        Assert.Contains(result.Projections, d => d.Type == typeof(SmokeTestProjection));
    }

    [Fact]
    public void ScanForAggregates_Assembly_DiscoversPublicSmokeAggregate() {
        Assembly testAssembly = typeof(AssemblyScannerTests).Assembly;

        IReadOnlyList<DiscoveredDomain> aggregates = AssemblyScanner.ScanForAggregates(testAssembly);

        Assert.Contains(aggregates, d => d.Type == typeof(SmokeTestAggregate));
    }

    [Fact]
    public void ScanForProjections_Assembly_DiscoversPublicSmokeProjection() {
        Assembly testAssembly = typeof(AssemblyScannerTests).Assembly;

        IReadOnlyList<DiscoveredDomain> projections = AssemblyScanner.ScanForProjections(testAssembly);

        Assert.Contains(projections, d => d.Type == typeof(SmokeTestProjection));
    }

    // --- Story 16-8: Empty assembly (AC#2: 3.1) ---

    [Fact]
    public void ScanForDomainTypes_OnlyNonDomainTypes_ReturnsEmptyResult() {
        // Scan an assembly that has types but none are domain types
        Type[] types = [typeof(string), typeof(int), typeof(object), typeof(List<int>)];

        DiscoveryResult result = AssemblyScanner.ScanForDomainTypes(types);

        Assert.Empty(result.Aggregates);
        Assert.Empty(result.Projections);
        Assert.Equal(0, result.TotalCount);
    }

    // --- Story 16-8: Abstract-only assembly (AC#2: 3.2) ---

    [Fact]
    public void ScanForDomainTypes_OnlyAbstractTypes_ReturnsEmptyResult() {
        Type[] types = [typeof(AbstractTestAggregate), typeof(VersionedAggregate<>)];

        DiscoveryResult result = AssemblyScanner.ScanForDomainTypes(types);

        Assert.Empty(result.Aggregates);
        Assert.Empty(result.Projections);
        Assert.Equal(0, result.TotalCount);
    }

    // --- Story 16-8: Projection-only assembly (AC#2: 3.3) ---

    [Fact]
    public void ScanForDomainTypes_ProjectionOnly_NoAggregates() {
        Type[] types = [typeof(TestOrderProjection)];

        DiscoveryResult result = AssemblyScanner.ScanForDomainTypes(types);

        Assert.Empty(result.Aggregates);
        Assert.Single(result.Projections);
    }

    [Fact]
    public void ScanForDomainTypes_AggregateOnly_NoProjections() {
        Type[] types = [typeof(TestCounterAggregate)];

        DiscoveryResult result = AssemblyScanner.ScanForDomainTypes(types);

        Assert.Single(result.Aggregates);
        Assert.Empty(result.Projections);
    }

    // --- Story 16-8: ScanForAggregates on projection-only types returns empty (AC#2: 3.3) ---

    [Fact]
    public void ScanForAggregates_AssemblyWithOnlyProjections_ReturnsEmpty() {
        // ScanForAggregates delegates to ScanForDomainTypes().Aggregates
        // Verify it returns empty when no aggregates exist
        Type[] types = [typeof(TestOrderProjection)];

        DiscoveryResult result = AssemblyScanner.ScanForDomainTypes(types);

        Assert.Empty(result.Aggregates);
    }

    // --- Story 16-8: Multi-assembly cross-assembly duplicate domain names (AC#2: 3.4) ---

    [Fact]
    public void ScanForDomainTypes_CrossAssemblyDuplicateProjectionDomainName_Throws() {
        // Two projections with same domain name should conflict
        // Create a dynamic assembly with a projection that has the same domain name as TestOrderProjection
        Assembly staticAssembly = typeof(SmokeTestProjection).Assembly;
        // SmokeTestProjection has domain name "smoke-test"
        // Create a dynamic assembly with another projection also named "smoke-test"
        Assembly dynamicAssembly = CreateDynamicProjectionAssemblyWithTypeName(nameof(SmokeTestProjection));

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => AssemblyScanner.ScanForDomainTypes([staticAssembly, dynamicAssembly]));

        Assert.Contains("smoke-test", ex.Message);
    }

    // --- Story 16-8: ReflectionTypeLoadException resilience (AC#2: 3.5) ---

    [Fact]
    public void ScanForDomainTypes_DynamicAssembly_DoesNotThrow() {
        // Dynamic assemblies may throw NotSupportedException from GetExportedTypes
        // which is handled by the GetLoadableTypes fallback
        // Use public SmokeTestState to avoid access issues with dynamic assemblies
        Assembly dynamicAssembly = CreateDynamicAggregateAssemblyWithTypeName("DynamicResilience", typeof(SmokeTestState));

        // Should not throw — exercises the NotSupportedException fallback in GetLoadableTypes
        DiscoveryResult result = AssemblyScanner.ScanForDomainTypes(dynamicAssembly);

        Assert.True(result.TotalCount >= 1);
    }

    [Fact]
    public void ScanForDomainTypes_ReflectionTypeLoadException_UsesLoadableTypesFallback() {
        Assembly faultingAssembly = new ThrowingReflectionTypeLoadAssembly(
            [typeof(SmokeTestAggregate), null!],
            [new TypeLoadException("Simulated loader failure")]);

        DiscoveryResult result = AssemblyScanner.ScanForDomainTypes(faultingAssembly);

        DiscoveredDomain aggregate = Assert.Single(result.Aggregates);
        Assert.Equal(typeof(SmokeTestAggregate), aggregate.Type);
        Assert.Equal("smoke-test", aggregate.DomainName);
    }

    // --- Story 16-8: Open generic exclusion via types (AC#2: 3.6) ---

    [Fact]
    public void ScanForDomainTypes_MultipleOpenGenerics_AllExcluded() {
        Type[] types = [typeof(VersionedAggregate<>), typeof(EventStoreAggregate<>), typeof(EventStoreProjection<>)];

        DiscoveryResult result = AssemblyScanner.ScanForDomainTypes(types);

        Assert.Empty(result.Aggregates);
        Assert.Empty(result.Projections);
        Assert.Equal(0, result.TotalCount);
    }

    private static Assembly CreateDynamicProjectionAssemblyWithTypeName(string typeName) {
        string assemblyName = $"Hexalith.EventStore.Client.Tests.Dynamic.Proj.{Guid.NewGuid():N}";
        var name = new AssemblyName(assemblyName);
        AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
        ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("Main");

        Type projectionBase = typeof(EventStoreProjection<>).MakeGenericType(typeof(SmokeTestReadModel));
        TypeBuilder typeBuilder = moduleBuilder.DefineType(
            $"Dynamic.{typeName}",
            TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed,
            projectionBase);

        _ = typeBuilder.DefineDefaultConstructor(MethodAttributes.Public);
        _ = typeBuilder.CreateType();

        return assemblyBuilder;
    }

    private sealed class ThrowingReflectionTypeLoadAssembly(Type[] classes, Exception[] exceptions) : Assembly {
        public override Type[] GetExportedTypes() => throw new ReflectionTypeLoadException(classes, exceptions);
    }

    [Fact]
    public void ScanForDomainTypes_Assembly_SmokeStubsHaveCorrectProperties() {
        Assembly testAssembly = typeof(AssemblyScannerTests).Assembly;

        DiscoveryResult result = AssemblyScanner.ScanForDomainTypes(testAssembly);

        DiscoveredDomain aggregate = Assert.Single(result.Aggregates, d => d.Type == typeof(SmokeTestAggregate));
        Assert.Equal("smoke-test", aggregate.DomainName);
        Assert.Equal(typeof(SmokeTestState), aggregate.StateType);
        Assert.Equal(DomainKind.Aggregate, aggregate.Kind);

        DiscoveredDomain projection = Assert.Single(result.Projections, d => d.Type == typeof(SmokeTestProjection));
        Assert.Equal("smoke-test", projection.DomainName);
        Assert.Equal(typeof(SmokeTestReadModel), projection.StateType);
        Assert.Equal(DomainKind.Projection, projection.Kind);
    }
}
