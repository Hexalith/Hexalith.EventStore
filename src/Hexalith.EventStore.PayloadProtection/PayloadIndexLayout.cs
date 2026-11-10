// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base section 10.2; owner decision D1 (2026-11-10).
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Binds one closed index schema's exact addresses, fields and record constructors to the shared placement rules.</summary>
/// <typeparam name="THead">The closed head schema.</typeparam>
/// <typeparam name="TPage">The closed page schema.</typeparam>
/// <typeparam name="TEntry">The closed entry schema.</typeparam>
internal sealed class PayloadIndexLayout<THead, TPage, TEntry>
    where THead : class
    where TPage : class
    where TEntry : class
{
    /// <summary>Gets the exact head address.</summary>
    internal required string HeadKey { get; init; }

    /// <summary>Gets the exact page address for a page number.</summary>
    internal required Func<uint, string> PageKey { get; init; }

    /// <summary>Gets whether a head carries this index's exact address fields.</summary>
    internal required Func<THead, bool> OwnsHead { get; init; }

    /// <summary>Gets the head's generation, page count and entry count.</summary>
    internal required Func<THead, (ulong Generation, uint PageCount, ulong EntryCount)> Head { get; init; }

    /// <summary>Gets whether a page carries this index's exact address fields and the given page number.</summary>
    internal required Func<TPage, uint, bool> OwnsPage { get; init; }

    /// <summary>Gets the generation of the head transaction that last wrote a page, and its entries.</summary>
    internal required Func<TPage, (ulong Generation, TEntry[] Entries)> Page { get; init; }

    /// <summary>Gets the codec identity that orders an entry within a page and seeds its placement hash.</summary>
    internal required Func<TEntry, string> Identity { get; init; }

    /// <summary>Gets the maximum entry count the head schema can record.</summary>
    internal required ulong MaximumEntries { get; init; }

    /// <summary>Creates a head record from its generation, page count, entry count and timestamp.</summary>
    internal required Func<ulong, uint, ulong, string, THead> CreateHead { get; init; }

    /// <summary>Creates a page record from its generation, page number and sorted entries.</summary>
    internal required Func<ulong, uint, TEntry[], TPage> CreatePage { get; init; }

    /// <summary>Formats no index address.</summary>
    public override string ToString() => nameof(PayloadIndexLayout<THead, TPage, TEntry>);
}
