namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Serializes the production-equivalent OQ8 PostgreSQL topology.</summary>
[CollectionDefinition("Oq8Postgresql", DisableParallelization = true)]
public sealed class Oq8PostgresqlCollection : ICollectionFixture<Oq8PostgresqlFixture>;
