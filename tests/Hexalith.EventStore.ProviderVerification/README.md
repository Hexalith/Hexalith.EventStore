# EventStore Provider Verification

This executable verifies external Pact V4 files against the production EventStore gateway pipeline on an OS-assigned IPv4 loopback Kestrel port. It validates every input before playback, runs each manifest interaction independently, discards native verifier output, and atomically writes a bounded support-safe JSON report.

Build the solution and focused tests:

```bash
dotnet build Hexalith.EventStore.slnx --configuration Release -m:1
dotnet build tests/Hexalith.EventStore.ProviderVerification.Tests/Hexalith.EventStore.ProviderVerification.Tests.csproj --configuration Release -m:1
dotnet tests/Hexalith.EventStore.ProviderVerification.Tests/bin/Release/net10.0/Hexalith.EventStore.ProviderVerification.Tests.dll
```

The focused built-DLL suite currently contains 74 tests, including a self-contained one-interaction Pact run through real Kestrel and the production controller/middleware pipeline followed by verified port closure.

Run the committed FrontComposer inputs from the EventStore repository root:

```bash
eventstore_root="$(pwd)"
frontcomposer_root="$eventstore_root/references/Hexalith.FrontComposer"
dotnet run --project tests/Hexalith.EventStore.ProviderVerification/Hexalith.EventStore.ProviderVerification.csproj --configuration Release --no-build -- \
  --pact-directory "$frontcomposer_root/tests/Hexalith.FrontComposer.Shell.Tests/Pact" \
  --manifest "$frontcomposer_root/tests/Hexalith.FrontComposer.Shell.Tests/Pact/interaction-manifest.json" \
  --provider-state-catalog "$frontcomposer_root/tests/Hexalith.FrontComposer.Shell.Tests/Pact/provider-state-catalog.json" \
  --identity-record "$eventstore_root/_bmad-output/implementation-artifacts/frontcomposer-11-24-runtime-identity-successor.md" \
  --identity-evidence-directory "$eventstore_root/_bmad-output/implementation-artifacts/evidence/frontcomposer-story-11-24/bb94d93e9b84132cff83a38fba84f25455820d31" \
  --report-output "$eventstore_root/_bmad-output/implementation-artifacts/evidence/frontcomposer-story-11-24/provider-verification/provider-verification.json"
```

The current command is expected to return nonzero: the decision record is non-authorizing and binds source `bb94d93e9b84132cff83a38fba84f25455820d31`, while this checkout is a different runtime. Playback still covers all 19 interactions so the report records the actual provider-wire differences.
