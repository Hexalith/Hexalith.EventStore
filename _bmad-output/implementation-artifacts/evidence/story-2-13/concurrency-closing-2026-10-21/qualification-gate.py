#!/usr/bin/env python3
"""Report the retained required application results without exempting failures."""
import json
from pathlib import Path
import sys
p = Path(__file__).resolve().parent.parent / 'runs-2026-10-21-concurrency-retirement'
names = ['retirement-final-focused-controls', 'retirement-runtime-build', 'retirement-final-focused-reused-endpoint',
         'retirement-final-complete-distribution-class', 'retirement-final-go-regressions',
         'retirement-focused-application-regressions', 'retirement-real-projection-regression',
         'retirement-ordinary-direct-regression', 'retirement-retained-redis-regression']
results = {name: json.loads((p / (name + '.json')).read_text())['exitCode'] for name in names}
failed = [name for name, code in results.items() if code != 0]
identity_drift = [name for name in names if (lambda d: d['binariesBefore'] != d['binariesAfter'])(json.loads((p / (name + '.json')).read_text()))]
if identity_drift:
    failed.append('binary-identity-ambiguity')
print(json.dumps({'qualification': 'incomplete' if failed else 'local-results-passed-with-other-gates-still-open',
                  'requiredCommands': results, 'failed': failed, 'identityDrift': identity_drift, 'acceptedExceptions': [],
                  'independentContractsBlocker': 'Retained historical failure; not rerun or exempted.',
                  'authority': 'No completion or production authority.'}, indent=2))
sys.exit(1 if failed else 0)
