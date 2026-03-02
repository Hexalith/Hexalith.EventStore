[← Back to Hexalith.EventStore](../README.md)

# FR Traceability Matrix

Every functional requirement (FR1–FR63) from the [product requirements document](../_bmad-output/planning-artifacts/prd-documentation.md) mapped to the documentation page(s) that address it. Use this document to identify coverage gaps and prioritize documentation work.

**Last reviewed:** 2026-03-02

## How to Use This Document

1. Scan the **Status** column for `GAP` or `PARTIAL` entries
2. Check the **Notes** column for which epic/phase will deliver the missing content
3. After creating or updating documentation, update this table and recalculate the summary
4. Review this document before each documentation milestone to confirm coverage

## Summary

| Metric           | Count                                             |
| ---------------- | ------------------------------------------------- |
| Total FRs        | 63                                                |
| Covered          | 39                                                |
| Partial          | 2                                                 |
| Gap              | 21                                                |
| Self-referential | 1 (FR62)                                          |
| Phase 1 coverage | 66% (42/63 covered, partial, or self-referential) |

## Traceability Matrix

### Documentation Discovery and Evaluation

| FR  | Description                                                                                              | Status    | Documentation Page(s)                                         | Notes                                                                                 |
| --- | -------------------------------------------------------------------------------------------------------- | --------- | ------------------------------------------------------------- | ------------------------------------------------------------------------------------- |
| FR1 | A .NET developer can understand what Hexalith.EventStore does within 30 seconds of landing on the README | `COVERED` | [README.md](../README.md)                                     | Hero section, programming model, architecture diagram                                 |
| FR2 | A developer can see the core programming model (pure function contract) within the first screen scroll   | `COVERED` | [README.md](../README.md)                                     | Pure function contract `(Command, CurrentState?) → List<DomainEvent>` in first scroll |
| FR3 | A developer can self-assess whether Hexalith fits their needs through a structured decision aid          | `COVERED` | [choose-the-right-tool.md](concepts/choose-the-right-tool.md) | Structured decision guide                                                             |
| FR4 | A developer can compare Hexalith's trade-offs against Marten, EventStoreDB, and custom implementations   | `COVERED` | [choose-the-right-tool.md](concepts/choose-the-right-tool.md) | Comparison table vs Marten, EventStoreDB, custom                                      |
| FR5 | A developer can see a visual demonstration of the system running before installing anything              | `COVERED` | [README.md](../README.md)                                     | Animated GIF demo (`docs/assets/quickstart-demo.gif`)                                 |
| FR6 | A developer can identify all prerequisites needed before attempting the quickstart                       | `COVERED` | [prerequisites.md](getting-started/prerequisites.md)          | Prerequisites page                                                                    |

### Getting Started and Onboarding

| FR   | Description                                                                                                                                      | Status    | Documentation Page(s)                                                                                                                       | Notes                                     |
| ---- | ------------------------------------------------------------------------------------------------------------------------------------------------ | --------- | ------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------- |
| FR7  | A developer can clone the repository and have the sample application running with events flowing on a local Docker environment within 10 minutes | `COVERED` | [quickstart.md](getting-started/quickstart.md)                                                                                              | 10-minute quickstart guide                |
| FR8  | A developer can follow step-by-step instructions to build and register a custom domain service                                                   | `COVERED` | [first-domain-service.md](getting-started/first-domain-service.md)                                                                          | Step-by-step domain service tutorial      |
| FR9  | A developer can experience an infrastructure backend swap (e.g., Redis to PostgreSQL) with zero code changes                                     | `COVERED` | [first-domain-service.md](getting-started/first-domain-service.md), `samples/dapr-components/redis/`, `samples/dapr-components/postgresql/` | Backend swap demo with YAML variants      |
| FR10 | A developer can send a test command and observe the resulting event in the event stream                                                          | `COVERED` | [quickstart.md](getting-started/quickstart.md)                                                                                              | Send command, observe event in quickstart |

### Concept Understanding

