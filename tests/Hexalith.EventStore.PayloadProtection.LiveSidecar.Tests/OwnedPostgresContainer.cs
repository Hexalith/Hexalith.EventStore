using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests;

/// <summary>Allocates and cleans only this fixture's labeled PostgreSQL container; injected operations are resource-free controls.</summary>
internal sealed class OwnedPostgresContainer
{
    internal enum Operation { Run, PortLookup, DiscoverOwned, InspectOwned, RemoveOwned }
    internal enum FailureStatus { Failed, StartFailed, Timeout, OutputOrProcessFailed }
    internal sealed class OperationFailure(Operation operation, FailureStatus status, int? exitCode, string? stderrSha256)
        : InvalidOperationException($"PF-02 Docker operation={operation}; status={status}; exit={(exitCode?.ToString(CultureInfo.InvariantCulture) ?? "unknown")}; stderrSha256={stderrSha256 ?? "unavailable"}.")
    {
        internal Operation Operation { get; } = operation;
        internal int? ExitCode { get; } = exitCode;
        internal string? StderrSha256 { get; } = stderrSha256;
    }

    private const string OwnerLabel = "hexalith.pf02.owner";
    private const string InspectFormat = "{\"Id\":{{json .Id}},\"Name\":{{json .Name}},\"Owner\":{{json (index .Config.Labels \"hexalith.pf02.owner\")}},\"Image\":{{json .Config.Image}},\"Running\":{{json .State.Running}},\"Ports\":{{json .NetworkSettings.Ports}}}";
    private readonly string _identity;
    private readonly Func<Operation, string[], string?, TimeSpan, Task<string>> _execute;
    private readonly Action<string> _report;
    private readonly object _cleanupLock = new();
    private bool _attempted;
    private string? _createdId;
    private Task? _cleanup;

    internal OwnedPostgresContainer(string identity, Func<Operation, string[], string?, TimeSpan, Task<string>>? execute = null, Action<string>? report = null)
    {
        if (!Regex.IsMatch(identity, "^pp-lifecycle-[0-9a-f]{32}$", RegexOptions.CultureInvariant)) { throw InvalidResponse(); }
        _identity = identity; _execute = execute ?? ExecuteAsync; _report = report ?? Console.Error.WriteLine;
    }

    internal async Task<int> StartAsync(string password)
    {
        if (_attempted || _cleanup is not null) { throw new InvalidOperationException("PF-02 container launch was already attempted."); }
        _attempted = true; // Docker may create the owned container even when the client subsequently fails.
        try
        {
            _createdId = (await _execute(Operation.Run, ["run", "--detach", "--rm", "--name", _identity,
                "--label", OwnerLabel + "=" + _identity, "--publish", "127.0.0.1::5432", "--env", "POSTGRES_PASSWORD",
                PayloadProtectionDaprFixture.PostgresImage], password, TimeSpan.FromSeconds(60)).ConfigureAwait(false)).Trim();
            if (!IsId(_createdId)) { throw InvalidResponse(); }
            string output = await _execute(Operation.PortLookup, InspectArguments(_identity), null, TimeSpan.FromSeconds(60)).ConfigureAwait(false);
            using JsonDocument document = ParseOwned(output, _createdId);
            JsonElement root = document.RootElement;
            if (!root.GetProperty("Running").GetBoolean()) { throw InvalidResponse(); }
            JsonElement ports = root.GetProperty("Ports"); ExactProperties(ports, "5432/tcp");
            JsonElement bindings = ports.GetProperty("5432/tcp");
            if (bindings.ValueKind != JsonValueKind.Array || bindings.GetArrayLength() != 1) { throw InvalidResponse(); }
            JsonElement binding = bindings[0]; ExactProperties(binding, "HostIp", "HostPort");
            if (binding.GetProperty("HostIp").ValueKind != JsonValueKind.String || binding.GetProperty("HostPort").ValueKind != JsonValueKind.String) { throw InvalidResponse(); }
            string? port = binding.GetProperty("HostPort").GetString();
            if (binding.GetProperty("HostIp").GetString() != "127.0.0.1" || port is null
                || !Regex.IsMatch(port, "^[1-9][0-9]{0,4}$", RegexOptions.CultureInvariant)
                || !int.TryParse(port, NumberStyles.None, CultureInfo.InvariantCulture, out int allocated) || allocated > 65535) { throw InvalidResponse(); }
            return allocated;
        }
        catch (Exception firstFailure)
        {
            try { await CleanupAsync().ConfigureAwait(false); }
            catch (Exception cleanupFailure)
            {
                string diagnostic = cleanupFailure is OperationFailure operation ? operation.Message : "PF-02 owned cleanup status=unknown-or-invalid-response.";
                firstFailure.Data["PF02OwnedCleanupFailure"] = diagnostic;
                try { _report(diagnostic); }
                catch { firstFailure.Data["PF02CleanupDiagnosticWriteFailed"] = true; }
            }
            throw;
        }
    }

    internal ValueTask CleanupAsync()
    {
        lock (_cleanupLock) { return new(_cleanup ??= _attempted ? CleanupCoreAsync() : Task.CompletedTask); }
    }

