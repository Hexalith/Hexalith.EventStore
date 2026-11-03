# Route catalog declarations and runtime certification

`eventstore-routing-catalog.json` is the **declared Development inventory and an
unactivated runtime candidate**, retained for review and compatibility verification. Its 30 routes
contain 17 command policies, ten query declarations and three callable
projections. The canonical root is
`2579a2d99016508de5ee3102233a4ccf4cad3d951fa84b26c6766fcbf76cc43a`.

The retained candidate follows the public Tenants 7.0.0 declarations with the
EventStore 4.0.4 dependency selection. It includes the global-administrator audit
query and the owning queries' explicit platform admission metadata. Those
declarations grant no human permission, runtime authority or activation.

Declaration construction and exact-byte checking validate compiled aggregate
inventories, policies, packaged capability declarations, configuration and
declared resolver targets. Tenants query capabilities come from the packaged
`TenantCatalogCapabilities` declarations. Its existing owning-host
`Hexalith.Tenants.IntegrationTests` boundary independently verifies registered
executable query handlers in
`DomainServiceEndpointsTests.RegisteredQueryInstancesMatchEveryPackagedDeclaration`
beside missing-registration controls; generation does not inspect that executable
host or its registrations.
Both commands below succeed with the retained bytes and explicitly report that
runtime certification, secret availability and catalog activation are unverified.
Checking rejects any byte drift with `development-catalog-drift`. Generation
rejects an output path that normalizes to either configuration input with
`development-output-input-overlap`; a failed construction or check preserves
existing output and configuration.

```bash
dotnet run tools/routing-catalog.cs -c Release -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true -- development . deploy/dapr/eventstore-routing-catalog.json
dotnet run tools/routing-catalog.cs -c Release -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true -- check-development . deploy/dapr/eventstore-routing-catalog.json
```

The gateway currently forwards unknown query types to an actor fallback and
accepts caller-supplied projection and actor types. Missing handler metadata can
also change a declared handler route to that fallback. Those outcomes lack a
finite, verifiable catalog boundary. The separate command below checks the
retained compiled Development profile's completeness boundary pending 5.13. It
examines neither a running deployment nor a supplied catalog file, and it does
not measure live runtime behavior. The known compiled-profile fallback causes
exit 2 with `$.developmentProfile: unrepresented-unbounded-query-fallback`; the
command never writes a catalog:

```bash
dotnet run tools/routing-catalog.cs -c Release -p:UseHexalithProjectReferences=false -p:UseNuGetDeps=true -- check-development-runtime .
```

Generic `generate` and `validate` continue to check complete declared inputs and
canonical bytes. Validating this retained candidate proves its schema and digest;
declaration success proves the declared inventory, not exhaustive runtime parity
or readiness. The existing gateway routing and admission behavior is unchanged.

The logical Development key references are unprovisioned declarations. Story 5.12
also remains subject to its separate package and runtime qualification boundary;
package-mode CI has not passed, and Story 5.12 remains in-progress. Story 5.13 owns
runtime catalog authority, rejection of undeclared targets, exhaustive runtime
parity and activation. When handler metadata is unavailable it must preserve the
declared target or reject, and all required hosts must agree on targets and
catalog digests. Stories 2.14/2.15 consume accepted declaration/schema/codec
delivery subject to package, migration and assurance gates; this amendment does
not automatically unblock them. This candidate does not establish secret
availability, production readiness or OQ8 authority.