| FR   | Description                                                                                   | Status    | Documentation Page(s)                                         | Notes                                           |
| ---- | --------------------------------------------------------------------------------------------- | --------- | ------------------------------------------------------------- | ----------------------------------------------- |
| FR11 | A developer can learn the architecture topology without prior DAPR knowledge                  | `COVERED` | [architecture-overview.md](concepts/architecture-overview.md) | Architecture topology with Mermaid diagram      |
| FR12 | A developer can understand the event envelope metadata structure                              | `COVERED` | [event-envelope.md](concepts/event-envelope.md)               | Event envelope metadata structure               |
| FR13 | A developer can understand the identity scheme and how it maps to actors, streams, and topics | `COVERED` | [identity-scheme.md](concepts/identity-scheme.md)             | Identity scheme and mapping                     |
| FR14 | A developer can trace the end-to-end lifecycle of a command through the system                | `COVERED` | [command-lifecycle.md](concepts/command-lifecycle.md)         | End-to-end command lifecycle trace              |
| FR15 | A developer can understand why DAPR was chosen and what trade-offs it introduces              | `COVERED` | [choose-the-right-tool.md](concepts/choose-the-right-tool.md) | DAPR trade-offs integrated into comparison page |
| FR16 | A developer can understand when Hexalith is NOT the right choice for their project            | `COVERED` | [choose-the-right-tool.md](concepts/choose-the-right-tool.md) | "When NOT to Use Hexalith" section              |

### API and Technical Reference

| FR   | Description                                                                    | Status    | Documentation Page(s)                            | Notes                                                  |
| ---- | ------------------------------------------------------------------------------ | --------- | ------------------------------------------------ | ------------------------------------------------------ |
| FR17 | A developer can look up any REST endpoint with request/response examples       | `COVERED` | [command-api.md](reference/command-api.md)       | REST endpoint reference with request/response examples |
| FR18 | A developer can determine which NuGet package to install for their use case    | `COVERED` | [nuget-packages.md](reference/nuget-packages.md) | NuGet package guide per use case                       |
| FR19 | A developer can browse auto-generated API documentation for all public types   | `GAP`     | —                                                | Epic 15, Phase 2 (story 15-2)                          |
| FR20 | A developer can view the dependency relationships between NuGet packages       | `COVERED` | [nuget-packages.md](reference/nuget-packages.md) | NuGet dependency graph                                 |
| FR21 | A developer can access a complete configuration reference for all system knobs | `GAP`     | —                                                | Epic 15, Phase 2 (story 15-1)                          |

### Deployment and Operations

| FR   | Description                                                                                                                                                                                        | Status | Documentation Page(s) | Notes                         |
| ---- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------ | --------------------- | ----------------------------- |
| FR22 | An operator can deploy the sample application to Docker Compose on a local development machine using a documented walkthrough                                                                      | `GAP`  | —                     | Epic 14, Phase 2 (story 14-1) |
| FR23 | An operator can deploy the sample application to an on-premise Kubernetes cluster using a documented walkthrough                                                                                   | `GAP`  | —                     | Epic 14, Phase 2 (story 14-2) |
| FR24 | An operator can deploy the sample application to Azure Container Apps using a documented walkthrough                                                                                               | `GAP`  | —                     | Epic 14, Phase 2 (story 14-3) |
| FR25 | An operator can configure each DAPR component (State Store, Pub/Sub, Actors, Configuration, Resiliency) for their target infrastructure with documented examples per backend                       | `GAP`  | —                     | Epic 14, Phase 2 (story 14-5) |
| FR26 | An operator can verify system health through documented health/readiness endpoints                                                                                                                 | `GAP`  | —                     | Epic 14, Phase 2              |
| FR27 | An operator can understand the security model and configure authentication                                                                                                                         | `GAP`  | —                     | Epic 14, Phase 2 (story 14-6) |
| FR57 | A developer can understand and set up the DAPR runtime for each target environment (local Docker, Kubernetes, Azure) as a prerequisite to deploying the sample application                         | `GAP`  | —                     | Epic 14, Phase 2              |
| FR58 | A developer can understand what infrastructure differences exist between local Docker, on-premise Kubernetes, and Azure cloud deployments and why each configuration differs                       | `GAP`  | —                     | Epic 14, Phase 2              |
| FR59 | A developer who completed the local Docker quickstart can transition to a Kubernetes or Azure deployment guide with explicit references to what they already know and what's new                   | `GAP`  | —                     | Epic 14, Phase 2              |
| FR60 | An operator can understand where event data is physically stored based on their DAPR state store configuration and what persistence guarantees each backend provides                               | `GAP`  | —                     | Epic 14, Phase 2              |
| FR63 | An operator can determine resource requirements (CPU, memory, storage) and pod sizing guidance for production deployment per target environment (Docker Compose, Kubernetes, Azure Container Apps) | `GAP`  | —                     | Epic 14, Phase 2              |

