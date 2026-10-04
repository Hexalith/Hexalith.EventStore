from pathlib import Path
import runpy
p=Path('/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/6-5d-simplification/verify.py')
n=runpy.run_path(str(p)); db=n['newdb']()
events=[{'sequence':10,'message':'m0','digest':n['hashbytes'](b'0')}]
db.external('drain',n['drain_fixture'](events,'success-events'),'legacy')
n['capsule_make'](db,events,cleanup=True);n['begin_resume'](db);n['finish_resume'](db);n['legacy_transition'](db,'execution','claimed',1)
address=n['provider_address']('live-drain','execution');original=db.external

def interrupt(k,b,owner):
    result=original(k,b,owner)
    if k==address: raise n['Crash']('after restored drain')
    return result

db.external=interrupt
try:n['legacy_transition'](db,'execution','draining',1)
except n['Crash']:pass
db=db.restart();d=n['getcontrol'](db,'execution','execution')
print('restore interruption:',d['phase'],d['intent']['kind'],d['legacy']['state'],'exact source present:',db.read(address,'execution') is not None)
for attempt in range(2):
    before=db.snapshot()
    try:n['legacy_transition'](db,'execution','draining',1)
    except n['Refusal'] as e:print('restore retry',attempt+1,':',str(e),'unchanged:',db.snapshot()==before)
try:n['begin_resume'](db)
except n['Refusal'] as e:print('fresh resume:',str(e))
