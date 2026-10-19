"""Finite predicate witnesses for independently recomputed executor checks.

Only the executor evaluates source expressions. Importers interpret bounded
comparison predicates and bind probe witnesses to retained literal outputs.
"""
import ast
import base64
import binascii
import inspect
import urllib.parse
from datetime import datetime, timedelta
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


def requires_qualified_snapshot(candidate_version, lane, identifier):
    """Derive the genuine successor seed requirement without trusting optional observations."""
    return candidate_version in ('3.121.0','3.122.1','3.125.0','3.125.1') and (lane == 'post-upgrade-restore' or (
        lane in ('snapshot-tail','retained-covered','retained-uncovered','metadata-write')
        and identifier.split('-to-',1)[0] in (candidate_version,'source')))


def snapshot_profile_limits(evidence, binding):
    """Read the full private fixture declaration from source bytes bound to the closure."""
    import re
    source = evidence.get('snapshot_fixture_source', {})
    path = str(Path(binding['repository']) / 'tools/p1r-published-consumers/FixtureSnapshotQualification.cs')
    declared = {str(Path(binding['repository']) / item['path']): item['sha256'] for item in binding['main']['files']}
    require(source.get('path') == path and source.get('sha256') == declared.get(path)
            and isinstance(source.get('content'), str) and digest(source['content'].encode()) == source['sha256'],
            'qualified snapshot finite profile lacks its retained shared fixture source')
    limits = {name:int(number.replace('_','')) for name,number in
              re.findall(r'(maximum\w+):\s*([0-9_]+)', source['content'])}
    names = ('CaptureWorkspaceBytes','PreCommandStateGraphBytes','WorkingStateGraphBytes','DetachedEventGraphBytes',
             'EventCount','EventBytes','StateJsonBytes','DomainParsedStateGraphBytes','EscapedWireStateBytes',
             'ActorParsedStateBytes','ActorParsedGraphBytes','CanonicalStateMeasurementBytes','CanonicalEnvelopeBytes',
             'NodeCount','Depth','SerializerScratchBytes','SimultaneousLiveBytes','ActorWorkspaceBytes')
    require(set(limits) == {'maximum'+name for name in names}, 'qualified snapshot finite declaration is incomplete')
    limits['domainReservationBytes'] = (limits['maximumPreCommandStateGraphBytes'] + limits['maximumWorkingStateGraphBytes']
        + limits['maximumDetachedEventGraphBytes'] + 2 * limits['maximumEventBytes'] + 2 * limits['maximumStateJsonBytes']
        + limits['maximumDomainParsedStateGraphBytes'] + limits['maximumEscapedWireStateBytes']
        + limits['maximumSerializerScratchBytes'] + 256 * limits['maximumEventCount'])
    limits['actorReservationBytes'] = (2 * limits['maximumActorParsedStateBytes'] + limits['maximumActorParsedGraphBytes']
        + 4 * limits['maximumCanonicalStateMeasurementBytes'] + 2 * limits['maximumCanonicalEnvelopeBytes']
        + limits['maximumSerializerScratchBytes'])
    return limits