    private async Task CleanupCoreAsync()
    {
        var deadline = Stopwatch.StartNew();
        Task<string> Execute(Operation operation, string[] arguments)
        {
            TimeSpan remaining = TimeSpan.FromSeconds(10) - deadline.Elapsed;
            if (remaining <= TimeSpan.Zero) { throw new OperationFailure(operation, FailureStatus.Timeout, null, null); }
            return _execute(operation, arguments, null, remaining);
        }
        string output = await Execute(Operation.DiscoverOwned, ["ps", "--all", "--quiet", "--no-trunc", "--filter", "label=" + OwnerLabel + "=" + _identity]).ConfigureAwait(false);
        if (output.Length > 8192) { throw InvalidResponse(); }
        string[] ids = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (ids.Length == 0) { return; }
        if (ids.Length != 1 || !IsId(ids[0]) || (_createdId is not null && IsId(_createdId) && ids[0] != _createdId)) { throw InvalidResponse(); }
        using JsonDocument owned = ParseOwned(await Execute(Operation.InspectOwned, InspectArguments(ids[0])).ConfigureAwait(false), ids[0]);
        _ = await Execute(Operation.RemoveOwned, ["rm", "--force", ids[0]]).ConfigureAwait(false);
    }

    private static string[] InspectArguments(string identity) => ["inspect", "--type", "container", "--format", InspectFormat, identity];
    private JsonDocument ParseOwned(string output, string expectedId)
    {
        if (output.Length > 8192) { throw InvalidResponse(); }
        JsonDocument document;
        try { document = JsonDocument.Parse(output, new JsonDocumentOptions { MaxDepth = 8 }); }
        catch (JsonException) { throw InvalidResponse(); }
        try
        {
            JsonElement root = document.RootElement; ExactProperties(root, "Id", "Name", "Owner", "Image", "Running", "Ports");
            if (new[] { "Id", "Name", "Owner", "Image" }.Any(name => root.GetProperty(name).ValueKind != JsonValueKind.String)) { throw InvalidResponse(); }
            if (root.GetProperty("Id").GetString() != expectedId || root.GetProperty("Name").GetString() != "/" + _identity
                || root.GetProperty("Owner").GetString() != _identity || root.GetProperty("Image").GetString() != PayloadProtectionDaprFixture.PostgresImage
                || root.GetProperty("Running").ValueKind is not (JsonValueKind.True or JsonValueKind.False)) { throw InvalidResponse(); }
            return document;
        }
        catch { document.Dispose(); throw; }
    }
    private static bool IsId(string value) => Regex.IsMatch(value, "^[0-9a-f]{64}$", RegexOptions.CultureInvariant);
    private static InvalidDataException InvalidResponse() => new("PF-02 owned container response is missing, foreign, malformed or ambiguous.");
    private static void ExactProperties(JsonElement value, params string[] names)
    {
        if (value.ValueKind != JsonValueKind.Object) { throw InvalidResponse(); }
        string[] actual = value.EnumerateObject().Select(property => property.Name).ToArray();
        if (actual.Length != names.Length || !actual.Order(StringComparer.Ordinal).SequenceEqual(names.Order(StringComparer.Ordinal))) { throw InvalidResponse(); }
    }

    private static async Task<string> ExecuteAsync(Operation operation, string[] arguments, string? password, TimeSpan timeout)
    {
        Process process;
        try { process = PayloadProtectionDaprFixture.Start("docker", arguments, password); }
        catch { throw new OperationFailure(operation, FailureStatus.StartFailed, null, null); }
        using (process)
        using (var deadline = new CancellationTokenSource(timeout))
        {
            Task<string> stdout = ReadBoundedAsync(process.StandardOutput, 8192, deadline.Token);
            Task<string> stderr = ReadBoundedAsync(process.StandardError, 65536, deadline.Token);
            var pending = new List<Task> { stdout, stderr, process.WaitForExitAsync(deadline.Token) };
            try
            {
                while (pending.Count > 0) { Task completed = await Task.WhenAny(pending).ConfigureAwait(false); await completed.ConfigureAwait(false); pending.Remove(completed); }
            }
            catch (Exception failure)
            {
                bool unknownClientCleanup = false;
                try { if (!process.HasExited) { process.Kill(entireProcessTree: true); } }
                catch { unknownClientCleanup = true; }
                deadline.Cancel();
                var safe = new OperationFailure(operation, failure is OperationCanceledException ? FailureStatus.Timeout : FailureStatus.OutputOrProcessFailed,
                    process.HasExited ? process.ExitCode : null, stderr.IsCompletedSuccessfully ? Hash(stderr.Result) : null);
                if (unknownClientCleanup) { safe.Data["PF02ClientCleanup"] = "unknown"; }
                throw safe;
            }
            if (process.ExitCode != 0) { throw new OperationFailure(operation, FailureStatus.Failed, process.ExitCode, Hash(stderr.Result)); }
            return stdout.Result;
        }
    }
    internal static async Task<string> ReadBoundedAsync(TextReader reader, int maximum, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var output = new StringBuilder(); var buffer = new char[1024]; int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false)) != 0)
        {
            if (output.Length + count > maximum) { throw InvalidResponse(); }
            output.Append(buffer, 0, count);
        }
        return output.ToString();
    }
    internal static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
