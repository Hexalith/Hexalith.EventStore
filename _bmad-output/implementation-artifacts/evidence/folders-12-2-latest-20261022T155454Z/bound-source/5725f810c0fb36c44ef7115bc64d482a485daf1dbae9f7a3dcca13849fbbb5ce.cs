namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Marks a serialized property as personal data that the payload-protection policy must protect.
/// Implements Story 8.1 section 9 under normative digest
/// <c>de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class PersonalDataAttribute : Attribute {
}
