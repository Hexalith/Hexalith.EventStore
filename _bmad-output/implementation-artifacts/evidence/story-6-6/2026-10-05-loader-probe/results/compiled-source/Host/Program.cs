using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

namespace Probe.Host;

/// <summary>Runs one bounded isolated candidate experiment and reports effects before final capability rejection.</summary>
internal static class Program
{
    private static readonly string[] SharedFrameworkNames =
    [
        "System.Private.CoreLib", "System.Runtime", "System.Runtime.Loader",
        "System.Runtime.InteropServices", "System.IO.FileSystem",
    ];

    /// <summary>Runs one scenario using only paths supplied by the owned temporary runner.</summary>
    private static int Main(string[] args)
    {
        if (args.Length != 5)
        {
            throw new ArgumentException("Expected mode, manifest, managed fixture, native fixture and effects path.");
        }

        string mode = args[0];
        PinnedAssembly[] pins = JsonSerializer.Deserialize<PinnedAssembly[]>(File.ReadAllText(args[1]))!;
        var managed = pins.ToDictionary(pin => pin.Name, StringComparer.Ordinal);
        var shared = SharedFrameworkNames.ToDictionary(name => name,
            name => AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(name)), StringComparer.Ordinal);
        // Prewarm host serialization/reflection before taking the observation baseline.
        _ = JsonSerializer.Serialize(new { sample = "prewarm" });
        _ = typeof(Program).GetMethods(BindingFlags.NonPublic | BindingFlags.Static);
        _ = SHA256.HashData([]);
        HashSet<Assembly> baseline = AppDomain.CurrentDomain.GetAssemblies().ToHashSet();
        var audit = new ProbeAudit();
        using var observation = new AssemblyObservation(audit, baseline, managed.Keys.ToHashSet(), shared.Values.ToHashSet());
        var candidate = new PinnedLoadContext(managed, shared, audit);
        int resolverCalls = 0;
        string? invocationError = null;
        bool callbackReturned = false;
        bool capabilityRejected = false;
        try
        {
            Assembly plugin = candidate.LoadPinnedRoot("Probe.Plugin");
            // The ordinary P/Invoke control falls through to the strict context hook.
            // The simple explicit NativeLibrary.Load overload must be tested against both hooks.
            NativeLibrary.SetDllImportResolver(plugin, (name, _, _) =>
            {
                resolverCalls++;
                audit.Record("per-assembly-native-resolver", name);
                return IntPtr.Zero;
            });
            MethodInfo run = plugin.GetType("Probe.Plugin.Entry", throwOnError: true)!
                .GetMethod("Run", BindingFlags.Public | BindingFlags.Static)!;
            audit.Record("host-before-invocation");
            if (!audit.Ready)
            {
                throw new FileLoadException("CapabilityMismatch: not ready before invocation.");
            }

            try
            {
                run.Invoke(null, [mode, args[2], args[3], args[4], (Action<string>)(kind => audit.Record(kind))]);
                callbackReturned = true;
                audit.Record("host-callback-returned");
            }
            catch (TargetInvocationException error)
            {
                invocationError = (error.InnerException ?? error).ToString();
                audit.Record("host-callback-threw", (error.InnerException ?? error).GetType().FullName!);
            }

            string[] effects = File.Exists(args[4]) ? File.ReadAllLines(args[4]) : [];
            if (effects.Any(effect => effect.StartsWith("native:", StringComparison.Ordinal)))
            {
                // Explicit-load handles can be freed before a maps scan. This owned marker
                // proves the effect first; detecting it here deliberately models a late audit.
                audit.Invalidate("post-callback-owned-native-effect-observed");
            }

            if (!audit.Ready || invocationError is not null)
            {
                capabilityRejected = true;
                audit.Record("host-capability-rejected");
            }

            Console.WriteLine(JsonSerializer.Serialize(new
            {
                mode,
                callbackReturned,
                invocationError,
                capabilityRejected,
                candidateReadyAtEnd = audit.Ready,
                managedResolutionCalls = candidate.ManagedResolutionCalls,
                nativeResolutionCalls = candidate.NativeResolutionCalls,
                perAssemblyNativeResolverCalls = resolverCalls,
                effects,
                events = audit.Events,
                sharedFramework = shared.Select(pair => new
                {
                    name = pair.Key, identity = pair.Value.FullName, path = pair.Value.Location,
                    sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(pair.Value.Location))),
                }),
                scope = "isolated resolver and observation candidate only; no production loader adoption",
            }));

            return 0;
        }
        finally
        {
            candidate.Unload();
        }
    }
}
