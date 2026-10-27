# Route catalog declaration candidate

`eventstore-routing-catalog.json` is an **incomplete, unactivated declaration
candidate**, retained for review and compatibility verification. Its 29 routes
contain 17 command policies, nine query declarations and three callable
projections. The canonical root is
`c156ce0b68bab43f561805db9cd02714d123026d388f65c764d7b6fa4e3b7308`.

The gateway currently forwards unknown query types to an actor fallback and
accepts caller-supplied projection and actor types. Those targets have no finite,
verifiable catalog boundary. Accordingly, both commands below exit 2 with
`$.developmentProfile: unrepresented-unbounded-query-fallback` and preserve any
existing output:

```bash
dotnet run tools/routing-catalog.cs -c Debug -p:UseHexalithProjectReferences=true -- development . deploy/dapr/eventstore-routing-catalog.json
dotnet run tools/routing-catalog.cs -c Debug -p:UseHexalithProjectReferences=true -- check-development . deploy/dapr/eventstore-routing-catalog.json
```

Generic `generate` and `validate` continue to check complete declared inputs and
canonical bytes. Validating this retained candidate proves its schema and digest;
it does not prove Development profile completeness. The existing gateway routing
and admission behavior is unchanged.

The logical Development key references are unprovisioned declarations. Story 5.12
also remains subject to the unreleased EventStore/Tenants package boundary;
package-mode CI has not passed. Story 5.13 owns catalog activation. This candidate
does not establish secret availability, production readiness or OQ8 authority.
