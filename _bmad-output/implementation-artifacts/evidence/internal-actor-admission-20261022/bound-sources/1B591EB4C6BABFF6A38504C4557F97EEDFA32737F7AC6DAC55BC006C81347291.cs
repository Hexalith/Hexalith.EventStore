// <copyright file="UniqueIdHelper.cs" company="ITANEO">
// Copyright (c) ITANEO (https://www.itaneo.com). All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Hexalith.Commons.UniqueIds;

using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;

using BaUlid = ByteAether.Ulid.Ulid;

/// <summary>
/// Provides static methods for generating unique identifiers in three strategies:
/// DateTime-based (human-readable, single-machine), Base64URL GUID (distributed-safe, unsorted),
/// and ULID (sortable, distributed-safe). Also provides conversion utilities between ULID strings and Guids.
/// </summary>
public static partial class UniqueIdHelper
{
    private static readonly Lock _dateTimeLock = new();

    /// <summary>
    /// Generation options for ULID with monotonic increment to ensure within-millisecond ordering.
    /// </summary>
    private static readonly BaUlid.GenerationOptions _ulidOptions = new()
    {
        Monotonicity = BaUlid.GenerationOptions.MonotonicityOptions.MonotonicIncrement,
    };

    private static DateTime _previous = DateTime.MinValue;

    /// <summary>
    /// Extracts the creation timestamp from a ULID string.
    /// </summary>
    /// <param name="ulid">A 26-character ULID string in Crockford Base32 format.</param>
    /// <returns>A <see cref="DateTimeOffset"/> representing the ULID's creation time in UTC.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="ulid"/> is null, empty, or whitespace.</exception>
    /// <exception cref="FormatException">Thrown when <paramref name="ulid"/> is not a valid ULID string.</exception>
    /// <remarks>
    /// The ULID timestamp encodes Unix epoch milliseconds in UTC. The returned
    /// <see cref="DateTimeOffset"/> always has zero UTC offset.
    /// </remarks>
    /// <example>
    /// <code>
    /// DateTimeOffset timestamp = UniqueIdHelper.ExtractTimestamp("01HYX7QS3NP8M4KQJR5A7CVWKM");
    /// // Returns the UTC time at which the ULID was generated.
    /// </code>
    /// </example>
    public static DateTimeOffset ExtractTimestamp(string ulid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ulid);
        if (!CrockfordBase32Pattern().IsMatch(ulid))
        {
            throw new FormatException($"The value '{ulid}' is not a valid ULID string.");
        }

