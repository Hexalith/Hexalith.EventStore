using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Microsoft.Extensions.Hosting;

namespace Hexalith.EventStore.Aspire;

/// <summary>Prepares optional compiled owner metadata through the selected child's ordinary build and actual Aspire launch model.</summary>
public static class DevelopmentRouteCatalogOwnerPreparationExtensions
{
    /// <summary>Builds and reads the selected child without adding an optional project reference or overriding its package/source mapping.</summary>
    /// <remarks>Call after selecting project metadata and before catalog composition. The finite deadline includes build and evaluation.
    /// Failed preparation retains private diagnostics at the path named by the exception. Dispose successful preparation after the application.</remarks>
    public static async Task<DevelopmentRouteCatalogPreparedOwner> PrepareEventStoreDevelopmentOwnerAsync(
        this IResourceBuilder<ProjectResource> project, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromMinutes(10)) throw new ArgumentOutOfRangeException(nameof(timeout));
        if (!project.ApplicationBuilder.ExecutionContext.IsRunMode || !project.ApplicationBuilder.Environment.IsDevelopment())
            throw new InvalidOperationException("Selected child metadata preparation requires an explicit Development run.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
#pragma warning disable ASPIREEXTENSION001
        var launch = await project.Resource.CreateLaunchConfigurationAsync(ExecutableLaunchMode.NoDebug, deadline.Token).ConfigureAwait(false)
            as ProjectLaunchConfiguration ?? throw new InvalidOperationException("The selected child must expose its actual .NET project launch configuration.");
#pragma warning restore ASPIREEXTENSION001
        deadline.Token.ThrowIfCancellationRequested();
        if (launch.SuppressBuild || project.Resource.GetProjectMetadata().SuppressBuild
            || project.Resource.Annotations.Any(annotation => annotation.GetType().Name is "LaunchToolArgsCallbackAnnotation" or "ProjectLaunchArgsOverrideAnnotation"))
            throw new InvalidOperationException("Selected child preparation does not support suppressed builds or overridden launch tool arguments.");
        string path = Path.GetFullPath(launch.ProjectPath);
        if (!File.Exists(path) || Path.GetExtension(path) != ".csproj"
            || path != Path.GetFullPath(project.Resource.GetProjectMetadata().ProjectPath))
            throw new InvalidOperationException("The selected child launch and project identities differ.");
        var loadedSdk = DevelopmentRouteCatalogOwnerLoadContext.CaptureLoadedSdkIdentity();
        string directory = System.IO.Directory.CreateTempSubdirectory("hexalith-selected-owner-").FullName;
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        DevelopmentRouteCatalogOwnerLoadContext? context = null;
        try
        {
            var start = new ProcessStartInfo("dotnet") { WorkingDirectory = launch.BuildWorkingDirectory ?? Path.GetDirectoryName(path)!,
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            foreach (string argument in new[] { "build", path, "--disable-build-servers", "-m:1", "-nodeReuse:false" }) start.ArgumentList.Add(argument);
            if (!string.IsNullOrWhiteSpace(launch.BuildConfiguration))
            {
                start.ArgumentList.Add("--configuration"); start.ArgumentList.Add(launch.BuildConfiguration);
            }
            foreach (var variable in launch.BuildEnvironment ?? []) start.Environment[variable.Key] = variable.Value;
            await RunAsync(start, directory, "build", deadline).ConfigureAwait(false);
            start.ArgumentList.Clear();
            foreach (string argument in new[] { "msbuild", path, "-m:1", "-nodeReuse:false",
                "-getProperty:TargetPath,Configuration,MSBuildProjectFullPath,TargetFramework,NETCoreSdkVersion" }) start.ArgumentList.Add(argument);
            if (!string.IsNullOrWhiteSpace(launch.BuildConfiguration)) start.ArgumentList.Add("-property:Configuration=" + launch.BuildConfiguration);
            await RunAsync(start, directory, "evaluation", deadline).ConfigureAwait(false);
            string diagnostics = await File.ReadAllTextAsync(Path.Combine(directory, "evaluation.stdout.log"), deadline.Token).ConfigureAwait(false);
            int properties = diagnostics.LastIndexOf("{\n  \"Properties\":", StringComparison.Ordinal);
            if (properties < 0) properties = diagnostics.LastIndexOf("{\r\n  \"Properties\":", StringComparison.Ordinal);
            if (properties < 0) throw new InvalidOperationException("The selected child did not produce evaluated build output metadata.");
            using var evaluated = JsonDocument.Parse(diagnostics[properties..]);
            var values = evaluated.RootElement.GetProperty("Properties");
            string target = Path.GetFullPath(values.GetProperty("TargetPath").GetString()!);
            string configuration = values.GetProperty("Configuration").GetString()!;
            if (Path.GetFullPath(values.GetProperty("MSBuildProjectFullPath").GetString()!) != path
                || string.IsNullOrWhiteSpace(values.GetProperty("TargetFramework").GetString())
                || !string.IsNullOrWhiteSpace(launch.BuildConfiguration) && configuration != launch.BuildConfiguration
                || !File.Exists(target)) throw new InvalidOperationException("The selected child evaluated build differs from its launch identity.");
            byte[] targetHash = SHA256.HashData(await File.ReadAllBytesAsync(target, deadline.Token).ConfigureAwait(false));
            string snapshot = Path.Combine(directory, "metadata"); System.IO.Directory.CreateDirectory(snapshot);
            Dictionary<string, string> identities = [];
            string[] dependencies = System.IO.Directory.EnumerateFiles(Path.GetDirectoryName(target)!, "*.dll")
                .Append(Path.ChangeExtension(target, ".deps.json")).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            foreach (string dependency in dependencies)
            {
                byte[] bytes = await File.ReadAllBytesAsync(dependency, deadline.Token).ConfigureAwait(false);
                string name = Path.GetFileName(dependency);
                using (var frozen = PrivateFile(Path.Combine(snapshot, name)))
                    await frozen.WriteAsync(bytes, deadline.Token).ConfigureAwait(false);
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(Path.Combine(snapshot, name), UnixFileMode.UserRead);
                identities.Add(name, Digest(bytes));
            }
            string metadataPath = Path.Combine(snapshot, Path.GetFileName(target));
            if (identities[Path.GetFileName(target)] != Convert.ToHexStringLower(targetHash))
                throw new InvalidOperationException("The selected child target changed before its immutable metadata was prepared.");
            context = new(metadataPath, loadedSdk);
            context.VerifySharedSdkIdentity();
            Assembly assembly = context.LoadFromAssemblyPath(metadataPath);
            var routes = CompiledRouteCatalogDeclarations.Read([assembly]);
            var operations = CompiledRouteCatalogDeclarations.ReadOperations([assembly]);
            if (!assembly.GetCustomAttributesData().Any(attribute => attribute.AttributeType.FullName == "Hexalith.EventStore.DomainService.RouteCatalogAdapterSourceAttribute"))
                throw new InvalidOperationException("The selected child lacks its actual compiled command adapter factory.");
            foreach (var dependency in identities)
                if (dependency.Value != Digest(await File.ReadAllBytesAsync(Path.Combine(Path.GetDirectoryName(target)!, dependency.Key), deadline.Token).ConfigureAwait(false))
                    || dependency.Value != Digest(await File.ReadAllBytesAsync(Path.Combine(snapshot, dependency.Key), deadline.Token).ConfigureAwait(false)))
                    throw new InvalidOperationException("The selected child output changed while its immutable metadata was prepared.");
            context.VerifySharedSdkIdentity();
            var receipt = new
            {
                project = project.Resource.Name, projectPath = path, projectSha256 = Digest(await File.ReadAllBytesAsync(path, deadline.Token).ConfigureAwait(false)),
                configuration, targetFramework = values.GetProperty("TargetFramework").GetString(), sdk = values.GetProperty("NETCoreSdkVersion").GetString(),
                workingDirectory = start.WorkingDirectory,
                buildEnvironmentDigest = Digest(JsonSerializer.SerializeToUtf8Bytes((launch.BuildEnvironment ?? []).OrderBy(variable => variable.Key, StringComparer.Ordinal))),
                targetPath = target, metadataPath, targetSha256 = Convert.ToHexStringLower(targetHash), assembly = assembly.FullName, moduleVersionId = assembly.ManifestModule.ModuleVersionId,
                declarations = routes.Select(route => route.RouteId).ToArray(), operations = operations.Select(operation => new { operation.RouteId, operation.Digest }).ToArray(),
                dependencies = identities.Select(dependency => new { name = dependency.Key, sha256 = dependency.Value }).ToArray(),
                loadedSdk = loadedSdk.OrderBy(dependency => dependency.Key.FullName, StringComparer.Ordinal)
                    .Select(dependency => new { assembly = dependency.Key.FullName, moduleVersionId = dependency.Key.ManifestModule.ModuleVersionId, sha256 = dependency.Value }).ToArray(),
            };
            using (var receiptFile = PrivateFile(Path.Combine(directory, "preparation.json")))
                await JsonSerializer.SerializeAsync(receiptFile, receipt, cancellationToken: deadline.Token).ConfigureAwait(false);
            return new(new(project, assembly, assembly), directory, context);
        }
        catch (Exception exception)
        {
            context?.Unload();
            throw new InvalidOperationException($"Selected child owner preparation refused; private diagnostics: {directory}", exception);
        }
    }

    private static string Digest(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static async Task RunAsync(ProcessStartInfo start, string directory, string name, CancellationTokenSource deadline)
    {
        deadline.Token.ThrowIfCancellationRequested();
        using var process = new Process { StartInfo = start };
        using var stdout = PrivateFile(Path.Combine(directory, name + ".stdout.log"));
        using var stderr = PrivateFile(Path.Combine(directory, name + ".stderr.log"));
        process.Start();
        Task output = PumpAsync(process.StandardOutput.BaseStream, stdout, deadline);
        Task error = PumpAsync(process.StandardError.BaseStream, stderr, deadline);
        try { await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false); }
        catch (OperationCanceledException cancellation)
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            try { await process.WaitForExitAsync(cleanup.Token).ConfigureAwait(false); }
            catch (OperationCanceledException exception)
            {
                throw new InvalidOperationException("Selected child cancellation could not confirm owned process termination within the cleanup deadline.", new AggregateException(cancellation, exception));
            }
            throw;
        }
        finally
        {
            // The diagnostic pumps share the original deadline. Even a failed termination
            // cannot extend the finite preparation API by leaving inherited pipe handles open.
            await Task.WhenAll(output, error).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        if (process.ExitCode != 0) throw new InvalidOperationException($"Selected child {name} failed with exit code {process.ExitCode}.");
    }

    private static async Task PumpAsync(Stream source, Stream destination, CancellationTokenSource deadline)
    {
        byte[] buffer = new byte[8192];
        long written = 0;
        try
        {
            while (await source.ReadAsync(buffer, deadline.Token).ConfigureAwait(false) is int count && count > 0)
            {
                if (written + count > 8 * 1024 * 1024)
                    throw new InvalidOperationException("Selected child build diagnostics exceeded the bounded stream limit.");
                await destination.WriteAsync(buffer.AsMemory(0, count), deadline.Token).ConfigureAwait(false);
                written += count;
            }
        }
        catch { await deadline.CancelAsync().ConfigureAwait(false); throw; }
    }

    private static FileStream PrivateFile(string path)
    {
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.ReadWrite, Share = FileShare.Read };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        return new(path, options);
    }
}
