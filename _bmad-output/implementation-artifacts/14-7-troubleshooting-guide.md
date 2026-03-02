# Story 14.7: Troubleshooting Guide

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer encountering errors,
I want a troubleshooting guide covering quickstart, DAPR integration, and deployment issues,
so that I can resolve problems without filing an issue.

## Acceptance Criteria

1. `docs/guides/troubleshooting.md` covers quickstart errors: Docker not running, port conflicts, DAPR sidecar timeout, .NET SDK version mismatch, sample build failure (FR47)
2. The page covers DAPR integration issues: sidecar injection failure, state store connection timeout, pub/sub message loss, actor activation conflict, component configuration mismatch (FR48)
3. The page covers deployment failures per target environment: Docker Compose, Kubernetes, Azure Container Apps (FR49)
4. Each issue includes: symptom description, probable cause, and step-by-step resolution
5. The page follows the standard page template (back-link `[<- Back to Hexalith.EventStore](../../README.md)`, H1, intro paragraph, prerequisites blockquote, content sections, next steps)
6. The page is self-contained — a developer arriving from search can understand the troubleshooting guidance without reading prerequisite pages (FR43); DAPR terms defined on first use
7. All internal links resolve to existing files
8. markdownlint-cli2 validation passes with zero errors

## Tasks / Subtasks

