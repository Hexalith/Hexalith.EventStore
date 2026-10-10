"""Finite predicate witnesses for independently recomputed executor checks.

Only the executor evaluates source expressions. Importers interpret bounded
comparison predicates and bind probe witnesses to retained literal outputs.
"""
import ast
import base64
import binascii
import inspect
from functools import lru_cache
from pathlib import Path

from p1r_qualification import canonical, digest, require


def value(node, frame):
    environment = dict(frame.f_globals, **frame.f_locals)
    return eval(compile(ast.Expression(node), '<executor-check-operand>', 'eval'), environment, environment)


def predicate(node, frame):
    if isinstance(node, ast.Compare):
        operands = [value(node.left, frame), *(value(item, frame) for item in node.comparators)]
        canonical(operands)
        return {'op': 'compare', 'operators': [type(item).__name__ for item in node.ops], 'operands': operands}
    if isinstance(node, ast.BoolOp):
        items = []
        for item in node.values:
            observed = predicate(item, frame)
            items.append(observed)
            if isinstance(node.op, ast.And) and not evaluate(observed) or isinstance(node.op, ast.Or) and evaluate(observed):
                break
        return {'op': type(node.op).__name__, 'items': items}
    if isinstance(node, ast.UnaryOp) and isinstance(node.op, ast.Not):
        return {'op': 'Not', 'item': predicate(node.operand, frame)}
    # Aggregate witnesses preserve each evaluated operand instead of a second
    # assertion total. Their Boolean result is independently recomputed.
    if isinstance(node, ast.Call) and isinstance(node.func, ast.Name) and node.func.id in ('all', 'any'):
        return {'op': node.func.id, 'items': list(value(node.args[0], frame))}
    if isinstance(node, ast.Call) and isinstance(node.func, ast.Name) and node.func.id == 'bool':
        operand = value(node.args[0], frame)
        return {'op': 'nonempty', 'size': len(operand)}
    return {'op': 'truth', 'value': bool(value(node, frame))}


@lru_cache(maxsize=64)
def source_tree(path, sha256):
    data = Path(path).read_bytes()
    require(digest(data) == sha256, "check witness source bytes changed")
    return ast.parse(data)


@lru_cache(maxsize=32)
def retained_tree(content):
    return ast.parse(content.encode())


def capture(identity):
    frame = inspect.currentframe().f_back.f_back
    path = Path(frame.f_code.co_filename).resolve()
    data = path.read_bytes()
    candidates = [node for node in ast.walk(source_tree(str(path), digest(data))) if isinstance(node, ast.Call)
                  and isinstance(node.func, ast.Attribute) and node.func.attr == 'check'
                  and len(node.args) >= 2 and node.lineno <= frame.f_lineno <= node.end_lineno]
    require(len(candidates) == 1, 'executed check witness source unavailable')
    call = candidates[0]
    return {'id': identity, 'kind': 'predicate', 'source': str(path), 'source_sha256': digest(data),
            'line': call.lineno, 'expression': ast.unparse(call.args[1]), 'predicate': predicate(call.args[1], frame)}


def evaluate(tree):
    require(isinstance(tree, dict), 'malformed check predicate witness')
    operation = tree.get('op')
    if operation == 'compare':
        comparisons = {'Eq': lambda a,b: a == b, 'NotEq': lambda a,b: a != b,
                       'Is': lambda a,b: type(a) is type(b) and a == b,
                       'IsNot': lambda a,b: type(a) is not type(b) or a != b,
                       'Lt': lambda a,b: a < b, 'LtE': lambda a,b: a <= b,
                       'Gt': lambda a,b: a > b, 'GtE': lambda a,b: a >= b,
                       'In': lambda a,b: a in b, 'NotIn': lambda a,b: a not in b}
        operands, operators = tree.get('operands'), tree.get('operators')
        require(isinstance(operands, list) and isinstance(operators, list)
                and len(operands) == len(operators) + 1 and operators
                and all(item in comparisons for item in operators), 'malformed comparison witness')
        return all(comparisons[operator](operands[index], operands[index+1]) for index,operator in enumerate(operators))
    if operation in ('And', 'Or'):
        results = [evaluate(item) for item in tree['items']]
        return all(results) if operation == 'And' else any(results)
    if operation == 'Not':
        return not evaluate(tree['item'])
    if operation in ('all', 'any'):
        require(isinstance(tree['items'], list) and all(type(item) is bool for item in tree['items']), 'invalid aggregate witness')
        return all(tree['items']) if operation == 'all' else any(tree['items'])
    if operation == 'nonempty':
        require(type(tree.get('size')) is int and tree['size'] >= 0, 'invalid size witness')
        return tree['size'] != 0
    require(operation == 'truth' and type(tree.get('value')) is bool, 'unknown predicate witness')
    return tree['value']


