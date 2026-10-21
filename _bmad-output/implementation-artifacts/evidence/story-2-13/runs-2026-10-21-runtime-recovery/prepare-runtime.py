#!/usr/bin/env python3
"""Apply only the retained local candidate patch to the hash-identified disposable source."""
import difflib
import hashlib
import json
from pathlib import Path

packet = Path(__file__).resolve().parent
work = Path(json.loads((packet / 'runtime-acquisition.json').read_text())['workDirectory'])
source = work / 'dapr-b32a815fa2260983e181b95272ea555fe17d3895'
router = source / 'pkg/actors/router/router.go'
before = router.read_text()
assert hashlib.sha256(router.read_bytes()).hexdigest() == hashlib.sha256((packet.parent / 'runs-2026-10-21-restart-trace/upstream/router.go').read_bytes()).hexdigest()
start = before.index('func (r *router) callRemoteActor(')
end = before.index('\nfunc (r *router) callRemoteActorReminder', start)
method = before[start:end]
assert method.count('defer cancel(false)') == 1
method = method.replace('defer cancel(false)', '''destroyConnection := false
\tdefer func() { cancel(destroyConnection) }()''')
method = method.replace('''\tif err != nil {
\t\treturn nil, err
\t}

\tif len(res.GetHeaders()''', '''\tif err != nil {
\t\t// Drop the failed channel rather than retaining its reconnect backoff.
\t\t// Caller cancellation never triggers eviction or expands the retry budget.
\t\tdestroyConnection = status.Code(err) == codes.Unavailable && ctx.Err() == nil
\t\tif destroyConnection {
\t\t\tlogger.NewLogger("dapr.runtime.actors.router.local-recovery").Debug("Evicting unavailable pooled remote actor channel in isolated runtime evaluation")
\t\t}
\t\treturn nil, err
\t}

\tif len(res.GetHeaders()''')
assert 'destroyConnection = status.Code(err)' in method
after = before[:start] + method + before[end:]
after = after.replace('"github.com/dapr/dapr/pkg/resiliency"', '"github.com/dapr/dapr/pkg/resiliency"\n\t"github.com/dapr/kit/logger"')
router.write_text(after)
(packet / 'runtime-recovery.patch').write_text(''.join(difflib.unified_diff(before.splitlines(True), after.splitlines(True), fromfile='a/pkg/actors/router/router.go', tofile='b/pkg/actors/router/router.go')))
(packet / 'router-candidate.go.source').write_text(after)
(packet / 'runtime-patch-identity.json').write_text(json.dumps({'revision':'b32a815fa2260983e181b95272ea555fe17d3895','path':'pkg/actors/router/router.go','beforeSha256':hashlib.sha256(before.encode()).hexdigest(),'afterSha256':hashlib.sha256(after.encode()).hexdigest(),'patchSha256':hashlib.sha256((packet/'runtime-recovery.patch').read_bytes()).hexdigest(),'scope':'ordinary remote actor invocations only; reminder/stream and global backoff defaults unchanged'},indent=2)+'\n')
