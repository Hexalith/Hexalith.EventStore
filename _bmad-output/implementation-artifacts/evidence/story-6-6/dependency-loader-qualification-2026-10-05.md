# Dedicated load-context candidate qualification

**Result: negative for this candidate.** A dedicated `AssemblyLoadContext`
with pinned managed resolution, ordinary native refusal and assembly-load
observation did not prevent unmanifested fixture code from executing before
the host rejected capability. This experiment does not establish that every
possible loading policy fails, adopt a production policy or grant readiness.
The [existing decision](dependency-loader-decision.md) remains proposed.

The isolated fixture and retained observations are in
[2026-10-05-loader-probe](2026-10-05-loader-probe/).
No product code, earlier sealed repair artifact, frozen intent, registration,
activation fence, dependency or global setting changed during this probe.

## Candidate and controls

The runner copies its sources into a fresh owned `/tmp` directory, outside the
repository build imports. It builds three managed Release projects and one
owned ELF shared library. The candidate accepts only two managed fixture rows,
loads them from content-addressed paths after identity/hash checks and shares
five explicitly listed framework assemblies. Host support assemblies are
prewarmed and captured separately; their identities, paths and hashes are in
each observation. No native implementation row is admitted.

The host installs an `AppDomain.AssemblyLoad` observer that invalidates readiness
on an unknown assembly. It checks readiness before entering the plugin and
rejects capability after the callback if invalidated. This deliberately gives
the proposed audit an opportunity to detect an escape; it does not pretend that
invalidating a flag interposes on code already executing. Native effect markers
are examined after return as an explicitly late audit, not a native loader hook.

All fixture effects append only to runner-owned temporary files. The native
library has both an ELF constructor and a callable export, allowing the probe
to distinguish execution during load from execution after load returns.

The final run used .NET SDK 10.0.401, runtime 10.0.12 and Linux x64. Builds had
60-second process-group deadlines; native compilation and each fresh scenario
had 20-second deadlines. All final builds passed with zero warnings/errors;
every scenario process exited zero and no deadline expired. A zero process
exit means observations were collected; the qualification result is negative.

| Final fresh-process scenario | Observed effect and capability outcome |
| --- | --- |
| Pinned managed dependency | `managed:declared`; callback returned, capability remained ready |
| Ordinary undeclared managed load by name | Candidate resolution refused; no effect, callback threw, host rejected capability |
| Explicit managed load into `Default` | `managed:undeclared`; observer invalidated at event 14, nested call still executed/returned at 15, host rejected at 18 |
| Explicit managed load into another context | Same effect/order as Default, with `fixture-explicit-other` recorded as owner |
| Ordinary undeclared P/Invoke | Per-assembly resolver and candidate native override were called; override refused before constructor/export effects |
| Explicit `NativeLibrary.Load(string)` | `native:constructor` and `native:export`; neither per-assembly resolver nor candidate native override was called; plugin returned at 15, host audit invalidated at 17 and rejected at 18 |

In both managed escapes, the observer detected the foreign load before the
nested foreign call but could not prevent that call inside the already admitted
callback. In the explicit native escape, the constructor executed before the
`native-load-returned` observation at event 13. The export returned at 14 while
readiness still read true. The late effect audit supplied evidence only after
both effects had occurred.

The plugin's declared dependency can be loaded while its entry method is being
prepared even when that scenario takes a different branch. Those loads were
pinned, passed hash checks and are visible in the event logs; they are not the
undeclared effect under test. Exception formatting in the ordinary refusal
controls also loads host `System.Diagnostics.StackTrace` after the original
refusal, visible as an additional audit invalidation. Neither changes the
successful positive control or the escape observations.

## Retained attempts and exact bytes

The final invocation was:

```text
python3 _bmad-output/implementation-artifacts/evidence/story-6-6/2026-10-05-loader-probe/run_probe.py --results-name attempt-3
```

Its owned runtime directory is `/tmp/story-6-6-loader-probe-ypaix5ll`.
[result.json](2026-10-05-loader-probe/attempt-3/result.json) records every
command, deadline, exit code, consumed-byte digest and ordered observation.
[raw-observations.json](2026-10-05-loader-probe/attempt-3/raw-observations.json)
and individual `run-*.log` / `effects-*.txt` files preserve the raw results.
The runner checks every consumed host, manifest, pinned and undeclared fixture
file immediately before and after each scenario; all remained unchanged.

| Final consumed binary | SHA-256 |
| --- | --- |
| `Probe.Host.dll` | `4f60e2efc5148dd038b934fb7cadddd2990bfbcbe0fe4667b85b39219782e324` |
| `Probe.Plugin.dll` | `bed8f953f06b9d36b8d1ec3882e8d8e7c6eaf3629fb91640e86b4b15e487d1ff` |
| `Probe.Declared.dll` | `ecf17c02d90b38e6fe561b7b1e4b313f3c9f7e35d5f367ffc8e4242000ac1ca3` |
| `Probe.Undeclared.dll` | `2f2f9aac2cfb6bc1e2e1aadf421db944ae323483eca7e28e8f15216f31cd113a` |
| `libprobe_unmanifested.so` | `8fb341d8cd4411254290f2a17b8274bacec0c8e7a5070dbe6463acf3616218f3` |

