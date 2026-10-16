import hashlib, json, os, pathlib, subprocess, sys
OWNER = pathlib.Path('/home/administrator/projects/hexalith/projects/references/Hexalith.EventStore')
PROJECTS = OWNER.parent.parent
OUT = OWNER / '_bmad-output/implementation-artifacts/evidence/6-1-p1r-canonical-reconciliation-20261016'
def sha(p):
    if p.is_symlink(): return 'symlink:' + os.readlink(p)
    return hashlib.file_digest(p.open('rb'), 'sha256').hexdigest()
def git(root,*args): return subprocess.check_output(['git', '-C', str(root), *args]).decode().strip()
def snapshot():
    result = {}
    for name, root in [('EventStore',OWNER),('Projects',PROJECTS)]:
        files = set(git(root,'ls-files','-z').split('\0')) - {''}
        if name == 'EventStore':
            for p in (root/'_bmad-output').rglob('*'):
                if OUT in p.parents or p == OUT: continue
                if p.is_file() or p.is_symlink(): files.add(p.relative_to(root).as_posix())
            files.update(git(root,'ls-files','--others','--exclude-standard','-z').split('\0'))
        files = {f for f in files if f and not f.startswith(OUT.relative_to(root).as_posix() + '/') } if name == 'EventStore' else files
        entries = {f:sha(root/f) for f in sorted(files) if (root/f).is_file() or (root/f).is_symlink()}
        index = pathlib.Path(git(root,'rev-parse','--path-format=absolute','--git-path','index'))
        result[name] = {'root':str(root),'head':git(root,'rev-parse','HEAD'), 'branch':git(root,'symbolic-ref','--quiet','HEAD'), 'index_sha256':sha(index), 'gitlinks':git(root,'ls-files','--stage').splitlines(), 'files':entries}
        result[name]['gitlinks'] = [s for s in result[name]['gitlinks'] if s.startswith('160000 ')]
    return result
if __name__ == '__main__':
    if sys.argv[1] == 'capture':
        (OUT/'starting-preservation.json').write_text(json.dumps(snapshot(),indent=2)+'\n')
        print('Preservation baseline captured')
    else:
        before=json.loads((OUT/'starting-preservation.json').read_text()); after=snapshot(); changes={}
        for name, state in before.items():
            current=after[name]
            changes[name]={'metadata_equal':all(state[k]==current[k] for k in ('head','branch','index_sha256','gitlinks')), 'starting_files':len(state['files']), 'changed_or_missing':[f for f,h in state['files'].items() if current['files'].get(f)!=h]}
        result={'checks':changes,'passed':all(v['metadata_equal'] and not v['changed_or_missing'] for v in changes.values())}
        (OUT/'final-preservation.json').write_text(json.dumps(result,indent=2)+'\n'); print(json.dumps(result)); sys.exit(0 if result['passed'] else 1)
