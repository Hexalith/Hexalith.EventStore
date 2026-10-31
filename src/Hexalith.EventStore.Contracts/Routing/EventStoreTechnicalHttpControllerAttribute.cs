namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Opts an SDK-owned MVC controller or individual compiled action into independent technical HTTP observation; grants no domain ownership.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
public sealed class EventStoreTechnicalHttpControllerAttribute : Attribute;
