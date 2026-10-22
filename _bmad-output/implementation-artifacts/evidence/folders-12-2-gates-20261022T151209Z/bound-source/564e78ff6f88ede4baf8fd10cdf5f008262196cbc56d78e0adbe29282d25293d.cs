namespace Hexalith.EventStore.ProviderVerification;

internal static class SafePath
{
    public static bool TryResolveExistingFile(string value, long maximumBytes, out string path, out string code)
    {
        path = string.Empty;
        code = "input.path.invalid";
        if (!TryResolve(value, out string candidate) || !File.Exists(candidate) || HasLink(candidate))
        {
            return false;
        }

        var info = new FileInfo(candidate);
        if (info.Length <= 0 || info.Length > maximumBytes)
        {
            code = "input.file.size-invalid";
            return false;
        }

        path = candidate;
        code = string.Empty;
        return true;
    }

    public static bool TryResolveExistingDirectory(string value, out string path, out string code)
    {
        path = string.Empty;
        code = "input.path.invalid";
        if (!TryResolve(value, out string candidate) || !Directory.Exists(candidate) || HasLink(candidate))
        {
            return false;
        }

        path = candidate;
        code = string.Empty;
        return true;
    }

    public static bool TryResolveOutputFile(string value, out string path, out string code)
    {
        path = string.Empty;
        code = "input.report-path.invalid";
        if (!TryResolve(value, out string candidate))
        {
            return false;
        }

        string? parent = Path.GetDirectoryName(candidate);
        if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent))
        {
            code = "input.report-parent.invalid";
            return false;
        }

        if (HasLink(parent))
        {
            code = "input.report-parent.symlink";
            return false;
        }

        if (File.Exists(candidate) && HasLink(candidate))
        {
            code = "input.report-file.symlink";
            return false;
        }

        path = candidate;
        code = string.Empty;
        return true;
    }

    private static bool TryResolve(string value, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(value)
            || value.IndexOf('\0') >= 0
            || value.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => string.Equals(segment, "..", StringComparison.Ordinal)))
        {
            return false;
        }

        try
        {
            path = Path.GetFullPath(value);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static bool HasLink(string path)
    {
        string current = Path.GetFullPath(path);
        while (!string.IsNullOrEmpty(current))
        {
            FileSystemInfo info = Directory.Exists(current)
                ? new DirectoryInfo(current)
                : new FileInfo(current);
            if (!string.IsNullOrEmpty(info.LinkTarget))
            {
                return true;
            }

            string? parent = Path.GetDirectoryName(current);
            if (string.IsNullOrEmpty(parent) || string.Equals(parent, current, StringComparison.Ordinal))
            {
                break;
            }

            current = parent;
        }

        return false;
    }
}
