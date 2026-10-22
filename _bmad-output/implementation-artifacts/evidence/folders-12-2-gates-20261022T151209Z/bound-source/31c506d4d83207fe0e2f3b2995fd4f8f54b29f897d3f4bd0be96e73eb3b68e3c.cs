namespace Hexalith.EventStore.Client.Reminders;

/// <summary>The target command a due reminder intent translates into.</summary>
/// <param name="CommandType">Target command type, as registered with the gateway's trusted-effect authority rules.</param>
/// <param name="Payload">Serialized target command payload.</param>
public sealed record ReminderCommand(string CommandType, byte[] Payload);
