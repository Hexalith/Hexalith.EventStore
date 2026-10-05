using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;

using Hexalith.EventStore.Server.Control;

using Npgsql;

using Shouldly;

namespace Hexalith.EventStore.Server.PostgreSql.Tests;

/// <summary>Isolated local SQL checks; no Dapr, OpenBao, production TLS pin or readiness claim.</summary>
public sealed class ControlTransactionLiveTests
{
    private const string PostgresImage = "postgres@sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636";

    [Fact]
    public async Task MultiRowCommitLostAckAndChangedPayloadOrIndexRequireCompleteFreshReadback()
    {
        string containerName = "eventstore-control-" + Guid.NewGuid().ToString("N");
        string password = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24));
        string? containerId = null;
        try
        {
            containerId = (await RunDockerAsync(["run", "--rm", "-d", "--name", containerName,
                "-e", "POSTGRES_PASSWORD", "-e", "POSTGRES_DB=eventstore", "-p", "127.0.0.1::5432", PostgresImage],
                password).ConfigureAwait(true)).Trim();
            string portText = (await RunDockerAsync(["port", containerId, "5432/tcp"]).ConfigureAwait(true)).Trim();
            int port = int.Parse(portText[(portText.LastIndexOf(':') + 1)..], CultureInfo.InvariantCulture);
            var connectionString = new NpgsqlConnectionStringBuilder
            {
                Host = "127.0.0.1", Port = port, Username = "postgres", Password = password,
                Database = "eventstore", SslMode = SslMode.Disable, Timeout = 5, CommandTimeout = 5,
            };
            await using NpgsqlDataSource dataSource = NpgsqlDataSource.Create(connectionString.ConnectionString);
            await WaitForDatabaseAsync(dataSource).ConfigureAwait(true);
            await ApplyReviewedSchemaAsync(dataSource).ConfigureAwait(true);

            var kernel = new PostgreSqlControlTransactionKernel(dataSource, new LocalTestAdmission());
            string left = "held-delivery:" + new string('0', 64);
            string right = "held-delivery:" + new string('1', 64);
            string firstFence = PostgreSqlOwnerFence.Create(DateTimeOffset.UnixEpoch, new byte[10]);
            string nextFence = PostgreSqlOwnerFence.Create(DateTimeOffset.UnixEpoch.AddMilliseconds(1), new byte[10]);
            var createdLeft = Image(0, firstFence, "left-0"u8.ToArray());
            var createdRight = Image(0, firstFence, "right-0"u8.ToArray());
            var create = new PostgreSqlControlTransactionPlan([
                new("control/held", right, null, createdRight),
                new("control/held", left, null, createdLeft),
            ]);

            PostgreSqlControlTransactionResult lostAck = await kernel.ApplyOnceAsync(create,
                new PostgreSqlControlDeadline(TimeSpan.FromSeconds(30)), CancellationToken.None, CancellationToken.None,
                _ => throw new IOException("Injected missing commit acknowledgement after the real COMMIT.")).ConfigureAwait(true);
            lostAck.ShouldBe(PostgreSqlControlTransactionResult.Committed);
            await RequireRowsAsync(dataSource, (left, 0, firstFence, "left-0"u8.ToArray()),
                (right, 0, firstFence, "right-0"u8.ToArray())).ConfigureAwait(true);

            PostgreSqlControlTransactionResult repeatedCreate = await kernel.ApplyOnceAsync(create,
                new PostgreSqlControlDeadline(TimeSpan.FromSeconds(30)), CancellationToken.None, CancellationToken.None).ConfigureAwait(true);
            repeatedCreate.ShouldBe(PostgreSqlControlTransactionResult.Committed);

            var updatedLeft = Image(1, firstFence, "left-1"u8.ToArray());
            var updatedRight = Image(1, firstFence, "right-1"u8.ToArray());
            var update = new PostgreSqlControlTransactionPlan([
                new("control/held", right, createdRight, updatedRight),
                new("control/held", left, createdLeft, updatedLeft),
            ]);
            (await kernel.ApplyOnceAsync(update, new PostgreSqlControlDeadline(TimeSpan.FromSeconds(30)),
                CancellationToken.None, CancellationToken.None).ConfigureAwait(true))
                .ShouldBe(PostgreSqlControlTransactionResult.Committed);
            await RequireRowsAsync(dataSource, (left, 1, firstFence, "left-1"u8.ToArray()),
                (right, 1, firstFence, "right-1"u8.ToArray())).ConfigureAwait(true);

            var transfer = new PostgreSqlControlTransactionPlan([
                new("control/held", left, updatedLeft, Image(2, nextFence, "left-2"u8.ToArray()), OwnershipTransfer: true),
                new("control/held", right, updatedRight, Image(2, nextFence, "right-2"u8.ToArray()), OwnershipTransfer: true),
            ]);
            (await kernel.ApplyOnceAsync(transfer, new PostgreSqlControlDeadline(TimeSpan.FromSeconds(30)),
                CancellationToken.None, CancellationToken.None,
                async _ =>
                {
                    await TamperPayloadAsync(dataSource, right, "right-changed"u8.ToArray()).ConfigureAwait(true);
                    throw new IOException("One participant changed after COMMIT and before fresh readback.");
                }).ConfigureAwait(true))
                .ShouldBe(PostgreSqlControlTransactionResult.EvidenceHold);
            (await kernel.ApplyOnceAsync(transfer, new PostgreSqlControlDeadline(TimeSpan.FromSeconds(30)),
                CancellationToken.None, CancellationToken.None).ConfigureAwait(true))
                .ShouldBe(PostgreSqlControlTransactionResult.EvidenceHold);
            await RequireRowsAsync(dataSource, (left, 2, nextFence, "left-2"u8.ToArray()),
                (right, 2, nextFence, "right-changed"u8.ToArray())).ConfigureAwait(true);

            string entry = "owner-registry-entry:" + new string('2', 64);
            var index = new PostgreSqlRegistryIndex("deployment"u8, "tenant"u8, "scope"u8, "subject"u8, 123);
            var indexedImage = Image(0, firstFence, "registry-entry"u8.ToArray(), index);
            var indexedCreate = new PostgreSqlControlTransactionPlan([
                new("control/registry-entry", entry, null, indexedImage),
            ]);
            (await kernel.ApplyOnceAsync(indexedCreate, new PostgreSqlControlDeadline(TimeSpan.FromSeconds(30)),
                CancellationToken.None, CancellationToken.None,
                _ => throw new IOException("Injected missing registry-entry acknowledgement after COMMIT.")).ConfigureAwait(true))
                .ShouldBe(PostgreSqlControlTransactionResult.Committed);
            await RequireRegistryEntryAsync(dataSource, entry, firstFence, index).ConfigureAwait(true);

            await using (NpgsqlConnection connection = await dataSource.OpenConnectionAsync().ConfigureAwait(true))
            await using (var tamper = new NpgsqlCommand("""
                UPDATE hexalith_eventstore_control.rows SET registry_subject = $1
                 WHERE "namespace" = $2 AND address = $3
                """, connection))
            {
                _ = tamper.Parameters.AddWithValue("other"u8.ToArray());
                _ = tamper.Parameters.AddWithValue(PostgreSqlControlStatements.Namespace);
                _ = tamper.Parameters.AddWithValue(entry);
                (await tamper.ExecuteNonQueryAsync().ConfigureAwait(true)).ShouldBe(1);
            }
            (await kernel.ApplyOnceAsync(indexedCreate, new PostgreSqlControlDeadline(TimeSpan.FromSeconds(30)),
                CancellationToken.None, CancellationToken.None).ConfigureAwait(true))
                .ShouldBe(PostgreSqlControlTransactionResult.EvidenceHold);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(containerId))
            {
                _ = await RunDockerAsync(["rm", "-f", containerId]).ConfigureAwait(true);
            }
        }
    }

    private static PostgreSqlControlRowImage Image(ulong generation, string fence, byte[] bytes,
        PostgreSqlRegistryIndex? index = null)
        => new(generation, fence, new PostgreSqlControlPayloadSource(bytes.Length,
            () => new MemoryStream(bytes, writable: false)), index);

    private static async Task TamperPayloadAsync(NpgsqlDataSource dataSource, string address, byte[] payload)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync().ConfigureAwait(true);
        await using var command = new NpgsqlCommand("""
            UPDATE hexalith_eventstore_control.rows SET payload = $1
             WHERE "namespace" = $2 AND address = $3
            """, connection);
        _ = command.Parameters.AddWithValue(payload);
        _ = command.Parameters.AddWithValue(PostgreSqlControlStatements.Namespace);
        _ = command.Parameters.AddWithValue(address);
        (await command.ExecuteNonQueryAsync().ConfigureAwait(true)).ShouldBe(1);
    }

    private static async Task RequireRegistryEntryAsync(NpgsqlDataSource dataSource, string address,
        string fence, PostgreSqlRegistryIndex expected)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync().ConfigureAwait(true);
        await using var command = new NpgsqlCommand("""
            SELECT generation, owner_fence, payload,
                   registry_deployment, registry_scope_kind, registry_scope_id, registry_subject,
                   registry_first_ticks, registry_shard, registry_deployment_hash, registry_scope_hash
              FROM hexalith_eventstore_control.rows WHERE "namespace" = $1 AND address = $2
            """, connection);
        _ = command.Parameters.AddWithValue(PostgreSqlControlStatements.Namespace);
        _ = command.Parameters.AddWithValue(address);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync().ConfigureAwait(true);
        (await reader.ReadAsync().ConfigureAwait(true)).ShouldBeTrue();
        reader.GetDecimal(0).ShouldBe(0m);
        reader.GetString(1).ShouldBe(fence);
        reader.GetFieldValue<byte[]>(2).ShouldBe("registry-entry"u8.ToArray());
        reader.GetFieldValue<byte[]>(3).ShouldBe(expected.Deployment);
        reader.GetFieldValue<byte[]>(4).ShouldBe(expected.ScopeKind);
        reader.GetFieldValue<byte[]>(5).ShouldBe(expected.ScopeId);
        reader.GetFieldValue<byte[]>(6).ShouldBe(expected.Subject);
        reader.GetInt64(7).ShouldBe(expected.FirstTicks);
        reader.GetInt16(8).ShouldBe((short)expected.Shard);
        reader.GetFieldValue<byte[]>(9).ShouldBe(expected.DeploymentHash);
        reader.GetFieldValue<byte[]>(10).ShouldBe(expected.ScopeHash);
        (await reader.ReadAsync().ConfigureAwait(true)).ShouldBeFalse();
    }

    private static async Task RequireRowsAsync(NpgsqlDataSource dataSource,
        params (string Address, int Generation, string Fence, byte[] Payload)[] expected)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync().ConfigureAwait(true);
        await using var command = new NpgsqlCommand("""
            SELECT address, generation, owner_fence, payload,
                   registry_deployment, registry_scope_kind, registry_scope_id, registry_subject,
                   registry_first_ticks, registry_shard, registry_deployment_hash, registry_scope_hash
              FROM hexalith_eventstore_control.rows WHERE "namespace" = $1 ORDER BY address
            """, connection);
        _ = command.Parameters.AddWithValue(PostgreSqlControlStatements.Namespace);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync().ConfigureAwait(true);
        foreach ((string address, int generation, string fence, byte[] payload) in expected)
        {
            (await reader.ReadAsync().ConfigureAwait(true)).ShouldBeTrue();
            reader.GetString(0).ShouldBe(address);
            reader.GetDecimal(1).ShouldBe((decimal)generation);
            reader.GetString(2).ShouldBe(fence);
            reader.GetFieldValue<byte[]>(3).ShouldBe(payload);
            for (int column = 4; column <= 11; column++) { reader.IsDBNull(column).ShouldBeTrue(); }
        }
        (await reader.ReadAsync().ConfigureAwait(true)).ShouldBeFalse();
    }

    private static async Task ApplyReviewedSchemaAsync(NpgsqlDataSource dataSource)
    {
        string root = FindRepositoryRoot();
        string sql = File.ReadAllText(Path.Combine(root, "src", "Hexalith.EventStore.Server", "Control", "Schema", "v1.sql"));
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync().ConfigureAwait(true);
        foreach (string statement in sql.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            await using var command = new NpgsqlCommand(statement, connection);
            _ = await command.ExecuteNonQueryAsync().ConfigureAwait(true);
        }
    }

    private static async Task WaitForDatabaseAsync(NpgsqlDataSource dataSource)
    {
        Exception? last = null;
        for (int attempt = 0; attempt < 60; attempt++)
        {
            try
            {
                await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync().ConfigureAwait(true);
                return;
            }
            catch (NpgsqlException error)
            {
                last = error;
                await Task.Delay(500).ConfigureAwait(true);
            }
        }
        throw new InvalidOperationException("Disposable local PostgreSQL did not become ready.", last);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Hexalith.EventStore.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new DirectoryNotFoundException("Story 6.6 root was not found.");
    }

    private static async Task<string> RunDockerAsync(IReadOnlyList<string> arguments, string? password = null)
    {
        var start = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string argument in arguments) { start.ArgumentList.Add(argument); }
        if (password is not null) { start.Environment["POSTGRES_PASSWORD"] = password; }
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Docker could not start.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        Task<string> output = process.StandardOutput.ReadToEndAsync(timeout.Token);
        Task<string> error = process.StandardError.ReadToEndAsync(timeout.Token);
        await process.WaitForExitAsync(timeout.Token).ConfigureAwait(true);
        string stdout = await output.ConfigureAwait(true);
        string stderr = await error.ConfigureAwait(true);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException("Disposable Docker PostgreSQL command failed: " + stderr);
        }
        return stdout;
    }

    private sealed class LocalTestAdmission : IPostgreSqlControlAdmission
    {
        public ValueTask RequireBackendAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (connection.Database != "eventstore" || connection.Host != "127.0.0.1")
            {
                throw new InvalidOperationException("Local test connected to a different backend.");
            }
            return ValueTask.CompletedTask;
        }

        public ValueTask RequireMutationAsync(PostgreSqlControlMutation mutation, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (mutation.Family is not ("control/held" or "control/registry-entry")
                || (mutation.Family == "control/held" && (mutation.Expected?.RegistryIndex is not null
                    || mutation.Next?.RegistryIndex is not null)))
            {
                throw new InvalidOperationException("This isolated test admits only its held and registry-entry fixtures.");
            }
            return ValueTask.CompletedTask;
        }
    }
}
