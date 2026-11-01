using System.Buffers.Binary;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Strictly attributes ordinary single-key writes and withholds success through observed idle commit.</summary>
/// <param name="armed">The currently selected exact-key gate.</param>
/// <param name="observe">Adds every write attempt to the complete ledger.</param>
/// <param name="update">Updates an observed execution.</param>
internal sealed class PostgresOneKeySession(Func<PostgresOneKeyFault?> armed,
    Func<string, string, string, string, string?, long> observe,
    Action<long, string> update, Func<bool>? workloadActive = null, Action<string, string>? initialization = null)
{
    // These are the pinned PostgreSQL v1 component's no-TTL SetQueryFn shapes.
    internal const string InsertSql = "INSERT INTO state AS t (key, value, isbinary, expiredate) VALUES ($1, $2, $3, NULL) ON CONFLICT (key) DO UPDATE SET value = excluded.value, isbinary = excluded.isBinary, updatedate = CURRENT_TIMESTAMP, expiredate = NULL";
    internal const string UpdateSql = "UPDATE state SET value = $2, isbinary = $3, updatedate = CURRENT_TIMESTAMP, expiredate = NULL WHERE key = $1 AND xmin = $4 AND (expiredate IS NULL OR expiredate > CURRENT_TIMESTAMP)";
    private readonly object _sync = new();
    private readonly Dictionary<string, (int Parameters, bool Write, string Sql)> _statements = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string Statement, string? Key, string? Value, string Sql)> _portals = new(StringComparer.Ordinal);
    private readonly Queue<(long Id, PostgresOneKeyFault? Fault, string Completion)> _pending = new();
    private readonly List<(long Id, PostgresOneKeyFault? Fault)> _writes = [];
    private readonly List<PostgresFrame> _withheld = [];
    private readonly Dictionary<long, PostgresOneKeyFault?> _executionFaults = [];
    private bool _error;
    private byte _status;
    private PostgresOneKeyFault? _withholding;

    /// <summary>Observes frontend frames before any complete execution frame is forwarded.</summary>
    internal async Task<long?> FrontendAsync(PostgresFrame frame, CancellationToken token)
    {
        var reader = new PostgresPayloadReader(frame.Payload);
        (long Id, PostgresOneKeyFault? Fault)? selected = null;
        lock (_sync)
        {
            switch ((char)frame.Type)
            {
                case 'P':
                    string statement = reader.String();
                    string sql = Normalize(reader.String());
                    int types = reader.Int16();
                    if (types < 0 || types > 64) throw new InvalidDataException("Unsupported PostgreSQL Parse type count.");
                    for (int index = 0; index < types; index++) _ = reader.Int32();
                    reader.End();
                    bool write = sql == Normalize(InsertSql) || sql == Normalize(UpdateSql);
                    ValidateSql(sql, write, "parse");
                    _statements[statement] = (sql == Normalize(UpdateSql) ? 4 : 3, write, sql);
                    break;
                case 'B':
                    string portal = reader.String();
                    string source = reader.String();
                    if (!_statements.TryGetValue(source, out var plan)) throw new InvalidDataException("Bind references an unknown statement.");
                    int formats = reader.Int16();
                    if (formats < 0 || formats > 64) throw new InvalidDataException("Unsupported PostgreSQL Bind formats.");
                    short[] codes = new short[formats];
                    for (int index = 0; index < formats; index++)
                    {
                        codes[index] = reader.Int16();
                        if (codes[index] is not (0 or 1)) throw new InvalidDataException("Unsupported PostgreSQL parameter format.");
                    }
                    int count = reader.Int16();
                    if (count < 0 || count > 64 || (formats != 0 && formats != 1 && formats != count)
                        || (plan.Write && count != plan.Parameters)) throw new InvalidDataException("Unsupported PostgreSQL parameter count.");
                    var values = new byte[]?[count];
                    for (int index = 0; index < count; index++)
                    {
                        int length = reader.Int32();
                        values[index] = length == -1 ? null : reader.Bytes(length);
                    }
                    int resultFormats = reader.Int16();
                    if (resultFormats < 0 || resultFormats > 64) throw new InvalidDataException("Unsupported PostgreSQL result formats.");
                    for (int index = 0; index < resultFormats; index++)
                        if (reader.Int16() is not (0 or 1)) throw new InvalidDataException("Unsupported PostgreSQL result format.");
                    reader.End();
                    string? key = null;
                    string? value = null;
                    if (plan.Write)
                    {
                        if (values.Any(bytes => bytes is null)) throw new InvalidDataException("NULL provider write parameter.");
                        short Format(int index) => formats == 0 ? (short)0 : codes[formats == 1 ? 0 : index];
                        key = PostgresPayloadReader.Decode(values[0]!);
                        byte[] json = values[1]!;
                        if (Format(1) == 1)
                        {
                            if (json.Length == 0 || json[0] != 1) throw new InvalidDataException("Unsupported binary JSONB version.");
                            json = json[1..];
                        }
                        value = PostgresPayloadReader.Decode(json);
                        using JsonDocument parsed = JsonDocument.Parse(value);
                        if (parsed.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Expected a complete provider state object.");
                        bool binaryFalse = Format(2) == 1 ? values[2]!.AsSpan().SequenceEqual(new byte[] { 0 })
                            : PostgresPayloadReader.Decode(values[2]!) is "f" or "false";
                        if (!binaryFalse) throw new InvalidDataException("Unsupported binary actor state encoding.");
                        if (count == 4)
                        {
                            if (Format(3) == 1 && values[3]!.Length != 4) throw new InvalidDataException("Invalid binary etag.");
                            if (Format(3) == 0 && !uint.TryParse(PostgresPayloadReader.Decode(values[3]!), System.Globalization.NumberStyles.None,
                                System.Globalization.CultureInfo.InvariantCulture, out _)) throw new InvalidDataException("Invalid text etag.");
                        }
                    }
                    _portals[portal] = (source, key, value, plan.Sql);
                    break;
                case 'E':
                    string executed = reader.String();
                    if (reader.Int32() != 0) throw new InvalidDataException("Partial portal execution is unsupported.");
                    reader.End();
                    if (!_portals.TryGetValue(executed, out var bound)) throw new InvalidDataException("Execute references an unknown or expired portal.");
                    ValidateSql(bound.Sql, bound.Key is not null, "execute");
                    if (_error) throw new InvalidDataException("Execute followed a provider error before ReadyForQuery.");
                    if (bound.Key is null)
                    {
                        if (_writes.Count != 0) throw new InvalidDataException("Multiple executions in a write transaction are unsupported.");
                        _pending.Enqueue((0, null, CompletionFor(bound.Sql)));
                    }
                    else
                    {
                        if (_status != (byte)'I') throw new InvalidDataException("Write started inside an explicit or failed transaction.");
                        if (_writes.Count != 0 || _pending.Count != 0) throw new InvalidDataException("Multiple executions in an implicit write transaction are unsupported.");
                        PostgresOneKeyFault? fault = armed();
                        if (fault?.Key != bound.Key) fault = null;
                        long id = observe(bound.Statement, executed, bound.Key, bound.Value!, fault?.Marker);
                        fault?.ObserveExecution();
                        selected = (id, fault);
                        _writes.Add(selected.Value);
                        _executionFaults.Add(id, fault);
                        _pending.Enqueue((id, fault, CompletionFor(bound.Sql)));
                    }
                    break;
                case 'Q':
                    string query = Normalize(reader.String());
                    reader.End();
                    ValidateSql(query, false, "query");
                    if (_writes.Count != 0) throw new InvalidDataException("Simple query interleaved with an implicit write.");
                    _pending.Enqueue((0, null, CompletionFor(query)));
                    break;
                case 'C':
                    byte kind = reader.Bytes(1)[0];
                    string closed = reader.String();
                    reader.End();
                    if (kind == (byte)'S') _statements.Remove(closed);
                    else if (kind == (byte)'P') _portals.Remove(closed);
                    else throw new InvalidDataException("Unsupported PostgreSQL Close target.");
                    break;
                case 'S':
                case 'H':
                case 'X': reader.End(); break;
                case 'D':
                    if (reader.Bytes(1)[0] is not ((byte)'S' or (byte)'P')) throw new InvalidDataException("Unsupported Describe target.");
                    _ = reader.String(); reader.End(); break;
                case 'p': break; // Authentication payload remains opaque and is never recorded.
                default: throw new InvalidDataException("Unsupported PostgreSQL frontend frame: " + (char)frame.Type);
            }
        }
        if (selected?.Fault?.Boundary == PostgresOneKeyBoundary.BeforeExecution) await selected.Value.Fault.GateAsync(token);
        return selected?.Id;
    }

    /// <summary>Records only a fully forwarded execution frame.</summary>
    internal void Forwarded(long id)
    {
        update(id, "forwarded");
        lock (_sync) _executionFaults[id]?.ObserveForwarded();
    }

    /// <summary>Relays backend messages, buffering success until idle commit has actually arrived.</summary>
    internal async Task BackendAsync(PostgresFrame frame, Stream target, CancellationToken token)
    {
        PostgresOneKeyFault? gate = null;
        PostgresFrame[]? release = null;
        bool hold;
        lock (_sync)
        {
            foreach (var write in _writes)
                if (write.Fault is not null && HasExactNotice(frame, write.Fault.Marker)) write.Fault.ObserveNotice();
            if (frame.Type == (byte)'E')
            {
                _error = true;
                foreach (var write in _writes) update(write.Id, "failed");
            }
            if (frame.Type == (byte)'I')
            {
                if (_error || frame.Payload.Length != 0 || !_pending.TryDequeue(out var empty) || empty.Id != 0 || empty.Completion != "EMPTY")
                    throw new InvalidDataException("Unexplained PostgreSQL EmptyQueryResponse.");
            }
            if (frame.Type == (byte)'C')
            {
                if (_error || !_pending.TryDequeue(out var completed))
                    throw new InvalidDataException("Unexplained or duplicate PostgreSQL CommandComplete.");
                var completedReader = new PostgresPayloadReader(frame.Payload);
                string command = completedReader.String();
                completedReader.End();
                if (completed.Completion == "EMPTY" || !Regex.IsMatch(command, "\\A(?:" + completed.Completion + ")\\z", RegexOptions.CultureInvariant))
                    throw new InvalidDataException("PostgreSQL completion does not match its exact pending execution.");
                if (completed.Id != 0)
                {
                    update(completed.Id, "command");
                    completed.Fault?.ObserveCommand();
                    if (completed.Fault?.Boundary == PostgresOneKeyBoundary.CommittedIdle)
                    {
                        if (completed.Fault.Notices != 1) throw new InvalidDataException("Missing or repeated exact trigger notice.");
                        _withholding = completed.Fault;
                    }
                }
            }
            if (frame.Type == (byte)'Z')
            {
                if (frame.Payload.Length != 1 || frame.Payload[0] is not ((byte)'I' or (byte)'T' or (byte)'E'))
                    throw new InvalidDataException("Invalid PostgreSQL ReadyForQuery.");
                if (_writes.Count != 0)
                {
                    if (_error || frame.Payload[0] != (byte)'I' || _pending.Count != 0)
                        throw new InvalidDataException("Provider write did not reach a successful implicit idle commit.");
                    foreach (var write in _writes)
                    {
                        update(write.Id, "idle");
                        write.Fault?.ObserveIdle();
                    }
                    if (_withholding is not null)
                    {
                        foreach (var write in _writes) update(write.Id, "withheld");
                        gate = _withholding;
                    }
                }
                if (_pending.Count != 0 && !_error)
                    throw new InvalidDataException("ReadyForQuery arrived without completing the pending read or schema execution.");
                if (_error && frame.Payload[0] == (byte)'T')
                    throw new InvalidDataException("Provider error cannot complete as a healthy transaction.");
                _status = frame.Payload[0];
                if (frame.Payload[0] == (byte)'I') _portals.Clear();
                _writes.Clear();
                _pending.Clear();
                _error = false;
            }
            hold = _withholding is not null;
            if (hold) _withheld.Add(frame);
            if (gate is not null)
            {
                release = _withheld.ToArray();
                _withheld.Clear();
                _withholding = null;
            }
        }
        if (gate is not null)
        {
            await gate.GateAsync(token);
            foreach (PostgresFrame buffered in release!) await buffered.WriteAsync(target, token);
        }
        else if (!hold) await frame.WriteAsync(target, token);
    }

    private static bool HasExactNotice(PostgresFrame frame, string marker)
    {
        if (frame.Type != (byte)'N') return false;
        var reader = new PostgresPayloadReader(frame.Payload);
        bool match = false;
        while (true)
        {
            byte code = reader.Bytes(1)[0];
            if (code == 0) { reader.End(); return match; }
            string value = reader.String();
            if (code == (byte)'M' && value == marker) match = true;
        }
    }

    private static string Normalize(string sql) => Regex.Replace(sql.Trim(), @"\s+", " ").ToLowerInvariant();

    private static readonly Dictionary<string, string> InitializationSql = CreateInitializationSql();
    internal const string GetSql = "SELECT key, value, isbinary, xmin AS etag, expiredate FROM state WHERE key = $1 AND (expiredate IS NULL OR expiredate >= CURRENT_TIMESTAMP)";

    private static Dictionary<string, string> CreateInitializationSql()
    {
        // Exact default-table statements from pinned components-contrib v1.18.6
        // state/postgresql/v1/migrations.go and sql/migrations/postgres/postgres_migrations.go.
        (string Sql, string Completion)[] pinned =
        [
            ("CREATE TABLE IF NOT EXISTS dapr_metadata ( key text NOT NULL PRIMARY KEY, value text NOT NULL )", "CREATE TABLE"),
            ("INSERT INTO dapr_metadata (key, value) VALUES ($1, 'lock') ON CONFLICT (key) DO NOTHING", "INSERT 0 [01]"),
            ("BEGIN", "BEGIN"), ("ROLLBACK", "ROLLBACK"),
            ("SELECT value FROM dapr_metadata WHERE key = $1 FOR UPDATE", "SELECT [0-9]+"),
            ("SELECT value FROM dapr_metadata WHERE key = 'migrations'", "SELECT [0-9]+"),
            ("INSERT INTO dapr_metadata (key, value) VALUES ('migrations', $1) ON CONFLICT (key) DO UPDATE SET value = $1", "INSERT 0 1"),
            ("CREATE TABLE IF NOT EXISTS state ( key text NOT NULL PRIMARY KEY, value jsonb NOT NULL, isbinary boolean NOT NULL, insertdate TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(), updatedate TIMESTAMP WITH TIME ZONE NULL )", "CREATE TABLE"),
            ("ALTER TABLE state ADD expiredate TIMESTAMP WITH TIME ZONE", "ALTER TABLE"),
            ("SELECT n.nspname, c.relname, format('%I.%I', n.nspname, c.relname) AS fqtn_quoted, n.nspname || '.' || c.relname AS reg_name FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace WHERE c.oid = to_regclass($1)", "SELECT [0-9]+"),
            ("SELECT EXISTS ( SELECT 1 FROM pg_attribute WHERE attrelid = to_regclass($1) AND attname = 'row_id' AND NOT attisdropped )", "SELECT [0-9]+"),
            ("ALTER TABLE public.state ADD COLUMN row_id BIGINT", "ALTER TABLE"),
            ("SELECT COUNT(*) FROM public.state WHERE row_id IS NULL", "SELECT [0-9]+"),
            ("WITH ranked AS ( SELECT key, ROW_NUMBER() OVER (ORDER BY insertdate ASC, key ASC) AS rn FROM public.state WHERE row_id IS NULL ) UPDATE public.state AS t SET row_id = r.rn FROM ranked r WHERE r.key = t.key", "UPDATE [0-9]+"),
            ("ALTER TABLE public.state ALTER COLUMN row_id SET NOT NULL", "ALTER TABLE"),
            ("SELECT EXISTS ( SELECT 1 FROM information_schema.columns WHERE table_schema = $1 AND table_name = $2 AND column_name = 'row_id' AND is_identity = 'YES' )", "SELECT [0-9]+"),
            ("ALTER TABLE public.state ALTER COLUMN row_id ADD GENERATED BY DEFAULT AS IDENTITY", "ALTER TABLE"),
            ("SELECT pg_get_serial_sequence($1, 'row_id')", "SELECT [0-9]+"),
            ("SELECT setval($1, COALESCE((SELECT MAX(row_id) FROM public.state), 0) + 1, false)", "SELECT [0-9]+"),
            ("CREATE UNIQUE INDEX IF NOT EXISTS \"state_row_id_uidx\" ON public.state (row_id)", "CREATE INDEX"),
        ];
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((string sql, string completion) in pinned)
        {
            result.Add(Normalize(sql), completion);
            // format('%I.%I') may quote identifiers; retain only the exact bound schema/table.
            if (sql.Contains("public.state", StringComparison.Ordinal))
                result.Add(Normalize(sql.Replace("public.state", "\"public\".\"state\"", StringComparison.Ordinal)), completion);
        }
        return result;
    }

    private static string CompletionFor(string sql)
    {
        if (sql == Normalize(InsertSql)) return "INSERT 0 1";
        if (sql == Normalize(UpdateSql)) return "UPDATE 1";
        if (sql == "-- ping") return "EMPTY";
        if (InitializationSql.TryGetValue(sql, out string? completion)) return completion;
        return "SELECT [0-9]+"; // Only the exact allowed workload reads can reach this point.
    }

    private void ValidateSql(string sql, bool write, string stage)
    {
        string get = Normalize(GetSql);
        bool read = sql == get || sql == get.Replace("key = $1", "key = any($1)", StringComparison.Ordinal)
            || sql is "select 1" or "select 1;" or "-- ping";
        if (workloadActive?.Invoke() == false)
        {
            if (write || (!read && !InitializationSql.ContainsKey(sql)))
                throw new InvalidDataException("Unsupported one-key initialization SQL.");
            initialization?.Invoke(stage, sql);
            return;
        }
        if (write || read) return;
        throw new InvalidDataException("Unsupported or explicit-transaction one-key workload SQL.");
    }
}
