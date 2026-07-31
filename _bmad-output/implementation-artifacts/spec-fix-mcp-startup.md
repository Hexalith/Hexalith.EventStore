---
title: 'Fix Codex MCP startup under WSL'
type: 'bugfix'
created: '2026-07-31'
status: 'done'
baseline_commit: '3ca3cbbf042365f5d876a3fe3d6cc19edd678e3b'
review_loop_iteration: 2
context: []
---

# Fix Codex MCP startup under WSL

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Codex reports that `aspire`, `codex_apps`, `fluent-ui-blazor`, `microsoft-learn`, and `node_repl` were not initialized. The warning is a cancelled startup round rather than five failures—four servers already work end to end—but `node_repl` has a real WSL interoperability defect: Codex sends a Linux `file:///home/...` sandbox working-directory URI to a Windows runtime, which rejects it with MCP error `-32602`.

**Approach:** Keep the valid repository MCP configuration and the four healthy servers unchanged. Add a user-scoped stdio adapter for `node_repl` that translates only WSL sandbox working-directory URIs into Windows-local WSL UNC file URIs, point the global `node_repl` entry at it, and verify all five servers through real read-only tool calls. Treat an explicit startup interruption as expected Codex behavior rather than hiding it with timeout or security changes.

## Boundaries & Constraints

**Always:** Preserve the complete JSON-RPC request and sandbox permission profile except for the necessary `sandboxCwd` URI translation; retain stream backpressure, EOF, stderr, exit status, and signal propagation; derive the distribution from `WSL_DISTRO_NAME`; keep the adapter user-scoped and dependency-free; leave `.mcp.json` and all healthy server definitions unchanged.

**Ask First:** Any need to edit the Windows Codex runtime, remove/disable an MCP server, alter repository-tracked MCP configuration, or change a permission boundary.

**Never:** Disable the Node sandbox; discard `codex/sandbox-state-meta`; hardcode a repository path or Windows runtime version; expose credentials; reinterpret the interruption warning as a timeout; modify vendor-managed files under `/mnt/c/Users/JeromePiquot/AppData/Local/OpenAI/Codex`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| WSL filesystem path | `file:///home/administrator/...` in `sandboxCwd` | `file://wsl.localhost/<distro>/home/administrator/...` | Reject malformed file URIs with a concise stderr diagnostic and nonzero exit. |
| Mounted Windows drive | `file:///mnt/c/...` in `sandboxCwd` | `file:///C:/...` with URI encoding preserved | Reject an invalid drive segment rather than guessing. |
| Already compatible URI | Windows-drive or `wsl.localhost` file URI | Pass through unchanged | N/A |
| Unrelated traffic | JSON-RPC without sandbox metadata, or non-JSON line | Pass through byte-equivalent line content | Do not terminate the server for unrelated input. |
| Missing WSL identity | Linux sandbox URI and no `WSL_DISTRO_NAME` | Do not weaken sandboxing or invent a host path | Fail fast with an actionable diagnostic. |

</frozen-after-approval>

## Code Map

- `/home/administrator/.local/libexec/codex-node-repl-wsl-proxy.mjs:1` -- user-scoped proxy implementation; lossless JSON token replacement, lexically preserved URI path text, JSON-RPC request targeting, and bounded stream/process lifecycle are mandatory.
- `/home/administrator/.codex/config.toml:289` -- global MCP registrations; only the `node_repl` command/args may change.
- `/home/administrator/.local/libexec/codex-node-repl-wsl-proxy.test.mjs:1` -- focused coverage for every approved I/O matrix row and invalid-input process behavior.
- `/home/administrator/.npm-global/bin/codex-node-repl:1` -- current WSL launcher into the Windows Codex App runtime; reuse as the adapter's child command.
- `/mnt/c/Users/JeromePiquot/AppData/Roaming/npm/codex-node-repl.js:1` -- vendor bridge that resolves the current Windows runtime; read-only evidence and child implementation.
- `.mcp.json:1` -- intentional project configuration for Aspire, Fluent UI, and Microsoft Learn; verified healthy and read-only for this fix.
- `.codex/config.toml:1` -- project setting enables hooks only; not an MCP repair surface.

## Tasks & Acceptance

**Execution:**
- [x] `/home/administrator/.local/libexec/codex-node-repl-wsl-proxy.mjs` -- add a line-oriented JSON-RPC adapter that replaces only the effective `sandboxCwd` string token in JSON-RPC requests; preserves raw URI path spelling, dot segments, Unicode, percent encoding, unsafe numeric tokens, and all unrelated bytes; rejects decoded controls/malformed UTF-8; bounds input; and robustly forwards process/stream lifecycle.
- [x] `/home/administrator/.codex/config.toml` -- route only `mcp_servers.node_repl` through the executable adapter while preserving its 120-second startup timeout, with automated assertions of the effective registration and real default child.
- [x] `_bmad-output/implementation-artifacts/spec-fix-mcp-startup.md` -- record reproducible asserted verification, results, integrity hashes, and rollback instructions without changing repository MCP definitions.

