using System.Reflection;

namespace Hexalith.EventStore.Client.Aggregates;

/// <summary>Describes one allow-listed convention Handle method.</summary>
/// <param name="Method">The discovered Handle method.</param>
/// <param name="CommandType">The method's command payload type.</param>
/// <param name="IsAsync">Whether the method returns an asynchronous domain result.</param>
/// <param name="IsStatic">Whether the method requires no aggregate instance.</param>
/// <param name="HasEnvelope">Whether the method accepts the original command envelope.</param>
/// <param name="HasCancellationToken">Whether the method accepts the originating cancellation token.</param>
internal sealed record AggregateCommandHandleMethod(
    MethodInfo Method,
    Type CommandType,
    bool IsAsync,
    bool IsStatic,
    bool HasEnvelope,
    bool HasCancellationToken);
