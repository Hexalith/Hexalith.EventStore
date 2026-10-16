import base64, concurrent.futures, datetime, hashlib, json, os, pathlib, re, urllib.request, urllib.error
ROOT=pathlib.Path('/home/administrator/projects/hexalith/p1r-canonical-reconciliation-20261016')
OUT=pathlib.Path(__file__).resolve().parent
manifest=json.loads((ROOT/'tools/release-packages.json').read_text())
versions=['3.122.1',json.loads((OUT/'commit-analysis.json').read_text())['analysis_only_version']]
versions=list(dict.fromkeys(v for v in versions if v))
def observe(url,method='GET',authorization=None):
 headers={'Accept':'application/json','User-Agent':'Hexalith-P1R-source-only-observation'}
 if authorization: headers['Authorization']=authorization
 request=urllib.request.Request(url,headers=headers,method=method)
 started=datetime.datetime.now(datetime.timezone.utc).isoformat()
 try:
  with urllib.request.urlopen(request,timeout=25) as r: status=r.status; body=r.read() if method=='GET' else b''
 except urllib.error.HTTPError as error: status=error.code; body=error.read()
 except (urllib.error.URLError,TimeoutError) as error: status=None; body=b''
 row={'url':url,'method':method,'started_at_utc':started,'status':status,'response_sha256':hashlib.sha256(body).hexdigest(),'response_bytes':len(body),'authenticated':authorization is not None}
 return row,body
jobs=[]
for item in manifest['packages']:
 name=item['id'].lower(); jobs.append(('index-'+name,'https://api.nuget.org/v3-flatcontainer/'+name+'/index.json','GET'))
 for v in versions:jobs.append(('archive-'+name+'-'+v,f'https://api.nuget.org/v3-flatcontainer/{name}/{v}/{name}.{v}.nupkg','HEAD'))
for v in versions:
 jobs.extend([('github-tag-'+v,'https://api.github.com/repos/Hexalith/Hexalith.EventStore/git/ref/tags/v'+v,'GET'),('github-release-'+v,'https://api.github.com/repos/Hexalith/Hexalith.EventStore/releases/tags/v'+v,'GET'),('oci-manifest-'+v,'https://registry.hexalith.com/v2/eventstore/manifests/'+v,'HEAD')])
jobs.append(('oci-tags','https://registry.hexalith.com/v2/eventstore/tags/list','GET'))
rows=[]
def execute(job):
 name,url,method=job; row,body=observe(url,method); row['name']=name
 if body:(OUT/(name+'.response')).write_bytes(body)
 if name.startswith('index-') and row['status']==200:
  vs=json.loads(body)['versions'];stable=[tuple(map(int,v.split('.'))) for v in vs if re.fullmatch(r'(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)',v)]
  row['highest_stable']='.'.join(map(str,max(stable))) if stable else None;row['examined_versions']={v:v in vs for v in versions}
 return row
with concurrent.futures.ThreadPoolExecutor(max_workers=6) as pool: rows=list(pool.map(execute,jobs))
user=os.environ.get('HEXALITH_ZOT_USERNAME','');key=os.environ.get('HEXALITH_ZOT_API_KEY','')
authrows=[]
if user and key:
 auth='Basic '+base64.b64encode((user+':'+key).encode()).decode()
 for name,url,method in [j for j in jobs if j[0].startswith('oci-')]:
  row,body=observe(url,method,auth);row['name']='authenticated-'+name;authrows.append(row)
  if body:(OUT/(row['name']+'.response')).write_bytes(body)
config=pathlib.Path(os.environ.get('DOCKER_CONFIG',str(pathlib.Path.home()/'.docker')))/'config.json'
try: cfg=json.loads(config.read_text())
except (FileNotFoundError,json.JSONDecodeError):cfg={}
registry_keys=('registry.hexalith.com','https://registry.hexalith.com')
configured=any(k in cfg.get('auths',{}) or k in cfg.get('credHelpers',{}) for k in registry_keys)
result={'scope':'read-only destination observations; versions are historical proposal and current analysis only; no reservation or selection','versions_examined':versions,'reserved_version':None,'selected_successor':None,'observations':rows,'authenticated_oci_observations':authrows,'oci_credentials':{'environment_pair_available':bool(user and key),'registry_entry_in_docker_config':configured,'global_credential_store_configured':bool(cfg.get('credsStore'))},'authenticated_registry_blocker':None if authrows else 'No publisher credential pair available to this process; unauthenticated 401 cannot establish tag absence or version floor. Authenticated publisher preflight remains required.'}
(OUT/'registry-observations.json').write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps({'observations':len(rows),'statuses':{str(s):sum(r['status']==s for r in rows) for s in set(r['status'] for r in rows)},'authenticated_registry_blocker':result['authenticated_registry_blocker'],'docker_registry_configured':configured}))
