using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text.Json;
var results = new List<object>();
bool failed = false;
foreach (string path in args)
{
    using Stream file = File.OpenRead(path);
    using MetadataReaderProvider provider = MetadataReaderProvider.FromPortablePdbStream(file);
    MetadataReader reader = provider.GetMetadataReader();
    var documents = new List<object>();
    int checkedCount = 0;
    int mismatches = 0;
    var missing = new List<string>();
    foreach (DocumentHandle handle in reader.Documents)
    {
        Document document = reader.GetDocument(handle);
        string source = reader.GetString(document.Name);
        if (source.Split('/').Any(part => part is "obj" or "bin")) continue;
        if (!File.Exists(source)) { missing.Add(source); failed = true; continue; }
        byte[] expected = reader.GetBlobBytes(document.Hash);
        byte[] actual = SHA256.HashData(File.ReadAllBytes(source));
        bool matches = actual.AsSpan().SequenceEqual(expected);
        checkedCount++;
        if (!matches) { mismatches++; failed = true; }
        documents.Add(new { source, sha256 = Convert.ToHexString(actual).ToLowerInvariant(), matches });
    }
    results.Add(new { pdb = path, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant(), checked_sources = checkedCount, mismatches, missing_sources = missing, documents });
}
Console.WriteLine(JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
return failed ? 1 : 0;
