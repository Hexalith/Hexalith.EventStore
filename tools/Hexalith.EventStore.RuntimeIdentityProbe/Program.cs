using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;

// Effect-free SDK-owned entry point. The selected child's runtimeconfig, deps and app base choose
// these assemblies; this process never loads or invokes the selected application's entry point.
if (args.Length != 0) return 2;
string host = Environment.ProcessPath ?? throw new InvalidOperationException("The actual runtime host is unavailable.");
Assembly[] assemblies = [typeof(object).Assembly, typeof(JsonSerializerOptions).Assembly,
    typeof(System.Text.Encodings.Web.JavaScriptEncoder).Assembly, typeof(System.IO.Pipelines.PipeReader).Assembly];
var modules = assemblies.Select(assembly =>
{
    string path = assembly.Location;
    if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("A resolved runtime module has no physical identity.");
    byte[] body = File.ReadAllBytes(path);
    using var stream = new MemoryStream(body, writable: false);
    using var image = new PEReader(stream);
    var metadata = image.GetMetadataReader();
    Guid mvid = metadata.GetGuid(metadata.GetModuleDefinition().Mvid);
    if (mvid != assembly.ManifestModule.ModuleVersionId)
        throw new InvalidOperationException("A resolved runtime module changed after loading.");
    return new { name = assembly.GetName().Name, assembly = assembly.FullName, path,
        moduleVersionId = mvid, sha256 = Convert.ToHexStringLower(SHA256.HashData(body)) };
}).ToArray();
Console.WriteLine(JsonSerializer.Serialize(new
{
    protocol = "EventStoreSelectedOwnerManagedRuntimeV1", runtime = Environment.Version.ToString(),
    hostPath = host, hostSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(host))), modules,
}));
return 0;