### Community and Contribution

| FR   | Description                                                                                        | Status    | Documentation Page(s)                                                                                                                                                                                                      | Notes                                             |
| ---- | -------------------------------------------------------------------------------------------------- | --------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------- |
| FR28 | A developer can find and follow a contribution workflow with documented fork, branch, and PR steps | `COVERED` | [CONTRIBUTING.md](../CONTRIBUTING.md)                                                                                                                                                                                      | Contribution workflow with fork, branch, PR steps |
| FR29 | A developer can identify beginner-friendly contribution opportunities                              | `COVERED` | [CONTRIBUTING.md](../CONTRIBUTING.md)                                                                                                                                                                                      | "Good First Issues" section with label strategy   |
| FR30 | A developer can file structured bug reports, feature requests, and documentation improvements      | `COVERED` | [01-bug-report.yml](../.github/ISSUE_TEMPLATE/01-bug-report.yml), [02-feature-request.yml](../.github/ISSUE_TEMPLATE/02-feature-request.yml), [03-docs-improvement.yml](../.github/ISSUE_TEMPLATE/03-docs-improvement.yml) | Three structured issue templates                  |
| FR31 | A developer can submit pull requests following a documented template and checklist                 | `COVERED` | [PULL_REQUEST_TEMPLATE.md](../.github/PULL_REQUEST_TEMPLATE.md)                                                                                                                                                            | PR template with checklist                        |
| FR32 | A developer can participate in community discussions organized by category                         | `COVERED` | [ideas.yml](../.github/DISCUSSION_TEMPLATE/ideas.yml), [q-a.yml](../.github/DISCUSSION_TEMPLATE/q-a.yml)                                                                                                                   | GitHub Discussions with Ideas and Q&A categories  |
| FR33 | A developer can view the public product roadmap                                                    | `GAP`     | —                                                                                                                                                                                                                          | Epic 15, Phase 2 (story 15-5)                     |

### Content Quality and Maintenance

| FR   | Description                                                                                                                                     | Status    | Documentation Page(s)                                                                                  | Notes                                                             |
| ---- | ----------------------------------------------------------------------------------------------------------------------------------------------- | --------- | ------------------------------------------------------------------------------------------------------ | ----------------------------------------------------------------- |
| FR34 | A CI pipeline can validate that all code examples in documentation compile and run                                                              | `COVERED` | [docs-validation.yml](../.github/workflows/docs-validation.yml)                                        | CI validates code examples via sample build/test                  |
| FR35 | A CI pipeline can detect broken links across all documentation pages                                                                            | `COVERED` | [docs-validation.yml](../.github/workflows/docs-validation.yml)                                        | CI detects broken links via lychee                                |
| FR36 | A CI pipeline can enforce markdown formatting standards                                                                                         | `COVERED` | [docs-validation.yml](../.github/workflows/docs-validation.yml)                                        | CI enforces markdown formatting via markdownlint-cli2             |
| FR37 | Documentation maintainers can identify stale content through automated checks                                                                   | `PARTIAL` | [docs-validation.yml](../.github/workflows/docs-validation.yml)                                        | Stale content detection CI configured but limited scope           |
| FR38 | A documentation reviewer can verify changes through the same PR process as code                                                                 | `COVERED` | [CONTRIBUTING.md](../CONTRIBUTING.md), [PULL_REQUEST_TEMPLATE.md](../.github/PULL_REQUEST_TEMPLATE.md) | PR review process documented                                      |
| FR61 | A documentation contributor can run the full validation suite (code compilation, link checking, markdown linting) locally with a single command | `COVERED` | [CONTRIBUTING.md](../CONTRIBUTING.md), `scripts/validate-docs.sh`, `scripts/validate-docs.ps1`         | "Run Docs Validation Locally" section with cross-platform scripts |
| FR62 | A maintainer can verify that every functional requirement has at least one corresponding documentation page through a traceability check        | `COVERED` | [fr-traceability.md](fr-traceability.md)                                                               | Self-referential: this document is the traceability check         |