**Acceptance Criteria:**
- Given the current WSL workspace and sandbox metadata, when `node_repl/js` executes `nodeRepl.write('node-repl-ok')`, then it returns `node-repl-ok` without MCP error `-32602` and without disabling sandbox enforcement.
- Given a fresh Codex session allowed to complete startup, when `/mcp` is inspected, then all five named servers expose their expected tools and no server has a failed status.
- Given the repaired session, when one read-only operation is invoked through each server, then Aspire, Codex Apps, Fluent UI Blazor, Microsoft Learn, and Node REPL all return non-error results.
- Given the user presses Esc or otherwise cancels startup, when Codex reports `MCP startup interrupted`, then the behavior remains distinguishable from `MCP startup incomplete (failed: ...)` and no timeout workaround masks it.

## Spec Change Log

- Iteration 1 (review): The original adapter parsed and reserialized complete JSON-RPC lines, which could alter unsafe integers and unrelated syntax despite the frozen preservation invariant; process lifecycle and configured-adoption success paths also lacked executable regression checks. Strengthened the Code Map, execution tasks, and verification plan to require token-local rewriting, bounded input, deterministic child fixtures, effective-config coverage, and reproducible commands. Avoid the known-bad parse/mutate/stringify design. KEEP: narrow WSL URI translation, permission-profile preservation, user-scoped dependency-free installation, unchanged healthy server definitions, successful real MCP exchange, explicit sandbox enforcement, and the 120-second startup timeout.
- Iteration 2 (review): The token-local reimplementation still rebuilt translated paths from normalized `URL.pathname`, changing dot segments and raw Unicode, and did not mechanically assert the real default child or forced-shutdown fallback. Strengthened execution and verification to preserve raw URI path spelling while using URL parsing only for validation, reject malformed decoded controls/UTF-8, target actual JSON-RPC requests, bound asynchronous lifecycle tests, assert a real default-child MCP result, and exercise forced shutdown. Avoid normalized-path reconstruction and success-by-exit-code-only checks. KEEP: every Iteration 1 item, lossless JSON token rewriting, bounded line buffering, executable config adoption, deterministic fixtures, stderr/EOF/exit/signal forwarding, and the 15 passing focused checks.

## Design Notes

The adapter is preferable to patching the vendor launcher because Codex App updates can replace vendor files. It must preserve the permission profile and convert only the filesystem identity boundary between Linux Codex and the Windows Node runtime; `--disable-sandbox` was tested and rejected because it neither fixes URI validation nor satisfies the security boundary.

## Verification

**Commands:**
- `node --check /home/administrator/.local/libexec/codex-node-repl-wsl-proxy.mjs` -- expected: syntax validation succeeds.
- `node --test /home/administrator/.local/libexec/codex-node-repl-wsl-proxy.test.mjs` -- expected: every matrix, raw-URI/lossless-token, chunk-boundary, bounded-input, effective-config, real-child, successful-subprocess, forced-shutdown, failure, and child-exit test passes with no skips.
- `printf '%s\n%s\n%s\n' '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"wsl-proxy-verification","version":"1.0"}}}' '{"jsonrpc":"2.0","method":"notifications/initialized","params":{}}' '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"js","arguments":{"code":"nodeRepl.write(\"node-repl-ok\")","title":"Verify WSL proxy"},"_meta":{"codex/sandbox-state-meta":{"sandboxCwd":"file:///home/administrator/projects/hexalith/eventstore","permissionProfile":{"type":"managed","file_system":{"type":"restricted","entries":[]},"network":"restricted"},"useLegacyLandlock":false}}}}' | timeout 30s /home/administrator/.local/libexec/codex-node-repl-wsl-proxy.mjs` -- expected: `node-repl-ok` with `isError: false`.
- The focused suite must run the same three-message exchange through the configured proxy without a child override, parse its JSON responses, and assert `node-repl-ok` with `isError: false`; it must also assert that a SIGTERM-ignoring fixture is forcibly stopped within a bounded timeout.
- `codex doctor --json` -- expected: overall status and MCP configuration remain `ok`.
- `codex mcp list` -- expected: four explicit servers remain enabled and `node_repl` points to the adapter.
- `sha256sum /home/administrator/.local/libexec/codex-node-repl-wsl-proxy.mjs /home/administrator/.local/libexec/codex-node-repl-wsl-proxy.test.mjs /home/administrator/.codex/config.toml .mcp.json .codex/config.toml /home/administrator/.npm-global/bin/codex-node-repl /mnt/c/Users/JeromePiquot/AppData/Roaming/npm/codex-node-repl.js` -- expected: hashes match the recorded results.

