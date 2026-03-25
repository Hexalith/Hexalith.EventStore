namespace Hexalith.EventStore.Admin.Cli;

/// <summary>
/// Parsed global option values shared across all subcommands.
/// </summary>
public record GlobalOptions(string Url, string? Token, string Format, string? OutputFile);
