"""Verify authorized sanitized-capture lineage without exposing header values."""
import pathlib,json,hashlib
root=pathlib.Path(__file__).resolve().parents[5]
dir=root/'_bmad-output/implementation-artifacts/evidence/story-6-6/otlp-capture-remediation-2026-10-09'
d=json.loads((dir/'disposition.json').read_text())
manifest=json.loads((root/'_bmad-output/implementation-artifacts/evidence/story-5-3-retired-captures.json').read_text())
assert len(d['captures'])==2
sha=lambda data:hashlib.sha256(data).hexdigest()
for entry in d['captures']:
 original=(root/entry['path']).read_bytes(); replacement=(root/entry['replacementPath']).read_bytes()
 assert sha(original)==entry['sha256'] and sha(replacement)==entry['replacementSha256']
 parts=entry['jsonPointer'].strip('/').split('/');data=json.loads(original)
 for part in parts:data=data[int(part)] if isinstance(data,list) else data[part]
 assert isinstance(data,str) and data.lower().startswith('x-otlp-api-key=')
 old=json.dumps(data,ensure_ascii=False).encode();new=json.dumps(data.split('=',1)[0]+'=[redacted]',ensure_ascii=False).encode()
 assert original.count(old)==1 and original.replace(old,new,1)==replacement
 retired=[e for e in manifest['retired'] if e['path']==entry['path']]
 assert len(retired)==1 and retired[0]==entry and entry['status']=='retired' and not entry['authoritative']
 assert entry['authorizationRecord']==str((dir/'disposition.json').relative_to(root))
 print(entry['path']+': original seal preserved; one header value replaced; retirement lineage verified')
print('PASS: two exact authorized replacements; frozen approval artifacts were not mutated.')
