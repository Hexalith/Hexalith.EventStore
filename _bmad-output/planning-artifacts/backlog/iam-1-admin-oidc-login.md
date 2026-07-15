---
id: IAM-1
title: Admin Interactive OIDC Login
classification: backlog
status: draft
source_story: 7.16
supersedes_source_story: 7.5
created: 2026-07-05
---

# IAM-1 - Admin Interactive OIDC Login

## Scope

Define a future Admin UI and Admin API authentication path using authorization-code with PKCE, forwarded end-user tokens, and claim normalization aligned with gateway tenant and permission rules. The capability must remove ROPC or self-minted end-user identity patterns from interactive admin flows.

## Non-Goals

- Do not implement interactive OIDC login in Phase 4 MVP.
- Do not weaken immediate production auth guards or secret-stripping work while this backlog item waits.
- Do not treat service identity as a substitute for attributable admin user identity.

## Dependencies

- Production authentication guard work in Story 5.3.
- Admin claims normalization in Story 7.2 and state-mutating audit in Story 7.3.
- Identity-provider deployment and callback URL decisions.

## Risks

- Incorrect token forwarding can blur service identity and user identity.
- Missing tenant and permission normalization can grant broad admin visibility.
- Login UX can expose unsafe details if denied states disclose tenant or resource existence.

## Validation Expectations

- Auth tests must cover PKCE callback validation, token forwarding, denied tenants, expired tokens, and missing permission claims.
- Admin audit evidence must include authenticated user identity and tenant context.
- UI tests must cover support-safe denied and expired-session states.
