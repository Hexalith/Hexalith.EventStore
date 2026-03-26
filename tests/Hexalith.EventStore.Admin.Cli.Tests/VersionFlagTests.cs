namespace Hexalith.EventStore.Admin.Cli.Tests;

using System.Diagnostics;
using System.Reflection;

public class VersionFlagTests
{
    private static readonly string _expectedVersion = typeof(ExitCodes).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion ?? throw new InvalidOperationException("AssemblyInformationalVersionAttribute is missing from the CLI assembly.");

    [Theory]
    [InlineData("--version")]
    [InlineData("-v")]
    public void VersionFlag_PrintsVersionAndExitsZero(string flag)
    {
        // Invoke the actual CLI binary with the version flag
        string cliDll = typeof(ExitCodes).Assembly.Location;
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{cliDll}\" {flag}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        process.Start();
        string output = process.StandardOutput.ReadToEnd().Trim();
        string stderr = process.StandardError.ReadToEnd();
        bool exited = process.WaitForExit(5000);
        if (!exited)
        {
            process.Kill();
        }

        exited.ShouldBeTrue("CLI process timed out");
        process.ExitCode.ShouldBe(0, $"stderr: {stderr}");
        output.ShouldBe(_expectedVersion);
    }

    [Fact]
    public void HelpOutput_ContainsDescription()
    {
        // Verify help output includes the root command description
        // Note: The command name shown in help depends on how the binary is invoked.
        // When installed as a dotnet tool, it shows "eventstore-admin" (from ToolCommandName).
        // When running tests via "dotnet <dll>", it shows the DLL path.
        // The description is always present regardless of invocation method.
        string cliDll = typeof(ExitCodes).Assembly.Location;
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{cliDll}\" --help",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        bool exited = process.WaitForExit(5000);
        if (!exited)
        {
            process.Kill();
        }

        exited.ShouldBeTrue("CLI process timed out");
        process.ExitCode.ShouldBe(0, $"stderr: {stderr}");
        output.ShouldContain("Hexalith EventStore administration CLI");
    }
}
