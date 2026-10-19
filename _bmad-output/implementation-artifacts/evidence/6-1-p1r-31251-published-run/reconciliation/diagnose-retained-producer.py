#!/usr/bin/env python3
"""Separate corrected reader over immutable old receipts; never qualification."""
import argparse
import json
from pathlib import Path
import sys
import uuid

p = argparse.ArgumentParser(description=__doc__)
for name in ('reader-root','reader-source','execution','inputs','out'):
    p.add_argument('--'+name,type=Path,required=True)
a = p.parse_args()
a.out.mkdir(parents=True,exist_ok=False)
(a.out/'receipts').mkdir()
sys.path.insert(0,str(a.reader_root/'tools'))
import p1r_qualification as preparation
import p1r_published_qualification as qualification

result = {'scope':'nonqualifying diagnostic with separate corrected reader and unchanged original producer binding',
          'qualified':False,'p1r_usable':False,'started_utc':preparation.stamp(),'finished_utc':None,
          'invocation':uuid.uuid4().hex,'reader_root':str(a.reader_root),'execution':str(a.execution),
          'packages':{},'receipts':[],'errors':[]}
script = Path(__file__).resolve()
result['script_sha256'] = preparation.digest(preparation.regular(script))
def save():
    preparation.write_json(a.out/'diagnostic.json',result)
save()
try:
    preparation.require(preparation.ROOT == a.reader_root.resolve(),'reader import root differs')
    reader = preparation.source_binding(a.reader_root,a.reader_source)
    preparation.write_json(a.out/'reader-source-binding.json',reader)
    result['reader_binding_sha256'] = preparation.digest(preparation.canonical(reader))
    producer_raw = preparation.regular(a.execution/'executor-source.json')
    producer = qualification.parse_json(producer_raw)
    result['producer_binding'] = {'path':str(a.execution/'executor-source.json'),'sha256':preparation.digest(producer_raw)}
    preparation.require(producer['repository'] != reader['repository'],'diagnostic must use a separate reader')
    preparation.check_current_source(producer,check_python=False)
    result['producer_source_check'] = {'physical_sources':'unchanged','python_check':False,
                                     'reason':'host diagnostic reader differs from recorded private producer interpreter',
                                     'producer_python':producer['python'],'reader_python':reader['python']}
    inputs,raw,scope = qualification.load_document(a.inputs,qualification.validate_inputs)
    qualification.validate_candidate_source(producer,inputs)
    execution = qualification.parse_json(preparation.regular(a.execution/'execution.json'))
    preparation.require(execution['errors'] == [] and execution['finished_utc']
                        and execution['executor_source_sha256'] == preparation.digest(producer_raw)
                        and execution['inputs_sha256'] == preparation.digest(raw),'old producer execution differs')
    context = {'inputs':{'value':inputs,'scope':scope,'sha256':preparation.digest(raw)},
               'source_binding':producer,'verified':{}}
    for role in ('candidate',*('comparison-'+v for v in qualification.COMPARISON_VERSIONS)):
        row,verified = qualification.execute_package_lane(a.out,result['invocation'],role,
            a.execution/'consumers'/role,context['inputs'],qualification.default_verifier)
        result['packages'][role] = row
        preparation.require(verified is not None,'diagnostic package reobservation failed: '+role)
        context['verified'][role] = verified
        save()
    paths = execution['receipts']
    preparation.require(len(paths) == len(set(paths)),'duplicate old receipt path')
    for name in paths:
        path = Path(name)
        preparation.require(path.parent == a.execution/'receipts','old receipt escaped execution')
        raw_receipt = preparation.regular(path)
        receipt = qualification.parse_json(raw_receipt)
        row = {'path':str(path),'sha256':preparation.digest(raw_receipt),'schema':receipt.get('schema'),
               'lane':receipt.get('lane',receipt.get('role','container-cleanup')),'accepted_by_diagnostic_reader':False}
        try:
            group,lane,outcome = qualification.receipt_outcome(receipt,context)
            row.update(group=group,lane=lane,outcome=outcome,accepted_by_diagnostic_reader=True)
        except Exception as error:
            row['error'] = {'type':type(error).__name__,'message':str(error)}
        result['receipts'].append(row)
        save()
    lanes = {row['lane'] for row in result['receipts'] if row.get('group') in ('scenarios','additions')}
    recovery = {row['lane'] for row in result['receipts'] if row.get('group') == 'recovery'}
    result['expected_lane_inventory'] = list(qualification.executed_case_inventory(inputs))
    result['all_19_lanes_readable'] = lanes == set(result['expected_lane_inventory'])
    result['both_recovery_receipts_readable'] = recovery == {'candidate-restore','container-cleanup'}
    preparation.check_current_source(producer,check_python=False)
    preparation.check_current_source(reader)
    preparation.require(preparation.regular(a.execution/'executor-source.json') == producer_raw,'producer binding bytes changed')
    preparation.require(preparation.digest(preparation.regular(script)) == result['script_sha256'],'diagnostic script changed')
    result['producer_and_reader_rechecked_after_all_receipts'] = True
except Exception as error:
    result['errors'].append({'type':type(error).__name__,'message':str(error)})
finally:
    result['finished_utc'] = preparation.stamp()
    save()
print(json.dumps({key:result.get(key) for key in ('errors','all_19_lanes_readable','both_recovery_receipts_readable','qualified','p1r_usable')}))
raise SystemExit(0 if not result['errors'] and result.get('all_19_lanes_readable') and result.get('both_recovery_receipts_readable') else 2)