### SEO and Discoverability

| FR   | Description                                                                                                 | Status    | Documentation Page(s)                                            | Notes                                                      |
| ---- | ----------------------------------------------------------------------------------------------------------- | --------- | ---------------------------------------------------------------- | ---------------------------------------------------------- |
| FR39 | The README can be discovered through GitHub search for key terms (event sourcing, .NET, DAPR, multi-tenant) | `COVERED` | [README.md](../README.md)                                        | GitHub search keywords in title, description, topics       |
| FR40 | Documentation pages can be indexed by search engines with descriptive URLs and structured headings          | `COVERED` | All `docs/**/*.md` pages                                         | Descriptive URLs via folder structure, structured headings |
| FR41 | A developer browsing event sourcing resources can discover Hexalith through the curated ecosystem page      | `COVERED` | [awesome-event-sourcing.md](community/awesome-event-sourcing.md) | Curated ecosystem page                                     |
| FR42 | A developer can navigate between related documentation pages through cross-linking                          | `COVERED` | All `docs/**/*.md` pages                                         | Cross-linking via "Next Steps" sections on every page      |

### Documentation Navigation and Structure

| FR   | Description                                                                                                  | Status    | Documentation Page(s)                               | Notes                                                                                      |
| ---- | ------------------------------------------------------------------------------------------------------------ | --------- | --------------------------------------------------- | ------------------------------------------------------------------------------------------ |
| FR43 | A developer can enter the documentation at any page and orient themselves without reading prerequisite pages | `COVERED` | All `docs/**/*.md` pages                            | Self-contained pages with summary and context                                              |
| FR44 | A developer can navigate a progressive complexity path from simple concepts to advanced patterns             | `COVERED` | [README.md](../README.md), navigation in docs pages | Progressive path: README, quickstart, concepts, reference                                  |
| FR45 | A developer can access architecture documentation directly from the README as a parallel entry point         | `COVERED` | [README.md](../README.md)                           | Architecture link in README as parallel entry point                                        |
| FR46 | A developer can identify their current position in the documentation structure                               | `PARTIAL` | All `docs/**/*.md` pages                            | Position hints via "Prerequisites" and "Next Steps" sections; no global navigation sidebar |

### Troubleshooting and Error Handling

| FR   | Description                                                                                                                                                                                                                       | Status | Documentation Page(s) | Notes                         |
| ---- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------ | --------------------- | ----------------------------- |
| FR47 | A developer can find troubleshooting guidance for quickstart errors including: Docker not running, port conflicts, DAPR sidecar timeout, .NET SDK version mismatch, and sample build failure                                      | `GAP`  | —                     | Epic 14, Phase 2 (story 14-7) |
| FR48 | A developer can find documented solutions for DAPR integration issues including: sidecar injection failure, state store connection timeout, pub/sub message loss, actor activation conflict, and component configuration mismatch | `GAP`  | —                     | Epic 14, Phase 2 (story 14-7) |
| FR49 | A developer can access troubleshooting information for deployment failures per target environment                                                                                                                                 | `GAP`  | —                     | Epic 14, Phase 2 (story 14-7) |

### Lifecycle and Versioning

