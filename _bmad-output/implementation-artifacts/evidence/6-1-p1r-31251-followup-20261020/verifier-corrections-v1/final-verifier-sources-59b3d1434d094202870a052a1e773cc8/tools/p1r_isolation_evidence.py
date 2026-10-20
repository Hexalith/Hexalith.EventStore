"""Strict successful local-controller journal validation for the proposed successor.

Historical receipts keep their original contract. This validator accepts the
producer's complete ordered lifecycle and never grants publication authority.
"""
from pathlib import Path
import json
import re

import p1r_isolation as isolation
import p1r_qualification as preparation
from p1r_qualification import require, digest, regular, validate_identity, validate_identity_list


# Measured from the unchanged Linux/amd64 manifest-pinned Docker and SDK images.
# These hashes bind copied tools even when both retained proofs are substituted.
PINNED_EXECUTABLES = {
    'docker': {'path':'/usr/local/bin/docker',
               'sha256':'6f7a95d27f98fc8c5f6b220898988bf8f61dd9b040936797917cbdc72c6cabe6'},
    'dotnet': {'path':'/usr/share/dotnet/dotnet',
               'sha256':'01d89e0a0191052bfea616cd4ce624c8faf13b05bbddf7f64499c23e2a9d9269'},
}


def canonical_engine_directory(value):
    """Validate an engine's absolute POSIX directory lexically, without local lookup."""
    return isinstance(value, str) and isolation.supported_workspace_path(value)


