using System.Buffers.Binary;
using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;

string root = args[0];
string inventory = args[1];
string output = args[2];
Directory.CreateDirectory(Path.Combine(output, "embedded-sources"));
using var freeze = JsonDocument.Parse(File.ReadAllText(inventory));
var results = new List<object>();
var errors = new List<string>();
var diskDrift = new List<object>();
var generatedHashes = new HashSet<string>();
int ordinaryCount = 0, generatedCount = 0;
foreach (var item in freeze.RootElement.GetProperty("assemblies_sha256").EnumerateObject().Where(x => x.Name.EndsWith(".pdb", StringComparison.Ordinal)))
{
    string pdbPath = Path.Combine(root, item.Name);
    string pePath = Path.ChangeExtension(pdbPath, ".dll");
    using var pdbStream = File.OpenRead(pdbPath);
    using var provider = MetadataReaderProvider.FromPortablePdbStream(pdbStream);
    var metadata = provider.GetMetadataReader();
    using var peStream = File.OpenRead(pePath);
    using var pe = new PEReader(peStream);
    var peMetadata = pe.GetMetadataReader();
    var contentId = new BlobContentId(metadata.DebugMetadataHeader!.Id);
    var debugEntry = pe.ReadDebugDirectory().Single(x => x.Type == DebugDirectoryEntryType.CodeView);
    var codeView = pe.ReadCodeViewDebugDirectoryData(debugEntry);
    bool identityMatches = codeView.Guid == contentId.Guid && debugEntry.Stamp == contentId.Stamp && codeView.Age == 1;
    if (!identityMatches) errors.Add("PE/PDB identity mismatch: " + pePath);
    var documents = new List<object>();
    foreach (var handle in metadata.Documents)
    {
        var document = metadata.GetDocument(handle);
        string path = metadata.GetString(document.Name);
        byte[] expected = metadata.GetBlobBytes(document.Hash);
        Guid algorithm = metadata.GetGuid(document.HashAlgorithm);
        byte[] Hash(byte[] bytes) => algorithm == new Guid("8829d00f-11b8-4213-878b-770e8597ac16") ? SHA256.HashData(bytes)
            : algorithm == new Guid("ff1816ec-aa5e-4d10-87f7-6f4963833460") ? SHA1.HashData(bytes)
            : throw new InvalidOperationException("Unsupported PDB source hash algorithm: " + algorithm);
        bool generated = path.Split('/').Any(segment => segment is "obj" or "bin");
        byte[]? disk = File.Exists(path) ? File.ReadAllBytes(path) : null;
        byte[]? embedded = null;
        foreach (var customHandle in metadata.GetCustomDebugInformation(handle))
        {
            var custom = metadata.GetCustomDebugInformation(customHandle);
            if (metadata.GetGuid(custom.Kind) != new Guid("0e8a571b-6926-466e-b4ad-8ab04611f5fe")) continue;
            byte[] blob = metadata.GetBlobBytes(custom.Value);
            int length = BinaryPrimitives.ReadInt32LittleEndian(blob);
            if (length == 0) embedded = blob.AsSpan(4).ToArray();
            else
            {
                if (length < 0 || length > 16_777_216) throw new InvalidOperationException("Invalid embedded source length.");
                using var compressed = new MemoryStream(blob, 4, blob.Length - 4);
                using var inflater = new DeflateStream(compressed, CompressionMode.Decompress);
                embedded = new byte[length];
                inflater.ReadExactly(embedded);
                if (inflater.ReadByte() != -1) throw new InvalidOperationException("Embedded source length mismatch.");
            }
        }
        bool? diskMatches = disk is null ? null : Hash(disk).AsSpan().SequenceEqual(expected);
        bool? embeddedMatches = embedded is null ? null : Hash(embedded).AsSpan().SequenceEqual(expected);
        // Ordinary current sources are authoritative. Generated documents may differ across build graphs;
        // preserve both observations and use embedded bytes only for those generated documents.
        byte[]? selected = generated && embedded is not null ? embedded : disk ?? embedded;
        string selectedFrom = generated && embedded is not null ? "embedded" : disk is not null ? "disk" : "embedded";
        bool matches = selected is not null && Hash(selected).AsSpan().SequenceEqual(expected);
        if (!matches || !generated && disk is not null && diskMatches != true)
            errors.Add("PDB source mismatch or missing: " + path + " in " + pdbPath);
        if (embeddedMatches == false) errors.Add("Embedded source mismatch: " + path);
        if (generated && diskMatches == false)
            diskDrift.Add(new { pdb = item.Name, source = path, expected = Convert.ToHexString(expected), diskSha256 = Convert.ToHexString(SHA256.HashData(disk!)), embeddedMatches });
        string? sha = selected is null ? null : Convert.ToHexString(SHA256.HashData(selected));
        if (generated)
        {
            generatedCount++;
            if (selected is not null)
            {
                generatedHashes.Add(sha!);
                File.WriteAllBytes(Path.Combine(output, "embedded-sources", sha + ".cs"), selected);
            }
        }
        else ordinaryCount++;
        documents.Add(new { path, generated, checksumAlgorithm = algorithm, expected = Convert.ToHexString(expected), sha256 = sha, matches, selectedFrom, diskMatches, embeddedMatches });
    }
    results.Add(new { pe = pePath, pdb = pdbPath, peSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(pePath))), pdbSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(pdbPath))), mvid = peMetadata.GetGuid(peMetadata.GetModuleDefinition().Mvid), identityMatches, documents });
}
var options = new JsonSerializerOptions { WriteIndented = true };
File.WriteAllText(Path.Combine(output, "all-pdb-documents.json"), JsonSerializer.Serialize(results, options) + "\n");
var summary = new { pdbCount = results.Count, ordinaryDocumentCopies = ordinaryCount, generatedDocumentCopies = generatedCount, uniqueArchivedGeneratedSources = generatedHashes.Count, currentGeneratedDiskDifferences = diskDrift, errors, passed = errors.Count == 0 };
File.WriteAllText(Path.Combine(output, "summary.json"), JsonSerializer.Serialize(summary, options) + "\n");
Console.WriteLine(JsonSerializer.Serialize(summary, options));
return errors.Count == 0 ? 0 : 1;