- [x] Task 1: Create `docs/guides/troubleshooting.md` (AC: all)
  - [x] 1.1 Write page header: back-link `[<- Back to Hexalith.EventStore](../../README.md)`, H1 title "Troubleshooting Guide", intro paragraph explaining this page covers common errors during quickstart, DAPR integration, and deployment — organized by symptom for quick lookup, and prerequisites blockquote linking to `../getting-started/prerequisites.md` (AC: #5, #6)
  - [x] 1.2 Write "Quickstart Errors" section (AC: #1, #4):
    - Document Docker daemon not running: symptom (connection refused errors), probable cause (Docker Desktop not started / WSL2 issues), step-by-step resolution (check docker info, restart Docker service, WSL2 fix commands)
    - Document port conflicts: symptom (address already in use), probable cause (Aspire default ports occupied), resolution (identify process with netstat/lsof, kill process or change ports in AppHost)
    - Document DAPR sidecar timeout: symptom (application starts but commands fail with 500), probable cause (DAPR not initialized, components not loaded), resolution (verify `dapr init` / `dapr init --slim`, check `~/.dapr/components/`, verify sidecar logs)
    - Document .NET SDK version mismatch: symptom (build errors or runtime failures), probable cause (global.json requires .NET 10 SDK 10.0.102), resolution (check `dotnet --list-sdks`, install correct SDK, verify global.json)
    - Document sample build failure: symptom (restore/build errors), probable cause (missing NuGet restore, outdated packages), resolution (`dotnet restore Hexalith.EventStore.slnx`, clear NuGet cache, check Directory.Packages.props)
  - [x] 1.3 Write "DAPR Integration Issues" section (AC: #2, #4):
    - Document sidecar injection failure: symptom (DAPR sidecar not starting alongside application), probable cause (DAPR not installed, Aspire not configured for DAPR), resolution (verify DAPR CLI installed, check Aspire DAPR configuration in AppHost, verify component YAML files)
    - Document state store connection timeout: symptom (state store operations time out or return errors), probable cause (Redis/PostgreSQL not running, wrong connection string, component YAML misconfigured), resolution (verify backend is running, test connectivity, check component YAML `spec.metadata` values)
    - Document pub/sub message loss: symptom (events published but subscribers never receive), probable cause (topic name mismatch, subscriber not registered, component scoping restricts access), resolution (verify topic naming `{tenant}.{domain}.events`, check subscription registration, verify component scopes)
    - Document actor activation conflict: symptom (actor calls fail with concurrency errors), probable cause (single-writer violation, actor reentrancy disabled by design), resolution (understand single-writer model, check for concurrent calls to same aggregate, verify DAPR actor configuration)
    - Document component configuration mismatch: symptom (DAPR logs show component load errors), probable cause (YAML syntax errors, missing secrets, wrong component type), resolution (validate YAML syntax, check DAPR component documentation, verify all secret references resolve, consult `docs/guides/dapr-component-reference.md`)
  - [x] 1.4 Write "Docker Compose Deployment Issues" section (AC: #3, #4):
    - Document health check failures: symptom (containers restart repeatedly), probable cause (health check endpoint not ready, dependencies not started), resolution (check container logs, verify health/readiness endpoint paths, adjust `healthcheck.interval`)
    - Document volume mount issues: symptom (data not persisted between restarts), probable cause (incorrect volume paths, permission issues), resolution (verify volume mounts in docker-compose.yml, check file permissions)
    - Document network connectivity issues: symptom (services cannot reach each other), probable cause (Docker network configuration, DNS resolution within compose), resolution (verify network definitions, use service names not localhost, check `docker network ls`)
    - Document Keycloak startup issues: symptom (authentication fails in local environment), probable cause (Keycloak not initialized, realm not imported, port 8180 not exposed), resolution (verify Keycloak container health, check realm import, verify port mapping)
  - [x] 1.5 Write "Kubernetes Deployment Issues" section (AC: #3, #4):
    - Document pod crash loops: symptom (`CrashLoopBackOff` or `1/2` ready), probable cause (DAPR sidecar injection issues, missing ConfigMaps/Secrets, image pull failures), resolution (check `kubectl describe pod`, verify DAPR annotations, check sidecar logs with `kubectl logs <pod> -c daprd`)
    - Document image pull errors: symptom (`ImagePullBackOff`), probable cause (private registry auth, image tag mismatch), resolution (verify image tag, check `imagePullSecrets`, test registry access)
    - Document ConfigMap/Secret configuration: symptom (application starts but fails at runtime), probable cause (missing environment variables, wrong Secret references), resolution (verify ConfigMap data, check Secret existence with `kubectl get secret`, validate YAML references)
    - Document DAPR operator installation issues: symptom (DAPR annotations ignored, sidecars not injected), probable cause (DAPR not installed on cluster, wrong namespace), resolution (verify `dapr status -k`, install/upgrade DAPR on cluster, check namespace annotations)
    - Document service mesh conflicts: symptom (intermittent connection failures between services), probable cause (Istio/Linkerd conflicting with DAPR mTLS), resolution (configure sidecar ordering, exclude DAPR ports from mesh, consult DAPR + mesh documentation)
  - [x] 1.6 Write "Azure Container Apps Deployment Issues" section (AC: #3, #4):
    - Document managed identity issues: symptom (access denied to Azure resources), probable cause (managed identity not assigned, role assignments missing), resolution (verify identity assignment with `az containerapp identity show`, check role assignments, verify scope)
    - Document Key Vault access issues: symptom (secrets not loading, 403 errors), probable cause (missing Key Vault access policy or RBAC role), resolution (verify access policy or RBAC assignment for managed identity, check Key Vault firewall settings)
    - Document ACR authentication: symptom (image pull failures in ACA), probable cause (system-assigned identity needs AcrPull role), resolution (assign AcrPull role to managed identity, verify with `az role assignment list`)
    - Document environment variable injection: symptom (configuration values missing at runtime), probable cause (ACA secrets/env vars not configured, references incorrect), resolution (verify secrets with `az containerapp secret list`, check environment variable references in revision template)
    - Document DAPR managed services: symptom (DAPR components not available), probable cause (DAPR not enabled on ACA environment, component scoping misconfigured), resolution (verify DAPR is enabled with `az containerapp env dapr-component list`, check component scoping matches app name)
  - [x] 1.7 Write "Diagnostic Commands Reference" section (AC: #4, #6):
    - Include a quick-reference table of diagnostic commands organized by area (Docker, DAPR, .NET, Kubernetes, Azure)
    - Include log collection commands for each environment
    - Include correlation ID tracing guidance: how to find a command's trace through structured logs and OpenTelemetry spans
    - Include dead-letter topic inspection commands per environment
  - [x] 1.8 Write "Next Steps" section: Links to:
    - [Security Model](security-model.md) — security configuration troubleshooting
    - [DAPR Component Configuration Reference](dapr-component-reference.md) — component configuration details
    - [Deployment Progression Guide](deployment-progression.md) — environment comparison
    - Deployment guides per environment (Docker Compose, Kubernetes, Azure Container Apps)
    - [Prerequisites](../getting-started/prerequisites.md) — verify installation requirements
- [x] Task 2: Update cross-references in existing documentation (AC: #7)
  - [x] 2.1 Update `docs/guides/security-model.md` line 513: change plain text troubleshooting reference to actual link `[Troubleshooting Guide](troubleshooting.md)`
  - [x] 2.2 Update `docs/guides/deployment-progression.md` line 230: change "(planned)" link to actual `[Troubleshooting Guide](troubleshooting.md)` link
  - [x] 2.3 Verify `docs/guides/dapr-component-reference.md` line 1000 link to `troubleshooting.md` will resolve correctly (already links correctly, just verify)
  - [x] 2.4 Update `docs/fr-traceability.md`: change FR47, FR48, FR49 status from `GAP` to `COVERED` with link to `docs/guides/troubleshooting.md`
- [x] Task 3: Validation (AC: all)
  - [x] 3.1 Verify the page structure follows the page template convention (back-link, H1, intro paragraph, prerequisites blockquote, content sections, next steps)
  - [x] 3.2 Verify all internal links resolve to existing files
  - [x] 3.3 Run markdownlint-cli2 on `docs/guides/troubleshooting.md` to ensure CI compliance
  - [x] 3.4 Verify each troubleshooting entry includes all three required elements: symptom, probable cause, step-by-step resolution
  - [x] 3.5 Verify FR47 coverage: all five quickstart errors documented (Docker not running, port conflicts, DAPR sidecar timeout, .NET SDK version mismatch, sample build failure)
  - [x] 3.6 Verify FR48 coverage: all five DAPR integration issues documented (sidecar injection failure, state store connection timeout, pub/sub message loss, actor activation conflict, component configuration mismatch)
  - [x] 3.7 Verify FR49 coverage: deployment failures documented for all three environments (Docker Compose, Kubernetes, Azure Container Apps)

## Dev Notes

### Architecture Patterns & Constraints

- **This is a DOCUMENTATION-ONLY story.** No application code changes. Primary output is `docs/guides/troubleshooting.md` with cross-reference updates to existing guides.
- **FR47 is the primary quickstart requirement:** "A developer can find troubleshooting guidance for quickstart errors including: Docker not running, port conflicts, DAPR sidecar timeout, .NET SDK version mismatch, and sample build failure"
- **FR48 is the DAPR integration requirement:** "A developer can find documented solutions for DAPR integration issues including: sidecar injection failure, state store connection timeout, pub/sub message loss, actor activation conflict, and component configuration mismatch"
- **FR49 is the deployment failure requirement:** "A developer can access troubleshooting information for deployment failures per target environment"
- **FR43 (self-contained pages):** The page must be navigable without reading prerequisite pages. Define DAPR terms on first use. Explain "sidecar", "mTLS", "state store", "pub/sub" on first mention.
- **DAPR explanation depth = Operational:** This is a troubleshooting guide. Show actual diagnostic commands, log output examples, and resolution steps. Assume reader knows .NET but NOT DAPR internals.
- **Format for each issue:** Consistent structure — Symptom (what the user sees), Probable Cause (why it happens), Resolution (step-by-step fix). Use H3 for each issue category, H4 for individual issues.

### Existing Documentation Cross-References

The troubleshooting guide will be referenced from multiple existing pages. These references need to be updated from "planned/coming" to actual links:

| Page | Current Reference | Line | Action |
|------|------------------|------|--------|
| `docs/guides/security-model.md` | `Troubleshooting Guide (troubleshooting.md) — ... (coming in Story 14-7)` | 513 | Update to real link |
| `docs/guides/deployment-progression.md` | `[Troubleshooting Guide (planned)](../../_bmad-output/...)` | 230 | Update to `[Troubleshooting Guide](troubleshooting.md)` |
| `docs/guides/dapr-component-reference.md` | `[Troubleshooting Guide](troubleshooting.md) — ... (Story 14-7)` | 1000 | Remove "(Story 14-7)" suffix; link already correct |
| `docs/fr-traceability.md` | FR47, FR48, FR49 marked `GAP` | 131-133, 159-161 | Update to `PASS` with file link |

### Deployment Environments & Key Ports

| Environment | Key Ports | Diagnostic Tool |
|------------|-----------|----------------|
| Docker Compose | 5001 (API), 8180 (Keycloak), 6379 (Redis), 5432 (PostgreSQL) | `docker compose logs`, `docker ps` |
| Kubernetes | Service ports via ClusterIP/NodePort | `kubectl logs`, `kubectl describe pod`, `kubectl get events` |
| Azure Container Apps | HTTPS ingress, system-assigned ports | `az containerapp logs show`, `az containerapp revision list` |

### DAPR Diagnostic Commands

| Area | Command | Purpose |
|------|---------|---------|
| Status check | `dapr status` | Verify DAPR runtime |
| Component list | `dapr components -k` (K8s) | List loaded components |
| Sidecar logs | `dapr logs --app-id commandapi` | Check sidecar output |
| Dashboard | `dapr dashboard` | Visual component status |
| K8s sidecar | `kubectl logs <pod> -c daprd` | DAPR sidecar container logs |

### Key Configuration Files to Reference

| File | What to Check |
|------|--------------|
| `global.json` | .NET SDK version (10.0.102) |
| `Directory.Packages.props` | Centralized NuGet package versions |
| `Hexalith.EventStore.slnx` | Solution file (modern XML format) |
| `src/Hexalith.EventStore.AppHost/` | Aspire AppHost with DAPR topology |
| `deploy/docker-compose/docker-compose.yml` | Docker Compose deployment config |
| `deploy/kubernetes/` | Kubernetes manifests |
| `deploy/azure/` | Azure Container Apps Bicep templates |
| `deploy/dapr/` | DAPR component YAML files |

### Error Pattern: Correlation ID Tracing

For operational troubleshooting, the system uses correlation IDs throughout the pipeline. Each command submission receives a correlation ID that appears in:
1. HTTP response header from Command API
2. Structured logs at every pipeline stage
3. OpenTelemetry spans across the entire processing chain
4. Dead-letter topic messages (full command payload + error context + correlation ID)
5. Command status queries (`/api/v1/commands/status/{correlationId}`)

The troubleshooting guide should explain how to use correlation IDs to trace a command through the system.

### Dead-Letter Topic Structure

Failed commands route to `deadletter.{tenant}.{domain}.events` topics containing:
- Full command payload
- Error message and stack trace
- Correlation ID
- Timestamp
- Source actor/processor identity

This is critical for the "DAPR Integration Issues" section, especially for pub/sub message loss debugging.

### Project Structure Notes

- File location: `docs/guides/troubleshooting.md` (per architecture-documentation.md — matches planned structure)
- `docs/guides/` already contains 6 guides (deployment-docker-compose, deployment-kubernetes, deployment-azure-container-apps, deployment-progression, dapr-component-reference, security-model) — this is the 7th guide
- No new directories needed
- No application code changes — documentation-only story with cross-reference updates
- Reference source code and deployment files for accurate diagnostic guidance but do NOT modify them

### Previous Story Intelligence (14-6)

Key learnings from Story 14-6 (Security Model Documentation):
- **Page template confirmed:** back-link `[<- Back to Hexalith.EventStore](../../README.md)`, H1, intro, prerequisites, content, next steps pattern is consistent across all Epic 14 stories
- **Mermaid diagrams must include `<details>` text descriptions** for accessibility (NFR7) — include if adding any diagrams to troubleshooting guide
- **markdownlint-cli2 must pass** — run validation before completion
- **Internal links verified manually** — ensure all links point to existing files (critical for cross-reference updates)
- **Code blocks need language hints** (`yaml`, `bash`, `json`, `csharp`) for syntax highlighting
- **YAML/command examples should be copy-pasteable** with inline comments
- **Cross-references to deployment guides work well** — readers appreciate being directed to environment-specific pages for detailed setup
- **Review findings pattern:** endpoint paths and claim names must match actual implementation. For troubleshooting, ensure all command paths, port numbers, and configuration keys match the actual codebase.
- **back-link text:** Use `[<- Back to Hexalith.EventStore](../../README.md)` (ASCII arrow `<-`, not Unicode `←`)
- **Commit pattern:** `feat(docs): <description> (Story 14-7)`
- **Branch pattern:** `docs/story-14-7-troubleshooting-guide`

### Git Intelligence

Recent commits for Epic 14:
- `206d011` Merge pull request #85 from Hexalith/docs/story-14-6-security-model-documentation
- `c3574c7` feat(docs): Add security model documentation (Story 14-6)
- `09a7ec9` Merge pull request #84 from Hexalith/docs/story-14-5-dapr-component-configuration-reference
- `0525b7a` feat(docs): Add DAPR component configuration reference (Story 14-5)
- `bc80b1a` Merge pull request #83 from Hexalith/docs/story-14-4-deployment-progression-guide
- `f04bdf7` feat(docs): Add deployment progression guide connecting all deployment environments (Story 14-4)

Pattern: Each story creates a single docs/guides/ page, creates a feature branch, and merges via PR.

### Markdownlint Rules

Configuration in `.markdownlint-cli2.jsonc`:
- `MD013`: disabled (no hard wrap)
- `MD014`: disabled (allow `$` prefix)
- `MD033`: allow `<details>`, `<summary>`, `<br>`, `<img>`, `<picture>`, `<source>`
- `MD024`: `siblings_only: true` (duplicate headings OK in different sections)
- `MD041`: disabled (nav links before H1 OK)
- `MD046`: `style: fenced` (fenced code blocks only)
- `MD048`: `style: backtick` (backtick fences only)
- `MD007`: `indent: 4` (4-space list indentation)
- `MD029`: `style: ordered` (sequential ordered list numbering)
- `MD036`: enabled (no bold-as-heading)
- `MD060`: disabled

### Content Voice & Tone

- **Voice:** Second person ("you"), active voice
- **Tone:** Professional-casual, peer-to-peer — "If you see this error, here's how to fix it" not "The system may exhibit the following error condition"
- **DAPR handling:** Assume reader does NOT know DAPR — explain what DAPR components do in context. e.g., "DAPR uses sidecar processes (small helper applications running alongside your main app) to handle infrastructure concerns"
- **Troubleshooting style:** Be direct and actionable. Lead with the symptom (what the user actually sees), explain why briefly, then give copy-pasteable resolution steps
- **Command examples:** Use `bash` code blocks with `$` prefix for commands. Show expected output where helpful.

### Target Length

This is a COMPREHENSIVE TROUBLESHOOTING page covering three major categories (quickstart, DAPR integration, deployment per environment) with 15+ individual issues. Target 400-600 lines. Comparable to the security model guide (512 lines). Each issue entry should be concise (15-25 lines) — symptom, cause, resolution steps.

### References

- [Source: _bmad-output/planning-artifacts/epics.md#Story 7.7 — Troubleshooting Guide acceptance criteria]
- [Source: _bmad-output/planning-artifacts/prd-documentation.md#FR47 — Quickstart troubleshooting]
- [Source: _bmad-output/planning-artifacts/prd-documentation.md#FR48 — DAPR integration troubleshooting]
- [Source: _bmad-output/planning-artifacts/prd-documentation.md#FR49 — Deployment failure troubleshooting]
- [Source: _bmad-output/planning-artifacts/prd-documentation.md#FR43 — Self-contained pages]
- [Source: _bmad-output/planning-artifacts/architecture-documentation.md — Page conventions, troubleshooting.md location]
- [Source: _bmad-output/planning-artifacts/epics.md#Epic 14 — Deployment & Operations Guides]
- [Source: docs/page-template.md — Standard page structure]
- [Source: docs/guides/security-model.md#L513 — Existing troubleshooting reference (needs update)]
- [Source: docs/guides/deployment-progression.md#L230 — Existing troubleshooting reference (needs update)]
- [Source: docs/guides/dapr-component-reference.md#L1000 — Existing troubleshooting link (verify)]
- [Source: docs/fr-traceability.md#L131-133 — FR47, FR48, FR49 GAP status (needs update to PASS)]
- [Source: docs/guides/deployment-docker-compose.md — Docker Compose deployment reference]
- [Source: docs/guides/deployment-kubernetes.md — Kubernetes deployment reference]
- [Source: docs/guides/deployment-azure-container-apps.md — Azure Container Apps deployment reference]
- [Source: docs/guides/dapr-component-reference.md — DAPR component configuration reference]
- [Source: docs/guides/security-model.md — Security model and authentication configuration]
- [Source: docs/getting-started/prerequisites.md — Prerequisites and installation requirements]
- [Source: global.json — .NET SDK version requirement (10.0.102)]
- [Source: src/Hexalith.EventStore.AppHost/ — Aspire AppHost configuration]
- [Source: deploy/ — Deployment configuration files]
- [Source: _bmad-output/implementation-artifacts/14-6-security-model-documentation.md — Previous story patterns and learnings]

## Dev Agent Record

### Agent Model Used

Claude Opus 4.6

### Debug Log References

- markdownlint-cli2 run: 0 errors on docs/guides/troubleshooting.md
- markdownlint-cli2 run on modified files: 0 new errors (3 pre-existing in dapr-component-reference.md at line 722-724, not related to this story)
- All internal links verified to resolve to existing files

### Completion Notes List

- Created comprehensive troubleshooting guide (1043 lines) covering 19 individual issues across 3 major categories
- FR47: All 5 quickstart errors documented (Docker not running, port conflicts, DAPR sidecar timeout, .NET SDK version mismatch, sample build failure)
- FR48: All 5 DAPR integration issues documented (sidecar injection failure, state store connection timeout, pub/sub message loss, actor activation conflict, component configuration mismatch)
- FR49: All 3 deployment environments documented (Docker Compose: 4 issues, Kubernetes: 5 issues, Azure Container Apps: 5 issues)
- Diagnostic Commands Reference section includes quick-reference table, log collection commands, correlation ID tracing, and dead-letter topic inspection
- DAPR terms explained on first use for self-contained reading (FR43)
- Each issue follows consistent format: Symptom, Probable Cause, Resolution with copy-pasteable commands
- Updated 4 existing files with cross-references: security-model.md, deployment-progression.md, dapr-component-reference.md, fr-traceability.md
- Also updated deployment-progression.md planned links for DAPR Component Reference and Security Model (both now exist from Stories 14-5 and 14-6)
- Senior code review follow-up fixes applied: corrected DAPR API version example (`v1alpha1`), replaced incorrect service-mesh documentation link, corrected Redis dead-letter inspection command to `PSUBSCRIBE`, and normalized FR traceability statuses/counts

### Change Log

- 2026-03-02: Created docs/guides/troubleshooting.md — comprehensive troubleshooting guide covering quickstart, DAPR integration, and deployment issues (FR47, FR48, FR49)
- 2026-03-02: Updated cross-references in security-model.md, deployment-progression.md, dapr-component-reference.md, fr-traceability.md
- 2026-03-02: Applied review-driven corrections to troubleshooting command accuracy and FR traceability status/count consistency

## Senior Developer Review (AI)

### Outcome

- Review status: **Approved after fixes**
- All HIGH and MEDIUM findings from the adversarial review were fixed in documentation

### Fixes Applied

- Updated `docs/guides/troubleshooting.md` DAPR state store example to `apiVersion: dapr.io/v1alpha1`
- Replaced Kubernetes service mesh reference with the correct DAPR service mesh documentation page
- Corrected Redis dead-letter inspection command from `SUBSCRIBE` to `PSUBSCRIBE`
- Replaced FR47/FR48/FR49 status values from `PASS` to `COVERED` in traceability matrix for taxonomy consistency
- Recalculated FR summary counts in `docs/fr-traceability.md` and updated Epic 14 gap count

### File List

- docs/guides/troubleshooting.md (NEW)
- docs/guides/security-model.md (MODIFIED — line 513: plain text to link)
- docs/guides/deployment-progression.md (MODIFIED — lines 228-230: planned links to actual links)
- docs/guides/dapr-component-reference.md (MODIFIED — lines 999-1000: removed story annotations)
- docs/fr-traceability.md (MODIFIED — FR47, FR48, FR49: GAP to COVERED with links; summary and gap analysis updated)
