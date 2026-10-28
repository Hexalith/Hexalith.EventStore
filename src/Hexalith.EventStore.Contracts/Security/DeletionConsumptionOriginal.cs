using System.Runtime.Serialization;

namespace Hexalith.EventStore.Contracts.Security;

/// <summary>An immutable retained operation input and its original receipt-bearing outcome. Reading it grants no effect.</summary>
/// <typeparam name="TRequest">The exact operation's request type.</typeparam>
/// <param name="Original">The independently anchored original input, never reconstructed from later batch state.</param>
/// <param name="Outcome">The outcome retained at that operation's commit, including a reservation before physical completion.</param>
[DataContract]
[KnownType(typeof(System.Collections.ObjectModel.ReadOnlyCollection<ProtectionTarget>))]
[KnownType(typeof(System.Collections.ObjectModel.ReadOnlyCollection<DeletionTargetReceipt>))]
public sealed record DeletionConsumptionOriginal<TRequest>([property: DataMember] TRequest Original,
    [property: DataMember] DeletionConsumptionOutcome Outcome);