| FR   | Description                                                                                                                                                                                                    | Status    | Documentation Page(s)                                | Notes                                               |
| ---- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------- | ---------------------------------------------------- | --------------------------------------------------- |
| FR50 | A developer can view a changelog of breaking changes and migration steps between releases                                                                                                                      | `COVERED` | [CHANGELOG.md](../CHANGELOG.md)                      | Changelog with breaking changes and migration steps |
| FR51 | A developer can understand how event versioning and schema evolution are handled                                                                                                                               | `GAP`     | —                                                    | Epic 15, Phase 2 (story 15-3)                       |
| FR52 | A developer can follow a documented upgrade path when moving between major versions                                                                                                                            | `GAP`     | —                                                    | Epic 15, Phase 2 (story 15-4)                       |
| FR53 | A developer can set up a local development environment matching the documented configuration                                                                                                                   | `COVERED` | [prerequisites.md](getting-started/prerequisites.md) | Local dev environment setup                         |
| FR54 | A developer can identify the library version documented by a documentation page via a version reference in the README linking to the corresponding release tag                                                 | `COVERED` | [README.md](../README.md)                            | Version reference linking to release tag            |
| FR55 | An operator can follow a documented disaster recovery procedure for the event store                                                                                                                            | `GAP`     | —                                                    | Epic 14, Phase 2 (story 14-8)                       |
| FR56 | A developer can follow a documented progression from the local Docker sample to on-premise Kubernetes to Azure cloud deployment using the same application code with only infrastructure configuration changes | `GAP`     | —                                                    | Epic 14, Phase 2 (story 14-4)                       |

## Gap Analysis by Phase

### Phase 2 — Epic 14: Deployment and Operations (16 gaps)

| FR   | Description                                 | Target Story      |
| ---- | ------------------------------------------- | ----------------- |
| FR22 | Docker Compose deployment walkthrough       | 14-1              |
| FR23 | Kubernetes deployment walkthrough           | 14-2              |
| FR24 | Azure Container Apps deployment walkthrough | 14-3              |
| FR25 | DAPR component configuration reference      | 14-5              |
| FR26 | Health/readiness endpoint documentation     | 14-6              |
| FR27 | Security model documentation                | 14-6              |
| FR47 | Quickstart troubleshooting                  | 14-7              |
| FR48 | DAPR integration troubleshooting            | 14-7              |
| FR49 | Deployment failure troubleshooting          | 14-7              |
| FR55 | Disaster recovery procedure                 | 14-8              |
| FR56 | Deployment progression guide                | 14-4              |
| FR57 | DAPR runtime setup per environment          | 14-1 through 14-3 |
| FR58 | Infrastructure differences documentation    | 14-4              |
| FR59 | Quickstart-to-deployment transition         | 14-4              |
| FR60 | Event data storage per backend              | 14-5              |
| FR63 | Resource sizing guidance                    | 14-1 through 14-3 |

### Phase 2 — Epic 15: Configuration, Versioning and Lifecycle (5 gaps)

| FR   | Description                           | Target Story |
| ---- | ------------------------------------- | ------------ |
| FR19 | Auto-generated API documentation      | 15-2         |
| FR21 | Configuration reference               | 15-1         |
| FR33 | Public product roadmap                | 15-5         |
| FR51 | Event versioning and schema evolution | 15-3         |
| FR52 | Upgrade path documentation            | 15-4         |

### Partial Coverage (2 items)

| FR   | Description                         | Gap Detail                                                                              |
| ---- | ----------------------------------- | --------------------------------------------------------------------------------------- |
| FR37 | Stale content detection             | CI configured but limited scope; deeper checks deferred                                 |
| FR46 | Position in documentation structure | Position hints via "Prerequisites" and "Next Steps" exist; no global navigation sidebar |

## Next Steps

- **Next:** Review gap analysis to prioritize Phase 2 documentation work
- **Related:** [Product Requirements Document](../_bmad-output/planning-artifacts/prd-documentation.md), [CONTRIBUTING.md](../CONTRIBUTING.md)
