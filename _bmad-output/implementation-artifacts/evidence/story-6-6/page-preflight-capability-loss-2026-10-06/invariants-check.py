import hashlib,json,re,subprocess
from pathlib import Path
p=Path('_bmad-output/implementation-artifacts/spec-6-6-event-versioning-and-upcasting-implementation.md')
a=p.read_text(); b=subprocess.check_output(['git','show','HEAD:'+str(p)],text=True)
frozen=lambda t:re.search(r'<frozen-after-approval[\s\S]*?</frozen-after-approval>',t).group()
assert frozen(a)==frozen(b)
assert "status: 'in-progress'" in a
assert "baseline_commit: '1329b35e52852952ecb2c94aabf100674e9691e3'" in a
assert len(re.findall(r'^- \[ \].* — M[1-8]:',a,re.M))==8
assert not re.search(r'^- \[[xX]\].* — M[1-8]:',a,re.M)
print(json.dumps({'result':'passed','frozen_sha256':hashlib.sha256(frozen(a).encode()).hexdigest(),'baseline_commit':'1329b35e52852952ecb2c94aabf100674e9691e3','status':'in-progress','open_parent_tasks':8},sort_keys=True))
