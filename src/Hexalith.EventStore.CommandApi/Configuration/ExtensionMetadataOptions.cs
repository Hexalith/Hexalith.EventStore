namespace Hexalith.EventStore.CommandApi.Configuration;

using Microsoft.Extensions.Options;

/// <summary>
/// Configuration options for extension metadata sanitization (SEC-4).
/// Bound from the "EventStore:ExtensionMetadata" configuration section.
/// </summary>
public record ExtensionMetadataOptions
{
    /// <summary>
    /// Gets the maximum total size of all extension keys and values in bytes.
    /// </summary>
    public int MaxTotalSizeBytes { get; init; } = 4096;

    /// <summary>
    /// Gets the maximum length of a single extension key.
    /// </summary>
    public int MaxKeyLength { get; init; } = 128;

    /// <summary>
    /// Gets the maximum length of a single extension value.
    /// </summary>
    public int MaxValueLength { get; init; } = 2048;

    /// <summary>
    /// Gets the maximum number of extension entries.
    /// </summary>
    public int MaxExtensionCount { get; init; } = 32;
}

/// <summary>
/// Validates that <see cref="ExtensionMetadataOptions"/> is properly configured at startup.
/// </summary>
public class ValidateExtensionMetadataOptions : IValidateOptions<ExtensionMetadataOptions>
{
    public ValidateOptionsResult Validate(string? name, ExtensionMetadataOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.MaxTotalSizeBytes <= 0)
        {
            return ValidateOptionsResult.Fail(
                "EventStore:ExtensionMetadata:MaxTotalSizeBytes must be greater than 0.");
        }

        if (options.MaxKeyLength <= 0)
        {
            return ValidateOptionsResult.Fail(
                "EventStore:ExtensionMetadata:MaxKeyLength must be greater than 0.");
        }

        if (options.MaxValueLength <= 0)
        {
            return ValidateOptionsResult.Fail(
                "EventStore:ExtensionMetadata:MaxValueLength must be greater than 0.");
        }

        if (options.MaxExtensionCount <= 0)
        {
            return ValidateOptionsResult.Fail(
                "EventStore:ExtensionMetadata:MaxExtensionCount must be greater than 0.");
        }

        return ValidateOptionsResult.Success;
    }
}
