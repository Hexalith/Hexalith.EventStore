// Normative authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e; sections 12.2 and 13.1.
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Declares one reader the compatibility router can execute, independently of any write mode or watermark.
/// </summary>
/// <param name="ReaderId">The exact reader identifier.</param>
/// <param name="SerializationFormat">The exact stored serialization format the reader owns.</param>
/// <param name="FormatVersion">The protected format version, or zero for an unversioned plaintext format.</param>
internal sealed record CompatibilityReaderCapability(
    string ReaderId,
    string SerializationFormat,
    int FormatVersion);
