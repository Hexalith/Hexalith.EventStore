#!/usr/bin/env python3
import collections,datetime,json,pathlib
counts=collections.Counter(); total=0;failed=0
for proc in pathlib.Path('/proc').glob('[0-9]*'):
 try:name=(proc/'comm').read_text().strip()
 except OSError:continue
 for fd in (proc/'fdinfo').glob('*'):
  try:n=sum(line.startswith('inotify wd:') for line in fd.read_text().splitlines())
  except OSError:failed+=1;continue
  total+=n;counts[name]+=n
print(json.dumps({'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'ceiling':int(pathlib.Path('/proc/sys/fs/inotify/max_user_watches').read_text()),'accessibleWatchLinesPerFd':total,'perProcessName':{k:v for k,v in counts.items() if v},'failedOrRacedReads':failed,'overcountCaveat':'Inherited/shared instances may be counted multiple times. No unrelated process changed.','restoreTo':1048576,'restoreBlockedByObservedUse':total>1048576},indent=2))
