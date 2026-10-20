using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

/// <summary>Observes the real fixture host after its service provider has been built.</summary>
public sealed class StartupHook : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>
{
    /// <summary>Installs the observer before the unmodified fixture entry point executes.</summary>
    public static void Initialize() => DiagnosticListener.AllListeners.Subscribe(new StartupHook());

    /// <summary>Subscribes to the minimal host's actual build events.</summary>
    public void OnNext(DiagnosticListener listener)
    {
        if (listener.Name == "Microsoft.Extensions.Hosting")
        {
            listener.Subscribe(this);
        }
    }

    /// <summary>Checks actual resolved registrations, then exits before starting external resources.</summary>
    public void OnNext(KeyValuePair<string, object?> notification)
    {
        if (notification.Key != "HostBuilt")
        {
            return;
        }

        try
        {
            object host = notification.Value ?? throw new InvalidOperationException("Host missing.");
            IServiceProvider services = (IServiceProvider)host.GetType().GetProperty("Services")!.GetValue(host)!;
            Type registryType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("Hexalith.EventStore.DomainService.FoldedSnapshotQualificationRegistry"))
                .OfType<Type>().Single();
            object registry = services.GetService(registryType)
                ?? throw new InvalidOperationException("Snapshot registry missing.");
            object entry = registryType.GetMethod("Find")!.Invoke(registry, ["counter", "counter"])
                ?? throw new InvalidOperationException("Counter registration missing.");
            object? Read(string name) => entry.GetType().GetProperty(name)!.GetValue(entry);
            string expectedState = Environment.GetEnvironmentVariable("P1R_EXPECTED_STATE")
                ?? throw new InvalidOperationException("Expected state missing.");
            Type fixture = Assembly.GetEntryAssembly()!.GetType("FixtureSnapshotQualification")!;
            object limits = fixture.GetProperty("Limits", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
            if ((string)Read("ProfileId")! != "p1r-counter-folded-v1"
                || (string)Read("ParityEvidenceId")! != "p1r-counter-roundtrip-event-parity-v1"
                || ((Type)Read("StateType")!).FullName != expectedState
                || !ReferenceEquals(Read("Limits"), limits)
                || registryType.GetMethod("Find")!.Invoke(registry, ["other", "counter"]) is not null)
            {
                throw new InvalidOperationException("Snapshot registration differs from the real fixture configuration.");
            }

            Console.WriteLine("P1R_REGISTRATION " + JsonSerializer.Serialize(new
            {
                state_type = expectedState,
                profile_id = Read("ProfileId"),
                parity_evidence_id = Read("ParityEvidenceId"),
                limits,
            }, JsonSerializerOptions.Web));
            Environment.Exit(0);
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("P1R_REGISTRATION_REFUSED " + error.Message);
            Environment.Exit(2);
        }
    }

    /// <summary>Surfaces diagnostics errors as test failures.</summary>
    public void OnError(Exception error) => throw error;

    /// <summary>Handles completion without changing the fixture.</summary>
    public void OnCompleted() { }
}
