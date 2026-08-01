# Requirements Traceability

The authoritative requirement text is in [`prd.md`](../../planning-artifacts/prd.md): functional requirements in section 6 and non-functional requirements in section 7. This companion intentionally contains identifiers, anchors, capability ownership, and story coverage only; it does not duplicate requirement prose.

## Capability To Requirement Coverage

| Capability | Requirement identifiers |
| --- | --- |
| CAP-1 Domain author self-service platform | FR1-FR10, FR36; supporting NFR2, NFR6-NFR9, NFR12, NFR14, NFR16 |
| CAP-2 External integration surfaces | FR11-FR16; supporting NFR2, NFR5, NFR8, NFR12-NFR16 |
| CAP-3 Release and repository reliability | FR17-FR22, FR25; supporting NFR9-NFR11, NFR16-NFR17 |
| CAP-4 Event correctness and recovery | FR23-FR24, FR27, FR29-FR31; supporting NFR7, NFR16 |
| CAP-5 Security and tenant isolation | FR26, FR28, FR32; supporting NFR1-NFR4, NFR16-NFR17 |
| CAP-6 Bounded cost and event evolution | FR33; supporting NFR8, NFR12-NFR13, NFR16 |
| CAP-7 Operator trust, admin honesty, and future backlog | FR34-FR35; supporting NFR1-NFR2, NFR4, NFR7, NFR10-NFR17 |
| CAP-8 Readiness recovery package | Readiness gates, both dated migration crosswalks, and the implementation-readiness assessment |
| CAP-9 Shared payload protection | FR37/NFR19; supporting NFR1-NFR4, NFR7, NFR9-NFR12, NFR16-NFR17 |

## Functional Requirement Registry

| ID | PRD anchor | Capability | Primary story coverage |
| --- | --- | --- | --- |
| FR1 | PRD §6 | CAP-1 | 1.1, 1.11 |
| FR2 | PRD §6 | CAP-1 | 1.1 |
| FR3 | PRD §6 | CAP-1 | 1.1 |
| FR4 | PRD §6 | CAP-1 | 1.2, 1.9, 1.13, 1.16, 2.7 |
| FR5 | PRD §6 | CAP-1 | 1.3-1.4, 1.9, 1.13-1.15 |
| FR6 | PRD §6 | CAP-1 | 1.5, 1.9, 1.13 |
| FR7 | PRD §6 | CAP-1 | 1.6, 1.10, 1.13, 1.17-1.19 |
| FR8 | PRD §6 | CAP-1 | 1.7 |
| FR9 | PRD §6 | CAP-1 | 1.8-1.11, 1.13 |
| FR10 | PRD §6 | CAP-1 | 1.11-1.12 |
| FR11 | PRD §6 | CAP-2 | 2.1, 2.4 |
| FR12 | PRD §6 | CAP-2 | 2.2, 2.9, 2.11 |
| FR13 | PRD §6 | CAP-2 | 2.3, 2.5-2.6, 2.10, 7.14 |
| FR14 | PRD §6 | CAP-2 | 2.3, 2.10 |
| FR15 | PRD §6 | CAP-2 | 2.4-2.7, 2.11-2.12, 4.7, 7.19 |
| FR16 | PRD §6 | CAP-2 | 2.8 |
| FR17 | PRD §6 | CAP-3 | 3.1, 3.10 |
| FR18 | PRD §6 | CAP-3 | 3.2 |
| FR19 | PRD §6 | CAP-3 | 3.3 |
| FR20 | PRD §6 | CAP-3 | 3.4 |
| FR21 | PRD §6 | CAP-3 | 2.12, 3.5, 3.11 |
| FR22 | PRD §6 | CAP-3 | 2.12, 3.6, 3.8, 3.11-3.12 |
| FR23 | PRD §6 | CAP-4 | 4.1 |
| FR24 | PRD §6 | CAP-4 | 4.6 |
| FR25 | PRD §6 | CAP-3 | 3.7-3.9, 3.11-3.12 |
| FR26 | PRD §6 | CAP-5 | 2.10, 5.1-5.4 |
| FR27 | PRD §6 | CAP-4 | 4.2, ledger 4.8, implementation 4.9-4.15 |
| FR28 | PRD §6 | CAP-5 | 2.10, 5.5 |
| FR29 | PRD §6 | CAP-4 | 4.3 |
| FR30 | PRD §6 | CAP-4 | 4.4 |
| FR31 | PRD §6 | CAP-4 | 4.5 |
| FR32 | PRD §6 | CAP-5 | 5.6-5.9 |
| FR33 | PRD §6 | CAP-6 | 1.19, 6.1-6.6 |
| FR34 | PRD §6 | CAP-7 | 2.6, 2.11, 3.10, 7.1-7.14, 7.19-7.20 |
| FR35 | PRD §6 | CAP-7 | 7.15-7.18 |
| FR36 | PRD §6 | CAP-1 | 1.2-1.4, 1.9-1.10, 1.14-1.20; deployed closure 3.13 |
| FR37 | PRD §6 | CAP-9 | 8.1-8.11 |

