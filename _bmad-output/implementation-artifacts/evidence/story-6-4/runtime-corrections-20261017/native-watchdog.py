"""Preserve the historical finite outer boundary without changing native assertions."""
import datetime, json, os, pathlib, signal, sys, time

pid, limit, drain = map(int, sys.argv[1:4])
target = pathlib.Path(sys.argv[4])
assert not target.exists()
stat = pathlib.Path('/proc')/str(pid)/'stat'
def state():
    try:
        raw = stat.read_text(); fields = raw[raw.rindex(')')+2:].split()
        return fields[19], fields[0]
    except OSError: return None
initial = state(); assert initial is not None
ticks = initial[0]
frequency = os.sysconf('SC_CLK_TCK')
def elapsed(): return float(pathlib.Path('/proc/uptime').read_text().split()[0])-int(ticks)/frequency
receipt = {'pid':pid,'startTicks':ticks,'limitSeconds':limit,'drainSeconds':drain,
           'attachedAt':datetime.datetime.now(datetime.timezone.utc).isoformat(),'elapsedAtAttach':elapsed(),'timedOut':False}
while True:
    current = state()
    if current is None or current[0] != ticks or current[1] == 'Z': break
    remaining = limit-elapsed()
    if remaining <= 0:
        receipt['timedOut'] = True
        receipt['timeoutAt'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        receipt['elapsedAtTimeout'] = elapsed()
        # This native invocation created its own session; verify both identity and ownership.
        assert os.getpgid(pid) == pid
        os.killpg(pid, signal.SIGINT)
        until = time.monotonic()+drain
        while time.monotonic()<until:
            current = state()
            if current is None or current[0] != ticks or current[1] == 'Z': break
            time.sleep(.2)
        current = state()
        if current is not None and current[0] == ticks and current[1] != 'Z':
            os.killpg(pid,signal.SIGKILL)
            receipt['killedAfterDrain'] = True
        break
    time.sleep(min(5,remaining))
receipt['endedAt'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
receipt['elapsedAtEnd'] = elapsed()
target.write_text(json.dumps(receipt,indent=2)+'\n')
print(json.dumps(receipt),flush=True)
