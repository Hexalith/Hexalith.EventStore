namespace Hexalith.EventStore.Server.Tests.Projections;

/// <summary>Observes the installed default actor serializer's actual destination array without copying its contents.</summary>
internal sealed class ObservedActorResponseMemoryStream(byte[] body, Action<MemoryStream> observe) : MemoryStream(body, 0, body.Length, writable: false, publiclyVisible: true)
{
    public override void CopyTo(Stream destination, int bufferSize)
    {
        // Preserve concrete MemoryStream.CopyTo's single Write. Calling its
        // base implementation on a subclass selects Stream.CopyTo's loop.
        destination.Write(GetBuffer().AsSpan(checked((int)Position), checked((int)(Length - Position))));
        Position = Length;
        observe((MemoryStream)destination);
    }
}