        return BaUlid.Parse(ulid.ToUpperInvariant(), CultureInfo.InvariantCulture).Time;
    }

    /// <summary>
    /// Generates a new unique ID based on the current UTC date/time with the format "yyyyMMddHHmmssfff".
    /// It ensures uniqueness by incrementing the time if multiple calls occur within the same millisecond.
    /// </summary>
    /// <returns>A unique 17-character ID string derived from the current date/time.</returns>
    /// <remarks>
    /// This method is thread-safe via an exclusive lock. The format is "yyyyMMddHHmmssfff",
    /// which provides human-readable, monotonically increasing IDs on a single machine.
    /// Not suitable for distributed systems — use <see cref="GenerateSortableUniqueStringId"/> instead.
    /// </remarks>
    /// <example>
    /// <code>
    /// string id = UniqueIdHelper.GenerateDateTimeId();
    /// // Returns: "20260314120530123" (17-character timestamp string)
    /// </code>
    /// </example>
    public static string GenerateDateTimeId()
    {
        using (_dateTimeLock.EnterScope())
        {
            DateTime now = DateTime.UtcNow;
            DateTime currentDateTime = new(
                now.Year,
                now.Month,
                now.Day,
                now.Hour,
                now.Minute,
                now.Second,
                now.Millisecond,
                DateTimeKind.Utc
            );
            while (currentDateTime <= _previous)
            {
                currentDateTime = currentDateTime.AddMilliseconds(1);
            }

            _previous = currentDateTime;
            return currentDateTime.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Generates a sortable unique 26-character ID string based on the ULID specification.
    /// ULIDs are chronologically sortable and distributed-safe, making them ideal for
    /// event sourcing, aggregate identifiers, and any use case requiring natural ordering.
    /// </summary>
    /// <returns>A 26-character Crockford Base32 encoded ULID string.</returns>
    /// <remarks>
    /// This method is thread-safe via monotonic increment options that guarantee
    /// within-millisecond ordering. ULIDs are suitable for event sourcing,
    /// aggregate identifiers, and any distributed system requiring natural time ordering.
    /// </remarks>
    /// <example>
    /// <code>
    /// string id = UniqueIdHelper.GenerateSortableUniqueStringId();
    /// // Returns: "01HYX7QS3NP8M4KQJR5A7CVWKM" (26-char Crockford Base32)
    /// </code>
    /// </example>
    public static string GenerateSortableUniqueStringId()
        => BaUlid.New(_ulidOptions).ToString();

    /// <summary>
    /// Generates a unique 22-character ID string derived from a GUID encoded in Base64 URL-safe format.
    /// </summary>
    /// <returns>A 22-character unique ID string.</returns>
    /// <remarks>
    /// This method is stateless and requires no locking. The character set is
    /// A-Za-z0-9, underscore, and hyphen (Base64 URL-safe). Use this method when
    /// a unique key is needed but sort order is not important.
    /// </remarks>
    /// <example>
    /// <code>
    /// string id = UniqueIdHelper.GenerateUniqueStringId();
    /// // Returns: "a1B2c3D4e5F6g7H8i9J0kL" (22-char Base64 URL-safe)
    /// </code>
    /// </example>
    public static string GenerateUniqueStringId() =>
        Convert
            .ToBase64String(Guid.NewGuid().ToByteArray())[..22]
            .Replace("/", "_", StringComparison.InvariantCulture)
            .Replace("+", "-", StringComparison.InvariantCulture);

    /// <summary>
    /// Converts a ULID string to a <see cref="Guid"/>.
    /// </summary>
    /// <param name="ulid">A 26-character Crockford Base32 ULID string.</param>
    /// <returns>A <see cref="Guid"/> preserving the ULID's 128-bit identity.</returns>
    /// <remarks>
    /// The resulting Guid preserves identity but NOT lexicographic sort order.
    /// Two ULIDs that sort correctly as strings may not sort the same way as Guids
    /// due to Guid byte-order differences.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="ulid"/> is null, empty, or whitespace.</exception>
    /// <exception cref="FormatException">Thrown when <paramref name="ulid"/> is not a valid ULID string.</exception>
    /// <example>
    /// <code>
    /// Guid guid = UniqueIdHelper.ToGuid("01HYX7QS3NP8M4KQJR5A7CVWKM");
    /// // Converts the ULID to a Guid preserving the 128-bit identity.
    /// </code>
    /// </example>
    public static Guid ToGuid(string ulid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ulid);
        if (!CrockfordBase32Pattern().IsMatch(ulid))
        {
            throw new FormatException($"The value '{ulid}' is not a valid ULID string.");
        }

        try
        {
            return BaUlid.Parse(ulid.ToUpperInvariant(), CultureInfo.InvariantCulture).ToGuid();
        }
        catch (Exception ex)
        {
            throw new FormatException($"The value '{ulid}' is not a valid ULID string.", ex);
        }
    }

    /// <summary>
    /// Converts a <see cref="Guid"/> to a 26-character ULID string.
    /// </summary>
    /// <param name="value">The Guid to convert.</param>
    /// <returns>A 26-character Crockford Base32 ULID string.</returns>
    /// <remarks>
    /// When called with a Guid not originally derived from a ULID (e.g., <see cref="Guid.NewGuid()"/>),
    /// the result is a valid ULID string but its embedded timestamp is meaningless.
    /// </remarks>
    /// <example>
    /// <code>
    /// string ulid = UniqueIdHelper.ToSortableUniqueId(myGuid);
    /// // Returns a 26-character ULID string from the Guid's 128 bits.
    /// </code>
    /// </example>
    public static string ToSortableUniqueId(Guid value)
        => BaUlid.New(value).ToString();

    [GeneratedRegex("^[0-9A-HJKMNP-TV-Z]{26}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CrockfordBase32Pattern();
}