def validate_shape(node, tree):
    """Bind predicate operators and literal operands to the retained source."""
    if isinstance(node, ast.Compare):
        require(tree.get('op') == 'compare' and tree.get('operators') == [type(item).__name__ for item in node.ops],
                'predicate operators differ from retained source')
        operands = [node.left, *node.comparators]
        require(len(tree.get('operands',[])) == len(operands), 'predicate operand inventory differs')
        for operand, actual in zip(operands, tree['operands']):
            if isinstance(operand,ast.Constant):
                require(type(actual) is type(operand.value) and actual == operand.value, 'predicate literal differs from retained source')
    elif isinstance(node, ast.BoolOp):
        items = tree.get('items',[])
        require(tree.get('op') == type(node.op).__name__ and 0 < len(items) <= len(node.values), 'predicate Boolean structure differs')
        for value, item in zip(node.values,items):
            validate_shape(value,item)
        if len(items) < len(node.values):
            require(evaluate(items[-1]) is isinstance(node.op,ast.Or), 'predicate omits an evaluated Boolean operand')
    elif isinstance(node,ast.UnaryOp) and isinstance(node.op,ast.Not):
        require(tree.get('op') == 'Not','predicate negation differs')
        validate_shape(node.operand,tree['item'])
    elif isinstance(node,ast.Call) and isinstance(node.func,ast.Name) and node.func.id in ('all','any','bool'):
        require(tree.get('op') == ('nonempty' if node.func.id == 'bool' else node.func.id),'predicate aggregate differs')
    else:
        require(tree.get('op') == 'truth','predicate leaf differs')
        if isinstance(node,ast.Constant):
            require(tree.get('value') is bool(node.value),'predicate literal outcome differs')