The actual binaries, PDBs and runtime configuration are retained under
[attempt-3/binaries](2026-10-05-loader-probe/attempt-3/binaries/) and
[consumed-binaries.zip](2026-10-05-loader-probe/attempt-3/consumed-binaries.zip).
Source and binary inventories bind the final copied/compiled fixture. Current
authored C# types and members have XML documentation and one type per file.

Two failed positive-control setup attempts are retained separately:

- [results/attempt.json](2026-10-05-loader-probe/results/attempt.json): owned
  `/tmp/story-6-6-loader-probe-0x3vc25a`; builds passed but the first positive
  process stopped before invocation. This attempt did not exercise escapes.
- [attempt-2/attempt.json](2026-10-05-loader-probe/attempt-2/attempt.json): owned
  `/tmp/story-6-6-loader-probe-nw257blr`; all six callbacks were refused before
  invocation. Structured events showed that host pin verification lazily loaded
  `System.Reflection.Metadata`, `System.IO.MemoryMappedFiles` and
  `System.Collections.Immutable` after the observation baseline. This was a
  fixture baseline omission, not escape qualification.

The exact first and second copied source snapshots and consumed runner versions
are adjacent to those attempts. Their source-inventory paths resolve against
their retained `compiled-source` snapshots, rather than the later current
fixture. The final setup prewarms `AssemblyName.GetAssemblyName` against the
owned plugin path before capturing the host baseline; no product policy was
relaxed. The documented native delegate also moved to its own file before
the second/final copies. No historical source or output was rewritten.

The final [inventory](2026-10-05-loader-probe/sealed-inventory.json) binds this
report, exact input identities, sources, binaries and outputs. Its supporting
checks also verify that all 104 previously sealed repair files still match
their recorded hashes.

## Primary .NET sources and limits

Microsoft describes load contexts as dependency-resolution scopes without
binary isolation. That supports interpreting this result as a resolution
boundary limitation, not complete execution isolation.
[AssemblyLoadContext concepts](https://learn.microsoft.com/en-us/dotnet/core/dependency-loading/understanding-assemblyloadcontext)

The managed algorithm assigns explicit path loads to the chosen context and
raises `AssemblyLoad` after a new assembly is loaded. The observed Default and
other-context behavior follows that documented algorithm.
[Managed assembly loading](https://learn.microsoft.com/en-us/dotnet/core/dependency-loading/loading-managed)

The ordinary P/Invoke algorithm consults the per-assembly resolver and active
context's unmanaged hook. The simple `NativeLibrary.Load(string)` API wraps
the OS loader; this probe exercised that overload only. It does not qualify
the other overload, all reflection APIs, generated code, hostile callbacks,
file replacement races, every OS, a complete catalog, retained-history bounds
or startup/per-call performance at maximum manifest sizes.
[P/Invoke loading](https://learn.microsoft.com/en-us/dotnet/core/dependency-loading/loading-unmanaged),
[NativeLibrary.Load](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.nativelibrary.load?view=net-10.0)

## Next dependent slice and owner decision

The earliest blocked production slice is **M1 trusted executable/catalog
closure**, followed by M2 hops and negotiated consumers. The missing input is
an approved enforceable execution/loading policy specifying how explicit
managed/native loads inside an already admitted callback are prevented or
refused before their callbacks/effects. The dedicated context plus observation
tested here does not supply that authority. Separately, actual production
registrations still need authoritative per-domain manifest bytes, exact gateway
pins and measured serializer bounds; synthetic probe rows cannot supply them.

The proposed owner decision is: **keep the dedicated context as a possible
resolution mechanism only, with M1 readiness false; choose and authorize
qualification of an additional enforcement boundary with explicit trust
assumptions for in-flight catalog code before migrating registrations.** That
input must name the authority that intercepts or constrains explicit loads
and nested effects. An unrestricted in-process context plus an audit flag is
insufficient. The experiment does not select a process boundary, a code
certification policy or any other replacement, and does not amend frozen
requirements to allow effects before rejection.

Missing production qualification is a later acceptance blocker, not a reason
to stop independent dormant local work. Concrete work still available without
this architecture decision includes the reader's exception-during-extension-
snapshot cleanup regression, fuller legacy API/wire/already-compiled fixtures
for the existing additive contract set, and the current source verifier's
factory-created application-storage-call coverage. These repairs can proceed
while activation remains fenced; none makes the failed loading boundary ready.

The earlier typed legacy intake guards prove refusal of non-null provenance
values. They do not distinguish an absent raw JSON property from an explicitly
null property. M1 raw-property-presence admission and the full consumer matrix
remain open; this loader experiment supplies no additional intake qualification.
