#!/usr/bin/env python3
"""Evaluate Dapr-owned retirement without closing leases held by other RPCs."""
from pathlib import Path
import difflib
import json
import tarfile

packet = Path(__file__).resolve().parent
a = json.loads((packet / "runtime-acquisition.json").read_text())
source = Path(a["sourceDirectory"])
names = ["pkg/actors/router/router.go", "pkg/api/grpc/manager/manager.go", "pkg/api/grpc/manager/pool.go"]
before = {name: (Path(a["retainedSourceDirectory"]) / name).read_text() for name in names}
router = before[names[0]]
router = router.replace('"google.golang.org/grpc/codes"', '"google.golang.org/grpc/codes"\n\t"google.golang.org/grpc/connectivity"')
router = router.replace('\tdestroyConnection := false\n\tdefer func() { cancel(destroyConnection) }()', '\tdefer cancel(false)')
old = '''		// Drop the failed channel rather than retaining its reconnect backoff.
		// Caller cancellation never triggers eviction or expands the retry budget.
		destroyConnection = status.Code(err) == codes.Unavailable && ctx.Err() == nil
		if destroyConnection {
			localRecoveryLog.Debug("Evicting unavailable pooled remote actor channel in isolated runtime evaluation")
		}'''
new = '''		// Retire failed transports from new leases while active callers drain.
		// The existing deferred release owns this invocation's lease exactly once.
		// Caller cancellation and Unavailable from a Ready server do not retire.
		if status.Code(err) == codes.Unavailable && ctx.Err() == nil && conn.GetState() == connectivity.TransientFailure {
			r.grpc.RetireGRPCConnection(lar.Address, conn)
			localRecoveryLog.Debug("Retiring unavailable pooled remote actor channel without interrupting active leases in isolated concurrency evaluation")
		}'''
if router.count(old) != 1:
    raise RuntimeError("Unexpected retained router")
router = router.replace(old, new).replace('// Destroy the connection and force a re-connection on the next attempt', '// Retry within the existing actor-not-found policy')
(source / names[0]).write_text(router)
manager = before[names[1]]
marker = 'func (g *Manager) connectRemote('
addition = '''// RetireGRPCConnection prevents new leases without interrupting active users.
// Retirement does not release the caller's lease; its normal teardown does so.
func (g *Manager) RetireGRPCConnection(address string, conn *grpc.ClientConn) {
	g.remoteConns.Retire(address, conn)
}

'''
if manager.count(marker) != 1:
    raise RuntimeError("Unexpected manager source")
(source / names[1]).write_text(manager.replace(marker, addition + marker))
pool = before[names[2]]
marker = '\tfor i := range len(p.connections) {\n'
if pool.count(marker) < 1:
    raise RuntimeError("Unexpected pool share loop")
pool = pool.replace(marker, marker + '''		if p.connections[i].retired {
			continue
		}
''', 1)
start = pool.index('func (p *ConnectionPool) Release(')
end = pool.index('// Destroy a connection', start)
replacement = '''func (p *ConnectionPool) Release(conn grpc.ClientConnInterface) {
	var closeConn grpc.ClientConnInterface
	p.lock.Lock()
	for i, el := range p.connections {
		if el.conn != conn {
			continue
		}
		count := atomic.AddInt32(&el.referenceCount, -1)
		if count <= 0 {
			if el.retired {
				closeConn = el.conn
				copy(p.connections[i:], p.connections[i+1:])
				p.connections[len(p.connections)-1] = nil
				p.connections = p.connections[:len(p.connections)-1]
			} else {
				el.MarkIdle()
			}
		}
		break
	}
	p.lock.Unlock()
	if closer, ok := closeConn.(interface{ Close() error }); ok {
		_ = closer.Close()
	}
}

// Retire excludes a connection from new leases and closes it after its final release.
// It never decrements references, including the lease owned by the retiring caller.
func (p *ConnectionPool) Retire(conn grpc.ClientConnInterface) {
	var closeConn grpc.ClientConnInterface
	p.lock.Lock()
	for i, el := range p.connections {
		if el.conn != conn {
			continue
		}
		el.retired = true
		if atomic.LoadInt32(&el.referenceCount) <= 0 {
			closeConn = el.conn
			copy(p.connections[i:], p.connections[i+1:])
			p.connections[len(p.connections)-1] = nil
			p.connections = p.connections[:len(p.connections)-1]
		}
		break
	}
	p.lock.Unlock()
	if closer, ok := closeConn.(interface{ Close() error }); ok {
		_ = closer.Close()
	}
}

'''
pool = pool[:start] + replacement + pool[end:]
pool = pool.replace('\treferenceCount int32\n', '\treferenceCount int32\n\tretired        bool // Protected by the owning pool lock.\n')
marker = '// Purge connections that have been idle for longer than maxConnIdle.\n// Note that this method should not be called by multiple goroutines at the same time.\nfunc (p *RemoteConnectionPool) Purge()'
addition = '''// Retire excludes a remote connection from new leases while active references drain.
func (p *RemoteConnectionPool) Retire(address string, conn grpc.ClientConnInterface) {
	item, ok := p.pool.Load(address)
	if ok {
		item.(*ConnectionPool).Retire(conn)
	}
}

'''
if pool.count(marker) != 1:
    raise RuntimeError("Unexpected remote-pool source")
pool = pool.replace(marker, addition + marker)
(source / names[2]).write_text(pool)

upstream = {}
with tarfile.open(a["sourceArchive"]["path"]) as tf:
    for name in names:
        archive_name = next(value for value in tf.getnames() if value.endswith('/' + name))
        upstream[name] = tf.extractfile(archive_name).read().decode()
for label, originals in (("correction-from-retained.patch", before), ("candidate-from-upstream.patch", upstream)):
    patch = ''.join(''.join(difflib.unified_diff(originals[name].splitlines(True), (source / name).read_text().splitlines(True), fromfile='a/' + name, tofile='b/' + name)) for name in names)
    (packet / label).write_text(patch)
for name in names:
    target = packet / "runtime-sources" / (name + '.source')
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes((source / name).read_bytes())
print(json.dumps({"source": str(source), "changedRuntimeFiles": names, "mechanism": "retire, skip new leases, drain active references, close after final release"}))
