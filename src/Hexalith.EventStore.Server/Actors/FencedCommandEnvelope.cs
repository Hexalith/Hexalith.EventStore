using System.Runtime.Serialization;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Commands;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Internal aggregate request carrying a command and its signed current-fence capability.</summary>
/// <param name="Command">The public command envelope with no opaque idempotency key.</param>
/// <param name="ExecutionContext">The internal signed execution capability.</param>
[DataContract]
public sealed record FencedCommandEnvelope(
    [property: DataMember] CommandEnvelope Command,
    [property: DataMember] IdempotencyExecutionContext ExecutionContext);
