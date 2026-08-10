namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Contains a support-safe projection of stable rotated directory authority.</summary>
/// <param name="IsCanonical">Whether the complete stable rotated authority shape was observed.</param>
/// <param name="CanonicalAuthorityCount">The observed activated, non-redirecting authority count.</param>
/// <param name="DirectoryStable">Whether the directory has no pending promotion work.</param>
/// <param name="SourceRedirectValid">Whether the retired source redirects to the canonical target.</param>
/// <param name="TargetActivated">Whether the target is activated and non-redirecting.</param>
internal sealed record Oq8RotatedAuthoritySnapshot(
    bool IsCanonical,
    int CanonicalAuthorityCount,
    bool DirectoryStable,
    bool SourceRedirectValid,
    bool TargetActivated);
