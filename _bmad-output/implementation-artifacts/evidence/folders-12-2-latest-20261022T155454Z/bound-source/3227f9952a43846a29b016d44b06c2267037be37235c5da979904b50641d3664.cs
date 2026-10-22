using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Sample.Greeting.Events;

namespace Hexalith.EventStore.Sample.Greeting;

/// <summary>Declares Greeting's exact legacy alias and two-byte marker writer.</summary>
public static class GreetingEventSerialization
{
    /// <summary>Registers the complete emitted-event inventory for the Greeting aggregate.</summary>
    /// <param name="services">The domain host's service collection.</param>
    /// <returns>The service collection for further configuration.</returns>
    public static IServiceCollection AddGreetingEventSerialization(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddEventStoreBoundedV1DomainSerialization("greeting", profile => profile
            .Add<GreetingSent>(typeof(GreetingSent).FullName!, "json", 2, SerializeMarkerAsync));
    }

    private static Task SerializeMarkerAsync(GreetingSent payload, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        cancellationToken.ThrowIfCancellationRequested();
        destination.Write("{}"u8);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
