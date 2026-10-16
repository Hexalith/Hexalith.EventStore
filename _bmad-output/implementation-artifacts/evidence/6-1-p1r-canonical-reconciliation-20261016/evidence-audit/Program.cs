using System.Collections;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

string owner = Path.GetFullPath(args[0]);
string directory = Path.GetFullPath(args[1]);
string assemblyPath = Path.GetFullPath(args[2]);
string dependencyDirectory = Path.GetDirectoryName(assemblyPath)!;
AssemblyLoadContext.Default.Resolving += (_, name) =>
{
    string candidate = Path.Combine(dependencyDirectory, name.Name + ".dll");
    return File.Exists(candidate) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate) : null;
};
Assembly assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
Type scanner = assembly.GetType("Hexalith.EventStore.Server.Tests.Security.SecretsProtectionTests", throwOnError: true)!;
MethodInfo scan = scanner.GetMethod("ScanTrackedFile", BindingFlags.Static | BindingFlags.NonPublic)!;
List<string> findings = [];
List<string> scanned = [];
foreach (string path in Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
{
    string relative = Path.GetRelativePath(owner, path).Replace('\\', '/');
    scanned.Add(relative);
    try
    {
        foreach (object? finding in (IEnumerable)scan.Invoke(null, [relative, owner])!)
        {
            findings.Add(finding?.ToString() ?? relative + ":null-finding");
        }
    }
    catch (TargetInvocationException error)
    {
        findings.Add(relative + ":reader-error:" + error.InnerException?.GetType().Name);
    }
}
Console.WriteLine(JsonSerializer.Serialize(new { scanned_files = scanned.Count, findings, passed = findings.Count == 0 }));
return findings.Count == 0 ? 0 : 1;
