# EventStore Provider Verification

This executable verifies external Pact V4 files against the production EventStore gateway pipeline on an OS-assigned IPv4 loopback Kestrel port. It validates every input before playback, runs each manifest interaction independently, discards native verifier output, and atomically writes a bounded support-safe JSON report.

Build the solution and focused tests:

```bash
dotnet build Hexalith.EventStore.slnx --configuration Release -m:1
dotnet build tests/Hexalith.EventStore.ProviderVerification.Tests/Hexalith.EventStore.ProviderVerification.Tests.csproj --configuration Release -m:1
dotnet tests/Hexalith.EventStore.ProviderVerification.Tests/bin/Release/net10.0/Hexalith.EventStore.ProviderVerification.Tests.dll
```

The focused built-DLL suite currently contains 77 tests, including a self-contained one-interaction Pact run through real Kestrel and the production controller/middleware pipeline followed by verified port closure.

Run the committed FrontComposer inputs in live compatibility mode from the EventStore repository root:

```bash
eventstore_root="$(pwd)"
frontcomposer_root="$eventstore_root/references/Hexalith.FrontComposer"
dotnet run --project tests/Hexalith.EventStore.ProviderVerification/Hexalith.EventStore.ProviderVerification.csproj --configuration Release --no-build -- \
  --verification-mode live-compatibility \
  --pact-directory "$frontcomposer_root/tests/Hexalith.FrontComposer.Shell.Tests/Pact" \
  --manifest "$frontcomposer_root/tests/Hexalith.FrontComposer.Shell.Tests/Pact/interaction-manifest.json" \
  --provider-state-catalog "$frontcomposer_root/tests/Hexalith.FrontComposer.Shell.Tests/Pact/provider-state-catalog.json" \
  --report-output "$frontcomposer_root/_bmad-output/implementation-artifacts/evidence/pact-provider-reconciliation/provider-verification.json"
```

Live compatibility records the current EventStore source SHA, Release package version, Builds SHA, and release-inventory hash without claiming migration approval. It succeeds only when provenance is internally consistent, all interactions pass, and Kestrel is stopped with its port closed.

Omit `--verification-mode` (or pass `historical-authorization`) and provide `--identity-record` plus `--identity-evidence-directory` to replay the immutable Story 11.24 authorization lane. That historical mode retains its exact approval, hash, and runtime-drift checks independently of current Pact compatibility.
