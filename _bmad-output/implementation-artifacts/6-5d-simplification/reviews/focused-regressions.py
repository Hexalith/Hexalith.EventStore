#!/usr/bin/env python3
"""Seven bounded regressions with exact named owning failures."""
import json
import subprocess
import tempfile
from pathlib import Path

HERE=Path(__file__).resolve().parents[1]
CHANGES={
    'signed foreign held identity':(' and f[3]==held_identity(d)','', 'AssertionError: expected owning refusal: redrive-claim-owner'),
    'legacy recovery owner':("need(legacy['owner']==owner,'legacy-owner');old=legacy['state']","old=legacy['state']", "AssertionError: expected owning refusal: legacy-owner"),
    'send terminal exclusion':("\n    need(route_authority(db,d,key)!='terminal','terminal-no-send')",'', 'AssertionError: expected owning refusal: terminal-no-send'),
    'accepted dispatch exclusion':("return [r['message'] for r in members if r['position'] not in d['accepted']]", "return [r['message'] for r in members]", 'AssertionError: accepted-dispatch-exclusion'),
    'original-route completion':("need(a==canonical({'carrier':d['carrier'],'outcome':'terminal'}),'route-terminal-readback')", 'pass', 'AssertionError: expected owning refusal: route-terminal-readback'),
    'refund delete readback':("for address in c['objects']:need(db.read(address) is None,'delete-readback')", 'for address in c[\'objects\']:pass', 'AssertionError: expected owning refusal: delete-readback'),
    'closed legacy failure reasons':("need(evidence['reason'] in classes,'legacy-failure');derived=classes[evidence['reason']]", "derived=classes.get(evidence['reason'],'transport-retryable')", 'AssertionError: expected owning refusal: legacy-failure'),
}

def main():
    source=(HERE/'verify.py').read_text();rows=[]
    with tempfile.TemporaryDirectory(prefix='6-5d-focused-regressions-') as directory:
        target=Path(directory)
        for name in ('known-answers.json','obligations.md'):
            (target/name).write_bytes((HERE/name).read_bytes())
        path=target/'verify.py';path.write_text(source)
        control=subprocess.run(['python3',str(path)],capture_output=True,text=True)
        assert control.returncode==0,control.stdout+control.stderr
        for label,(old,new,expected) in CHANGES.items():
            assert source.count(old)==1,(label,'anchor changed')
            path.write_text(source.replace(old,new,1))
            result=subprocess.run(['python3',str(path)],capture_output=True,text=True)
            assert result.returncode!=0,(label,'demonstrated removal still passes')
            assert 'SyntaxError' not in result.stderr,result.stderr
            observed=result.stderr.strip().splitlines()[-1]
            assert observed==expected,(label,'wrong owning failure',observed,expected)
            rows.append({'case':label,'exitCode':result.returncode,'owningFailure':observed,'expectedOwningFailure':expected})
    print(json.dumps({'result':'passed','rejectedRegressions':len(rows),'cases':rows},indent=2))

if __name__=='__main__':main()
