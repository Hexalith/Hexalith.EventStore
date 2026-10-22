namespace Hexalith.EventStore.DomainService;

/// <summary>Signals that bounded synchronous-read workers are exhausted.</summary>
internal sealed class ProcessReadAdmissionException() : InvalidOperationException("Process read worker admission is exhausted.");
