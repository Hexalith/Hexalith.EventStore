#!/usr/bin/env python3
"""Register the local diagnostic logger before runtime options apply; retain a new candidate identity."""
import difflib
import hashlib
import json
from pathlib import Path

packet = Path(__file__).resolve().parent
work = Path(json.loads((packet / 'runtime-acquisition.json').read_text())['workDirectory'])
source = work / 'dapr-b32a815fa2260983e181b95272ea555fe17d3895'
router = source / 'pkg/actors/router/router.go'
before = router.read_text()
assert hashlib.sha256(router.read_bytes()).hexdigest() == json.loads((packet / 'runtime-patch-identity.json').read_text())['afterSha256']
after = before.replace('type Interface interface {', 'var localRecoveryLog = logger.NewLogger("dapr.runtime.actors.router.local-recovery")\n\ntype Interface interface {', 1)
after = after.replace('logger.NewLogger("dapr.runtime.actors.router.local-recovery").Debug(', 'localRecoveryLog.Debug(')
router.write_text(after)
original = (packet.parent / 'runs-2026-10-21-restart-trace/upstream/router.go').read_text()
(packet / 'final-runtime-recovery.patch').write_text(''.join(difflib.unified_diff(original.splitlines(True), after.splitlines(True), fromfile='a/pkg/actors/router/router.go', tofile='b/pkg/actors/router/router.go')))
(packet / 'router-final-candidate.go.source').write_text(after)
(packet / 'final-runtime-patch-identity.json').write_text(json.dumps({'revision':'b32a815fa2260983e181b95272ea555fe17d3895','originalSha256':hashlib.sha256(original.encode()).hexdigest(),'beforeDiagnosticCorrectionSha256':hashlib.sha256(before.encode()).hexdigest(),'afterSha256':hashlib.sha256(after.encode()).hexdigest(),'patchSha256':hashlib.sha256((packet/'final-runtime-recovery.patch').read_bytes()).hexdigest(),'change':'identical eviction logic; register logger at package initialization so --log-level applies'},indent=2)+'\n')
