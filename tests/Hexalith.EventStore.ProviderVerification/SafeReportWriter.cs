using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hexalith.EventStore.ProviderVerification;

internal static class SafeReportWriter
{
    internal const int MaximumReportBytes = 1024 * 1024;

    private static readonly string[] _forbiddenFragments =
    [
        "FC_CONTRACT_TOKEN",
        "http://",
        "https://",
        "stacktrace",
        "system.exception",
        " at hexalith.",
        "authorization",
        "secret",
    ];

    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public static bool TryWrite(string requestedPath, ProviderVerificationReport report, out string failureCode)
    {
        failureCode = "report.path.invalid";
        if (!SafePath.TryResolveOutputFile(requestedPath, out string outputPath, out string pathCode))
        {
            failureCode = pathCode;
            return false;
        }

        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(report, _options);
        if (bytes.Length > MaximumReportBytes || !IsRedactionClean(bytes))
        {
            failureCode = bytes.Length > MaximumReportBytes
                ? "report.size.exceeded"
                : "report.redaction.failed";
            return false;
        }

        string directory = Path.GetDirectoryName(outputPath)!;
        string temporaryPath = Path.Combine(directory, $".{Path.GetFileName(outputPath)}.{Guid.NewGuid():N}.tmp");
        bool succeeded = false;
        try
        {
            using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, outputPath, overwrite: true);
            failureCode = string.Empty;
            succeeded = true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            failureCode = "report.write.failed";
        }
        finally
        {
            if (File.Exists(temporaryPath) && !TryDeleteTemporaryFile(temporaryPath, out string cleanupCode))
            {
                failureCode = cleanupCode;
                succeeded = false;
            }
        }

        return succeeded;
    }

    internal static bool IsRedactionClean(ReadOnlySpan<byte> bytes)
    {
        string text = Encoding.UTF8.GetString(bytes);
        string bearerScan = text.Replace("bearer requirement", string.Empty, StringComparison.OrdinalIgnoreCase);
        if (Regex.IsMatch(bearerScan, @"\bbearer\s+\S+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            || _forbiddenFragments.Any(fragment => text.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            || Regex.IsMatch(
                text,
                @"(?<![A-Za-z0-9])(?:localhost|127(?:\.\d{1,3}){3}|10(?:\.\d{1,3}){3}|192\.168(?:\.\d{1,3}){2}|172\.(?:1[6-9]|2\d|3[01])(?:\.\d{1,3}){2}|\[?::1\]?):\d{1,5}",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(text);
            return EnumerateStrings(document.RootElement).All(IsSafeString);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    internal static bool TryDeleteTemporaryFile(string path, out string failureCode)
    {
        try
        {
            File.Delete(path);
            failureCode = string.Empty;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            failureCode = "report.temporary-cleanup.failed";
            return false;
        }
    }

    private static IEnumerable<string> EnumerateStrings(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            yield return element.GetString() ?? string.Empty;
            yield break;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                foreach (string value in EnumerateStrings(item))
                {
                    yield return value;
                }
            }

            yield break;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                foreach (string value in EnumerateStrings(property.Value))
                {
                    yield return value;
                }
            }
        }
    }

    private static bool IsSafeString(string value)
        => !Regex.IsMatch(value, @"(?<![A-Za-z0-9])/(?!/)[^\s\""\\]+", RegexOptions.CultureInvariant)
            && !Regex.IsMatch(value, @"(?<![A-Za-z0-9])[A-Za-z]:[\\/][^\s\""\\]+", RegexOptions.CultureInvariant)
            && !value.StartsWith("\\\\", StringComparison.Ordinal)
            && !value.StartsWith("//", StringComparison.Ordinal);
}
