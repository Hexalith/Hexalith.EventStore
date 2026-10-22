using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Contracts.Reminders;

/// <summary>
/// Version one of the AD-11/AD-25 reminder identity encoding. Actor identifiers and schedule tokens are
/// uppercase Crockford Base32 SHA-256 digests over length-prefixed canonical tuples, using the same text and
/// integer rules as <see cref="EffectIdentityCodec"/>: text is NFC UTF-8 preceded by a four-byte big-endian
/// length, and integers are signed eight-byte big-endian.
/// </summary>
public static class ReminderIdentityCodec
{
    /// <summary>Gets the immutable reminder codec version.</summary>
    public const int Version = 1;

    /// <summary>Gets the prefix of every reminder actor identifier.</summary>
    public const string ActorIdPrefix = "wra-";

    /// <summary>Gets the prefix of every schedule token.</summary>
    public const string ScheduleTokenPrefix = "wrs-";

    /// <summary>Gets the reminder-name prefix of the <see cref="EffectKindCatalog.DateResume"/> kind.</summary>
    public const string DateReminderNamePrefix = "date-";

    /// <summary>Gets the reminder-name prefix of the <see cref="EffectKindCatalog.Expiry"/> kind.</summary>
    public const string ExpiryReminderNamePrefix = "expiry-";

    private const string ActorTupleTag = "reminder-actor";
    private const string ScheduleTupleTag = "schedule";
    private const int DigestLength = 52;
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    /// <summary>Returns whether version one maps the kind to a reminder name.</summary>
    /// <param name="kind">The candidate effect kind.</param>
    /// <returns><see langword="true"/> for <see cref="EffectKindCatalog.DateResume"/> and <see cref="EffectKindCatalog.Expiry"/> only.</returns>
    public static bool IsSupportedKind(string? kind)
        => kind is EffectKindCatalog.DateResume or EffectKindCatalog.Expiry;

    /// <summary>Gets the reminder-name prefix from the closed version-one kind map.</summary>
    /// <param name="kind">A supported reminder kind.</param>
    /// <returns><c>date-</c> or <c>expiry-</c>.</returns>
    /// <exception cref="ArgumentException">The kind is not a version-one reminder kind.</exception>
    public static string GetReminderNamePrefix(string kind)
        => kind switch
        {
            EffectKindCatalog.DateResume => DateReminderNamePrefix,
            EffectKindCatalog.Expiry => ExpiryReminderNamePrefix,
            _ => throw new ArgumentException("The kind is not a version-one reminder kind.", nameof(kind)),
        };

