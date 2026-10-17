# Story 3.6 production authority technical preparation

This is a local technical verification and bounded native-rights record. It
establishes no deployed Timesheets application, authority enrollment, approval,
legacy source coverage, maintenance window or deployed runtime acceptance.
Owner self-review applies to this project document.

The source implementation is in the owning EventStore repository on `main`,
based on `77d9af18` with the uncommitted changes hashed in `verification.json`.
`docs/security/workload-binding-authority.md` describes the provider, configuration,
production enrollment prerequisites and rollback boundary. There are no Git,
dependency-version, permission or live application changes.

`verification.json` retains exact command arrays, times, exit codes and current
source SHA-256 values. Final Debug source-reference Server.Tests build passed with
zero warnings/errors; the full authentication namespace passed **187/187**, zero
failed/skipped (5.979s). The focused 71 cases cover broker/profile and prior issuer
behavior; the broader namespace exercises shared authentication regression.
Test RSA certificates, credentials, clients and grants were synthetic, temporary
and local. No real TLS handshake is claimed by the in-memory HTTP tests.

Initial `dotnet build tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj --no-restore -p:UseHexalithProjectReferences=true -m:1 --nologo -v:minimal`
failed on stale restored assets: `CS1705`, JwtBearer 10.0.12 versus source 10.0.13,
with 16 assembly-conflict warnings. Restoring that same pinned graph using
`DOTNET_CLI_HOME=/tmp/dotnet-cli-home dotnet restore tests/Hexalith.EventStore.Server.Tests/Hexalith.EventStore.Server.Tests.csproj -p:UseHexalithProjectReferences=true --nologo -v:minimal`
passed, with no dependency-version edits. Two subsequent local compile issues
(CA1062 parameter validation and obsolete TestServer construction) were fixed;
final logs record the passing result without bypassing any gate.

`current-rights.json` records the actual existing native subject
`jpiquot`/`system:authenticated` and twelve bounded `kubectl auth can-i` answers,
all `yes`, at its recorded current cut. These use nonpersisting
SelfSubjectAccessReview; they create no grants or resource objects and constitute
no approval to exercise the observed mutation rights. The broader root acceptance
record separately retains complete resolved rules, RBAC source and actual TLS
public-client observations. Historic grant/revocation completeness remains
unestablished. No Timesheets domain or migration authority follows from native
rights.

Runtime Timesheets migration authority/inventory providers remain deny/unavailable.
Absence of an identifiable deployment cannot certify empty legacy issuance.
Full installation history/source cut, owner continuity, pending-delivery recovery
and enforced writer/delivery quiescence must be factually established before
migration. No empty receipt, source checkpoint or maintenance claim is created.
