"""Retain only this qualification's process and disposable Dapr configuration identities."""
import hashlib
import json
import pathlib
import shutil

destination = pathlib.Path(__file__).resolve().parent
processes = {}
for path in pathlib.Path('/proc').iterdir():
    if not path.name.isdigit():
        continue
    try:
        arguments = (path / 'cmdline').read_bytes().split(b'\0')
        arguments = [value.decode() for value in arguments if value]
        status = (path / 'status').read_text()
        parent = int(next(line.split()[1] for line in status.splitlines() if line.startswith('PPid:')))
        processes[int(path.name)] = dict(pid=int(path.name), parent=parent, arguments=arguments)
    except (OSError, ValueError, UnicodeDecodeError, StopIteration):
        pass
roots = {pid for pid, value in processes.items() if value['arguments']
         and pathlib.Path(value['arguments'][0]).name == 'dotnet'
         and any(argument.endswith('/final-live.xml') for argument in value['arguments'])}
assert len(roots) == 1, roots
selected = set(roots)
while True:
    more = {pid for pid, value in processes.items() if value['parent'] in selected}
    if more.issubset(selected):
        break
    selected.update(more)
result = []
configuration = []
for pid in sorted(selected):
    value = processes[pid]
    try:
        executable = pathlib.Path('/proc') / str(pid) / 'exe'
        value['executable'] = str(executable.resolve())
        value['executableSha256'] = hashlib.sha256(executable.read_bytes()).hexdigest()
    except OSError:
        value['exitedBeforeExecutableCapture'] = True
    arguments = value['arguments']
    if any('daprd' in argument for argument in arguments):
        for flag in ('--resources-path', '--components-path', '--config'):
            if flag not in arguments:
                continue
            source = pathlib.Path(arguments[arguments.index(flag) + 1])
            files = sorted(source.glob('*')) if source.is_dir() else [source]
            for path in files:
                if path.is_file():
                    target = destination / 'runtime-configuration' / path.name
                    target.parent.mkdir(exist_ok=True)
                    shutil.copyfile(path, target)
                    configuration.append(dict(originalPath=str(path), retainedPath=str(target),
                                              sha256=hashlib.sha256(target.read_bytes()).hexdigest()))
    result.append(value)
(destination / 'runtime-processes.json').write_text(json.dumps(dict(processes=result, configuration=configuration), indent=2) + '\n')
print(json.dumps(dict(qualification_pids=sorted(selected), configuration_files=len(configuration))))
