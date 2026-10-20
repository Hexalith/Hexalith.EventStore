using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
var records = new List<object>();
bool failed = false;
foreach (string pdbPath in args)
{
    string pePath = Path.ChangeExtension(pdbPath, ".dll");
    using var pdbStream = File.OpenRead(pdbPath);
    using var provider = MetadataReaderProvider.FromPortablePdbStream(pdbStream);
    MetadataReader reader = provider.GetMetadataReader();
    var contentId = new BlobContentId(reader.DebugMetadataHeader!.Id);
    using var peStream = File.OpenRead(pePath);
    using var pe = new PEReader(peStream);
    DebugDirectoryEntry entry = pe.ReadDebugDirectory().Single(item => item.Type == DebugDirectoryEntryType.CodeView);
    CodeViewDebugDirectoryData codeView = pe.ReadCodeViewDebugDirectoryData(entry);
    bool matches = codeView.Guid == contentId.Guid && entry.Stamp == contentId.Stamp && codeView.Age == 1;
    failed |= !matches;
    records.Add(new { pe = pePath, pe_sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(pePath))).ToLowerInvariant(),
        pdb = pdbPath, pdb_sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(pdbPath))).ToLowerInvariant(),
        codeview_guid = codeView.Guid, pdb_guid = contentId.Guid, pe_stamp = entry.Stamp, pdb_stamp = contentId.Stamp, matches });
}
Console.WriteLine(JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true }));
return failed ? 1 : 0;
