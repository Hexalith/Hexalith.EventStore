using System.Collections;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>Hostile false-count vector; each element is empty to avoid hiding traversal behind the byte allowance.</summary>
internal sealed class FalseCountGuardMutations : IReadOnlyList<GuardedStateMutation>
{
    public int Count => 0;
    public GuardedStateMutation this[int index] => new("cell-" + index, 0, GuardedTransactionFixture.Hash([]), []);
    public IEnumerator<GuardedStateMutation> GetEnumerator() { for (int index = 0; index < 1002; index++) { yield return this[index]; } }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
