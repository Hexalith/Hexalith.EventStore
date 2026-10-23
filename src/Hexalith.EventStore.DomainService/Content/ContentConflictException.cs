namespace Hexalith.EventStore.DomainService.Content;

/// <summary>The immutable publication identity, file incarnation or current fence conflicts.</summary>
public sealed class ContentConflictException : InvalidOperationException
{
    /// <summary>Creates a body-free conflict message.</summary>
    public ContentConflictException() : base("Authoritative content publication conflicts.") { }
}
