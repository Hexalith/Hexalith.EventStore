import base64,datetime,hashlib,json,pathlib,shutil,subprocess,urllib.request,urllib.error
out=pathlib.Path(__file__).resolve().parent
helper=shutil.which('docker-credential-desktop.exe');records=[]
result={'scope':'read-only authenticated registry observation using existing Docker credentials; no credentials or approvals created','credential_helper_argv':['docker-credential-desktop.exe','get'],'credential_stdout_retained':False,'credential_material_retained':False,'observations':records,'blocker':None}
try:
 if helper is None:raise RuntimeError('configured Docker credential helper is not executable in this environment')
 r=subprocess.run([helper,'get'],input=b'registry.hexalith.com\n',capture_output=True,timeout=15);result['credential_helper_exit_code']=r.returncode
 if r.returncode:raise RuntimeError('configured Docker credential helper failed to return credentials')
 c=json.loads(r.stdout);u=c.get('Username');s=c.get('Secret')
 if not u or not s:raise RuntimeError('configured Docker credential helper returned no usable credential pair')
 auth='Basic '+base64.b64encode((u+':'+s).encode()).decode()
 for name,path,method in [('tags','tags/list','GET'),('existing-3.122.0','manifests/3.122.0','HEAD'),('historical-3.122.1','manifests/3.122.1','HEAD'),('analysis-3.123.0','manifests/3.123.0','HEAD')]:
  url='https://registry.hexalith.com/v2/eventstore/'+path;headers={'Authorization':auth,'Accept':'application/vnd.oci.image.index.v1+json,application/vnd.docker.distribution.manifest.list.v2+json','User-Agent':'Hexalith-P1R-read-only-observation'};start=datetime.datetime.now(datetime.timezone.utc).isoformat();request=urllib.request.Request(url,headers=headers,method=method)
  try:
   with urllib.request.urlopen(request,timeout=25) as response:status=response.status;body=response.read() if method=='GET' else b'';link=response.headers.get('Link');digest=response.headers.get('Docker-Content-Digest')
  except urllib.error.HTTPError as error:status=error.code;body=error.read();link=None;digest=None
  except (urllib.error.URLError,TimeoutError):status=None;body=b'';link=None;digest=None
  row={'url':url,'method':method,'started_at_utc':start,'authenticated':True,'status':status,'response_sha256':hashlib.sha256(body).hexdigest(),'response_bytes':len(body),'next_page_link':link,'docker_content_digest':digest};records.append(row)
  if body:(out/('authenticated-'+name+'.response')).write_bytes(body)
  if name=='tags' and status==200:
   tags=json.loads(body).get('tags',[]);row['tags']=tags;row['historical_proposal_present']='3.122.1' in tags;row['analysis_version_present']='3.123.0' in tags
 if any(row['status'] in (None,401,403) for row in records):result['blocker']='Authenticated registry requests did not all provide usable destination evidence; inspect literal statuses.'
 if records and records[0].get('next_page_link'):result['blocker']='Registry tag listing is paginated; complete version-floor observation still required.'
except (RuntimeError,ValueError,subprocess.TimeoutExpired) as error:result['blocker']=str(error) if isinstance(error,RuntimeError) else type(error).__name__
(out/'authenticated-registry-observations.json').write_text(json.dumps(result,indent=2)+'\n')
print(json.dumps({'blocker':result['blocker'],'statuses':[r['status'] for r in records],'tags':records[0].get('tags') if records else None}))
