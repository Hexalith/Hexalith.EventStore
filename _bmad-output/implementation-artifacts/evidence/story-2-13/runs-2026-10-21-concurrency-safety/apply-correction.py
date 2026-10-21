#!/usr/bin/env python3
"""Apply only the source-supported non-destructive local reconnect evaluation."""
from pathlib import Path
import difflib
import json
import shutil

packet = Path(__file__).resolve().parent
acquisition = json.loads((packet / "runtime-acquisition.json").read_text())
source = Path(acquisition["sourceDirectory"])
router = source / "pkg/actors/router/router.go"
before = router.read_text()
(packet / "retained-candidate-router.go.source").write_bytes(router.read_bytes())
before_block = '''	destroyConnection := false
	defer func() { cancel(destroyConnection) }()'''
after_block = '''	defer cancel(false)'''
if before.count(before_block) != 1:
    raise RuntimeError("Unexpected retained release block")
after = before.replace(before_block, after_block)
before_block = '''		// Drop the failed channel rather than retaining its reconnect backoff.
		// Caller cancellation never triggers eviction or expands the retry budget.
		destroyConnection = status.Code(err) == codes.Unavailable && ctx.Err() == nil
		if destroyConnection {
			localRecoveryLog.Debug("Evicting unavailable pooled remote actor channel in isolated runtime evaluation")
		}'''
after_block = '''		// A failed transport may retain reconnect backoff after its endpoint returns.
		// Wake that backoff without closing a channel shared by active RPCs.
		// Caller cancellation and server-returned Unavailable on Ready channels
		// do not trigger recovery or expand the existing retry budget.
		if status.Code(err) == codes.Unavailable && ctx.Err() == nil && conn.GetState() == connectivity.TransientFailure {
			conn.ResetConnectBackoff()
			localRecoveryLog.Debug("Resetting unavailable pooled remote actor reconnect backoff in isolated concurrency evaluation")
		}'''
if after.count(before_block) != 1:
    raise RuntimeError("Unexpected retained error block")
after = after.replace(before_block, after_block).replace('"google.golang.org/grpc/codes"', '"google.golang.org/grpc/codes"\n\t"google.golang.org/grpc/connectivity"')
after = after.replace('// Destroy the connection and force a re-connection on the next attempt', '// Retry within the existing actor-not-found policy')
router.write_text(after)
(packet / "candidate-router.go.source").write_bytes(router.read_bytes())
(packet / "correction-from-retained.patch").write_text(''.join(difflib.unified_diff(before.splitlines(True), after.splitlines(True), fromfile="a/pkg/actors/router/router.go", tofile="b/pkg/actors/router/router.go")))
upstream = (packet / "upstream-router.go.source").read_text()
(packet / "candidate-from-upstream.patch").write_text(''.join(difflib.unified_diff(upstream.splitlines(True), after.splitlines(True), fromfile="a/pkg/actors/router/router.go", tofile="b/pkg/actors/router/router.go")))

# Preserve the old mechanism test and change its correction-specific observation.
test = source / "pkg/actors/router/local_recovery_test.go"
(packet / "retained-local-recovery-test.go.source").write_bytes(test.read_bytes())
old = test.read_text()
new = old.replace('require.Equal(t, connectivity.Shutdown, conn.GetState(), "failed channel must be evicted, not released with backoff intact")', '''require.Equal(t, connectivity.Ready, conn.GetState(), "failed transport backoff wakes without closing the shared channel")
	next, teardown, getErr := mgr.GetGRPCConnection(ctx, address, "target", "default")
	require.NoError(t, getErr)
	defer teardown(true)
	require.Same(t, conn, next)''').replace('bounded actor retry recovered on unchanged endpoint after eviction', 'bounded actor retry recovered on unchanged endpoint after in-place backoff reset')
if new == old:
    raise RuntimeError("Expected correction-specific regression assertion missing")
test.write_text(new)
(packet / "recovery-test-correction.patch").write_text(''.join(difflib.unified_diff(old.splitlines(True), new.splitlines(True), fromfile="a/local_recovery_test.go", tofile="b/local_recovery_test.go")))

dependencies = packet / "pinned-sources"
dependencies.mkdir()
for name in ("manager.go", "pool.go", "remote_pool.go"):
    path = source / "pkg/api/grpc/manager" / name
    if path.exists():
        shutil.copyfile(path, dependencies / (name + ".source"))
grpc = Path(acquisition["gopath"]) / "pkg/mod/google.golang.org/grpc@v1.83.2"
for name in ("clientconn.go", "backoff.go"):
    if (grpc / name).exists():
        shutil.copyfile(grpc / name, dependencies / ("grpc-1.83.2-" + name + ".source"))
print(json.dumps({"source": str(source), "correction": "state-gated reconnect-backoff reset; no channel close"}))
