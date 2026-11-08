namespace Hexalith.EventStore.DomainService;

/// <summary>The EventStore-owned extension API version window that composed gateways admit.</summary>
/// <remarks>
/// EventStore alone owns these values. Composers and validators read them from this type and keep no copy. The window
/// holds the current major and, once a later major exists, the previous one.
/// </remarks>
public static class EventStoreExtensionApi
{
    /// <summary>The current extension-API major.</summary>
    public const int CurrentMajor = 1;

    /// <summary>Gets the supported extension-API majors, current first.</summary>
    public static IReadOnlyList<int> SupportedMajors { get; } = Array.AsReadOnly(new[] { CurrentMajor });

    /// <summary>Returns whether an extension-API major is inside the supported window.</summary>
    /// <param name="major">The extension-API major to check.</param>
    /// <returns><see langword="true"/> when the gateway admits the major.</returns>
    public static bool IsSupported(int major) => SupportedMajors.Contains(major);
}
