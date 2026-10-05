using System.Data;
using System.Security.Cryptography;

using Npgsql;
using NpgsqlTypes;

namespace Hexalith.EventStore.Server.Control;

#pragma warning disable CA2007 // Npgsql asynchronous disposal is performed under the shared operation/recovery boundary.

/// <summary>One unregistered serializable SQL attempt over an admitted declared participant set.</summary>
/// <remarks>It grants no readiness without the required family codecs, authority, provider pin and B6 qualification.</remarks>
internal sealed class PostgreSqlControlTransactionKernel
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly IPostgreSqlControlAdmission _admission;

    internal PostgreSqlControlTransactionKernel(NpgsqlDataSource dataSource, IPostgreSqlControlAdmission admission)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(admission);
        _dataSource = dataSource;
        _admission = admission;
    }

    /// <summary>Preflights all images, compares every predecessor, writes and rereads every participant, then freshly reconciles after commit.</summary>
    internal async Task<PostgreSqlControlTransactionResult> ApplyOnceAsync(
        PostgreSqlControlTransactionPlan plan,
        PostgreSqlControlDeadline deadline,
        CancellationToken operationToken,
        CancellationToken recoveryToken,
        Func<CancellationToken, ValueTask>? commitAcknowledgementProbe = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(deadline);
        foreach (PostgreSqlControlMutation mutation in plan.Mutations)
        {
            using CancellationTokenSource linked = deadline.Link(operationToken);
            await _admission.RequireMutationAsync(mutation, linked.Token).ConfigureAwait(false);
            if (mutation.Expected is not null)
            {
                await mutation.Expected.Payload.RequireExactLengthAsync(linked.Token).ConfigureAwait(false);
            }
            if (mutation.Next is not null)
            {
                await mutation.Next.Payload.RequireExactLengthAsync(linked.Token).ConfigureAwait(false);
            }
        }

        try
        {
            using CancellationTokenSource acquisition = deadline.Link(operationToken);
            await using NpgsqlConnection connection = await _dataSource.OpenConnectionAsync(acquisition.Token).ConfigureAwait(false);
            await _admission.RequireBackendAsync(connection, acquisition.Token).ConfigureAwait(false);
            await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(
                IsolationLevel.Serializable, acquisition.Token).ConfigureAwait(false);
            await SetLocalTimeoutsAsync(connection, transaction, deadline, operationToken).ConfigureAwait(false);

            foreach (PostgreSqlControlMutation mutation in plan.Mutations)
            {
                if (!await RowMatchesAsync(connection, transaction, mutation.Address, mutation.Expected,
                    locked: true, deadline, operationToken).ConfigureAwait(false))
                {
                    throw new PostgreSqlControlConflictException();
                }
            }

            foreach (PostgreSqlControlMutation mutation in plan.Mutations)
            {
                await WriteAsync(connection, transaction, mutation, deadline, operationToken).ConfigureAwait(false);
            }

            foreach (PostgreSqlControlMutation mutation in plan.Mutations)
            {
                if (!await RowMatchesAsync(connection, transaction, mutation.Address, mutation.Next,
                    locked: false, deadline, operationToken).ConfigureAwait(false))
                {
                    throw new PostgreSqlControlConflictException();
                }
            }

            using CancellationTokenSource commit = deadline.Link(operationToken);
            await transaction.CommitAsync(commit.Token).ConfigureAwait(false);
            if (commitAcknowledgementProbe is not null)
            {
                await commitAcknowledgementProbe(commit.Token).ConfigureAwait(false);
            }
        }
        catch (Exception error) when (error is NpgsqlException or TimeoutException or OperationCanceledException
            or IOException or PostgreSqlControlConflictException)
        {
            // A failed or cancelled COMMIT is ambiguous. The original owner/intent remains until
            // one complete independent readback proves its exact result; an unchanged predecessor
            // alone does not prove that no future write from the old attempt can arrive.
        }

        try
        {
            using CancellationTokenSource readback = deadline.Link(recoveryToken);
            await using NpgsqlConnection connection = await _dataSource.OpenConnectionAsync(readback.Token).ConfigureAwait(false);
            await _admission.RequireBackendAsync(connection, readback.Token).ConfigureAwait(false);
            foreach (PostgreSqlControlMutation mutation in plan.Mutations)
            {
                if (!await RowMatchesAsync(connection, transaction: null, mutation.Address, mutation.Next,
                    locked: false, deadline, recoveryToken).ConfigureAwait(false))
                {
                    return PostgreSqlControlTransactionResult.EvidenceHold;
                }
            }
            return PostgreSqlControlTransactionResult.Committed;
        }
        catch (Exception error) when (error is NpgsqlException or TimeoutException or OperationCanceledException or IOException)
        {
            return PostgreSqlControlTransactionResult.EvidenceHold;
        }
    }

    private static async Task SetLocalTimeoutsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        PostgreSqlControlDeadline deadline, CancellationToken cancellationToken)
    {
        using CancellationTokenSource linked = deadline.Link(cancellationToken);
        int milliseconds = deadline.RequireRemainingMilliseconds();
        await using var command = new NpgsqlCommand("""
            SELECT set_config('lock_timeout', $1, true),
                   set_config('statement_timeout', $1, true),
                   set_config('idle_in_transaction_session_timeout', $1, true)
            """, connection, transaction);
        command.CommandTimeout = Math.Max(1, (milliseconds + 999) / 1000);
        Add(command, NpgsqlDbType.Text, milliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture) + "ms");
        _ = await command.ExecuteNonQueryAsync(linked.Token).ConfigureAwait(false);
    }

    private static async Task<bool> RowMatchesAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction,
        string address, PostgreSqlControlRowImage? expected, bool locked,
        PostgreSqlControlDeadline deadline, CancellationToken cancellationToken)
    {
        if (transaction is not null)
        {
            await SetLocalTimeoutsAsync(connection, transaction, deadline, cancellationToken).ConfigureAwait(false);
        }
        using CancellationTokenSource linked = deadline.Link(cancellationToken);
        await using var command = new NpgsqlCommand(locked ? PostgreSqlControlStatements.SelectForUpdate
            : PostgreSqlControlStatements.Select, connection, transaction);
        command.CommandTimeout = Math.Max(1, (deadline.RequireRemainingMilliseconds() + 999) / 1000);
        AddAddress(command, address);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, linked.Token).ConfigureAwait(false);
        if (!await reader.ReadAsync(linked.Token).ConfigureAwait(false)) { return expected is null; }
        if (expected is null) { return false; }

        decimal generation = reader.GetDecimal(0);
        if (generation != (decimal)expected.Generation
            || !string.Equals(reader.GetString(1), expected.OwnerFence, StringComparison.Ordinal))
        {
            return false;
        }
        using (Stream actual = reader.GetStream(2))
        using (Stream admitted = expected.Payload.OpenRead())
        {
            if (!await ExactPayloadAsync(actual, admitted, expected.Payload.Length, linked.Token).ConfigureAwait(false))
            {
                return false;
            }
        }
        return ExactIndex(reader, expected.RegistryIndex);
    }

    private static async Task<bool> ExactPayloadAsync(Stream actual, Stream admitted, int length, CancellationToken cancellationToken)
    {
        byte[] actualWindow = new byte[64 * 1024];
        byte[] admittedWindow = new byte[64 * 1024];
        try
        {
            int total = 0;
            while (total < length)
            {
                int size = Math.Min(actualWindow.Length, length - total);
                await actual.ReadExactlyAsync(actualWindow.AsMemory(0, size), cancellationToken).ConfigureAwait(false);
                await admitted.ReadExactlyAsync(admittedWindow.AsMemory(0, size), cancellationToken).ConfigureAwait(false);
                if (!actualWindow.AsSpan(0, size).SequenceEqual(admittedWindow.AsSpan(0, size))) { return false; }
                total = checked(total + size);
            }
            return await actual.ReadAsync(actualWindow.AsMemory(0, 1), cancellationToken).ConfigureAwait(false) == 0
                && await admitted.ReadAsync(admittedWindow.AsMemory(0, 1), cancellationToken).ConfigureAwait(false) == 0;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(actualWindow);
            CryptographicOperations.ZeroMemory(admittedWindow);
        }
    }

    private static bool ExactIndex(NpgsqlDataReader reader, PostgreSqlRegistryIndex? expected)
    {
        if (expected is null)
        {
            for (int column = 3; column <= 10; column++) { if (!reader.IsDBNull(column)) { return false; } }
            return true;
        }
        return ExactBytes(reader, 3, expected.Deployment) && ExactBytes(reader, 4, expected.ScopeKind)
            && ExactBytes(reader, 5, expected.ScopeId) && ExactBytes(reader, 6, expected.Subject)
            && !reader.IsDBNull(7) && reader.GetInt64(7) == expected.FirstTicks
            && !reader.IsDBNull(8) && reader.GetInt16(8) == expected.Shard
            && ExactBytes(reader, 9, expected.DeploymentHash) && ExactBytes(reader, 10, expected.ScopeHash);
    }

    private static bool ExactBytes(NpgsqlDataReader reader, int ordinal, byte[] expected)
    {
        if (reader.IsDBNull(ordinal)) { return false; }
        byte[] actual = reader.GetFieldValue<byte[]>(ordinal);
        try { return actual.AsSpan().SequenceEqual(expected); }
        finally { CryptographicOperations.ZeroMemory(actual); }
    }

    private static async Task WriteAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        PostgreSqlControlMutation mutation, PostgreSqlControlDeadline deadline, CancellationToken cancellationToken)
    {
        await SetLocalTimeoutsAsync(connection, transaction, deadline, cancellationToken).ConfigureAwait(false);
        using CancellationTokenSource linked = deadline.Link(cancellationToken);
        string sql = mutation.Expected is null ? PostgreSqlControlStatements.Insert
            : mutation.Next is null ? PostgreSqlControlStatements.Delete : PostgreSqlControlStatements.Update;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.CommandTimeout = Math.Max(1, (deadline.RequireRemainingMilliseconds() + 999) / 1000);
        AddAddress(command, mutation.Address);
        using Stream? nextPayload = mutation.Next?.Payload.OpenRead();
        using Stream? expectedPayload = mutation.Expected?.Payload.OpenRead();
        if (mutation.Next is not null)
        {
            AddImage(command, mutation.Next, nextPayload!);
        }
        if (mutation.Expected is not null)
        {
            AddImage(command, mutation.Expected, expectedPayload!);
        }
        if (await command.ExecuteNonQueryAsync(linked.Token).ConfigureAwait(false) != 1)
        {
            throw new PostgreSqlControlConflictException();
        }
    }

    private static void AddAddress(NpgsqlCommand command, string address)
    {
        Add(command, NpgsqlDbType.Text, PostgreSqlControlStatements.Namespace);
        Add(command, NpgsqlDbType.Text, address);
    }

    private static void AddImage(NpgsqlCommand command, PostgreSqlControlRowImage image, Stream payload)
    {
        Add(command, NpgsqlDbType.Numeric, (decimal)image.Generation);
        Add(command, NpgsqlDbType.Text, image.OwnerFence);
        Add(command, NpgsqlDbType.Bytea, payload);
        PostgreSqlRegistryIndex? index = image.RegistryIndex;
        Add(command, NpgsqlDbType.Bytea, index?.Deployment ?? (object)DBNull.Value);
        Add(command, NpgsqlDbType.Bytea, index?.ScopeKind ?? (object)DBNull.Value);
        Add(command, NpgsqlDbType.Bytea, index?.ScopeId ?? (object)DBNull.Value);
        Add(command, NpgsqlDbType.Bytea, index?.Subject ?? (object)DBNull.Value);
        Add(command, NpgsqlDbType.Bigint, index is null ? DBNull.Value : index.FirstTicks);
        Add(command, NpgsqlDbType.Smallint, index is null ? DBNull.Value : (short)index.Shard);
        Add(command, NpgsqlDbType.Bytea, index?.DeploymentHash ?? (object)DBNull.Value);
        Add(command, NpgsqlDbType.Bytea, index?.ScopeHash ?? (object)DBNull.Value);
    }

    private static void Add(NpgsqlCommand command, NpgsqlDbType type, object value)
        => command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = type, Value = value });
}

#pragma warning restore CA2007
