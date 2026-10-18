"""A Docker boundary fixture driving the real controller, finalizer and importer."""
import base64
import copy
from contextlib import contextmanager
from datetime import datetime, timedelta, timezone
import json
import os
import sys
import subprocess
from pathlib import Path
from types import SimpleNamespace
from unittest import mock

import p1r_isolation as isolation
import p1r_published_qualification as qualification
import p1r_qualification as preparation

_SOURCE_BINDING = preparation.source_binding


@contextmanager
def separate_source_fixture(root):
    """Use real disposable Git inventories; only replace the immutable release coordinates."""
    root = Path(root)
    def command(repository, *argv):
        return subprocess.check_output(['git', *argv], cwd=repository, stderr=subprocess.PIPE).decode().strip()
    def repository(path, content):
        path.mkdir(parents=True)
        command(path, 'init', '-q')
        command(path, 'config', 'user.email', 'fixture@example.invalid')
        command(path, 'config', 'user.name', 'P1R fixture')
        (path/'README.md').write_text(content+'\n')
        command(path, 'add', 'README.md')
        command(path, 'commit', '-qm', 'test: disposable binding fixture')
        return path
    tooling = repository(root/'tooling', 'tooling')
    builds = repository(tooling/'references/Hexalith.Builds', 'qualification catalog')
    source_builds = repository(root/'runtime-builds', 'release catalog')
    source = repository(root/'runtime', 'release source')
    for owner, dependency in ((tooling, builds), (source, source_builds)):
        (owner/'.gitmodules').write_text('[submodule "Builds"]\n path = references/Hexalith.Builds\n url = fixture\n')
        (owner/'src').mkdir()
        (owner/'src/Runtime.cs').write_text('// committed runtime\n')
        command(owner, 'add', '.gitmodules', 'src/Runtime.cs')
        command(owner, 'update-index', '--add', '--cacheinfo', '160000,'+command(dependency,'rev-parse','HEAD')+',references/Hexalith.Builds')
        command(owner, 'commit', '-qm', 'test: declare fixture inputs')
    release = command(source, 'rev-parse', 'HEAD')
    release_builds = command(source_builds, 'rev-parse', 'HEAD')
    command(source, 'tag', 'v3.125.1')
    pins = {'commit':command(tooling,'rev-parse','HEAD'), 'builds_commit':command(builds,'rev-parse','HEAD')}
    with mock.patch.object(preparation,'RUNTIME_COMMIT',release), \
            mock.patch.object(preparation,'RUNTIME_BUILDS_COMMIT',release_builds), \
            mock.patch.object(preparation,'retained_inputs',return_value=[]), \
            mock.patch.object(preparation,'source_binding',_SOURCE_BINDING):
        yield SimpleNamespace(root=root,tooling=tooling,builds=builds,source=source,source_builds=source_builds,
                              release=release,release_builds=release_builds,pins=pins,git=command)


def separate_inputs(fixture):
    """Select a non-executable test tuple with the complete real owner-input grammar."""
    import p1r_published_executor as executor
    def candidate(version, commit):
        return {'version':version,'tag':'v'+version,'tag_commit':commit,
                'builds':{'version':'4.30.1','commit':'b'*40},'feed':'https://fixture.invalid/v3/index.json',
                'packages':[{'id':identifier,'archive_sha256':'c'*64,'content_hash':base64.b64encode(b'x'*64).decode(),
                             'repository_commit':commit} for identifier in executor.PACKAGE_IDS]}
    selected = candidate('3.125.1',fixture.release)
    selected['tag_builds_commit'] = fixture.release_builds
    return {'schema':qualification.INPUTS_SCHEMA,'fixture':None,
            'authority':{'owner':'fixture','reference':'temporary-repositories-only','date':'2026-10-18'},
            'candidate':selected,'rollback':None,'tooling':dict(fixture.pins),
            'comparisons':[candidate(version,'d'*40) for version in qualification.COMPARISON_VERSIONS],
            'operational_profile':{'runtime':'dapr','runtime_version':'1.18.2','backend':'state.postgresql',
                'backend_image':executor.POSTGRES,'selected_by':'fixture','reference':'temporary-repositories-only'},
            'assertion_instrumentation':{'mechanism':executor.MECHANISM,'accepted_by':'test-owner','reference':'fixture'},
            'selected_additions':list(qualification.ADDITIONS)}


