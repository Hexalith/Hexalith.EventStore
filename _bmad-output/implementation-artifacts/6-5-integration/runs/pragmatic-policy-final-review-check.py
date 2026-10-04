from pathlib import Path
from unittest.mock import patch
import importlib.util
import sys
sys.dont_write_bytecode=True
here=Path('_bmad-output/implementation-artifacts/6-5-integration').resolve()
def load(name,path):
    spec=importlib.util.spec_from_file_location(name,path)
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
    return module
b=load('policy_generator',here/'build-integration.py')
v=load('policy_verifier',here/'verify.py')
doc=v.normative_text(v.DOC.read_bytes())
ledger_path=v.ART/'deferred-work.md'
ledger=ledger_path.read_text()
original=v.dispositions(doc)
assert original['obligations']==20 and original['openImplementationFollowups']==47
append='\n- source_spec: `unrelated-owner.md`\n  summary: Unrelated owner follow-up.\n  evidence: Unrelated progress.\n  status: open — proposed owner follow-up\n'
changed=(ledger+append).replace('  status: open\n','  status: resolved by unrelated owner progress\n',1)
assert changed!=ledger+append
again,untouched=b.owner_policy(doc,changed)
assert untouched==changed and b.record_owner_approval(again)==doc
original_read_text=Path.read_text
def read_text(path,*args,**kwargs):
    return changed if path==ledger_path else original_read_text(path,*args,**kwargs)
with patch.object(Path,'read_text',read_text):assert v.dispositions(doc)==original
old_doc=(Path('/tmp/story-6-5-pragmatic-_yzds0jh')/'_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md').read_text()
old_adapter=old_doc.split('<!-- imported-adapter-contract-start -->\n',1)[1].split('\n<!-- imported-adapter-contract-end -->',1)[0]
new_adapter=(here/'metadata-adapter-contract.md').read_text()
expected=old_adapter.replace('AD-26 production-profile approval, its actual database/runtime pins, two-host evidence and §12 AD-13 approval remain separate and unapproved.',
    'AD-26 production-profile approval remains pending; its actual database/runtime pins and two-host qualification evidence remain unproved. The separate §12 AD-13 conversational owner approval is recorded.')
assert expected==new_adapter
assert doc.split('<!-- imported-adapter-contract-start -->\n',1)[1].split('\n<!-- imported-adapter-contract-end -->',1)[0]==new_adapter
assert 'including AD-26’s separate production-profile approval and qualification requirements' in doc
print('passed: full disposition check permits unrelated ledger append/edit, generator preserves unrelated proposed status, all 20 obligations remain checked, adapter technical body unchanged, profile approval remains separate')
