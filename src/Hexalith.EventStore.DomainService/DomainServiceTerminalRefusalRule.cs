namespace Hexalith.EventStore.DomainService;

/// <summary>One explicit exact domain/wire-command refusal callback; never an executable route.</summary>
internal sealed record DomainServiceTerminalRefusalRule(string Domain, string CommandType,
    Func<IServiceProvider, DomainServiceTerminalRefusalContext, CancellationToken, Task<DomainServiceTerminalRefusal>> EvaluateAsync,
    BoundedV1DomainResultProducer Producer);