## Non-Functional Requirement Registry

| ID | PRD anchor | Primary story coverage or governing gate |
| --- | --- | --- |
| NFR1 | PRD §7 | 5.2, 5.3, 5.5, 7.2, 7.3, 7.7, 8.3, 8.5-8.7, 8.9, 8.11 |
| NFR2 | PRD §7 | 1.5, 1.9-1.10, 2.5, 5.2, 5.5-5.8, 5.10, 7.2-7.3, 8.5, 8.7, 8.9, 8.11 |
| NFR3 | PRD §7 | 5.3, 8.3, 8.6, 8.9, 8.11 |
| NFR4 | PRD §7 | 5.3, 7.6, 8.5-8.6, 8.9, 8.11 |
| NFR5 | PRD §7 | 2.8 |
| NFR6 | PRD §7 | 1.10, 1.18, 7.1 |
| NFR7 | PRD §7 | 1.3, 1.15, 1.17-1.18, 4.1-4.2, 4.4-4.5, 4.9-4.15, 5.1, 7.11, 8.4-8.5, 8.7, 8.9-8.11 |
| NFR8 | PRD §7 | 1.2, 1.9, 1.13, 1.16, 1.19, 2.11, 4.7, 6.2-6.4 |
| NFR9 | PRD §7 | 2.12, 3.5, 3.8, 3.11-3.13, 8.8, 8.11 |
| NFR10 | PRD §7 | 3.7, 3.11, 7.10, 7.12-7.13, 8.8, 8.11 |
| NFR11 | PRD §7 | 3.8-3.9, 3.12, 7.9, 8.8, 8.11 |
| NFR12 | PRD §7 | 1.17, 1.20, 2.7, 2.12, 3.13, 7.5, 8.2, 8.4, 8.9-8.11 |
| NFR13 | PRD §7 | 2.1-2.4 and generated-code validation gates |
| NFR14 | PRD §7 | 1.8, 1.11, 2.5-2.6, 2.11, 7.5, 7.14, 7.19 |
| NFR15 | PRD §7 | 1.16, 2.6, 7.3-7.4, 7.19 |
| NFR16 | PRD §7 | 1.2-1.5, 1.9-1.10, 1.13-1.15, 1.18-1.20, 2.7, 2.11-2.12, 3.10-3.13, 4.7, 4.9-4.15, 5.8, 7.3, 7.10-7.12, 8.7-8.11 |
| NFR17 | PRD §7 | 3.12, 5.6-5.9, 7.6-7.9, 8.6, 8.11 |
| NFR18 | PRD §7 | Architecture/project-context AOT exclusion; applies to all reflection-based seams |
| NFR19 | PRD §7 | 8.1-8.7, 8.9-8.11 |

## AD-24 Operational Secret Invariant

Architecture AD-24 is an adopted companion invariant for FR34, NFR4, NFR17, and Story 7.6. The architecture companion owns the full decision; these consequences are binding:

| Concern | Binding consequence |
| --- | --- |
| Provider and API | Production operational and application secrets resolve through DAPR component `openbao` using `secretstores.hashicorp.vault` v1. Dependent components use `auth.secretStore` and `secretKeyRef`; application code uses the DAPR Secrets API. |
| Value-free contract | `deploy/dapr/openbao-secret-contract.yaml` is the sole catalog for logical names, map keys and shapes, consumers, dependent resources, retrieval lifecycle, OpenBao policy paths, and generation/cache/rotation bounds; it contains no secret values. |
| Least privilege | Singleton component scopes, per-app DAPR `defaultAccess: deny` plus explicit `allowedSecrets`, and OpenBao ACLs derive from the contract. Missing, extra, or mismatched grants fail deployment validation. |
| Bootstrap | OpenBao token, DAPR API token, and TLS trust material are out-of-band hosting inputs with no dependency on DAPR or OpenBao retrieval; committed inline credentials are forbidden. |
| Readiness and failure | Hosts resolve declared required secrets before readiness. Missing startup inputs fail startup; runtime lookup or refresh failure fails closed, disables the dependent operation, expires unusable cached values, and holds readiness false until bounded recovery. |
| Rotation | Atomic secret maps carry a non-secret generation. Rotation publishes a new generation, preserves overlap, waits for every cataloged consumer to acknowledge while ready, and only then revokes old material; incomplete acknowledgement retains old validity or publishes a restored generation. |
| Evidence and profiles | Release evidence includes a real-OpenBao integration lane. Development substitutes preserve the same logical contract and default-deny behavior; Azure Container Apps managed DAPR is non-conforming until an approved compatible profile proves equivalent support and scoping. |
| Key-custody separation | AD-24 does not approve or modify AD-23 or the payload-protection Azure Key Vault Premium RSA-HSM KEK design. DAPR secret stores are not production `pdenc-v2` key custody. |
