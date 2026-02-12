namespace Hexalith.EventStore.DesignDirections.Models;

public enum CommandStatus
{
    Received,
    Processing,
    EventsStored,
    EventsPublished,
    Completed,
    Rejected,
    PublishFailed,
    TimedOut,
}

public record CommandEntry(
    string CorrelationId,
    string CommandType,
    string Tenant,
    string Domain,
    string AggregateId,
    CommandStatus Status,
    TimeSpan? Duration,
    DateTime Timestamp,
    string? ErrorMessage = null);

public record ServiceEntry(
    string Name,
    string Version,
    bool IsHealthy,
    TimeSpan AverageLatency);

public record TenantHealth(
    string TenantId,
    int CommandCount,
    double SuccessRate,
    TimeSpan AverageLatency,
    int DeadLetterCount);

public static class SampleData
{
    public static DateTime BaseTime { get; } = new(2026, 2, 12, 8, 42, 30);

    public static List<CommandEntry> Commands =>
    [
        new("c8f2a1b3", "TransferFunds", "acme", "banking", "xfer-0042", CommandStatus.PublishFailed, TimeSpan.FromMilliseconds(1200), BaseTime.AddMinutes(-0.25), "DAPR pub/sub timeout after 3 retries"),
        new("d9e3b2c4", "TransferFunds", "acme", "banking", "xfer-0041", CommandStatus.PublishFailed, TimeSpan.FromMilliseconds(1100), BaseTime.AddMinutes(-0.53), "DAPR pub/sub timeout after 3 retries"),
        new("a1b2c3d4", "CreateOrder", "contoso", "sales", "ord-0099", CommandStatus.Rejected, TimeSpan.FromMilliseconds(45), BaseTime.AddMinutes(-2.13), "Insufficient inventory for SKU-4421"),
        new("e5f6a7b8", "UpdateInventory", "acme", "warehouse", "inv-0155", CommandStatus.Processing, null, BaseTime.AddMinutes(-2.58)),
        new("f7a8b9c0", "PlaceOrder", "contoso", "sales", "ord-0098", CommandStatus.Completed, TimeSpan.FromMilliseconds(89), BaseTime.AddMinutes(-3.3)),
        new("b2c3d4e5", "RegisterUser", "acme", "identity", "usr-0217", CommandStatus.Completed, TimeSpan.FromMilliseconds(52), BaseTime.AddMinutes(-3.77)),
        new("c3d4e5f6", "PlaceOrder", "contoso", "sales", "ord-0097", CommandStatus.Completed, TimeSpan.FromMilliseconds(91), BaseTime.AddMinutes(-4.5)),
        new("d4e5f6a7", "TransferFunds", "acme", "banking", "xfer-0040", CommandStatus.Completed, TimeSpan.FromMilliseconds(120), BaseTime.AddMinutes(-7.17)),
        new("e5f6a7b9", "UpdateInventory", "acme", "warehouse", "inv-0154", CommandStatus.Completed, TimeSpan.FromMilliseconds(34), BaseTime.AddMinutes(-8.0)),
        new("f6a7b8c9", "RegisterUser", "acme", "identity", "usr-0216", CommandStatus.Completed, TimeSpan.FromMilliseconds(48), BaseTime.AddMinutes(-9.2)),
    ];

    public static List<ServiceEntry> Services =>
    [
        new("EventStore", "v1.2.0", true, TimeSpan.FromMilliseconds(247)),
        new("banking-service", "v2.1.0", true, TimeSpan.FromMilliseconds(18)),
        new("warehouse-service", "v1.0.3", true, TimeSpan.FromMilliseconds(12)),
        new("identity-service", "v1.1.0", true, TimeSpan.FromMilliseconds(8)),
    ];

    public static List<TenantHealth> Tenants =>
    [
        new("tenant-acme", 823, 97.2, TimeSpan.FromMilliseconds(142), 23),
        new("tenant-contoso", 424, 99.7, TimeSpan.FromMilliseconds(67), 0),
    ];

    public static int TotalCommands => 1247;
    public static int CompletedCount => 1189;
    public static int ProcessingCount => 23;
    public static int RejectedCount => 12;
    public static int FailedCount => 23;
}