    /// <summary>Encodes the actor tuple <c>(reminder-actor, tenant, item)</c>.</summary>
    /// <param name="tenant">Canonical lowercase tenant.</param>
    /// <param name="item">Reminder item, which is the target aggregate identifier.</param>
    /// <returns>The canonical tuple bytes.</returns>
    public static byte[] EncodeActorTuple(string tenant, string item)
    {
        ValidateTenantAndItem(tenant, item);
        var buffer = new ArrayBufferWriter<byte>();
        WriteText(buffer, ActorTupleTag);
        WriteText(buffer, tenant);
        WriteText(buffer, item);
        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>Encodes the schedule tuple <c>(schedule, tenant, item, due UTC ticks, revision)</c>.</summary>
    /// <param name="tenant">Canonical lowercase tenant.</param>
    /// <param name="item">Reminder item, which is the target aggregate identifier.</param>
    /// <param name="dueUtc">Due instant with a zero UTC offset.</param>
    /// <param name="scheduleRevision">Nonnegative domain schedule revision.</param>
    /// <returns>The canonical tuple bytes.</returns>
    public static byte[] EncodeScheduleTuple(string tenant, string item, DateTimeOffset dueUtc, long scheduleRevision)
    {
        ValidateTenantAndItem(tenant, item);
        ValidateSchedule(dueUtc, scheduleRevision);
        var buffer = new ArrayBufferWriter<byte>();
        WriteText(buffer, ScheduleTupleTag);
        WriteText(buffer, tenant);
        WriteText(buffer, item);
        WriteLong(buffer, dueUtc.UtcTicks);
        WriteLong(buffer, scheduleRevision);
        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>Computes the <c>wra-&lt;digest&gt;</c> actor identifier of one tenant item.</summary>
    /// <param name="tenant">Canonical lowercase tenant.</param>
    /// <param name="item">Reminder item, which is the target aggregate identifier.</param>
    /// <returns>The 56-character actor identifier.</returns>
    public static string ComputeActorId(string tenant, string item)
        => ActorIdPrefix + EffectIdentityCodec.RenderDigest(SHA256.HashData(EncodeActorTuple(tenant, item)));

    /// <summary>Computes the <c>wrs-&lt;digest&gt;</c> schedule token of one schedule witness.</summary>
    /// <param name="tenant">Canonical lowercase tenant.</param>
    /// <param name="item">Reminder item, which is the target aggregate identifier.</param>
    /// <param name="dueUtc">Due instant with a zero UTC offset.</param>
    /// <param name="scheduleRevision">Nonnegative domain schedule revision.</param>
    /// <returns>The 56-character schedule token.</returns>
    public static string ComputeScheduleToken(string tenant, string item, DateTimeOffset dueUtc, long scheduleRevision)
        => ScheduleTokenPrefix + EffectIdentityCodec.RenderDigest(
            SHA256.HashData(EncodeScheduleTuple(tenant, item, dueUtc, scheduleRevision)));

    /// <summary>Computes the <c>date-&lt;token&gt;</c> or <c>expiry-&lt;token&gt;</c> reminder name.</summary>
    /// <param name="kind">A supported reminder kind.</param>
    /// <param name="scheduleToken">A <c>wrs-</c> schedule token.</param>
    /// <returns>The reminder name.</returns>
    public static string ComputeReminderName(string kind, string scheduleToken)
    {
        string prefix = GetReminderNamePrefix(kind);
        if (!IsScheduleToken(scheduleToken))
        {
            throw new ArgumentException("The schedule token is not a version-one wrs token.", nameof(scheduleToken));
        }

        return prefix + scheduleToken;
    }

    /// <summary>Computes the actor identifier of an intent's item.</summary>
    /// <param name="intent">The reminder intent.</param>
    /// <returns>The <c>wra-</c> actor identifier.</returns>
    public static string ComputeActorId(ReminderIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        return ComputeActorId(intent.Tenant, intent.TargetAggregate);
    }

    /// <summary>Computes the schedule token of an intent.</summary>
    /// <param name="intent">The reminder intent.</param>
    /// <returns>The <c>wrs-</c> schedule token.</returns>
    public static string ComputeScheduleToken(ReminderIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        return ComputeScheduleToken(intent.Tenant, intent.TargetAggregate, intent.DueUtc, intent.ScheduleRevision);
    }

    /// <summary>Computes the reminder name of an intent.</summary>
    /// <param name="intent">The reminder intent.</param>
    /// <returns>The <c>date-</c> or <c>expiry-</c> reminder name.</returns>
    public static string ComputeReminderName(ReminderIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        return ComputeReminderName(intent.Kind, ComputeScheduleToken(intent));
    }

    /// <summary>Parses a reminder name into its kind and schedule token without trusting either.</summary>
    /// <param name="reminderName">The reminder name received from the scheduler.</param>
    /// <param name="kind">The kind mapped from the name prefix.</param>
    /// <param name="scheduleToken">The <c>wrs-</c> token carried by the name.</param>
    /// <returns><see langword="true"/> when the name has a known prefix and a well-formed token.</returns>
    public static bool TryParseReminderName(string? reminderName, out string kind, out string scheduleToken)
    {
        kind = string.Empty;
        scheduleToken = string.Empty;
        if (reminderName is null)
        {
            return false;
        }

        string candidateKind;
        string token;
        if (reminderName.StartsWith(DateReminderNamePrefix, StringComparison.Ordinal))
        {
            candidateKind = EffectKindCatalog.DateResume;
            token = reminderName[DateReminderNamePrefix.Length..];
        }
        else if (reminderName.StartsWith(ExpiryReminderNamePrefix, StringComparison.Ordinal))
        {
            candidateKind = EffectKindCatalog.Expiry;
            token = reminderName[ExpiryReminderNamePrefix.Length..];
        }
        else
        {
            return false;
        }

        if (!IsScheduleToken(token))
        {
            return false;
        }

        kind = candidateKind;
        scheduleToken = token;
        return true;
    }

    /// <summary>
    /// Returns whether a stored full tuple re-derives both the actor identifier and the reminder name. A
    /// mismatch is a tuple collision or corrupted evidence and must be quarantined, never executed.
    /// </summary>
    /// <param name="tenant">Stored tenant.</param>
    /// <param name="item">Stored item, which is the target aggregate identifier.</param>
    /// <param name="kind">Stored reminder kind.</param>
    /// <param name="dueUtc">Stored due instant.</param>
    /// <param name="scheduleRevision">Stored schedule revision.</param>
    /// <param name="actorId">Actor identifier that received the callback.</param>
    /// <param name="reminderName">Reminder name that fired.</param>
    /// <returns><see langword="true"/> only when every derived value matches ordinally.</returns>
    public static bool Rederives(
        string tenant,
        string item,
        string kind,
        DateTimeOffset dueUtc,
        long scheduleRevision,
        string actorId,
        string reminderName)
    {
        try
        {
            return string.Equals(ComputeActorId(tenant, item), actorId, StringComparison.Ordinal)
                && string.Equals(
                    ComputeReminderName(kind, ComputeScheduleToken(tenant, item, dueUtc, scheduleRevision)),
                    reminderName,
                    StringComparison.Ordinal);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool IsScheduleToken(string? token)
    {
        if (token is null
            || token.Length != ScheduleTokenPrefix.Length + DigestLength
            || !token.StartsWith(ScheduleTokenPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        foreach (char character in token.AsSpan(ScheduleTokenPrefix.Length))
        {
            if (!Alphabet.Contains(character, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static void ValidateTenantAndItem(string tenant, string item)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(item);

        // AggregateIdentity owns the canonical character rules; it folds tenant case, so a changed value
        // proves the caller supplied a non-canonical tenant, which is refused rather than transformed.
        var identity = new AggregateIdentity(tenant, "reminders", item);
        if (!string.Equals(identity.TenantId, tenant, StringComparison.Ordinal))
        {
            throw new ArgumentException("Reminder tenant must be canonical lowercase.", nameof(tenant));
        }
    }

    private static void ValidateSchedule(DateTimeOffset dueUtc, long scheduleRevision)
    {
        if (dueUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Reminder due instant must carry a zero UTC offset.", nameof(dueUtc));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(scheduleRevision);
    }

    private static void WriteText(ArrayBufferWriter<byte> buffer, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!string.Equals(value, value.Normalize(NormalizationForm.FormC), StringComparison.Ordinal))
        {
            throw new ArgumentException("Reminder identity text must be canonical NFC.", nameof(value));
        }

        byte[] bytes = new UTF8Encoding(false, true).GetBytes(value);
        Span<byte> target = buffer.GetSpan(sizeof(int) + bytes.Length);
        BinaryPrimitives.WriteInt32BigEndian(target, bytes.Length);
        bytes.CopyTo(target[sizeof(int)..]);
        buffer.Advance(sizeof(int) + bytes.Length);
    }

    private static void WriteLong(ArrayBufferWriter<byte> buffer, long value)
    {
        Span<byte> target = buffer.GetSpan(sizeof(long));
        BinaryPrimitives.WriteInt64BigEndian(target, value);
        buffer.Advance(sizeof(long));
    }
}