**Manual checks (if no CLI):**
- Start a new Codex TUI session, allow MCP startup to finish without Esc, run `/mcp`, and confirm all five inventories are populated.

**Rollback:** Restore `mcp_servers.node_repl.command = "codex-node-repl"` in `/home/administrator/.codex/config.toml`, then remove the two user-scoped proxy files after confirming no active Codex process uses them.

**Results (2026-07-31):**
- Proxy syntax check passed; all 25 focused Node tests passed with no skips. The suite covers every matrix row, raw URI spelling, decoded control/malformed UTF-8 and encoded-separator rejection, root-escape prevention, JSON-RPC targeting, lossless unsafe-number/token handling, duplicate-key semantics, BOM/deep/non-JSON preservation, chunk boundaries, both size-bound branches, pre-EOF forwarding, effective Codex config adoption, the real default child, early child exit/signal races, success/failure paths, EOF, stderr, child status, normal signals, SIGQUIT mapping, and forced shutdown.
- A real initialize/initialized/`node_repl/js` exchange using Linux sandbox metadata and a managed permission profile (`file_system` restricted with no entries; network restricted) returned `node-repl-ok` with `isError: false`.
- `codex doctor --json` reported overall, configuration, MCP, network, and WebSocket status `ok`; `codex mcp list` retained all four explicit enabled entries and the 120-second Node REPL timeout.
- Read-only calls through Aspire, Codex Apps, Fluent UI Blazor, and Microsoft Learn returned non-error results; the repaired Node REPL call supplied the fifth server check.
- A fresh TUI `/mcp` inventory listed tools for all five servers. The diagnostic harness yield reproduced the separate cancellation warning without producing a failed server status.
- SHA-256: proxy `9444f3a3d7b60a764d6fbd5524693b3762840686164cbf2b75d402782f10b996`; tests `b757e8baa1d2a1ca825d9ac84d22478d67985f99c7c1bd9f262b455ed429a9e8`; global config `415974cb781234a1a463b41bdd5b992133e8e186fb71b6fdd4fd4856296147a9`; repository `.mcp.json` `f299f54e425f506d045ea9b6915f6bc3549199bd2b662ff73735e20854cccb7c`; repository `.codex/config.toml` `d37497c3278121598a663564ab38b53f658969717f78decb661ddd11c66551ea`; WSL launcher `f8f67424102e13135a72f6ee86e0c238757dcc375cb754d9a55121fefb6d5d7c`; vendor bridge `681908155134e10ebd7040dc25c1ec4e94ec7428a2d0179257cbae19eca80baf`.

## Suggested Review Order

**Filesystem identity boundary**

- Validate without normalizing, then translate only the cross-OS filesystem identity.
  [`codex-node-repl-wsl-proxy.mjs:155`](../../../../../.local/libexec/codex-node-repl-wsl-proxy.mjs#L155)

- Replace only effective JSON-RPC sandbox URI tokens; preserve every unrelated byte.
  [`codex-node-repl-wsl-proxy.mjs:472`](../../../../../.local/libexec/codex-node-repl-wsl-proxy.mjs#L472)

**Streaming and process lifecycle**

- Bound line buffering while forwarding complete messages before client EOF.
  [`codex-node-repl-wsl-proxy.mjs:530`](../../../../../.local/libexec/codex-node-repl-wsl-proxy.mjs#L530)

- Reconcile child-exit races and preserve bounded signal, stderr, and exit behavior.
  [`codex-node-repl-wsl-proxy.mjs:622`](../../../../../.local/libexec/codex-node-repl-wsl-proxy.mjs#L622)

**Verification and adoption**

- Prove configured live-stream forwarding and effective Codex registration.
  [`codex-node-repl-wsl-proxy.test.mjs:353`](../../../../../.local/libexec/codex-node-repl-wsl-proxy.test.mjs#L353)

- Exercise early child exit and signal races that previously misreported failures.
  [`codex-node-repl-wsl-proxy.test.mjs:394`](../../../../../.local/libexec/codex-node-repl-wsl-proxy.test.mjs#L394)

- Parse the real Windows-backed MCP result under managed sandbox metadata.
  [`codex-node-repl-wsl-proxy.test.mjs:558`](../../../../../.local/libexec/codex-node-repl-wsl-proxy.test.mjs#L558)

- Route only global `node_repl`; preserve arguments and the startup timeout.
  [`config.toml:289`](../../../../../.codex/config.toml#L289)

**Deferred vendor boundary**

- Track descendant cleanup separately without modifying vendor-managed runtime files.
  [`deferred-work.md:796`](deferred-work.md#L796)
