#!/usr/bin/env python3
"""Run each SDK regression assembly separately and preserve every exit status."""
import json
from pathlib import Path
import subprocess
import sys

packet = Path(__file__).resolve().parent
root = packet.parents[3]
lanes = [('testing-full', 'Testing.Tests', []),
         ('public-throttling-health', 'IntegrationTests', ['-class', 'Hexalith.EventStore.IntegrationTests.EventStore.RateLimitingIntegrationTests', '-class', 'Hexalith.EventStore.IntegrationTests.EventStore.PerTenantRateLimitingTests', '-class', 'Hexalith.EventStore.IntegrationTests.EventStore.PerConsumerRateLimitingTests']),
         ('client-full', 'Client.Tests', []),
         ('domain-full', 'DomainService.Tests', []),
         ('server-full', 'Server.Tests', []),
         ('contracts-full', 'Contracts.Tests', []),
         ('apphost-full', 'AppHost.Tests', []),
         ('testing-integration-full', 'Testing.Integration.Tests', []),
         ('sample-full', 'Sample.Tests', []),
         ('signalr-full', 'SignalR.Tests', []),
         ('generators-full', 'RestApi.Generators.Tests', []),
         ('query-routing-full', 'QueryRouting.Tests', []),
         ('admin-abstractions-full', 'Admin.Abstractions.Tests', []),
         ('admin-server-full', 'Admin.Server.Tests', []),
         ('admin-host-full', 'Admin.Server.Host.Tests', []),
         ('operations-full', 'Operations.Tests', [])]
outcomes = []
for label, suffix, filters in lanes:
    name = 'Hexalith.EventStore.' + suffix
    argv = ['dotnet', str(root / 'tests' / name / 'bin/Debug/net10.0' / (name + '.dll')), *filters, '-result-xml', str(packet / (label + '.xml'))]
    result = subprocess.run([sys.executable, str(packet / 'run.py'), label, *argv], cwd=root)
    outcomes.append({'label': label, 'exit_code': result.returncode})
    (packet / 'regression-progress.json').write_text(json.dumps(outcomes, indent=2) + '\n')
sys.exit(1 if any(x['exit_code'] for x in outcomes) else 0)
