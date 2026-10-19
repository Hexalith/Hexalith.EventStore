import json,re,subprocess,sys
from pathlib import Path
base=Path(__file__).resolve().parent
record=base/'record-command.py'
names=('qualification','published_qualification','published_executor','snapshot_registration','isolation','isolation_hardening')
commands=[(name,['python3','-m','unittest','discover','-s','tools/tests','-p','test_p1r_'+name+'.py']) for name in names]
commands.append(('isolation_hardening_optimized',['python3','-O','-m','unittest','discover','-s','tools/tests','-p','test_p1r_isolation_hardening.py']))
rows=[]
for name,argv in commands:
    target=base/(name+'-attempt1.json')
    code=subprocess.run(['python3',str(record),str(target),*argv]).returncode
    text=target.with_suffix('.log').read_text()
    count=re.findall(r'^Ran (\d+) tests? in ',text,re.M)
    row={'name':name,'record':str(target),'exit_code':code,'tests':int(count[-1]) if count else None,
         'skips':'skipped=' in text,'ok':text.rstrip().endswith('OK')}
    rows.append(row)
    (base/'seven-suites.json').write_text(json.dumps(rows,indent=2)+'\n')
    print(json.dumps(row),flush=True)
raise SystemExit(0 if all(row['exit_code']==0 and row['ok'] and not row['skips'] for row in rows) else 2)