def validate_qualified_snapshot(observed, prefix, checks, evidence, binding, writer_version):
    """Bind genuine snapshot creation to physical consumers, owned launches and persisted stream."""
    import json
    import p1r_qualification as preparation
    import p1r_qualification_runtime as runtime
    from p1r_published_executor import DOCKER_FORMAT
    required = ('snapshot-fixture-enrolled','snapshot-state-roundtrip-parity','snapshot-canonical-event-parity',
                'snapshot-detached-apply-parity','snapshot-nine-events-committed','tenant-a:committed-count',
                'qualified-snapshot-created-by-published-actor','qualified-snapshot-envelope-version-one',
                'qualified-snapshot-persisted-nine','qualified-snapshot-prefix-coordinate-exact',
                'qualified-snapshot-tail-coordinate-exact','qualified-snapshot-and-prefix-bytes-preserved')
    require(all(prefix + identity in checks for identity in required), 'qualified snapshot proof drops a required check')
    seed = observed.get('seed_snapshot')
    require(isinstance(seed,dict), 'qualified snapshot proof omits its genuine seed observations')
    commands = {command['id']:command for command in evidence['commands']}
    outputs = {}
    for identity,command in commands.items():
        if command.get('exit_code') == 0 and command.get('output') is not None:
            try:
                outputs[identity] = json.loads(command['output'])
            except (ValueError,TypeError):
                pass
    role, configuration = ('source','Debug') if writer_version == 'source' else ('candidate','Release')
    require(writer_version in ('3.121.0','3.122.1','3.125.0','3.125.1','source'), 'qualified snapshot writer is not the selected successor')
    def probe_command(check_prefix, operation):
        identities = {record.get('command') for record in evidence['check_witnesses']
                      if record.get('kind') == 'probe' and record.get('id','').startswith(prefix + check_prefix)}
        require(len(identities) == 1, 'qualified snapshot lacks one exact ' + operation + ' probe')
        command = commands.get(next(iter(identities)))
        require(command is not None and command.get('exit_code') == 0 and len(command['argv']) >= 6
                and command['argv'][:1] == ['dotnet'] and command['argv'][2] == operation
                and command['argv'][4:6] == ['tenant-a','fixture'],
                'qualified snapshot ' + operation + ' probe command substituted or failed')
        binary = command.get('probe_binary',{})
        path = Path(command['argv'][1])
        require(path.is_absolute() and path.name == 'Probe.dll' and len(path.parents) >= 7,
                'qualified snapshot probe physical binary path differs')
        root = path.parents[4]
        manifest = json.loads(preparation.regular(root / 'evidence.json'))
        projects = [item for item in manifest.get('projects',[]) if item.get('name') == 'Probe']
        require(root.parent.name == 'consumers' and root.name == role and manifest.get('role') == role
                and manifest.get('configuration') == configuration and len(projects) == 1
                and path == root / projects[0]['output'] / 'Probe.dll'
                and projects[0]['output'] == 'probe/bin/'+configuration+'/net10.0'
                and binary == json.loads(preparation.regular(root / 'probe-binary.json'))
                and binary == {'role':role,'configuration':configuration,'path':str(path),
                               'sha256':digest(preparation.regular(path))},
                'qualified snapshot probe binary role, configuration or physical digest differs')
        output = outputs.get(command['id'])
        require(isinstance(output,dict) and isinstance(output.get('observation'),dict), 'qualified snapshot probe output missing')
        return command,output['observation'],root
    nine_command,nine,consumer = probe_command('tenant-a:prefix:','seed')
    manual_command,manual,manual_consumer = probe_command('snapshot:','manual-snapshot')
    tail_command,tail,tail_consumer = probe_command('tenant-a:tail:','seed')
    require(consumer == manual_consumer == tail_consumer and nine_command['argv'][-1] == '9'
            and len(nine_command['argv']) == len(tail_command['argv']) == 7 and tail_command['argv'][-1] == '3'
            and len(manual_command['argv']) == 6 and nine_command['id'] < manual_command['id'] < tail_command['id']
            and nine_command['argv'][3] == manual_command['argv'][3],
            'qualified snapshot lacks nine commits followed by manual creation and three actual appends')
    require(seed.get('manual') == manual and seed.get('tail') == tail,
            'qualified snapshot result differs from retained probe output')
    for command,observed_probe,count in ((nine_command,nine,9),(tail_command,tail,3)):
        appends = outputs[command['id']]['measurement']['checks']
        require(0 < len(appends) <= count*2 and len(appends) % 2 == 0
                and [item['id'] for item in appends] == [identity for index in range(1,len(appends)//2+1)
                    for identity in ('append-'+str(index)+'-accepted','append-'+str(index)+'-one-event')],
                'qualified snapshot actual append measurement inventory differs')
        committed = 0
        for index in range(0,len(appends),2):
            if appends[index]['passed'] and appends[index+1]['passed']:
                committed += 1
            else:
                require(index+2 == len(appends), 'qualified snapshot append continues after recorded refusal')
        require(observed_probe.get('committed') == committed and (committed < count or len(appends) == count*2),
                'qualified snapshot committed count contradicts actual append results')
    times = {identity:preparation.validate_times(command) for identity,command in commands.items()}
    require(times[nine_command['id']][1] <= times[manual_command['id']][0]
            and times[manual_command['id']][1] <= times[tail_command['id']][0],
            'qualified snapshot nine/manual/three commands are not chronological')
    configurations = {item['id']:item for item in evidence.get('configurations',[])}
    def argument(command, name):
        argv = command['argv']
        return argv[argv.index(name)+1] if argv.count(name) == 1 and argv.index(name)+1 < len(argv) else None
    def readiness_get(command, url):
        argv = command['argv']
        return argv == ['HTTP','GET',url] or argv == ['HTTP','GET',url,'request-sha256='+digest(b'')]
    def launched(probe, project, appid, endpoint=None):
        start,end = times[probe['id']]
        matches = []
        def stopped(command):
            return command.get('exit_code') in (0,-15) and bool(command.get('cleanup')) and all(
                not attempt.get('remaining') and not attempt.get('errors') for attempt in command['cleanup'])
        for sidecar in commands.values():
            if (not stopped(sidecar) or argument(sidecar,'--app-id') != appid
                    or argument(sidecar,'--app-channel-address') != '127.0.0.1'
                    or endpoint is not None and endpoint != 'http://127.0.0.1:'+str(argument(sidecar,'--dapr-http-port'))):
                continue
            side_start,side_end = times[sidecar['id']]
            if not side_start < start <= end < side_end:
                continue
            config = configurations.get(sidecar.get('runtime_configuration'))
            require(config is not None and config.get('version') == writer_version and config.get('snapshot_interval') == 10
                    and sidecar.get('runtime_configuration_sha256') == digest(canonical(config)),
                    'qualified snapshot launch configuration differs')
            hosts = [command for command in commands.values() if stopped(command)
                     and command['argv'] == ['dotnet',str(consumer / project.lower() / 'bin' / configuration / 'net10.0' / (project+'.dll'))]
                     and times[command['id']][0] < start <= end < times[command['id']][1]
                     and 'Now listening on: http://127.0.0.1:'+str(argument(sidecar,'--app-port')) in command.get('output','')]
            require(len(hosts) == 1, 'qualified snapshot endpoint lacks its successful physical host launch')
            host_binary = hosts[0].get('consumer_binary')
            host_path = Path(hosts[0]['argv'][1])
            manifest = json.loads(preparation.regular(consumer / 'serving-binaries.json'))
            require(host_binary == manifest.get(project)
                    and host_binary == {'role':role,'configuration':configuration,'project':project,
                                        'path':str(host_path),'sha256':digest(preparation.regular(host_path))},
                    'qualified snapshot serving binary role, configuration or physical digest differs')
            for process in (sidecar,hosts[0]):
                preparation.validate_identity(process.get('process'))
                owned_processes = process.get('owned_processes')
                preparation.validate_identity_list(owned_processes)
                require(process['process'] in owned_processes and all(
                    isinstance(attempt.get('before'),list) and all(item in owned_processes for item in attempt['before'])
                    and attempt.get('remaining') == [] and attempt.get('errors') == [] for attempt in process['cleanup']),
                    'qualified snapshot launch cleanup ownership differs')
            for url,ready_start in (('http://127.0.0.1:'+str(argument(sidecar,'--app-port'))+'/ready',times[hosts[0]['id']][0]),
                        ('http://127.0.0.1:'+str(argument(sidecar,'--dapr-http-port'))+'/v1.0/healthz/outbound',side_start)):
                require(any(command.get('exit_code') == 0 and readiness_get(command,url)
                            and ready_start <= times[command['id']][0]
                            and times[command['id']][1] < start for command in commands.values()),
                        'qualified snapshot launch lacks successful endpoint readiness')
            matches.append((sidecar,hosts[0],config))
        require(len(matches) == 1, 'qualified snapshot endpoint is unbound or ambiguous')
        return matches[0]
    first_sidecar,first_host,first_config = launched(nine_command,'Host','eventstore',nine_command['argv'][3])
    manual_launch = launched(manual_command,'Host','eventstore',manual_command['argv'][3])
    tail_sidecar,tail_host,tail_config = launched(tail_command,'Host','eventstore',tail_command['argv'][3])
    require(manual_launch[0]['id'] == first_sidecar['id'] and first_sidecar['process'] != tail_sidecar['process']
            and first_host['process'] != tail_host['process']
            and max(times[first_sidecar['id']][1],times[first_host['id']][1]) <
                min(times[tail_sidecar['id']][0],times[tail_host['id']][0]),
            'qualified snapshot tail does not use a distinct restarted writer')
    database = first_config.get('state_container')
    def state_file(config):
        values = [item for item in config.get('files',[]) if item.get('name') == 'state.yaml']
        require(len(values) == 1, 'qualified snapshot lacks state-store configuration')
        return values[0]
    require(database == tail_config.get('state_container') and first_config.get('backend_image') == tail_config.get('backend_image')
            and first_config.get('state_component_sha256') == tail_config.get('state_component_sha256')
            and state_file(first_config) == state_file(tail_config),
            'qualified snapshot tail state-store configuration or persisted stream differs')
    run_root = consumer.parent.parent
    execution = json.loads(preparation.regular(run_root / 'execution.json'))
    require(json.loads(preparation.regular(run_root / 'executor-source.json')) == binding
            and execution.get('executor_source_sha256') == digest(preparation.regular(run_root / 'executor-source.json')),
            'qualified snapshot consumer invocation source differs')
    owned = execution.get('owned_containers',{}).get(database)
    require(isinstance(owned,dict) and owned.get('image') == first_config.get('backend_image')
            and owned.get('name','').startswith('p1r-'+execution['invocation']+'-'),
            'qualified snapshot provider is not the invocation-owned database')
    retained_commands = json.loads(preparation.regular(run_root / 'commands.json'))
    require(any(command.get('exit_code') == 0 and command['argv'][:2] == ['docker','inspect']
                and any(value.get('Id') == database and value.get('Name') == '/'+owned['name']
                        and value.get('Config',{}).get('Labels',{}).get('hexalith.p1r.invocation') == execution['invocation']
                        for value in (json.loads(line) for line in command.get('output','').splitlines() if line.strip()))
                for command in retained_commands), 'qualified snapshot database lacks retained container ownership inspection')
    domain_sidecar,domain_host,domain_config = launched(nine_command,'Domain','counter')
    require(domain_config['id'] == first_config['id'], 'qualified snapshot registration uses another state-store configuration')
    readiness_sidecars = [first_sidecar,tail_sidecar,domain_sidecar]
    if any(argument(command,'--app-id') == 'counter' and command.get('runtime_configuration') == tail_config['id']
           for command in commands.values()):
        tail_domain_sidecar,_,tail_domain_config = launched(tail_command,'Domain','counter')
        require(tail_domain_config['id'] == tail_config['id'], 'qualified snapshot restarted Domain configuration differs')
        readiness_sidecars.append(tail_domain_sidecar)
    readiness_urls = {url for sidecar in readiness_sidecars for url in (
        'http://127.0.0.1:'+str(argument(sidecar,'--app-port'))+'/ready',
        'http://127.0.0.1:'+str(argument(sidecar,'--dapr-http-port'))+'/v1.0/healthz/outbound')}
    registration = seed.get('registration')
    require(isinstance(registration,dict) and any(readiness_get(command,
                'http://127.0.0.1:'+str(argument(domain_sidecar,'--app-port'))+'/ready')
                and isinstance(outputs.get(identity),dict) and outputs[identity].get('snapshot_qualification') == registration
                and times[domain_host['id']][0] <= times[identity][0] <= times[identity][1] < times[nine_command['id']][0]
                for identity,command in commands.items()), 'qualified snapshot registration differs from retained Domain readiness')
    require(registration.get('profile_id') == 'p1r-counter-folded-v1'
            and registration.get('parity_evidence_id') == 'p1r-counter-roundtrip-event-parity-v1'
            and registration.get('state_json') == {'count':registration.get('roundtrip_count')}
            and registration.get('state_sha256') == digest(canonical(registration['state_json']))
            and registration.get('limits') == snapshot_profile_limits(evidence,binding),
            'qualified snapshot parity or finite profile binding differs')
    expected = {
        'snapshot-fixture-enrolled': registration.get('registered') is True and registration.get('test_only') is True,
        'snapshot-state-roundtrip-parity': registration.get('state_roundtrip') is True
            and registration.get('original_count') == registration.get('roundtrip_count') == 9,
        'snapshot-canonical-event-parity': registration.get('canonical_event_parity') is True
            and registration.get('canonical_event_sha256') == digest(b'{}'),
        'snapshot-detached-apply-parity': registration.get('detached_apply') is True and registration.get('applied_count') == 10,
        'snapshot-nine-events-committed': nine.get('committed') == 9 and nine.get('sequence') == 9,
        'tenant-a:committed-count': nine.get('committed') == 9 and tail.get('committed') == 3 and tail.get('sequence') == 12,
        'qualified-snapshot-created-by-published-actor': manual.get('outcome') == 'Created' and manual.get('sequence') == 9,
    }
    manual_checks = {item['id']:item['passed'] for item in outputs[manual_command['id']]['measurement']['checks']}
    expected_manual = {'manual-snapshot-created': manual.get('outcome') == 'Created',
                       'manual-snapshot-at-nine': manual.get('sequence') == 9,
                       'manual-snapshot-no-failure': manual.get('reason_code') is None and manual.get('message') is None}
    require(manual_checks == expected_manual and manual.get('snapshot_key') == 'tenant-a:counter:fixture:snapshot',
            'qualified snapshot manual probe checks or stream contradict its result')
    for identity,passed in expected_manual.items():
        require(checks.get(prefix+'snapshot:'+identity) is passed, 'qualified snapshot drops or substitutes a manual result check')
    snapshot = seed.get('persisted_snapshot')
    require(isinstance(snapshot,dict), 'qualified snapshot envelope observation missing')
    snapshot_key = 'eventstore||AggregateActor||tenant-a:counter:fixture||tenant-a:counter:fixture:snapshot'
    query = "SELECT coalesce(json_agg(json_build_array(key,value) ORDER BY key)::text,'[]') FROM state"
    provider_argv = ['docker','exec','-i',database,'psql','-U','postgres','-d','eventstore','-At','-v','ON_ERROR_STOP=1','-f','-']
    inventory_argv = [binding['python']['path'],'-I',str(Path(binding['repository'])/'tools/p1r-published-executor.py'),'inventory',
                      '--container',database,'--invocation',execution['invocation'],'--backend','state.postgresql']
    for identity,command in commands.items():
        if (times[identity][0] <= times[tail_command['id']][1]
                and times[identity][1] >= times[nine_command['id']][0]):
            argv = command['argv']
            # A finite producer grammar keeps shell multiplexers and wrappers out
            # without guessing every possible interpreter name.
            require(argv in (nine_command['argv'], manual_command['argv'], tail_command['argv'],
                             first_host['argv'], domain_host['argv'], tail_host['argv'], inventory_argv, provider_argv)
                    or argv[:2] == ['HTTP', 'GET'] and len(argv) == 3
                    or any(readiness_get(command,url) for url in readiness_urls)
                    or argv[:2] == ['docker', 'exec']  # Exact read-only SQL profile is required below.
                    or argv == ['docker', 'ps', '-aq']
                    or argv[:4] == ['docker', 'inspect', '--format', DOCKER_FORMAT]
                    or argv[0] == first_sidecar['argv'][0] and command.get('runtime_configuration') in configurations
                       and command.get('runtime_configuration_sha256') == digest(canonical(configurations[command['runtime_configuration']])),
                    'qualified snapshot unapproved or shell-mediated command during genuine seed')
        if (command['argv'][:2] == ['docker','exec']
                and times[identity][0] <= times[tail_command['id']][1]
                and times[identity][1] >= times[nine_command['id']][0]):
            require(command['argv'] == provider_argv and command.get('provider_sql') == query
                    and command.get('input_sha256') == digest(query.encode())
                    and command.get('input_bytes') == len(query.encode()),
                    'qualified snapshot owned provider mutation or unbound SQL during genuine seed')
    provider_matches = []
    for identity,value in outputs.items():
        command = commands[identity]
        if (command['argv'] == provider_argv and command.get('provider_sql') == query
                and command.get('input_sha256') == digest(query.encode()) and command.get('input_bytes') == len(query.encode())
                and max(times[first_sidecar['id']][1],times[first_host['id']][1]) <= times[identity][0]
                and times[identity][1] < times[tail_command['id']][0] and isinstance(value,list)):
            values = {pair[0]:pair[1] for pair in value if isinstance(pair,list) and len(pair) == 2}
            actual = values.get(snapshot_key,{})
            raw = base64.b64decode(actual,validate=True) if isinstance(actual,str) else canonical(actual)
            actual = json.loads(raw)
            provider_matches.append((command,actual,raw))
    provider_snapshot_matches = [(command,raw) for command,actual,raw in provider_matches if actual == snapshot]
    require(provider_snapshot_matches, 'qualified snapshot envelope differs from exact stopped-writer owned provider output')
    if snapshot:
        require(set(snapshot) == {'tenantId','domain','aggregateId','sequenceNumber','snapshotEnvelopeVersion','state','createdAt','protectionMetadata'}
                and snapshot.get('tenantId') == 'tenant-a' and snapshot.get('domain') == 'counter' and snapshot.get('aggregateId') == 'fixture'
                and snapshot.get('protectionMetadata') == {'state':0,'metadataVersion':1,'scheme':None,'keyAlias':None,'contentHint':None,'compatibilityFlags':None},
                'qualified snapshot envelope stream identity or supported protection contract differs')
        import datetime
        created = datetime.datetime.fromisoformat(snapshot['createdAt'].replace('Z','+00:00'))
        require(times[manual_command['id']][0] <= created <= times[manual_command['id']][1],
                'qualified snapshot envelope creation time is outside the manual invocation')
    expected['qualified-snapshot-envelope-version-one'] = snapshot.get('snapshotEnvelopeVersion') == 1
    expected['qualified-snapshot-persisted-nine'] = snapshot.get('sequenceNumber') == 9 and snapshot.get('state') == {'count':9}
    inventory_commands = {}
    for name in ('before_tail','after_tail'):
        runtime.validate_rows(seed.get(name))
        matches = [command for identity,command in commands.items() if command.get('exit_code') == 0
                   and command['argv'] == inventory_argv
                   and outputs.get(identity) == seed[name]
                   and (times[identity][1] < min(times[tail_sidecar['id']][0],times[tail_host['id']][0])
                        and times[identity][0] >= max(times[first_sidecar['id']][1],times[first_host['id']][1])
                        if name == 'before_tail' else times[identity][0] > times[tail_command['id']][1])]
        require(matches, 'qualified snapshot tail inventory differs from distinct ordered owned commands')
        inventory_commands[name] = matches[0]
    require(inventory_commands['before_tail']['id'] < tail_command['id'] < inventory_commands['after_tail']['id']
            and any(times[command['id']][1] <= times[inventory_commands['before_tail']['id']][0] for command,_,_ in provider_matches),
            'qualified snapshot provider and before/after tail inventories are reordered')
    for name,head,identity in (('before_tail',9,'qualified-snapshot-prefix-coordinate-exact'),
                               ('after_tail',12,'qualified-snapshot-tail-coordinate-exact')):
        rows = seed[name]
        metadata,_,events = runtime.stream(rows,'tenant-a')
        require(all(item['key'].startswith('eventstore||AggregateActor||tenant-a:counter:fixture||')
                    for item in runtime.domain(rows,'tenant-a')), 'qualified snapshot inventory substitutes another tenant stream')
        expected[identity] = metadata is not None and metadata['sequence'] == head and sorted(events) == list(range(1,head+1))
    originals = {item['key']:item['sha256'] for item in seed['before_tail'] if item['kind'] in ('event','snapshot')}
    after = {item['key']:item['sha256'] for item in seed['after_tail']}
    expected['qualified-snapshot-and-prefix-bytes-preserved'] = all(after.get(key) == value for key,value in originals.items())
    snapshot_rows = [item for item in seed['before_tail'] if item['key'] == snapshot_key]
    require((snapshot == {} and snapshot_rows == []) or len(snapshot_rows) == 1
            and any(snapshot_rows[0]['sha256'] == digest(raw) for _,raw in provider_snapshot_matches),
            'qualified snapshot envelope differs from persisted inventory hash')
    for identity,passed in expected.items():
        require(checks[prefix+identity] is passed, 'qualified snapshot check contradicts retained execution output')

def reminder_name(due_utc, revision, tenant='tenant-a', item='fixture'):
    """Recompute the version-one published schedule tuple without calling the fixture."""
    due = datetime.fromisoformat(due_utc)
    require(due.utcoffset() == timedelta(0) and type(revision) is int and revision >= 0,
            'invalid reminder schedule identity')
    elapsed = due.replace(tzinfo=None) - datetime(1,1,1)
    ticks = (elapsed.days*86400+elapsed.seconds)*10_000_000+elapsed.microseconds*10
    data = b''
    for value in ('schedule',tenant,item):
        encoded = value.encode('utf-8')
        data += len(encoded).to_bytes(4,'big')+encoded
    data += ticks.to_bytes(8,'big',signed=True)+revision.to_bytes(8,'big',signed=True)
    bits = int(digest(data),16) << 4
    alphabet = '0123456789ABCDEFGHJKMNPQRSTVWXYZ'
    return 'date-wrs-'+''.join(alphabet[(bits >> (5*(51-index))) & 31] for index in range(52))


def validate_reminder_identity(observation, successful_http, checks, identifier):
    registrations = [(command,response) for command,response in successful_http
                     if command['argv'][:2] == ['HTTP','POST'] and '/reminder/register/' in command['argv'][2]]
    callbacks = [(command,response) for command,response in successful_http
                 if command['argv'][:2] == ['HTTP','POST'] and '/reminder/callback/' in command['argv'][2]]
    require(len(registrations) == 2 and len(callbacks) == 1, 'reminder identity lacks exact registration and callback commands')
    for index,(command,response) in enumerate(registrations):
        revision = 2 if index == 1 and identifier == 'stale-generation-refusal' else 1
        due = observation['deadline_windows'][index*2]['due_utc']
        require(urllib.parse.urlsplit(command['argv'][2]).path == '/reminder/register/'+str(revision)
                and response == observation['registration' if index == 0 else 'rearmed']
                and response.get('intent_due') == due and response.get('intent_revision') == revision
                and response.get('reminder_name') == reminder_name(due,revision),
                'armed reminder identity differs from literal due, revision or published tuple')
    original = registrations[0][1]
    command,response = callbacks[0]
    callback = urllib.parse.urlsplit(command['argv'][2])
    require(callback.path == '/reminder/callback/1'
            and urllib.parse.parse_qs(callback.query,strict_parsing=True) == {
                'due':[original['intent_due']], 'reminderName':[original['reminder_name']]}
            and response == observation.get('repeated_or_stale')
            and all(response.get(key) == original[key] for key in ('intent_due','intent_revision','reminder_name'))
            and checks.get('callback-replays-original-armed-identity') is True,
            'callback does not replay the exact original armed reminder')
    accepted = type(response.get('callback_status')) is int and 200 <= response['callback_status'] < 300
    require(checks.get('callback-actor-request-accepted') is accepted,
            'callback actor request status contradicts its measured acceptance')


def validate_reminder_windows(observation, successful_http, configurations, checks, identifier):
    windows = observation.get('deadline_windows')
    phases = ('initial-before-register', 'initial-after-register', 'restart-before-register', 'restart-after-register')
    require(isinstance(windows,list) and len(windows) == 4
            and [row.get('phase') for row in windows] == list(phases), 'reminder future deadline proof missing')
    registrations = [command for command,response in successful_http if command['argv'][:2] == ['HTTP','POST']
                     and '/reminder/register/' in command['argv'][2]]
    require(len(registrations) == 2, 'reminder deadline lacks exact registration commands')
    for index,command in enumerate(registrations):
        before, after = windows[index*2:index*2+2]
        require(before['selected_utc'] == after['selected_utc'] and before['due_utc'] == after['due_utc'],
                'reminder immutable intent changed during registration')
        started, finished = (datetime.fromisoformat(command[key]) for key in ('started_utc','finished_utc'))
        require(datetime.fromisoformat(before['observed_utc']) <= started <= finished
                <= datetime.fromisoformat(after['observed_utc']), 'reminder deadline proof does not bracket registration')
        for window in (before,after):
            selected, due, observed = (datetime.fromisoformat(window[key]) for key in ('selected_utc','due_utc','observed_utc'))
            remaining = (due-observed).total_seconds()
            require(all(value.utcoffset() == timedelta(0) for value in (selected,due,observed))
                    and selected <= observed and (due-selected).total_seconds() == 120
                    and window.get('startup_lead_seconds') == 120 and window.get('minimum_remaining_seconds') == 5
                    and window.get('remaining_seconds') == remaining and remaining >= 5
                    and checks.get('reminder-'+window['phase']+'-deadline-future') is True,
                    'reminder deadline expired or timing proof substituted')
        eligible = [row for row in configurations if datetime.fromisoformat(row['observed_utc']) <= started]
        require(eligible and max(eligible,key=lambda row: row['observed_utc']).get('reminder_due') == before['due_utc'],
                'reminder due differs from actual launched configuration')
    if identifier != 'stale-generation-refusal':
        require(windows[0]['due_utc'] == windows[2]['due_utc']
                and windows[0]['selected_utc'] == windows[2]['selected_utc'],
                'armed reminder intent was rescheduled during restart')
    require(observation.get('due') == windows[-1]['due_utc'], 'reminder final due differs from retained intent')


def validate_case(row, evidence, binding, lane, retained_sources, candidate_version='3.119.0'):
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
    if not incomplete and row['operation'] != 'unsupported' and lane in ('legacy-metadata', 'metadata-read'):
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
                                 if not incomplete and lane == 'checkout' and row['id'] != 'current-build' else row['id'])
        if not incomplete and row['operation'] != 'unsupported' and requires_qualified_snapshot(candidate_version, coordinate_lane, coordinate_identifier):
            validate_qualified_snapshot(observed, prefix, checks, evidence, binding,
                candidate_version if coordinate_lane == 'post-upgrade-restore' else coordinate_identifier.split('-to-',1)[0])
        if not incomplete and row['operation'] != 'unsupported' and coordinate_lane in ('legacy-metadata','metadata-read'):
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
        if not incomplete and row['operation'] != 'unsupported' and coordinate_lane in ('full-replay','snapshot-tail','retained-covered','retained-uncovered','missing-event','metadata-write'):
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
        require(isinstance(comparison,dict) and comparison.get('selected_version') == candidate_version, 'source case lacks actual selected-package comparison')
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
        if candidate_version == '3.125.1':
            validate_reminder_windows(observation, successful_http, configurations, checks, row['id'])
            validate_reminder_identity(observation, successful_http, checks, row['id'])
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

    if not incomplete and lane == 'mixed-api' and row['id'].endswith('-status') and row['operation'] != 'unsupported':
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