def validate_case(row, evidence, binding, lane, retained_sources):
    """Recompute check, probe, inventory and rendered-runtime consistency."""
    import json
    import p1r_qualification as preparation
    import p1r_qualification_runtime as runtime
    records = evidence.get('check_witnesses')
    require(isinstance(records, list) and [item.get('id') for item in records] == [item['id'] for item in row['checks']],
            'executed checks omit or contradict retained check witnesses')
    commands = {command['id']: command for command in evidence['commands']}
    require(len(commands) == len(evidence['commands']), 'duplicate executed command identity')
    sources = {str(Path(binding['repository']) / item['path']): item['sha256'] for item in binding['main']['files']}
    retained = {}
    require(isinstance(retained_sources, list), 'missing retained predicate source bytes')
    for item in retained_sources:
        require(isinstance(item,dict) and sources.get(item.get('path')) == item.get('sha256')
                and isinstance(item.get('content'),str) and digest(item['content'].encode()) == item['sha256'],
                'retained predicate source differs from executor closure')
        require(item['path'] not in retained, 'duplicate retained predicate source')
        retained[item['path']] = item['content']
    probe_records = {}
    for record, check in zip(records, row['checks']):
        if record.get('kind') == 'probe':
            command = commands.get(record.get('command'))
            require(command is not None and command.get('output') is not None, 'probe check lacks its retained command')
            probe = json.loads(command['output'])
            preparation.validate_counter(probe['measurement']['assertions'], probe['measurement']['checks'])
            matches = [item for item in probe['measurement']['checks'] if item['id'] == record.get('probe_id')]
            require(len(matches) == 1 and matches[0]['passed'] is check['passed'], 'case check contradicts retained probe measurement')
            probe_records.setdefault(command['id'], []).append(record['probe_id'])
        else:
            require(record.get('kind') == 'predicate' and sources.get(record.get('source')) == record.get('source_sha256'),
                    'check witness source differs from executor closure')
            require(record['source'] in retained, 'check lacks retained predicate source bytes')
            tree = retained_tree(retained[record['source']])
            call = next((node for node in ast.walk(tree) if isinstance(node, ast.Call) and node.lineno == record.get('line')
                         and isinstance(node.func, ast.Attribute) and node.func.attr == 'check' and len(node.args) >= 2), None)
            require(call is not None and ast.unparse(call.args[1]) == record.get('expression'), 'check predicate source substituted')
            validate_shape(call.args[1], record.get('predicate',{}))
            require(evaluate(record.get('predicate')) is check['passed'], 'case check contradicts retained predicate operands')
    probes = []
    inventories = []
    successful_http = []
    for command in evidence['commands']:
        output = command.get('output')
        if output is None:
            continue
        try:
            value = json.loads(output)
        except (ValueError, TypeError):
            continue
        if command['argv'][:1] == ['HTTP'] and command.get('exit_code') == 0:
            successful_http.append((command, value))
        if isinstance(value, dict) and value.get('instrumentation') == 'p1r-executed-checks-v1':
            require(probe_records.get(command['id'], []) == [item['id'] for item in value['measurement']['checks']],
                    'executed check inventory silently drops a probe failure or check')
            probe_checks = {item['id']:item['passed'] for item in value['measurement']['checks']}
            observed_probe = value.get('observation',{})
            if 'metadata' in command['argv'] and 'operation-completed' not in probe_checks:
                expected_checks = {'sequence-twelve': observed_probe.get('sequence') == 12,
                                   'etag-preserved': observed_probe.get('etag') == 'fixture-etag',
                                   'last-modified-preserved': observed_probe.get('last_modified') in ('2026-01-01T00:00:00+00:00','2026-01-01T00:00:00Z')}
                require(probe_checks == expected_checks, 'metadata probe checks contradict retained probe observation')
            probes.append((command, value))
        if 'inventory' in command['argv'] and isinstance(value, list):
            runtime.validate_rows(value)
            inventories.append(value)
    observation = evidence.get('observations')
    require(isinstance(observation, dict), 'missing retained case observations')
    if 'before' in observation and 'after' in observation:
        for name in ('before', 'after'):
            runtime.validate_rows(observation[name])
            require(observation[name] in inventories, 'persisted inventory differs from retained command observation')
        before = digest(canonical([item for item in observation['before'] if item['kind'] != 'bookkeeping']))
        after = digest(canonical([item for item in observation['after'] if item['kind'] != 'bookkeeping']))
    else:
        before = after = digest(canonical(observation))
    require(row['inventory'] == {'before_sha256': before, 'after_sha256': after}, 'invented persisted inventory hashes')
    checks = {item['id']: item['passed'] for item in row['checks']}
    incomplete = row['outcome'] == 'error'
    require(not incomplete or checks.get('execution-completed') is False, 'incomplete case drops executed failure')
    if not incomplete and lane in ('legacy-metadata', 'metadata-read'):
        measured = [value['observation'] for command,value in probes if 'metadata' in command['argv']]
        require(len(measured) == 1, 'metadata case lacks actual selected probe')
        version, style, _, floor = row['id'].split('-')
        expected = 1 if floor == 'None' else int(floor)
        require(checks.get('retained-floor-read') is (measured[0].get('floor') == expected),
                'metadata-read check contradicts retained floor observation')
        require(set(('probe:sequence-twelve', 'probe:etag-preserved', 'probe:last-modified-preserved', 'retained-floor-read')) <= set(checks),
                'metadata case drops a stable required check')
    common = {'prior-event-and-snapshot-bytes-preserved': lambda before,after: all(
                    {item['key']:item['sha256'] for item in after}.get(item['key']) == item['sha256']
                    for item in before if item['kind'] in ('event','snapshot')),
              'second-tenant-state-preserved': lambda before,after: runtime.domain(before,'tenant-b') == runtime.domain(after,'tenant-b'),
              'second-tenant-preserved': lambda before,after: runtime.domain(before,'tenant-b') == runtime.domain(after,'tenant-b'),
              'mixed-domain-preserved': lambda before,after: [item for item in before if item['kind'] != 'bookkeeping'] ==
                    [item for item in after if item['kind'] != 'bookkeeping'],
              'hydration-domain-inventory-unchanged': lambda before,after: [item for item in before if item['kind'] != 'bookkeeping'] ==
                    [item for item in after if item['kind'] != 'bookkeeping'],
              'refusal-domain-preserved': lambda before,after: [item for item in before if item['kind'] != 'bookkeeping'] ==
                    [item for item in after if item['kind'] != 'bookkeeping']}
    observed_sets = [(observation, '')]
    if not incomplete and lane == 'checkout' and row['id'] != 'current-build':
        paired = observation.get('paired_observations')
        require(isinstance(paired, dict) and set(paired) == {'package','source'}, 'source comparison lacks both measured branches')
        observed_sets = [(paired[name], name + ':') for name in ('package','source')]
    for observed, prefix in observed_sets:
        if 'before' in observed and 'after' in observed:
            for name in ('before','after'):
                runtime.validate_rows(observed[name])
                require(observed[name] in inventories, 'paired inventory differs from retained command observation')
            for identity, evaluate_inventory in common.items():
                if prefix + identity in checks:
                    require(checks[prefix + identity] is evaluate_inventory(observed['before'],observed['after']),
                            'case check contradicts retained persisted inventory')
        measured_observations = [value['observation'] for command,value in probes]
        for name in ('metadata','primary','secondary','append','replay','actor','writer','reader'):
            if isinstance(observed.get(name),dict):
                require(observed[name] in measured_observations, 'case semantic observation contradicts retained probe result')
        coordinate_lane = row['id'] if lane == 'checkout' else lane
        coordinate_identifier = (observation['comparison']['source_case' if prefix == 'source:' else 'selected_case']
                                 if lane == 'checkout' and row['id'] != 'current-build' else row['id'])
        if not incomplete and coordinate_lane in ('legacy-metadata','metadata-read'):
            expected_floor = coordinate_identifier.rsplit('-',1)[1]
            expected_floor = 1 if expected_floor == 'None' else int(expected_floor)
            require(prefix+'retained-floor-read' in checks and checks[prefix+'retained-floor-read'] is (observed['metadata'].get('floor') == expected_floor),
                    'paired metadata check contradicts retained selected probe floor')
        if not incomplete and coordinate_lane in ('query-wire','projection-wire') and observed.get('reader',{}).get('handling') == 'executed':
            import datetime
            identifier = row['id'] if lane != 'checkout' else observation['comparison']['selected_case']
            expected_wire = ({'tenantId':'tenant-a','domain':'counter','aggregateId':'fixture','queryType':'FixtureQuery',
                              'payload':'e30=','correlationId':'fixture-correlation','userId':'fixture-user','entityId':'fixture',
                              'isGlobalAdmin':False,'paging':None} if coordinate_lane == 'query-wire' else
                             {'sequenceNumber':12,'globalPosition':987,'eventTypeName':'P1R.Counter.CounterIncremented',
                              'payload':'e30=','serializationFormat':'json','timestamp':'2026-01-01T00:00:00Z',
                              'messageId':'fixture-message','correlationId':'fixture-correlation','userId':'fixture-user'})
            if coordinate_lane == 'query-wire' and identifier.endswith('-dual'):
                expected_wire.update(originalActorId='original-fixture',authenticatedWorkloadId='workload-fixture',isDelegated=True,
                                     delegationId='delegation-fixture',scopes=['counter.read'],audience=['audience-fixture'],
                                     identityAdmissionProof='bounded-wire-fixture-proof')
            actual_wire = {name[0].lower()+name[1:]:value for name,value in observed['reader']['fields'].items()}
            for name,expected_value in expected_wire.items():
                actual_value = actual_wire.get(name)
                if name == 'timestamp' and isinstance(actual_value,str):
                    actual_value = datetime.datetime.fromisoformat(actual_value.replace('Z','+00:00')).isoformat()
                    expected_value = datetime.datetime.fromisoformat(expected_value.replace('Z','+00:00')).isoformat()
                identity = prefix+'preserved:'+name
                require(identity in checks and checks[identity] is (actual_value == expected_value),
                        'wire preservation check contradicts independently derived reader field')
        if not incomplete and 'before' in observed:
            require(all(prefix + identity in checks for identity in ('tenant-a:committed-count','tenant-b:committed-count','seed-persisted-fifteen-events')),
                    'operational case drops a stable fixture seed check')
        if not incomplete and coordinate_lane in ('full-replay','snapshot-tail','retained-covered','retained-uncovered','missing-event','metadata-write'):
            required = ('primary-expected-hydration','hydration-no-events','second-tenant-hydrates','primary-sequence-twelve','persisted-current-sequence-exact','persisted-event-sequence-set-exact',
                        'persisted-snapshot-coordinate-exact','prior-event-and-snapshot-bytes-preserved','second-tenant-state-preserved')
            require(all(prefix + identity in checks for identity in required), 'replay case drops a stable required coordinate check')
            metadata, snapshot, events = runtime.stream(observed['after'], 'tenant-a')
            appended = observed.get('append')
            head = 13 if appended and appended.get('accepted') is True and appended.get('event_count') == 1 else 12
            first = 5 if coordinate_lane in ('retained-covered','retained-uncovered','metadata-write') else 1
            sequences = list(range(first,head+1))
            if coordinate_lane == 'missing-event':
                sequences.remove(7 if coordinate_identifier.endswith('interior') else 12)
            coordinate = (None if coordinate_identifier.endswith('absent') else 2) if coordinate_lane == 'retained-uncovered' else 9 if coordinate_lane in ('snapshot-tail','retained-covered','metadata-write') else None
            expected = {'primary-sequence-twelve': observed['primary'].get('sequence') == 12,
                        'persisted-current-sequence-exact': metadata is not None and metadata['sequence'] == head,
                        'persisted-event-sequence-set-exact': sorted(events) == sequences,
                        'persisted-snapshot-coordinate-exact': (None if snapshot is None else snapshot['sequence']) == coordinate}
            for identity, passed in expected.items():
                require(checks[prefix + identity] is passed, 'replay check contradicts independently derived coordinates')
            if coordinate_lane == 'metadata-write':
                require(all(prefix + identity in checks for identity in ('append-effect-accepted','append-thirteen','retained-floor-five-preserved')),
                        'metadata-write drops a stable append or retained-floor check')
                require(checks[prefix + 'retained-floor-five-preserved'] is (metadata is not None and metadata['floor'] == 5),
                        'metadata-write check contradicts retained floor inventory')
            if coordinate_lane == 'metadata-write' and head == 13:
                replay = observed.get('replay')
                require(isinstance(replay,dict) and prefix + 'supported-append-restart-rehydrates-thirteen' in checks,
                        'supported metadata append lacks actual restart replay')
                require(checks[prefix + 'supported-append-restart-rehydrates-thirteen'] is (replay.get('accepted') is True and replay.get('sequence') == 13),
                        'supported restart check contradicts replay result')
    configurations = evidence.get('configurations')
    require(isinstance(configurations,list), 'missing rendered runtime configuration list')
    runtime_commands = [command for command in evidence['commands'] if '--resources-path' in command['argv']]
    operational_probes = [command for command,value in probes if any(argument in ('actor','seed','sequence','retained-floor') for argument in command['argv'])]
    require(not operational_probes or bool(runtime_commands), 'runtime operation lacks actual sidecar command configuration')
    require(not runtime_commands or bool(configurations), 'runtime case omits required rendered configuration')
    by_id = {item.get('id'):item for item in configurations}
    require(len(by_id) == len(configurations), 'duplicate rendered configuration identity')
    bound = set()
    for command in runtime_commands:
        configuration = by_id.get(command.get('runtime_configuration'))
        require(configuration is not None, 'sidecar command lacks its exact rendered configuration')
        arguments = command['argv']
        config_path = arguments[arguments.index('--config')+1]
        resource_path = arguments[arguments.index('--resources-path')+1]
        files = {item.get('path'):item for item in configuration.get('files',[])}
        require(config_path in files and str(Path(resource_path)/'state.yaml') in files
                and str(Path(resource_path)/'pubsub.yaml') in files, 'sidecar arguments differ from rendered configuration paths')
        require(command.get('runtime_configuration_sha256') == digest(canonical(configuration)), 'sidecar command configuration content binding differs')
        bound.add(configuration['id'])
    require(incomplete or bound == set(by_id), 'rendered configuration lacks executed sidecar binding')
    if not incomplete and lane == 'checkout' and row['id'] != 'current-build':
        comparison = observation.get('comparison')
        require(isinstance(comparison,dict) and comparison.get('selected_version') == '3.119.0', 'source case lacks actual selected-package comparison')
        def normalize(branch):
            value = paired[branch]
            result = {'outcome': 'refusal' if value.get('primary',value.get('actor',{})).get('accepted') is False else 'effect'}
            for name in ('metadata','primary','secondary','actor','append','replay'):
                if isinstance(value.get(name),dict):
                    result[name] = {key: value[name][key] for key in ('sequence','floor','accepted','event_count','etag','last_modified') if key in value[name]}
            for name in ('writer','reader'):
                if name in value:
                    result[name] = {key:item for key,item in value[name].items() if key != 'output_sha256'}
                    if value[name].get('handling') != 'executed':
                        result['outcome'] = 'refusal'
            for name in ('before','after','appended_inventory'):
                if value.get(name) is not None:
                    result[name] = [{key:item[key] for key in ('tenant','kind','sequence','floor')} for item in value[name] if item['kind'] != 'bookkeeping']
            return result
        require(comparison.get('package') == normalize('package') and comparison.get('source') == normalize('source'),
                'normalized source/package semantics contradict retained paired observations')
        equivalent = comparison.get('package') == comparison.get('source')
        require(checks.get('source-package-semantics-equivalent') is equivalent, 'source/package equivalence contradicts retained normalized observations')
        require(not equivalent or comparison.get('delta') == [], 'equivalence carries contradictory semantic deltas')

    if not incomplete and lane == 'reminder-recovery':
        for name, identity in (('registration','initial-convergence-no-direct-submission'), ('rearmed','restart-convergence-no-direct-submission')):
            value = observation.get(name)
            require(isinstance(value,dict) and identity in checks, 'reminder attribution drops stable submitted check')
            require(any(command['argv'][:2] == ['HTTP','POST'] and '/reminder/register/' in command['argv'][2]
                        and response == value for command,response in successful_http),
                    'reminder convergence differs from retained HTTP measurement')
            require(checks[identity] is (value.get('submitted') == 0), 'natural reminder attribution contradicts direct submission count')
        natural = observation.get('natural_scheduler_effect')
        require(isinstance(natural,dict) and checks.get('natural-scheduler-effect-completed-before-manual-callback') is natural.get('completed'),
                'reminder completion differs from retained scheduler observation')
        require(not natural.get('completed') or any(item.get('sequence') == 13 and item.get('completed_before_deadline') is True for item in natural.get('observations',[])),
                'late reminder completion cannot establish natural delivery')
    if not incomplete and lane == 'mixed-api' and row['id'].endswith(('stale-fence','unauthorized-effect')) and row['operation'] != 'unsupported':
        value = observation.get('operation')
        require(isinstance(value,dict) and any(command['argv'][:2] == ['HTTP','POST'] and '/qualification/' in command['argv'][2]
                    and response == value for command,response in successful_http),
                'protected refusal differs from retained HTTP observation')
        identities = [('stale-context-refused','stale_refused','stale_denial','stale-fencing-token'),
                      ('forged-proof-refused','forged_refused','forged_denial','stale-or-invalid-fence')] if row['id'].endswith('stale-fence') else [
                      ('unauthorized-proof-refused','unauthorized_refused','denial','invalid-gateway-proof')]
        for identity,refused,reason,expected in identities:
            diagnostic = 'stale_diagnostic' if refused == 'stale_refused' else 'forged_diagnostic'
            denial_message = ('The idempotency execution authority is no longer current.' if refused == 'stale_refused'
                              else 'The idempotency execution fence is missing, stale, or invalid.')
            specific = value.get(refused) is True and value.get(reason) == expected
            if row['id'].endswith('stale-fence'):
                specific = specific and value.get('stale_actor_method' if refused == 'stale_refused' else 'forged_actor_method') == 'ProcessFencedCommandAsync'
                specific = specific and any(item.get('actual_exception_type') == 'System.InvalidOperationException'
                                            and item.get('denial_message') == denial_message
                                            for item in value.get(diagnostic, []))
            require(identity in checks and checks[identity] is specific,
                    'refusal check lacks the specific expected denial')
        require(value.get('accepted') is False and value.get('unexpected') is False and value.get('sequence') == 12
                and checks.get('refusal-domain-preserved') is True,
                'protected refusal lacks unchanged persisted domain state')

    if not incomplete and lane == 'mixed-api' and row['id'].endswith('-status'):
        status_rows = observation.get('operation')
        require(isinstance(status_rows,list) and len(status_rows) == 3, 'status case drops a retryability shape')
        for status_row in status_rows:
            label = status_row['retryability_case']
            readback = status_row['readback']
            require(any(command['argv'][:2] == ['HTTP','GET'] and response == readback
                        for command,response in successful_http), 'status readback contradicts retained HTTP response')
            for name in ('messageId','correlationId','retryable','recoveryReasonCode','drainAttemptCount','domain','committedEventSequence'):
                identity = 'status:'+label+':'+name
                require(identity in checks and checks[identity] is (name in readback and readback[name] == status_row['input'][name]),
                        'status preservation drops or contradicts a stable required field')
    if not incomplete and lane == 'pre-upgrade-restore':
        require(observation.get('containment_only') is True and observation.get('rpo_zero') is False and row['disposition'] == 'incompatible',
                'pre-upgrade containment cannot claim lossless compatibility')
    if not incomplete and lane == 'logical-event-evolution':
        identity = 'registered-logical-alias-evolution-executed'
        registration = observation.get('domain_registration_observation')
        require(isinstance(registration,dict) and any(command['argv'][:2] == ['HTTP','GET']
                    and command['argv'][2].endswith('/ready') and isinstance(response,dict)
                    and response.get('evolution_registration') == registration
                    for command,response in successful_http),
                'logical evolution registration contradicts retained Domain readiness response')
        require(isinstance(registration,dict) and registration.get('manifest_registered') is True
                and registration.get('fixture_manifest_fingerprint') == '5a5748913258b5832b333fe507bc689f1ac9e53a9201b4bb5cb983b535da933b'
                and registration.get('fixture_manifest_test_only') is True and registration.get('gateway_authority') is False,
                'logical evolution registration lacks independent test-only pin')
        actor = observation.get('actor')
        actor_probes = [command for command,value in probes if 'actor' in command['argv'] and value['observation'] == actor]
        require(isinstance(actor,dict) and len(actor_probes) == 1,
                'logical evolution lacks retained aggregate response')
        actor_command = actor_probes[0]
        actor_argv = actor_command['argv']
        require(actor_command.get('exit_code') == 0 and len(actor_argv) == 8
                and actor_argv[0] == 'dotnet' and Path(actor_argv[1]).name == 'Probe.dll'
                and actor_argv[2] == 'actor'
                and actor_argv[4:] == ['tenant-a','fixture','12','AssertCounter'],
                'logical evolution lacks exact successful actor probe command')
        actor_base = actor_argv[3]
        require(isinstance(actor_base,str) and actor_base.startswith('http://127.0.0.1:')
                and '/' not in actor_base[len('http://'):], 'logical evolution lacks retained actor sidecar URL')
        unknown = row['id'] == 'unknown-version-refusal'
        if unknown:
            version_input = observation.get('unknown_version_input')
            require(isinstance(version_input,dict) and version_input.get('key_suffix') == 'tenant-a:counter:fixture:events:7'
                    and version_input.get('metadata_version') == 987
                    and checks.get('unknown-version-fixture-persisted') is True,
                    'unknown version lacks persisted diagnostic input')
            persisted = []
            for command in evidence['commands']:
                if command.get('exit_code') != 0 or command.get('output') is None:
                    continue
                try:
                    pairs = json.loads(command['output'])
                except (ValueError,TypeError):
                    continue
                if not isinstance(pairs,list):
                    continue
                for pair in pairs:
                    if (isinstance(pair,list) and len(pair) == 2
                            and pair[0] == 'eventstore||AggregateActor||tenant-a:counter:fixture||tenant-a:counter:fixture:events:7'):
                        persisted.append(base64.b64decode(pair[1]) if isinstance(pair[1],str) else canonical(pair[1]))
            require(any(digest(data) == version_input.get('sha256')
                        and json.loads(data).get('metadataVersion') == 987 for data in persisted),
                    'unknown version input differs from retained provider diagnostic')
        require(checks.get('unknown-version-refused-before-application') is (not unknown or (
                    actor.get('accepted') is False and actor.get('event_count') == 0 and actor.get('sequence') == 12
                    and actor.get('error') == 'Protected data diagnostic details were redacted. ReasonCode=logical-event-read-rejected; Stage=Processing.')),
                'unknown version lacks actor logical-read refusal')
        alias = observation.get('legacy_alias')
        readbacks = observation.get('dapr_application_readback')
        require(alias == 'P1R.Legacy.CounterIncremented' and isinstance(readbacks,list) and len(readbacks) == 12,
                'logical alias readback inventory incomplete')
        before_events = {item['sequence']:item for item in observation['before']
                         if item['kind'] == 'event' and item['tenant'] == 'tenant-a'}
        require(len(before_events) == 12 and [item for item in observation['before'] if item['kind'] != 'bookkeeping']
                == [item for item in observation['after'] if item['kind'] != 'bookkeeping'],
                'logical evolution changed persisted inventory')
        for sequence, item in enumerate(readbacks, 1):
            actor_id = 'tenant-a%3Acounter%3Afixture'
            event_key = f'tenant-a%3Acounter%3Afixture%3Aevents%3A{sequence}'
            expected_path = f'/v1.0/actors/AggregateActor/{actor_id}/state/{event_key}'
            require(item.get('sequence') == sequence and item.get('url') == actor_base + expected_path,
                    'logical readback has wrong actor state URL')
            matches = [value for command,value in successful_http if command['argv'][:3] == ['HTTP','GET',item['url']]]
            require(len(matches) == 1 and isinstance(matches[0],dict)
                    and matches[0].get('sequenceNumber') == sequence
                    and matches[0].get('eventTypeName') == alias
                    and digest(canonical(matches[0])) == item.get('sha256'),
                    'logical readback differs from exact retained actor state')
            provider = before_events.get(sequence)
            require(provider is not None
                    and provider['key'] == f'eventstore||AggregateActor||tenant-a:counter:fixture||tenant-a:counter:fixture:events:{sequence}'
                    and provider['sha256'] == item['sha256'],
                    'logical readback differs from exact provider event inventory')
            payload = matches[0].get('payload')
            require(isinstance(payload,str), 'logical readback lacks retained payload')
            try:
                payload_bytes = base64.b64decode(payload, validate=True)
            except (ValueError,binascii.Error):
                require(False, 'logical readback has invalid retained payload')
            require(digest(payload_bytes) == item.get('payload_sha256'),
                    'logical readback payload digest differs from retained HTTP payload')
        executed = actor.get('accepted') is True and actor.get('sequence') == 12 and not unknown
        require(identity in checks and checks[identity] is True
                and observation.get('registered_logical_alias_evolution',{}).get('executed') is executed,
                'logical evolution execution contradicts routed aggregate readback')
        require(executed or unknown or row['disposition'] == 'incompatible',
                'missing registered logical evolution cannot claim compatibility')

    if not incomplete and lane == 'reminder-recovery':
        import datetime
        deadline = datetime.datetime.fromisoformat(natural['deadline_utc'])
        start = datetime.datetime.fromisoformat(natural['started_utc'])
        require(natural.get('budget_seconds') == 30 and (deadline-start).total_seconds() == 30,
                'natural Reminder deadline differs from finite observation budget')
        observed_commands = []
        for item in natural['observations']:
            command = commands.get(item['command'])
            require(command is not None and 'sequence' in command['argv'] and command.get('output') is not None,
                    'natural Reminder observation lacks its read-only probe')
            output = json.loads(command['output'])['observation']
            require(item['sequence'] == output.get('sequence'), 'natural Reminder sequence contradicts retained probe')
            require(0 < command.get('timeout_seconds',0) <= 30 and datetime.datetime.fromisoformat(command['started_utc']) < deadline,
                    'natural Reminder call is not bounded by its remaining deadline')
            require(command['timeout_seconds'] <= (deadline-datetime.datetime.fromisoformat(command['started_utc'])).total_seconds()+.02,
                    'natural Reminder call exceeds the remaining observation budget')
            if item.get('completed_before_deadline'):
                require(datetime.datetime.fromisoformat(command['finished_utc']) <= deadline,
                        'late read-only probe cannot establish natural Reminder completion')
            observed_commands.append(command['id'])
        callbacks = [command['id'] for command in evidence['commands'] if command['argv'][:2] == ['HTTP','POST'] and '/reminder/callback/' in command['argv'][2]]
        require(not observed_commands or callbacks and min(callbacks) > max(observed_commands),
                'manual callback precedes claimed natural Reminder observation')
