namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Independent source owner's exact mixed-snapshot entry enrollment. It authenticates the content-free skeleton even when every root is erased.</summary>
/// <param name="Parent">Original whole-snapshot occurrence.</param><param name="Path">Original protected value path.</param><param name="ContainerDigest">SHA-256 of the content-free complete technical skeleton.</param>
/// <param name="EntryCount">Complete immutable entry count, including all targets.</param>
public sealed record SnapshotEntrySourceBinding(PayloadProtectionOccurrenceContext Parent, string Path, string ContainerDigest, int EntryCount);
