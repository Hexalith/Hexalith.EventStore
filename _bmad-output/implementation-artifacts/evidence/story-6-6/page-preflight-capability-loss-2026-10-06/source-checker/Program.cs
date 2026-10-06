using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
string root = args[0];
string[] sources = JsonSerializer.Deserialize<string[]>(File.ReadAllText(args[1]))!;
var records = new List<object>();
foreach (string project in new[] { "Hexalith.EventStore.Client", "Hexalith.EventStore.Server", "Hexalith.EventStore.Client.Tests", "Hexalith.EventStore.Server.Tests" })
{
    string group = project.EndsWith(".Tests", StringComparison.Ordinal) ? "tests" : "src";
    string prefix = group + "/" + project + "/";
    string assembly = Path.Combine(root, group, project, "bin", "Release", "net10.0", project + ".dll");
    string pdb = Path.ChangeExtension(assembly, ".pdb");
    using FileStream pdbStream = File.OpenRead(pdb);
    using MetadataReaderProvider provider = MetadataReaderProvider.FromPortablePdbStream(pdbStream);
    MetadataReader reader = provider.GetMetadataReader();
    using FileStream assemblyStream = File.OpenRead(assembly);
    using var pe = new PEReader(assemblyStream);
    Guid pdbId = new(reader.DebugMetadataHeader!.Id.Take(16).ToArray());
    if (!pe.ReadDebugDirectory().Any(entry => entry.Type == DebugDirectoryEntryType.CodeView && pe.ReadCodeViewDebugDirectoryData(entry).Guid == pdbId))
        throw new InvalidOperationException("Assembly/PDB identity differs: " + project);
    var documents = reader.Documents.Select(reader.GetDocument).ToDictionary(document => reader.GetString(document.Name), StringComparer.Ordinal);
    foreach (string source in sources.Where(source => source.StartsWith(prefix, StringComparison.Ordinal)))
    {
        string full = Path.Combine(root, source);
        if (!documents.TryGetValue(full, out Document document)) throw new InvalidOperationException("Compiled source absent: " + source);
        if (reader.GetGuid(document.HashAlgorithm) != new Guid("8829d00f-11b8-4213-878b-770e8597ac16")) throw new InvalidOperationException("Compiled source algorithm differs: " + source);
        string actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(full))).ToLowerInvariant();
        string compiled = Convert.ToHexString(reader.GetBlobBytes(document.Hash)).ToLowerInvariant();
        if (actual != compiled) throw new InvalidOperationException("Compiled source differs: " + source);
        records.Add(new { source, sha256 = actual, assembly = Path.GetRelativePath(root, assembly), pdb = Path.GetRelativePath(root, pdb), result = "matched compiled source" });
    }
}
if (records.Count != sources.Length) throw new InvalidOperationException("Owned source count differs");
Console.WriteLine(JsonSerializer.Serialize(new { result = "passed", matched_source_files = records.Count, checks = records }, new JsonSerializerOptions { WriteIndented = true }));
