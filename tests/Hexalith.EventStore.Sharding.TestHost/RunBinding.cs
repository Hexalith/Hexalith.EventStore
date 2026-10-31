using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hexalith.EventStore.Sharding.TestHost;

/// <summary>Local run identity and deadline, not an authenticated production approval.</summary>
/// <param name="RunId">The private run identity.</param>
/// <param name="CreatedUtc">The canonical UTC binding time.</param>
/// <param name="ExpiresUtc">The operator's exclusive canonical UTC expiry.</param>
/// <param name="CandidateCommit">The approved planning subject.</param>
/// <param name="SuccessorBlob">The approved successor blob.</param>
/// <param name="NormativeSha256">The approved normative digest.</param>
/// <param name="HostFiles">Exact host/runtime dependency hashes.</param>
/// <param name="TestFiles">Exact executable test/runtime dependency hashes.</param>
/// <param name="Runtime">Local tool, image and runtime identities.</param>
/// <param name="Configuration">Redacted topology, configuration hashes, and environment.</param>
public sealed record RunBinding(
    string RunId,
    string CreatedUtc,
    string ExpiresUtc,
    string CandidateCommit,
    string SuccessorBlob,
    string NormativeSha256,
    Dictionary<string, string> HostFiles,
    Dictionary<string, string> TestFiles,
    Dictionary<string, string> Runtime,
    Dictionary<string, string> Configuration)
{
    /// <summary>The required operator environment variable.</summary>
    public const string ExpiryVariable = "EVENTSTORE_SHARDING_EXPIRES_UTC";

    /// <summary>The host's required binding path variable.</summary>
    public const string BindingVariable = "EVENTSTORE_SHARDING_BINDING";

    /// <summary>Parses an exact second-precision UTC expiry and enforces the one-hour cap.</summary>
    /// <param name="input">The supplied value.</param>
    /// <param name="now">The binding instant.</param>
    /// <returns>The exclusive expiry.</returns>
    public static DateTimeOffset ParseExpiry(string? input, DateTimeOffset now)
    {
        if (!DateTimeOffset.TryParseExact(input, "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset expiry)
            || expiry <= now || expiry > now.AddHours(1))
        {
            throw new InvalidOperationException($"{ExpiryVariable} must be a future exclusive UTC second within one hour, formatted yyyy-MM-ddTHH:mm:ssZ.");
        }

        return expiry;
    }

    /// <summary>Reads, validates and binds the host to its exact artifact before startup.</summary>
    /// <param name="path">The operator-bound file.</param>
    /// <param name="hostDirectory">The running host directory.</param>
    /// <returns>The validated binding.</returns>
    public static RunBinding LoadHost(string? path, string hostDirectory)
    {
        if (path is null || !Path.IsPathFullyQualified(path))
        {
            throw new InvalidOperationException("An absolute run binding path is required.");
        }

        RunBinding binding = JsonSerializer.Deserialize<RunBinding>(File.ReadAllText(path))
            ?? throw new InvalidOperationException("The run binding is missing.");
        DateTimeOffset created = ParseCanonical(binding.CreatedUtc);
        DateTimeOffset expiry = ParseExpiry(binding.ExpiresUtc, created);
        if (created > DateTimeOffset.UtcNow || DateTimeOffset.UtcNow >= expiry
            || string.IsNullOrWhiteSpace(binding.RunId)
            || binding.CandidateCommit != "411005b4ccb2352249c83330c54967c6a22beba1"
            || binding.SuccessorBlob != "160331d25451928ff3c3dea2300b65cab4f97c3b"
            || binding.NormativeSha256 != "995fcecd16b3421ec9ff666d0884bfb5e436932aa49529c152fb7c439172a1fd"
            || binding.Runtime is null || binding.Runtime.Count == 0
            || binding.Configuration is null || binding.Configuration.Count == 0
            || binding.TestFiles is null || binding.TestFiles.Count == 0
            || binding.HostFiles is null || !ManifestsEqual(binding.HostFiles, HashDirectory(hostDirectory)))
        {
            throw new InvalidOperationException("The host binding does not match the approved local subject, deadline or artifact.");
        }

        return binding;
    }

    /// <summary>Hashes every regular output file, with ordinal relative paths.</summary>
    /// <param name="directory">The built artifact directory.</param>
    /// <returns>The exact dependency manifest.</returns>
    public static Dictionary<string, string> HashDirectory(string directory)
        => Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .ToDictionary(file => Path.GetRelativePath(directory, file).Replace('\\', '/'), HashFile, StringComparer.Ordinal);

    /// <summary>Hashes the bytes of one file.</summary>
    /// <param name="path">The file.</param>
    /// <returns>Its lowercase SHA-256.</returns>
    public static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    /// <summary>Hashes an exact UTF-8 configuration string.</summary>
    /// <param name="value">The string.</param>
    /// <returns>Its lowercase SHA-256.</returns>
    public static string HashText(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static DateTimeOffset ParseCanonical(string value)
        => DateTimeOffset.ParseExact(value, "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

    private static bool ManifestsEqual(Dictionary<string, string> expected, Dictionary<string, string> actual)
        => expected.Count == actual.Count && expected.All(entry => actual.GetValueOrDefault(entry.Key) == entry.Value);
}
