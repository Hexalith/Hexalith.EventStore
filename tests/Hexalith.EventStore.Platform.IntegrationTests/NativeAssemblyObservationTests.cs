namespace Hexalith.EventStore.Platform.IntegrationTests;

using Shouldly;

/// <summary>Exercises actual local diagnostic file boundaries without simulating a CI or native owner.</summary>
public sealed class NativeAssemblyObservationTests
{
    /// <summary>Preserves the original private diagnostic and refuses a duplicate write.</summary>
    [Fact]
    public void PrivateDiagnosticRetainsFirstBytesAndRefusesOverwrite()
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("This file boundary control requires Linux.");
        string directory = Directory.CreateTempSubdirectory("eventstore-diagnostic-io-control-").FullName;
        try
        {
            const string fileIdentity = "local-io-control";
            const string observation = "{\"scope\":\"local-file-control-no-native-credit\"}";
            NativeAssemblyObservations.WritePrivateObservation(directory, fileIdentity, observation);
            string path = Path.Combine(directory, "loaded-assemblies-" + fileIdentity + ".json");
            byte[] original = File.ReadAllBytes(path);
            File.GetUnixFileMode(path).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
            Should.Throw<IOException>(() => NativeAssemblyObservations.WritePrivateObservation(directory, fileIdentity, "replacement"));
            File.ReadAllBytes(path).ShouldBe(original);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    /// <summary>Refuses a linked directory before creating any diagnostic file.</summary>
    [Fact]
    public void PrivateDiagnosticRefusesSymbolicDirectory()
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("This file boundary control requires Linux.");
        string directory = Directory.CreateTempSubdirectory("eventstore-diagnostic-link-control-").FullName;
        try
        {
            string target = Directory.CreateDirectory(Path.Combine(directory, "target")).FullName;
            string linked = Path.Combine(directory, "linked");
            Directory.CreateSymbolicLink(linked, target);
            Should.Throw<IOException>(() => NativeAssemblyObservations.WritePrivateObservation(linked, "local-io-control", "{}"));
            Directory.GetFiles(target).ShouldBeEmpty();
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