def validate(wrapper, private, context, run_root, wrapper_path, times):
    """Check every literal command, measured identity and timed cleanup observation."""
    repository = Path(context['source_binding']['repository'])
    scratch = Path(wrapper['owned_scratch'])
    require(scratch.is_absolute() and scratch == scratch.resolve(), 'isolation scratch path substituted')
    require(bool(re.fullmatch(r'unix:///[^\s?#]+', wrapper.get('outer_host', ''))),
            'only a local Unix Docker endpoint is supported')
    owned = wrapper['owned_containers']
    require(len(owned) == 3 and sorted(row.get('role', '') for row in owned.values()) == ['daemon','python-tools','sdk'],
            'isolation lacks exact daemon, Python-tools and SDK roles')
    roles = {row['role']:identity for identity,row in owned.items()}
    require(all(re.fullmatch(r'[0-9a-f]{64}', identity) for identity in owned), 'invalid outer container identity')
    sdk, daemon, python = (roles[name] for name in ('sdk','daemon','python-tools'))
    require(wrapper.get('sdk') == sdk and wrapper.get('daemon') == daemon, 'outer role identity substituted')
    invocation, volume = wrapper['invocation'], wrapper['owned_volume']
    require(volume == 'p1r-isolation-'+invocation, 'outer volume identity substituted')
    mounts = wrapper['runner_proof']['Mounts']
    workspace = Path(next(row['Destination'] for row in mounts if row['Type'] == 'bind' and repository.is_relative_to(Path(row['Destination']))))
    source = context['source_binding'].get('candidate_source')
    source_root = Path(source['repository']) if source is not None else None
    bind_roots = {workspace} | ({source_root} if source_root is not None and not source_root.is_relative_to(workspace) else set())
    if source is not None:
        require(wrapper.get('source_root') == str(source_root)
                and isolation.command_source_root(wrapper['inner_command']) == source_root,
                'isolation selected tagged source root differs')
        controller = wrapper.get('controller_source_binding', {})
        def without_python(binding):
            return {key:(without_python(value) if key == 'candidate_source' else value)
                    for key,value in binding.items() if key != 'python'}
        require(without_python(controller) == without_python(context['source_binding']),
                'controller and private source closures differ')
    outer_socket = Path(wrapper['outer_host'].removeprefix('unix://'))
    require(canonical_engine_directory(str(workspace)) and not outer_socket.is_relative_to(workspace)
            and not outer_socket.resolve().is_relative_to(workspace), 'isolation workspace exposes the outer Docker socket')
    require(workspace.is_absolute() and workspace == workspace.resolve() and repository.is_relative_to(workspace)
            and Path(wrapper_path).is_relative_to(workspace) and run_root.is_relative_to(workspace),
            'isolation workspace containment differs')
    command = wrapper['inner_command']
    selected = isolation.executor_input(command, repository)
    preparation.validate_selected_source(context['inputs']['value'], context['source_binding'])
    runtime = context['source_binding'].get('runtime_source')
    paths = isolation.source_arguments(command)
    require(paths == ((Path(runtime['repository']), Path(runtime['builds']['path'])) if runtime else ()),
            'isolation runtime source paths differ from selected binding')
    require(all(path.is_relative_to(workspace) for path in paths),
            'runtime source paths are outside the isolated workspace')
    if runtime:
        preparation.check_current_source(context['source_binding'])
    require(command[4] == str(run_root) and selected == wrapper.get('selected_input')
            and selected['sha256'] == context['inputs']['sha256']
            and json.loads(regular(Path(selected['path']))) == context['inputs']['value'],
            'isolation selected physical input or hash substituted')
    proof, measured = private.get('proof', {}), wrapper.get('toolchain_proof', {})
    require(isinstance(measured,dict) and measured, 'isolation toolchain measurement missing')
    for name in ('executables','python_version','dotnet_version','docker_version','glibc','daemon','outer_daemon_id','socket','namespaces','daemon_namespaces'):
        require(proof.get(name) == measured.get(name) and name in proof, 'isolation measured toolchain differs: '+name)
    for name in ('python','docker','dotnet'):
        item = proof['executables'].get(name,{})
        require(set(item) == {'path','sha256'} and Path(item['path']).is_absolute()
                and bool(re.fullmatch(r'[0-9a-f]{64}', item['sha256'])), 'invalid private executable identity')
    require(all(proof['executables'][name] == expected for name,expected in PINNED_EXECUTABLES.items()),
            'pinned Docker or dotnet executable bytes or location differ')
    for name in ('namespaces','daemon_namespaces'):
        links = proof[name]
        require(isinstance(links,dict) and set(links) == {'net','pid','mnt'}
                and all(isinstance(value,str) and re.fullmatch(kind+r':\[[1-9][0-9]*\]',value)
                        for kind,value in links.items()), 'complete private namespace links missing or invalid')
    require(isinstance(proof['glibc'],str) and bool(re.fullmatch(r'glibc [0-9]+\.[0-9]+',proof['glibc'])),
            'private glibc measurement missing or invalid')
    require(proof['dotnet_version'] == '10.0.401'
            and proof['docker_version'].get('Client',{}).get('Version') == '29.8.2'
            and proof['docker_version'].get('Server',{}).get('Version') == proof['daemon'].get('ServerVersion') == '29.8.2',
            'pinned private Docker or dotnet version differs')
    for name in ('supervisor','root'):
        validate_identity(private.get(name))
    daemon_process = preparation.identity(proof.get('process',{}))
    validate_identity(daemon_process)
    require(daemon_process == preparation.identity(measured.get('process',{})), 'dockerd process identity substituted')
    require(len({row['pid'] for row in (private['supervisor'],private['root'],daemon_process)}) == 3,
            'private supervisor, root and dockerd identities overlap')
    processes = private.get('owned_processes')
    validate_identity_list(processes)
    protected = {private['supervisor']['pid'],daemon_process['pid']}
    require(private['root']['pid'] == private['root']['pgid'] == private['root']['session'],
            'private root does not own its new session')
    require(private['root'] in processes and all(row['pid'] not in protected for row in processes),
            'private owned process inventory or root missing')
    attempts = private.get('cleanup',[])
    require(len(attempts) == 2, 'private cleanup must contain two distinct observations')
    previous = times(private)[0]
    previous_owned, absent = {}, set()
    identity_key = lambda row:(row['pid'],row['start_ticks'])
    final_owned = {identity_key(row):row for row in processes}
    for index,attempt in enumerate(attempts,1):
        start,end = times(attempt)
        require(type(attempt.get('id')) is int and attempt['id'] == index and previous <= start <= end <= times(private)[1],
                'private cleanup observations duplicate or reorder')
        for name in ('owned_processes','before','remaining'):
            validate_identity_list(attempt.get(name))
        snapshot = {identity_key(row):row for row in attempt['owned_processes']}
        require(private['root'] in attempt['owned_processes']
                and all(snapshot.get(key) == row for key,row in previous_owned.items())
                and all(final_owned.get(key) == row for key,row in snapshot.items()),
                'private cleanup ownership inventory is incomplete or not monotonic')
        observations = [*attempt['before'],*attempt['remaining']]
        require(all(row['pid'] not in protected for row in [*attempt['owned_processes'],*observations])
                and all(identity_key(row) not in absent for row in observations),
                'protected process alias or resurrected cleanup process')
        require(all(snapshot.get(identity_key(row)) == row for row in observations)
                and attempt['remaining'] == [] and attempt.get('errors') == [],
                'private cleanup ownership or absence differs')
        absent.update(snapshot.keys()-{identity_key(row) for row in attempt['remaining']})
        previous_owned, previous = snapshot, end
    require(previous_owned == final_owned, 'final private ownership differs from complete cleanup inventory')
    commands = wrapper.get('commands',[])
    prefix = ['docker','--host',wrapper['outer_host'],'--config',str(scratch/'docker-config')]
    require(isinstance(commands,list) and commands, 'isolation command journal missing')
    previous = times(wrapper)[0]
    for index,row in enumerate(commands,1):
        start,end = times(row)
        require(type(row.get('id')) is int and row['id'] == index and previous <= start <= end <= times(wrapper)[1]
                and row.get('argv',[])[:5] == prefix and type(row.get('exit_code')) is int
                and isinstance(row.get('output'),str) and digest(row['output'].encode()) == row.get('output_sha256'),
                'isolation command IDs, timestamps, endpoint or output differ')
        previous = end
    cursor = 0
    def take(argv, success=True):
        nonlocal cursor
        require(cursor < len(commands), 'isolation literal lifecycle command missing')
        row = commands[cursor]
        cursor += 1
        require(row['argv'][5:] == list(map(str,argv)), 'isolation literal lifecycle command substituted')
        require(success is None or (row['exit_code'] == 0) == success, 'isolation command exit differs')
        return row
    def parsed(row):
        try:
            return json.loads(row['output'])
        except (ValueError,TypeError) as error:
            raise preparation.InvalidPacket('isolation measured command output is invalid') from error
    static_inspections = {}
    def inspected(identity, absent=False):
        row = take(['inspect','--format',isolation.INSPECT,identity], not absent)
        if absent:
            require('no such' in row['output'].lower(), 'container absence command differs')
            return None
        value = parsed(row)
        role = owned[identity]['role']
        image_role = 'python' if role == 'python-tools' else role
        require(value.get('Id') == identity and value.get('Name') == '/p1r-isolation-'+invocation+'-'+role
                and value.get('Labels',{}).get(isolation.LABEL) == invocation
                and value.get('Image') == wrapper['image_measurements'][image_role]['Id'],
                'literal container inspection or measured image substituted')
        settings = {name:value.get(name) for name in ('NetworkMode','PidMode','Privileged','Ports','Mounts')}
        expected_modes = {'daemon':('bridge','',True), 'python-tools':('none','',False),
                          'sdk':('container:'+daemon,'container:'+daemon,False)}
        network,pid,privileged = expected_modes[role]
        require(settings['NetworkMode'] == network and settings['PidMode'] == pid
                and settings['Privileged'] is privileged and settings['Ports'] == {},
                'observed role namespace, privilege or ports contradict creation')
        observed_mounts = settings['Mounts']
        require(isinstance(observed_mounts,list), 'observed role mounts missing')
        expected_destinations = {'daemon':{'/var/lib/docker'},'python-tools':set(),
                                 'sdk':{'/var/lib/docker',*(str(path) for path in bind_roots)}}[role]
        require(len(observed_mounts) == len(expected_destinations)
                and {mount.get('Destination') for mount in observed_mounts} == expected_destinations,
                'observed role mounts contradict creation')
        for mount in observed_mounts:
            require(mount.get('RW') is True
                    and (mount['Destination'] == '/var/lib/docker' and mount.get('Type') == 'volume'
                         and mount.get('Name') == volume
                         or Path(mount['Destination']) in bind_roots and mount.get('Type') == 'bind'
                         and canonical_engine_directory(mount.get('Source'))), 'observed role mount identity or access differs')
        require(identity not in static_inspections or static_inspections[identity] == settings,
                'observed static role configuration changed between inspections')
        static_inspections[identity] = settings
        return value
    require(take(['info','--format','{{.ID}}'])['output'].strip() == wrapper['outer_daemon_id'],
            'outer daemon literal identity differs')
    outer = wrapper.get('outer_toolchain',{})
    executable = outer.get('docker',{})
    require(Path(executable.get('path','')).is_absolute() and bool(re.fullmatch(r'[0-9a-f]{64}',executable.get('sha256','')))
            and parsed(take(['version','--format','{{json .}}'])) == outer.get('version')
            and outer.get('version',{}).get('Client',{}).get('Version')
            and outer.get('version',{}).get('Server',{}).get('Version'), 'outer Docker toolchain identity missing')
    for role in ('sdk','daemon','python'):
        image = wrapper['images'][role]
        take(['pull','--platform','linux/amd64',image])
        image_value = parsed(take(['image','inspect','--format',isolation.IMAGE_INSPECT,image]))
        require(image_value == wrapper.get('image_measurements',{}).get(role)
                and image_value.get('Os') == 'linux' and image_value.get('Architecture') == 'amd64'
                and image in image_value.get('RepoDigests',[])
                and bool(re.fullmatch(r'sha256:[0-9a-f]{64}',image_value.get('Id',''))),
                'literal image OS, architecture, digest or image ID differs')
    require(take(['volume','create','--label',isolation.LABEL+'='+invocation,volume])['output'].strip() == volume,
            'literal owned volume creation differs')
    def created(role,image,options,entry):
        identity = roles[role]
        require(owned[identity] == {'role':role,'image':image}, 'owned image role differs')
        row = take(['create','--cidfile',scratch/(role+'.cid'),'--name','p1r-isolation-'+invocation+'-'+role,
                    '--label',isolation.LABEL+'='+invocation,*options,image,*entry])
        require(row['output'].strip() == identity, 'literal created container ID differs')
        inspected(identity)
    created('daemon',isolation.DAEMON_IMAGE,
            ['--privileged','--network','bridge','--mount','type=volume,src='+volume+',dst=/var/lib/docker','--entrypoint','dockerd'],
            ['--host='+isolation.SOCKET,'--data-root=/var/lib/docker/data','--exec-root=/var/lib/docker/p1r-exec',
             '--pidfile=/var/lib/docker/p1r-run/dockerd.pid','--storage-driver=vfs','--tls=false'])
    start_command = take(['start',daemon])
    startup_start = times(start_command)[0]
    require((times(start_command)[1]-startup_start).total_seconds() <= isolation.STARTUP_SECONDS,
            'private daemon start exceeds startup deadline')
    while True:
        row = take(['exec',daemon,'docker','--host',isolation.SOCKET,'info','--format','{{.ID}}'],None)
        start,end = times(row)
        require((end-startup_start).total_seconds() <= isolation.STARTUP_SECONDS
                and (end-start).total_seconds() <= isolation.PROBE_SECONDS, 'private startup probes exceed deadline')
        if row['exit_code'] == 0:
            require(row['output'].strip() == proof['daemon']['ID'], 'private startup daemon ID differs')
            break
    created('python-tools',isolation.PYTHON_IMAGE,['--network','none','--entrypoint','/bin/true'],[])
    take(['cp',python+':/usr/local',scratch/'python'])
    take(['cp',daemon+':/usr/local/bin/docker',scratch/'docker'])
    created('sdk',isolation.SDK_IMAGE,isolation.sdk_arguments(workspace,repository,invocation,daemon,volume,
            paths or ((source_root,) if source_root is not None else ())),['infinity'])
    take(['cp',scratch/'python',sdk+':/opt/python'])
    take(['cp',scratch/'docker',sdk+':/usr/local/bin/docker'])
    take(['start',sdk])
    require(inspected(sdk) == wrapper['runner_proof'], 'runner inspection differs from literal output')
    require(parsed(take(['exec',sdk,'python3',repository/'tools/p1r-isolated-runner.py','_proof','--outer-id',wrapper['outer_daemon_id']])) == measured,
            'literal SDK toolchain proof substituted')
    source_arguments = ['--source-root', str(source_root)] if source_root is not None else []
    launched = take(['exec',sdk,'python3',repository/'tools/p1r-isolated-runner.py','_inner','--out',Path(wrapper_path).parent,
                     '--outer-id',wrapper['outer_daemon_id'],*source_arguments,'--',*command])
    require(times(launched)[0] <= times(private)[0] <= times(private)[1] <= times(launched)[1]
            and launched['output'] == wrapper.get('command_stdout'), 'actual SDK launch or private interval substituted')
    require(times(private)[0] <= times(private['cleanup'][0])[0]
            and times(private['cleanup'][0])[0] >= times(context['execution'])[1],
            'process cleanup precedes executor completion')
    outer_attempts = wrapper['cleanup']
    require(len(outer_attempts) == 2, 'outer cleanup requires two distinct observations')
    previous = times(launched)[1]
    for index,attempt in enumerate(outer_attempts,1):
        start,end = times(attempt)
        require(type(attempt.get('id')) is int and attempt['id'] == index and previous <= start <= end <= times(wrapper)[1],
                'outer cleanup observations duplicate or reorder')
        first_cursor = cursor
        if index == 1:
            inspected(daemon)
            inventory = take(['exec',daemon,'docker','--host',isolation.SOCKET,'ps','-aq','--no-trunc'])
            require(not inventory['output'].strip() and attempt.get('nested_inventory') == {'daemon':daemon,'complete':True,'containers':[]},
                    'private complete empty inventory differs')
        else:
            inspected(daemon,True)
            require(attempt.get('nested_inventory') == {'daemon_absent':True,'prior_empty_inventory':True},
                    'private daemon absence lacks earlier complete inventory')
        for identity in (sdk,python,daemon):
            inspected(identity, index == 2)
            if index == 1:
                if identity == daemon:
                    take(['logs','--tail','200',identity],None)
                take(['rm','-f',identity])
            inspected(identity,True)
            inspected(identity,True)
        row = take(['volume','inspect','--format','{{json .}}',volume],index == 1)
        if index == 1:
            value = parsed(row)
            require(value.get('Name') == volume and value.get('Labels',{}).get(isolation.LABEL) == invocation,
                    'volume cleanup ownership differs')
            take(['volume','rm',volume])
        else:
            require('no such volume' in row['output'].lower(), 'literal volume absence differs')
        for _ in range(2):
            row = take(['volume','inspect','--format','{{json .}}',volume],False)
            require('no such volume' in row['output'].lower(), 'literal repeated volume absence differs')
        require(start <= times(commands[first_cursor])[0] and times(commands[cursor-1])[1] <= end,
                'cleanup commands escape timed observation')
        require(attempt.get('targeted') == [sdk,python,daemon,'volume:'+volume,'scratch:'+str(scratch)],
                'cleanup targeted identities differ')
        previous = end
    require(cursor == len(commands), 'unexpected Docker command in isolation journal')
