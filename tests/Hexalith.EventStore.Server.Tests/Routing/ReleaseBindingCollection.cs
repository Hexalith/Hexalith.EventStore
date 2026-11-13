namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Isolates complete real-store allocation measurements from unrelated parallel test work.</summary>
[CollectionDefinition("ReleaseBinding", DisableParallelization = true)]
public sealed class ReleaseBindingCollection;
