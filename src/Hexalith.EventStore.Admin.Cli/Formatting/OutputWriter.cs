using System.Text;

namespace Hexalith.EventStore.Admin.Cli.Formatting;

/// <summary>
/// Writes formatted output to stdout or to a file. Errors always go to stderr.
/// </summary>
public class OutputWriter
{
    private readonly string? _filePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="OutputWriter"/> class.
    /// </summary>
    /// <param name="filePath">Optional file path. When null, output goes to stdout.</param>
    public OutputWriter(string? filePath)
    {
        _filePath = filePath;
    }

    /// <summary>
    /// Writes the content to stdout or the configured file. Returns exit code 2 on file I/O errors.
    /// </summary>
    /// <returns>0 on success, 2 on file write error.</returns>
    public int Write(string content)
    {
        if (_filePath is null)
        {
            Console.WriteLine(content);
            return ExitCodes.Success;
        }

        try
        {
            File.WriteAllText(_filePath, content + Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return ExitCodes.Success;
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"Cannot write to {_filePath}: {ex.Message}");
            return ExitCodes.Error;
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.Error.WriteLine($"Cannot write to {_filePath}: {ex.Message}");
            return ExitCodes.Error;
        }
    }
}
