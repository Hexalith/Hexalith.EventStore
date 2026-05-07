
using System.Reflection;

using Hexalith.EventStore.Contracts.Aggregates;
using Hexalith.EventStore.Sample.Counter.State;
using Hexalith.EventStore.Testing.Compliance;

using Shouldly;

namespace Hexalith.EventStore.Sample.Tests.Compliance;

/// <summary>
/// Test ID: 1.5-UNIT-012. Closes Epic-1 R-T3 / TG-2 (runtime-only ITerminatable Apply contract — adoption gap).
/// <para>
/// Architectural Tier 1 scan: walks the production assembly graph reachable from this test project's
/// reference closure, finds every concrete <see cref="ITerminatable"/> implementor, and asserts each one
/// satisfies the <c>Apply(<see cref="Hexalith.EventStore.Contracts.Events.AggregateTerminated"/>)</c>
/// contract via <see cref="TerminatableComplianceAssertions"/>. Per-domain compliance calls (e.g.,
/// <c>CounterAggregateTests.AssertTerminatableCompliance&lt;CounterState&gt;</c>) remain valuable — but
/// this scan is the safety net that makes the rule automatic for any new domain that ships without one.
/// </para>
/// <para>
/// Scope: assemblies named <c>Hexalith.EventStore.*</c> reachable from the
/// <see cref="CounterState"/> assembly's transitive references. New production projects that introduce
/// <see cref="ITerminatable"/> states must either be reachable from this graph or must add their own
/// equivalent scan in their tests project.
/// </para>
/// </summary>
public class ITerminatableSolutionComplianceTests {
    private const string AssemblyPrefix = "Hexalith.EventStore";

    public static TheoryData<Type> ITerminatableImplementors() {
        var data = new TheoryData<Type>();
        foreach (Type implementor in DiscoverITerminatableImplementors()) {
            data.Add(implementor);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ITerminatableImplementors))]
    public void EveryProductionITerminatable_SatisfiesApplyAggregateTerminatedContract(Type stateType) {
        MethodInfo helper = typeof(TerminatableComplianceAssertions)
            .GetMethod(
                name: nameof(TerminatableComplianceAssertions.AssertTerminatableCompliance),
                genericParameterCount: 1,
                types: Type.EmptyTypes)
            ?? throw new InvalidOperationException(
                $"Cannot resolve {nameof(TerminatableComplianceAssertions)}.{nameof(TerminatableComplianceAssertions.AssertTerminatableCompliance)}<TState>().");
        MethodInfo invocation = helper.MakeGenericMethod(stateType);

        try {
            _ = invocation.Invoke(null, parameters: null);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null) {
            // Surface the underlying MissingApplyMethodException with its rich state-type / event-type context
            // instead of the wrapper, so a failing scan reads identically to a per-domain compliance call.
            throw ex.InnerException;
        }
    }

    [Fact]
    public void Scan_FindsAtLeastOneITerminatableImplementor_GuardsAgainstSilentEmptyPass() {
        // If the scan returns no types — for example because production references are pruned by a refactor
        // — every theory fact above would skip silently. This canary forces the regression to be visible.
        IReadOnlyList<Type> implementors = DiscoverITerminatableImplementors();

        implementors.ShouldNotBeEmpty();
        implementors.ShouldContain(typeof(CounterState));
    }

    private static IReadOnlyList<Type> DiscoverITerminatableImplementors() {
        var implementors = new List<Type>();
        foreach (Assembly assembly in DiscoverProductionAssemblies()) {
            Type[] types;
            try {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex) {
                types = ex.Types.Where(t => t is not null).Cast<Type>().ToArray();
            }

            foreach (Type type in types) {
                if (type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition) {
                    continue;
                }

                if (!type.IsClass) {
                    continue;
                }

                if (!typeof(ITerminatable).IsAssignableFrom(type)) {
                    continue;
                }

                implementors.Add(type);
            }
        }

        return implementors;
    }

    private static IEnumerable<Assembly> DiscoverProductionAssemblies() {
        // Anchor the walk at the Sample assembly (where production ITerminatable implementors live today)
        // and traverse the Hexalith.EventStore.* reference closure breadth-first. Force-loads via Assembly.Load
        // so types from referenced assemblies that have not yet been touched at test time still get scanned.
        Assembly anchor = typeof(CounterState).Assembly;
        var visited = new HashSet<Assembly> { anchor };
        var queue = new Queue<Assembly>();
        queue.Enqueue(anchor);

        while (queue.Count > 0) {
            Assembly current = queue.Dequeue();

            foreach (AssemblyName referenced in current.GetReferencedAssemblies()) {
                if (referenced.Name is null
                    || !referenced.Name.StartsWith(AssemblyPrefix, StringComparison.Ordinal)) {
                    continue;
                }

                Assembly loaded;
                try {
                    loaded = Assembly.Load(referenced);
                }
                catch (FileNotFoundException) {
                    // Skip assemblies that cannot be loaded from the test runtime — the scan covers what is
                    // resolvable; new production projects that need coverage must be reachable.
                    continue;
                }

                if (visited.Add(loaded)) {
                    queue.Enqueue(loaded);
                }
            }
        }

        return visited;
    }
}
