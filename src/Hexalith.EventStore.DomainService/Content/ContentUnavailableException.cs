namespace Hexalith.EventStore.DomainService.Content;

/// <summary>Content authority or integrity cannot establish an exact successful result.</summary>
public sealed class ContentUnavailableException : InvalidOperationException
{
    /// <summary>Creates a body-free failure message.</summary>
    public ContentUnavailableException() : base("Authoritative content is unavailable.") { }
}
