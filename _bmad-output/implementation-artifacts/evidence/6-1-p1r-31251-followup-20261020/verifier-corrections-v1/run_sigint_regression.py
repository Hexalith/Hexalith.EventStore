"""Run the unchanged SIGINT unit test, preserving its otherwise temporary packet."""
from pathlib import Path
import datetime
import json
import sys
import tempfile
import unittest
import uuid
from unittest import mock
ROOT=Path(__file__).resolve().parents[5]
BASE=Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT/'tools/tests'))
import test_p1r_qualification as tests
OUT=BASE/('sigint-regression-'+uuid.uuid4().hex)
OUT.mkdir()
original=tempfile.TemporaryDirectory
class RetainTemporary:
 def __init__(self,**kwargs):
  self.path=tempfile.mkdtemp(dir=OUT,**kwargs)
 def __enter__(self): return self.path
 def __exit__(self,*args): return False
result_file=OUT/'result.json'
record={'scope':'unchanged single SIGINT process cleanup regression; temporary packet retained for diagnosis', 'argv':sys.argv,'started_utc':datetime.datetime.now(datetime.timezone.utc).isoformat()}
with (OUT/'stdout').open('w') as out, (OUT/'stderr').open('w') as err:
 with mock.patch.object(tests.tempfile,'TemporaryDirectory',RetainTemporary):
  result=unittest.TextTestRunner(stream=err,verbosity=2).run(unittest.TestSuite([tests.SourceAndProcessTests('testExternalSigintPreservesReceiptAndCleansActualDescendants')]))
record.update(passed=result.wasSuccessful(),failures=len(result.failures),errors=len(result.errors),finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
result_file.write_text(json.dumps(record,indent=2)+'\n')
print(json.dumps({'out':str(OUT),**record}))
raise SystemExit(0 if result.wasSuccessful() else 1)
