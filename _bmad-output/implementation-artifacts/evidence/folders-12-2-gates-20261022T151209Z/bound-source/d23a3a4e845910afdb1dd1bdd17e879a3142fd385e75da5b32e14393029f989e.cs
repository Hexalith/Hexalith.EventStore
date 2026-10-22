namespace Hexalith.EventStore.Admin.Cli.Profiles;

/// <summary>
/// A named connection profile storing Admin API connection settings.
/// </summary>
public record ConnectionProfile(string Url, string? Token, string? Format);
