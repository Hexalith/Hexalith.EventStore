namespace Hexalith.EventStore.Contracts.Events;

/// <summary>Validates the stable text identity fields used by event evolution metadata.</summary>
internal static class EventContractIdentityValidator {
    public static string ValidateContractType(string value, string parameterName) {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        if (value.Length is < 1 or > 64) {
            throw new ArgumentOutOfRangeException(parameterName, value.Length, "Event contract type must contain 1 to 64 ASCII characters.");
        }

        for (int i = 0; i < value.Length; i++) {
            char character = value[i];
            bool isAlphaNumeric = character is >= 'a' and <= 'z' or >= '0' and <= '9';
            if (!isAlphaNumeric && (character != '-' || i == 0 || i == value.Length - 1)) {
                throw new ArgumentException("Event contract type must use canonical lower-case kebab-case.", parameterName);
            }
        }

        return value;
    }

    public static int ValidatePayloadVersion(int value, string parameterName) => value is >= 1 and <= 1024
        ? value
        : throw new ArgumentOutOfRangeException(parameterName, value, "Payload version must be between 1 and 1024.");

    public static string ValidateKeyId(string value, string parameterName) => ValidateContractType(value, parameterName);
}
