#!/usr/bin/env python3
"""Observe the inotify ceiling without changing it or any process."""
import datetime
import json
from pathlib import Path
import time

out = Path(__file__).resolve().with_name('ceiling-events.jsonl')
def utc():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()
def ceiling():
    return int(Path('/proc/sys/fs/inotify/max_user_watches').read_text())
with out.open('x') as stream:
    def record(event, value):
        row = {'utc': utc(), 'event': event, 'ceiling': value}
        stream.write(json.dumps(row) + '\n')
        stream.flush()
        print(json.dumps(row), flush=True)
    previous = ceiling()
    record('start', previous)
    started = time.monotonic()
    reason = 'bounded-duration-complete'
    try:
        while time.monotonic() - started < 1200:
            time.sleep(0.5)
            current = ceiling()
            if current != previous:
                record('observed-change-origin-unknown', current)
                previous = current
    except KeyboardInterrupt:
        reason = 'owned-observer-stopped'
    record(reason, ceiling())
