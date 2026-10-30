using System.Globalization;
using Microsoft.NET.HostModel.AppHost;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

/// <summary>Uses hostpolicy's actual dependency resolver without loading or invoking the role entry assembly.</summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 2) { return 2; }
            string entry = Path.GetFullPath(args[0]);
            string expectedHost = Path.Combine(Path.GetTempPath(), "protection-apphost-" + Guid.NewGuid().ToString("N"));
            try
            {
                HostWriter.CreateAppHost(args[1], expectedHost, Path.GetFileName(entry));
                if (!File.ReadAllBytes(expectedHost).AsSpan().SequenceEqual(File.ReadAllBytes(entry[..^4]))) { return 2; }
            }
            finally { File.Delete(expectedHost); }
            var resolver = new AssemblyDependencyResolver(entry);
            var files = new HashSet<string>(StringComparer.Ordinal) { entry };
            using var deps = JsonDocument.Parse(File.ReadAllBytes(Path.ChangeExtension(entry, ".deps.json")));
            foreach (var target in deps.RootElement.GetProperty("targets").EnumerateObject())
            {
                foreach (var library in target.Value.EnumerateObject())
                {
                    foreach (var group in library.Value.EnumerateObject())
                    {
                        if (group.Name is not ("runtime" or "runtimeTargets" or "native" or "resources")) { continue; }
                        foreach (var asset in group.Value.EnumerateObject())
                        {
                            string name = Path.GetFileName(asset.Name);
                            if (name == "_._") { continue; }
                            bool native = group.Name == "native" || group.Name == "runtimeTargets" && asset.Value.GetProperty("assetType").GetString() == "native";
                            string? resolved;
                            if (native)
                            {
                                resolved = resolver.ResolveUnmanagedDllToPath(name);
                                resolved ??= resolver.ResolveUnmanagedDllToPath(Path.GetFileNameWithoutExtension(name));
                                if (resolved is null && name.StartsWith("lib", StringComparison.Ordinal)) { resolved = resolver.ResolveUnmanagedDllToPath(Path.GetFileNameWithoutExtension(name)[3..]); }
                            }
                            else
                            {
                                var identity = new AssemblyName(Path.GetFileNameWithoutExtension(name));
                                if (group.Name == "resources" && asset.Value.TryGetProperty("locale", out var locale)) { identity.CultureInfo = CultureInfo.GetCultureInfo(locale.GetString()!); }
                                resolved = resolver.ResolveAssemblyToPath(identity);
                            }
                            if (resolved is not null) { files.Add(Path.GetFullPath(resolved)); }
                            else if (group.Name is not "runtimeTargets") { throw new InvalidOperationException("Unresolved required application asset."); }
                        }
                    }
                }
            }
            Console.WriteLine(JsonSerializer.Serialize(new { files = files.Order(StringComparer.Ordinal).ToArray(), runtime = Environment.Version.ToString() }));
            return 0;
        }
        catch { return 2; }
    }
}
