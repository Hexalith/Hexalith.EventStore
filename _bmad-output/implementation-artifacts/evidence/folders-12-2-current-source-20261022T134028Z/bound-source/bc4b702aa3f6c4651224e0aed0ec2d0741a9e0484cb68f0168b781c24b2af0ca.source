using Hexalith.EventStore.Contracts.Effects;

namespace Hexalith.EventStore.Client.Effects;

/// <summary>Read-only authenticated origin for the current domain invocation, isolated across async requests.</summary>
public sealed class TrustedEffectOriginScope : IDisposable
{
    private static readonly AsyncLocal<TrustedEffectOrigin?> s_current = new();
    private readonly TrustedEffectOrigin? _prior;

    private TrustedEffectOriginScope(TrustedEffectOrigin? origin)
    {
        _prior = s_current.Value;
        s_current.Value = origin;
    }

    /// <summary>Gets verified transport origin, or null for an ordinary command invocation.</summary>
    public static TrustedEffectOrigin? Current => s_current.Value;

    /// <summary>Enters a scope only after the SDK has authenticated and bound its origin.</summary>
    internal static TrustedEffectOriginScope Enter(TrustedEffectOrigin? origin) => new(origin);

    /// <summary>Restores the enclosing origin on success, cancellation, or failure.</summary>
    public void Dispose() => s_current.Value = _prior;
}