class DockerBoundary:
    """Only Docker's OS boundary is simulated; lifecycle receipts use the producer."""
    def __init__(self, root, receipt, context, private_failure=False, outer_failure=False, real_inner=False, mapped_bind=False, late_process=False):
        self.real_run = subprocess.run
        self.real_popen = subprocess.Popen
        self.use_real_inner = real_inner
        self.mapped_bind = mapped_bind
        self.late_process = late_process
        self.root, self.receipt, self.context = Path(root), receipt, context
        self.private_failure, self.outer_failure = private_failure, outer_failure
        self.current = datetime(2026,10,13,23,59,50,tzinfo=timezone.utc)
        self.containers, self.volume = {}, None
        self.images = {image:{'Id':'sha256:'+str(index)*64,'RepoDigests':[image],'Os':'linux','Architecture':'amd64'}
                       for index,image in enumerate((isolation.SDK_IMAGE,isolation.DAEMON_IMAGE,isolation.PYTHON_IMAGE),1)}
        python = context['source_binding']['python']
        self.proof = {'outer_daemon_id':'outer','daemon':{'ID':'private','DockerRootDir':'/var/lib/docker/data','ServerVersion':'29.8.2'},
            'socket':isolation.SOCKET,'namespaces':{'net':'net:[1]','pid':'pid:[2]','mnt':'mnt:[3]'},
            'daemon_namespaces':{'net':'net:[1]','pid':'pid:[2]','mnt':'mnt:[4]'},
            'process':{'pid':7,'start_ticks':7,'pgid':7,'session':7},'glibc':'glibc 2.36',
            'executables':{'python':{'path':python['path'],'sha256':python['sha256']},
                           'docker':{'path':'/usr/local/bin/docker','sha256':'6f7a95d27f98fc8c5f6b220898988bf8f61dd9b040936797917cbdc72c6cabe6'},
                           'dotnet':{'path':'/usr/share/dotnet/dotnet','sha256':'01d89e0a0191052bfea616cd4ce624c8faf13b05bbddf7f64499c23e2a9d9269'}},
            'python_version':python['version'],'dotnet_version':'10.0.401',
            'docker_version':{'Client':{'Version':'29.8.2'},'Server':{'Version':'29.8.2'}}}

    def stamp(self):
        self.current += timedelta(milliseconds=10)
        return self.current.isoformat()

    def run(self, argv, **kwargs):
        if argv[0] != "docker":
            return self.real_run(argv, **kwargs)
        args = argv[5:]
        code, output = 0, ''
        if args[0] == 'info':
            output = 'outer\n'
        elif args[0] == 'version':
            output = json.dumps(self.proof['docker_version'])
        elif args[:2] == ['image','inspect']:
            output = json.dumps(self.images[args[-1]])
        elif args[:2] == ['volume','create']:
            self.volume = {'Name':args[-1],'Labels':{isolation.LABEL:self.worker.invocation}}
            output = args[-1]+'\n'
        elif args[0] == 'create':
            name = args[args.index('--name')+1]
            role = name.split(self.worker.invocation+'-',1)[1]
            identity = {'daemon':'d','python-tools':'e','sdk':'c'}[role]*64
            image = {'daemon':isolation.DAEMON_IMAGE,'python-tools':isolation.PYTHON_IMAGE,'sdk':isolation.SDK_IMAGE}[role]
            value = {'Id':identity,'Image':self.images[image]['Id'],'Name':'/'+name,
                     'Labels':{isolation.LABEL:self.worker.invocation},
                     'NetworkMode':'bridge' if role=='daemon' else 'none', 'PidMode':'',
                     'Ports':{}, 'Privileged':role=='daemon',
                     'Mounts':[{'Destination':'/var/lib/docker','Type':'volume','Name':self.worker.volume,'RW':True}] if role=='daemon' else []}
            if role == 'sdk':
                value.update(NetworkMode='container:'+'d'*64,PidMode='container:'+'d'*64,Ports={},Privileged=False,
                    Mounts=[{'Destination':'/var/lib/docker','Type':'volume','Name':self.worker.volume,'RW':True},
                            {'Destination':str(self.worker.workspace),'Source':('/run/desktop/mnt/host/wsl/docker-desktop-bind-mounts/fixture/'+('a'*64)
                                      if self.mapped_bind else str(self.worker.workspace)),'Type':'bind','RW':True}])
            self.containers[identity] = value
            Path(args[args.index('--cidfile')+1]).write_text(identity)
            output = identity+'\n'
        elif args[0] == 'inspect':
            value = self.containers.get(args[-1])
            code,output = (0,json.dumps(value)) if value else (1,'No such container')
        elif args[:2] == ['volume','inspect']:
            code,output = (0,json.dumps(self.volume)) if self.volume else (1,'no such volume')
        elif args[:2] == ['volume','rm']:
            self.volume = None
        elif args[:2] == ['rm','-f']:
            if self.outer_failure and args[2] == 'c'*64:
                return SimpleNamespace(returncode=1,stdout=b'injected removal failure')
            self.containers.pop(args[2],None)
        elif args[0] == 'exec':
            if args[2:4] == ['docker','--host']:
                output = 'private\n' if 'info' in args else ''
            elif '_proof' in args:
                output = json.dumps(self.proof)
            elif '_inner' in args:
                self.persist_private()
                code = 2 if self.private_failure else 0
                self.current = datetime(2026,10,14,0,0,32,tzinfo=timezone.utc)
        return SimpleNamespace(returncode=code,stdout=output.encode())

    def persist_private(self):
        if self.use_real_inner:
            command = self.worker.result['inner_command']
            # Exercise the real supervisor, procfs discovery and teardown. Only
            # the published executor OS launch is replaced by harmless Python.
            child = "import time; time.sleep(30)"
            script = ("import subprocess,sys,time; subprocess.Popen([sys.executable,'-c',"+repr(child)+"],"
                      "start_new_session=True); time.sleep(.2)")
            def launch(argv, **kwargs):
                if argv == command:
                    self.current = datetime(2026,10,14,0,0,31,tzinfo=timezone.utc)
                    return self.real_popen([sys.executable,'-c',script],**kwargs)
                return self.real_popen(argv,**kwargs)
            original_cleanup = preparation.OwnedProcesses.cleanup
            cleanup_calls, late_children = 0, []
            def cleanup(owner):
                nonlocal cleanup_calls
                cleanup_calls += 1
                if self.late_process and cleanup_calls == 2:
                    late_children.append(self.real_popen([sys.executable,'-c',child],start_new_session=True,
                        env=dict(os.environ,HEXALITH_P1R_CONTROL_NONCE=owner.nonce)))
                return original_cleanup(owner)
            self.current = datetime(2026,10,13,23,59,59,tzinfo=timezone.utc)
            with mock.patch.dict(os.environ,{isolation.MARKER:self.worker.invocation}), \
                    mock.patch.object(isolation,'private_proof',return_value=copy.deepcopy(self.proof)), \
                    mock.patch.object(isolation,'private_environment',return_value=dict(os.environ)), \
                    mock.patch.object(preparation,'source_binding',return_value=self.context['source_binding']), \
                    mock.patch.object(preparation,'check_current_source'), \
                    mock.patch.object(subprocess,'Popen',side_effect=launch), \
                    mock.patch.object(preparation.OwnedProcesses,'cleanup',cleanup):
                preparation.require(isolation.inner(self.worker.output,'outer',command) == 0,
                                    'real local supervisor control failed')
            for process in late_children:
                process.wait(timeout=2)
            return
        identity = lambda pid:{'pid':pid,'start_ticks':pid,'pgid':pid,'session':pid}
        private = {'schema':isolation.SCHEMA,'scope':'isolated-command-verification','invocation':self.worker.invocation,
            'started_utc':'2026-10-13T23:59:59+00:00','finished_utc':'2026-10-14T00:00:32+00:00',
            'passed':not self.private_failure,'exit_code':2 if self.private_failure else 0,
            'errors':['injected private failure'] if self.private_failure else [],'command':self.worker.result['inner_command'],
            'source_binding':self.context['source_binding'],'source_checked_after_cleanup':True,
            'supervisor':identity(100),'root':identity(101),'owned_processes':[identity(101)],'proof':copy.deepcopy(self.proof),
            'cleanup':[{'id':index+1,'owned_processes':[identity(101)],'before':[identity(101)] if index==0 else [],'remaining':[],'errors':[],
                        'started_utc':f'2026-10-14T00:00:31.{index*3+1}00000+00:00',
                        'finished_utc':f'2026-10-14T00:00:31.{index*3+2}00000+00:00'} for index in range(2)]}
        isolation.write_json(self.worker.output/'private-execution.json',private)


