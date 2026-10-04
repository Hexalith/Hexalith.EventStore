from pathlib import Path
from unittest.mock import patch
import importlib.util
import json
import re
import sys

sys.dont_write_bytecode=True
here=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('integration_verifier',here/'verify.py')
v=importlib.util.module_from_spec(spec)
spec.loader.exec_module(v)
doc=v.normative_text(v.DOC.read_bytes())
execution_path=v.ART/'spec-6-5-event-versioning-and-upcasting-spec.md'
execution=execution_path.read_text()
actual=v.dispositions(doc)
assert actual['pass1Findings']==59
original_read_text=Path.read_text

def with_execution(text):
    def read_text(path,*args,**kwargs):
        return text if path==execution_path else original_read_text(path,*args,**kwargs)
    with patch.object(Path,'read_text',read_text):
        return v.dispositions(doc)

current_rows=re.findall(r'^\| (?:BH-R|EC-R|VG-R)[^ |]+ \|.*\n',execution,re.M)
assert current_rows, 'missing current-review collision fixture'
without_current=re.sub(r'^\| (?:BH-R|EC-R|VG-R)[^ |]+ \|.*\n','',execution,flags=re.M)
assert with_execution(without_current)==actual
with_extra=execution+'\n| BH-R-extra | current review row | patch |\n| EC-R-extra | current review row | patch |\n| VG-R-extra | current review row | patch |\n'
assert with_execution(with_extra)==actual
historical_row=next(line for line in execution.splitlines(keepends=True) if line.startswith('| VG-1 |'))
try:
    with_execution(execution.replace(historical_row,'',1))
except v.Refusal as error:
    assert str(error)=='raw-pass1-dispositions', str(error)
else:
    raise AssertionError('removed historical row survived')
print(json.dumps({'result':'passed','actualCurrentExecution':actual,
    'currentReviewRowsIgnored':len(current_rows),'additionalCurrentRowsIgnored':3,
    'removedHistoricalRow':{'id':'VG-1','owningFailure':'raw-pass1-dispositions','result':'rejected'},
    'currentPins':v.current_pins(doc),'normativeDigest':v.approval(v.DOC.read_bytes()),
    'authority':'UNAPPROVED; Story 6.5 in progress; Story 6.6 unauthorized'},sort_keys=True))
