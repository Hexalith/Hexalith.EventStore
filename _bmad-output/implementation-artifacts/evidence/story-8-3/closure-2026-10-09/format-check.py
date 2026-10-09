"""Validate scoped Allman/LF whitespace in an isolated copy without changing repository configuration."""
import pathlib,subprocess,shutil,tempfile,json,hashlib
root=pathlib.Path(__file__).resolve().parents[5]
projects={
 'tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj':['tests/Hexalith.EventStore.Contracts.Tests/Packaging/ReleasePackageManifestTests.cs'],
 'tests/Hexalith.EventStore.PayloadProtection.Tests/Hexalith.EventStore.PayloadProtection.Tests.csproj':['tests/Hexalith.EventStore.PayloadProtection.Tests/ClosureRegressionTests.cs','tests/Hexalith.EventStore.PayloadProtection.Tests/DiagnosticsTests.cs'],
 'src/Hexalith.EventStore.PayloadProtection/Hexalith.EventStore.PayloadProtection.csproj':['src/Hexalith.EventStore.PayloadProtection/PayloadProtectionDiagnostics.cs']}
with tempfile.TemporaryDirectory(prefix='story83-whitespace-') as directory:
 temp=pathlib.Path(directory)
 for f in ['Directory.Build.props','Directory.Build.targets','Directory.Packages.props','global.json']:shutil.copy2(root/f,temp/f)
 config=(root/'.editorconfig').read_text().replace('end_of_line = crlf','end_of_line = lf').replace('csharp_new_line_before_open_brace = all:warning','csharp_new_line_before_open_brace = all')
 (temp/'.editorconfig').write_text(config)
 (temp/'references').mkdir();(temp/'references/Hexalith.Builds').symlink_to(root/'references/Hexalith.Builds',target_is_directory=True)
 for f in ['src/Hexalith.EventStore.Contracts','src/Hexalith.EventStore.PayloadProtection','tests/Hexalith.EventStore.PayloadProtection.Tests','tests/Hexalith.EventStore.Contracts.Tests']:shutil.copytree(root/f,temp/f)
 for project,files in projects.items():
  command=['dotnet','format','whitespace',str(temp/project),'--verify-no-changes','--no-restore','--include',*files]
  result=subprocess.run(command,cwd=temp,capture_output=True,text=True)
  print(json.dumps({'command':command,'exitCode':result.returncode,'stdout':result.stdout,'stderr':result.stderr,'sourceSha256':{f:hashlib.sha256((root/f).read_bytes()).hexdigest() for f in files}}))
  if result.returncode:raise SystemExit(result.returncode)
 print('PASS: Allman statement braces, LF, and whitespace; repository configuration untouched.')
