using Xunit;

// Tier 3 spins up full Aspire topologies per collection fixture
// (AspireContractTestFixture, AspireTopologyFixture, KeycloakAuthFixture,
// AspireProjectionFaultTestFixture). Each sets process-level environment
// variables (EnableKeycloak, ASPNETCORE_ENVIRONMENT, …) before building the
// DistributedApplication, so collections running in parallel race on those
// variables and one topology ends up with the wrong resources (e.g. keycloak
// missing). Serialize collections at the assembly level.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
