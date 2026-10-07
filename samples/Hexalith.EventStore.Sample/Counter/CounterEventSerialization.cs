using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Sample.Counter.Events;

namespace Hexalith.EventStore.Sample.Counter;

/// <summary>Declares the Counter domain's exact legacy aliases and bounded V1 JSON writers.</summary>
/// <remarks>
/// The five application events are empty marker records and serialize to exactly two bytes.
/// Counter's framework termination rejection carries its fixed aggregate type and the validated
/// ASCII aggregate identifier. These declarations select bounded local V1 writing only.
/// </remarks>
public static class CounterEventSerialization
{
    /// <summary>Registers Counter's complete emitted-event inventory without changing other domains.</summary>
    /// <param name="services">The domain host's service collection.</param>
    /// <returns>The service collection for further configuration.</returns>
    public static IServiceCollection AddCounterEventSerialization(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddEventStoreBoundedV1DomainSerialization("counter", profile => profile
            .Add<CounterIncremented>(typeof(CounterIncremented).FullName!, "json", 2, SerializeMarkerAsync)
            .Add<CounterDecremented>(typeof(CounterDecremented).FullName!, "json", 2, SerializeMarkerAsync)
            .Add<CounterReset>(typeof(CounterReset).FullName!, "json", 2, SerializeMarkerAsync)
            .Add<CounterClosed>(typeof(CounterClosed).FullName!, "json", 2, SerializeMarkerAsync)
            .Add<CounterCannotGoNegative>(typeof(CounterCannotGoNegative).FullName!, "json", 2, SerializeMarkerAsync)
            .Add<AggregateTerminated>(typeof(AggregateTerminated).FullName!, "json", 309, SerializeTerminatedAsync));
    }

    private static Task SerializeMarkerAsync<TPayload>(TPayload payload, Stream destination, CancellationToken cancellationToken)
        where TPayload : IEventPayload
    {
        ArgumentNullException.ThrowIfNull(payload);
        cancellationToken.ThrowIfCancellationRequested();
        destination.Write("{}"u8);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    private static Task SerializeTerminatedAsync(AggregateTerminated payload, Stream destination, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.Equals(payload.AggregateType, nameof(CounterAggregate), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("CapabilityMismatch: the termination rejection belongs to another aggregate route.");
        }

        // Identity admission excludes characters requiring JSON escaping and caps this token at
        // 256 ASCII bytes before allocating or writing. The legacy default JSON uses PascalCase.
        _ = new AggregateIdentity("system", "counter", payload.AggregateId);
        Span<byte> identifier = stackalloc byte[256];
        try
        {
            int length = Encoding.ASCII.GetBytes(payload.AggregateId, identifier);
            cancellationToken.ThrowIfCancellationRequested();
            destination.Write("{\"AggregateType\":\"CounterAggregate\",\"AggregateId\":\""u8);
            destination.Write(identifier[..length]);
            destination.Write("\"}"u8);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(identifier);
        }
    }
}