def run_controller(root, receipt, context, private_failure=False, outer_failure=False, finalize=None, real_inner=False, mapped_bind=False, late_process=False,
                   source_paths=()):
    """Persist producer output through real _run; no finalizer or importer replacement."""
    root = Path(root)
    selected = root/'inputs.json'
    isolation.write_json(selected,context['inputs']['value'])
    selected_hash = preparation.digest(selected.read_bytes())
    context['inputs']['sha256'] = receipt['inputs_sha256'] = selected_hash
    path = root/'receipts'/(receipt['id']+'.json')
    isolation.write_json(path,receipt)
    execution = json.loads((root/'execution.json').read_bytes())
    execution.update(schema='hexalith.p1r.executor.v1',started_utc='2026-10-14T00:00:00+00:00',
        finished_utc='2026-10-14T00:00:31+00:00',errors=[],inputs_sha256=selected_hash,receipts=[str(path)])
    isolation.write_json(root/'execution.json',execution)
    boundary = DockerBoundary(root,receipt,context,private_failure,outer_failure,real_inner,mapped_bind,late_process)
    command = ['python3',str(preparation.ROOT/'tools/p1r-published-executor.py'),'run','--out',str(root),'--inputs',str(selected)]
    if source_paths:
        command += ['--source-root',str(source_paths[0]),'--source-builds-root',str(source_paths[1])]
    with mock.patch.object(isolation,'stamp',side_effect=boundary.stamp), \
            mock.patch.object(isolation.subprocess,'run',side_effect=boundary.run), \
            mock.patch.object(qualification,'load_document',return_value=(context['inputs']['value'],selected.read_bytes(),'owner-selected')):
        worker = isolation.IsolatedRunner(root/'wrapper',workspace=preparation.ROOT,outer_host='unix:///outer')
        boundary.worker = worker
        # Fixture executor timestamps are historical, so launch starts before them.
        original_run = boundary.run
        def run(argv,**kwargs):
            if '_inner' in argv:
                boundary.current = datetime(2026,10,13,23,59,58,tzinfo=timezone.utc)
            return original_run(argv,**kwargs)
        with mock.patch.object(isolation.subprocess,'run',side_effect=run):
            if finalize is None:
                code = worker._run(command)
            else:
                with mock.patch.object(qualification,'finalize_isolation_receipts',side_effect=finalize):
                    code = worker._run(command)
    return code,worker,json.loads(path.read_bytes())
