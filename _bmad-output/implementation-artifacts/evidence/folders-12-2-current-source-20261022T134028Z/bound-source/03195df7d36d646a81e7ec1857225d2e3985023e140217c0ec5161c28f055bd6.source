using System.Buffers.Binary;
using System.IO.Compression;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Projections;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>Binds actual loaded runtime images to portable PDB documents and current source bytes.</summary>
internal static class ProjectionBoundedIdentityRuntimeProof
{
    internal static JsonArray Capture()
    {
        var result = new JsonArray();
        foreach (Assembly assembly in new[] { typeof(SharedProjectionEpochCoordinator).Assembly,
            typeof(DomainSharedProjectionRebuildDispatcher).Assembly, typeof(SharedProjectionSourceBeginProducer).Assembly,
            typeof(ProjectionBoundedIdentityProcessWorkerTests).Assembly })
        {
            string pe = assembly.Location;
            string pdb = Path.ChangeExtension(pe, ".pdb");
            using var stream = File.OpenRead(pdb);
            using var provider = MetadataReaderProvider.FromPortablePdbStream(stream);
            var metadata = provider.GetMetadataReader();
            using var peStream = File.OpenRead(pe);
            using var peReader = new PEReader(peStream);
            var peMetadata = peReader.GetMetadataReader();
            Guid peMvid = peMetadata.GetGuid(peMetadata.GetModuleDefinition().Mvid);
            if (peMvid != assembly.ManifestModule.ModuleVersionId)
                throw new InvalidOperationException("The loaded module differs from the exact runtime PE being hashed.");
            var codeView = peReader.ReadDebugDirectory().Single(item => item.Type == DebugDirectoryEntryType.CodeView);
            var peCodeView = peReader.ReadCodeViewDebugDirectoryData(codeView);
            byte[] pdbId = metadata.DebugMetadataHeader!.Id.ToArray();
            if (new Guid(pdbId.AsSpan(0, 16)) != peCodeView.Guid) throw new InvalidOperationException("Runtime PE and portable PDB identities differ.");
            var documents = new JsonArray();
            foreach (var handle in metadata.Documents)
            {
                var document = metadata.GetDocument(handle);
                string path = metadata.GetString(document.Name);
                // SDK-generated sources are separately bound by their containing PE/PDB.
                if (!path.Contains("/src/", StringComparison.Ordinal) && !path.Contains("/tests/", StringComparison.Ordinal)) continue;
                byte[] bytes = ReadDocumentSource(metadata, handle, path), expected = metadata.GetBlobBytes(document.Hash);
                Guid algorithm = metadata.GetGuid(document.HashAlgorithm);
                byte[] actual = algorithm == new Guid("8829d00f-11b8-4213-878b-770e8597ac16") ? SHA256.HashData(bytes)
                    : algorithm == new Guid("ff1816ec-aa5e-4d10-87f7-6f4963833460") ? SHA1.HashData(bytes)
                    : throw new InvalidOperationException("Unsupported PDB source hash algorithm.");
                if (!actual.AsSpan().SequenceEqual(expected)) throw new InvalidOperationException("Runtime PDB source differs: " + path);
                string sourceHash = Convert.ToHexString(SHA256.HashData(bytes));
                bool generated = path.Contains("/obj/", StringComparison.Ordinal);
                if (generated && Environment.GetEnvironmentVariable("BOUNDED_IDENTITY_GENERATED_SOURCE_DIR") is { } captureDirectory)
                {
                    Directory.CreateDirectory(captureDirectory);
                    File.WriteAllBytes(Path.Combine(captureDirectory, sourceHash + ".cs"), bytes);
                }
                documents.Add(new JsonObject { ["path"] = path, ["sha256"] = sourceHash,
                    ["pdbChecksum"] = Convert.ToHexString(expected), ["generated"] = generated });
            }
            peStream.Position = 0; stream.Position = 0;
            result.Add(new JsonObject { ["name"] = assembly.GetName().Name, ["pe"] = pe,
                ["peSha256"] = Convert.ToHexString(SHA256.HashData(peStream)), ["pdb"] = pdb,
                ["pdbSha256"] = Convert.ToHexString(SHA256.HashData(stream)), ["loadedMvid"] = assembly.ManifestModule.ModuleVersionId.ToString(),
                ["peMvid"] = peMvid.ToString(), ["loadedMvidMatchesPe"] = true, ["documents"] = documents });
        }
        return result;
    }

    private static byte[] ReadDocumentSource(MetadataReader metadata, DocumentHandle handle, string path)
    {
        if (File.Exists(path)) return File.ReadAllBytes(path);
        foreach (var debugHandle in metadata.GetCustomDebugInformation(handle))
        {
            var debug = metadata.GetCustomDebugInformation(debugHandle);
            if (metadata.GetGuid(debug.Kind) != new Guid("0e8a571b-6926-466e-b4ad-8ab04611f5fe")) continue;
            byte[] blob = metadata.GetBlobBytes(debug.Value);
            if (blob.Length < 4) throw new InvalidOperationException("Malformed embedded PDB source.");
            int length = BinaryPrimitives.ReadInt32LittleEndian(blob);
            if (length == 0) return blob.AsSpan(4).ToArray();
            if (length < 0 || length > 16_777_216) throw new InvalidOperationException("Embedded PDB source exceeds its bounded admission.");
            using var compressed = new MemoryStream(blob, 4, blob.Length - 4);
            using var inflater = new DeflateStream(compressed, CompressionMode.Decompress);
            byte[] source = new byte[length];
            inflater.ReadExactly(source);
            if (inflater.ReadByte() != -1) throw new InvalidOperationException("Embedded PDB source length differs from its header.");
            return source;
        }
        throw new InvalidOperationException("Runtime PDB source is unavailable and is not embedded: " + path);
    }
}
