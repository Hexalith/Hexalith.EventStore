from hashlib import sha256
from pathlib import Path
import subprocess

root = Path('.')
pins = {
    '_bmad-output/implementation-artifacts/spec-6-5a-event-contract-writer-and-migration-evidence.md': '31f63fc0ea54043e7b821f311b28d3e856667789eac9eca8f79807f6d2e74444',
    '_bmad-output/implementation-artifacts/spec-6-5b-verified-read-replay-and-projection.md': 'b889951bc248a7a4d19067a84d197bbe8dd1bc14457d56ed5665cc9c72347152',
    '_bmad-output/implementation-artifacts/spec-6-5c-publication-subscription-and-rollout.md': 'f24116aaf4dc3cd041034f40a1d858e114f9f8a4219b1ca0cc40bdea80385ca9',
    '_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md': 'c474df76a687b2798757051efbdb382bb6b9650f7bd7a7637d0c67bfc5e7b715',
}
for name, expected in pins.items():
    assert sha256((root/name).read_bytes()).hexdigest() == expected, name
historical = subprocess.check_output(['git','show','288a6190:_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md'])
assert sha256(historical).hexdigest() == pins['_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md'], 'historical AD-13 hash'
parent = (root/'_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md').read_text(encoding='utf-8')
for label in ['ApprovalDigest','Approver','ApprovalDateUtc','Authorization','ApprovalEvidence']:
    assert f'{label}: UNAPPROVED' in parent, ('AD-13 approval label',label)
assert 'ApprovalScope: Story 6.5 AD-13 normative artifact' in parent, 'AD-13 approval scope'
allowed = {'_bmad-output/implementation-artifacts/deferred-work.md', '_bmad-output/implementation-artifacts/6-5d-simplification/known-answers.json', '_bmad-output/implementation-artifacts/spec-6-5d-hold-lifecycle-resume-and-legacy-admission.md', '_bmad-output/implementation-artifacts/6-5d-simplification/previous-candidate.md', '_bmad-output/implementation-artifacts/6-5d-simplification/obligations.md', '_bmad-output/implementation-artifacts/6-5d-simplification/verify.py', '_bmad-output/implementation-artifacts/sprint-status.yaml', '_bmad-output/implementation-artifacts/spec-6-5d-hold-lifecycle-resume-and-legacy-admission-2.md', '_bmad-output/implementation-artifacts/6-5d-simplification/previous-execution.md'}
changed = set(subprocess.check_output(['git','diff','--name-only','01498ac721db7c44f18fcf9591ffbbf30ba245e2']).decode().splitlines())
untracked = {line[3:] for line in subprocess.check_output(['git','status','--porcelain','--untracked-files=all']).decode().splitlines() if line.startswith('?? ')}
checkpoint = '2c58ffda41759e895ace4b9625c9bd931a217672'
checkpoint_paths = {
    '_bmad-output/implementation-artifacts/6-1-p2-query-security-projection-capability-acceptance-record.md',
    '_bmad-output/implementation-artifacts/evidence/6-1-p1r-3110/README.md',
    '_bmad-output/implementation-artifacts/evidence/6-1-p1r-3110/consumer/Consumer.csproj',
    '_bmad-output/implementation-artifacts/evidence/6-1-p1r-3110/consumer/NuGet.Config',
    '_bmad-output/implementation-artifacts/evidence/6-1-p1r-3110/consumer/PublishedApiSmoke.cs',
    '_bmad-output/implementation-artifacts/evidence/6-1-p1r-3110/public-packages.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p1r-3110/verify_public_packages.py',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/admin-denial.ctrf.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/admin-denial.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/admin-denial.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/client.ctrf.json.gz',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/client.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/client.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/client.xml.gz',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/contracts-blockers.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/contracts-build-initial.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/contracts-build-initial.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/contracts-build.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/contracts-build.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/contracts-full.ctrf.json.gz',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/contracts-full.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/contracts-full.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/contracts-p2.ctrf.json.gz',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/contracts-p2.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/contracts-p2.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/contracts-p2.xml',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/diff-check.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/diff-check.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/final-solution-build.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/final-solution-build.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/g4-runner.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/g4-runner.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/live-sidecar-build-initial.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/live-sidecar-build-initial.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/live-sidecar-build.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/live-sidecar-build.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/manifest-before-rebuild-proof-correction.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/manifest.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/manifest.sha256',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/matrix-tests-after-rebuild-proof-correction.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/matrix-tests.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/process-restart.ctrf.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/process-restart.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/process-restart.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/process-restart.xml',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/projection-rebuild.ctrf.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/projection-rebuild.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/projection-rebuild.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/projection-rebuild.xml',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/public-signatures.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/public-signatures.txt',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/query-routing.ctrf.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/query-routing.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/query-routing.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/query-routing.xml',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/rebuild-proof-build-rerun.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/rebuild-proof-build-rerun.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/rebuild-proof-build.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/rebuild-proof-build.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/rebuild-proof-diff-check.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/rebuild-proof-diff-check.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/rebuild-proof-process-restart-rerun.ctrf.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/rebuild-proof-process-restart-rerun.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/rebuild-proof-process-restart-rerun.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/rebuild-proof-process-restart-rerun.xml',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/rebuild-proof-process-restart.ctrf.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/rebuild-proof-process-restart.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/rebuild-proof-process-restart.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/rebuild-proof-process-restart.xml',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/reminder-fixture-build.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/reminder-fixture-build.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/reminder-fixture-rerun.ctrf.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/reminder-fixture-rerun.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/reminder-fixture-rerun.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/reminder-fixture.ctrf.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/reminder-fixture.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/reminder-fixture.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/secret-guard-rerun.ctrf.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/secret-guard-rerun.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/secret-guard-rerun.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/secret-guard.ctrf.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/secret-guard.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/secret-guard.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/server-full-rerun.ctrf.json.gz',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/server-full-rerun.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/server-full-rerun.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/server-full.ctrf.json.gz',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/server-full.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/server-full.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/server-p2.ctrf.json.gz',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/server-p2.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/server-p2.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/server-p2.xml.gz',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/solution-build.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/solution-build.log',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/solution-restore.json',
    '_bmad-output/implementation-artifacts/evidence/6-1-p2-local-2026-10-01/solution-restore.log',
    '_bmad-output/implementation-artifacts/review-6-5d-loop6-blind-hunter-standalone.md',
    '_bmad-output/implementation-artifacts/review-6-5d-loop6-edge-case-hunter-standalone.md',
    '_bmad-output/implementation-artifacts/review-6-5d-loop6-verification-gap-standalone.md',
    'docs/guides/configuration-reference.md',
    'docs/guides/typed-reminders.md',
    'src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj',
    'src/Hexalith.EventStore.Client/Reminders/IReminderIntentSource.cs',
    'src/Hexalith.EventStore.Client/Reminders/ReminderDelegationRequest.cs',
    'src/Hexalith.EventStore.Contracts/Reminders/ReminderIntent.cs',
    'src/Hexalith.EventStore.DomainService/EventStoreReminderOptions.cs',
    'src/Hexalith.EventStore.DomainService/EventStoreReminderServiceCollectionExtensions.cs',
    'src/Hexalith.EventStore.DomainService/ReminderActor.cs',
    'src/Hexalith.EventStore.DomainService/ReminderCoordinator.cs',
    'src/Hexalith.EventStore.DomainService/ReminderDispositionRecord.cs',
    'src/Hexalith.EventStore.DomainService/ReminderEntry.cs',
    'src/Hexalith.EventStore.DomainService/ReminderFailClosedException.cs',
    'src/Hexalith.EventStore.DomainService/ReminderIntentIndex.cs',
    'src/Hexalith.EventStore.DomainService/ReminderLog.cs',
    'src/Hexalith.EventStore.DomainService/ReminderReconciler.cs',
    'src/Hexalith.EventStore.DomainService/ReminderReconciliationPass.cs',
    'src/Hexalith.EventStore.DomainService/ReminderRuntimeStatus.cs',
    'tests/Hexalith.EventStore.Contracts.Tests/Queries/ProjectionAdapterContractTests.cs',
    'tests/Hexalith.EventStore.Contracts.Tests/Reminders/ReminderIdentityCodecTests.cs',
    'tests/Hexalith.EventStore.DomainService.Tests/EventStoreReminderCompositionTests.cs',
    'tests/Hexalith.EventStore.DomainService.Tests/Fixtures/DispositionFailingReadModelStore.cs',
    'tests/Hexalith.EventStore.DomainService.Tests/Fixtures/FakeReminderIntentSource.cs',
    'tests/Hexalith.EventStore.DomainService.Tests/Fixtures/FakeReminderScheduler.cs',
    'tests/Hexalith.EventStore.DomainService.Tests/Fixtures/FakeTrustedEffectSubmitter.cs',
    'tests/Hexalith.EventStore.DomainService.Tests/Fixtures/ReminderDiagnosticLogger.cs',
    'tests/Hexalith.EventStore.DomainService.Tests/Fixtures/ReminderEnvironmentCollection.cs',
    'tests/Hexalith.EventStore.DomainService.Tests/Fixtures/ReminderTestHarness.cs',
    'tests/Hexalith.EventStore.DomainService.Tests/ReminderCallbackAdmissionTests.cs',
    'tests/Hexalith.EventStore.DomainService.Tests/ReminderCoordinatorTests.cs',
    'tests/Hexalith.EventStore.DomainService.Tests/ReminderDiagnosticsTests.cs',
    'tests/Hexalith.EventStore.DomainService.Tests/ReminderReconcilerTests.cs',
    'tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8DiagnosticRecordingLogger.cs',
    'tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8DiagnosticResponseHandler.cs',
    'tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8DiscoveryConfiguration.cs',
    'tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8DiscoveryConfigurationTests.cs',
    'tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8HostingStartup.cs',
    'tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8InvocationDiagnosticHandler.cs',
    'tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8InvocationDiagnosticHandlerTests.cs',
    'tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8OwnedContainerLaunch.cs',
    'tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8OwnedContainerLaunchTests.cs',
    'tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8PostgresqlFixture.cs',
    'tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8QualificationOverrides.cs',
    'tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Fixtures/Oq8QualificationOverridesTests.cs',
    'tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/ProjectionWatermarkProcessHandler.cs',
    'tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/ProjectionWatermarkProcessRestartTests.cs',
    'tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/ProjectionWatermarkProcessWorkerTests.cs',
    'tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/ReminderRecoveryLiveSidecarTests.cs',
    'tools/validate-oq8-platform-evidence.py',
}
submodule_pins = {
    'references/Hexalith.Builds': '21ce044ab465ccb2adab58b3d66e394ffbecf3c2',
    'references/Hexalith.Commons': 'c13dc6679aa91144b6d541078f3f20019d79c2eb',
    'references/Hexalith.FrontComposer': 'b6a4536fc12b64927ad6dfbc46a5f45c8b7f229e',
    'references/Hexalith.McpCli': '7e3226ba612a3e7fb3a8969c4197a8f1e4c0c2ed',
    'references/Hexalith.Platform': '7c2f0f89f29c79f5d7ab4b731155e2cb07fc690f',
    'references/Hexalith.Tenants': '3d7c07363d7a06a24cbba0d777899b2071b02f06',
}
registration_paths = {'.gitmodules'}
gitmodules_blob = 'c62b48798894bb3576f02fdf4ebb8c552756e35b'
assert subprocess.check_output(['git','rev-parse',f'{checkpoint}:.gitmodules']).decode().strip() == gitmodules_blob, 'checkpoint .gitmodules blob'
assert subprocess.check_output(['git','ls-files','--stage','--','.gitmodules']).decode().split() == ['100644',gitmodules_blob,'0','.gitmodules'], 'staged .gitmodules blob'
assert subprocess.check_output(['git','hash-object','--no-filters','.gitmodules']).decode().strip() == gitmodules_blob, 'worktree .gitmodules blob'
assert (root/'.gitmodules').read_bytes() == subprocess.check_output(['git','show',gitmodules_blob]), '.gitmodules bytes'
added_registrations = {
    'references/Hexalith.McpCli': ('Hexalith.McpCli','https://github.com/Hexalith/Hexalith.McpCli.git'),
    'references/Hexalith.Platform': ('Hexalith.Platform','https://github.com/Hexalith/Hexalith.Platform.git'),
}
for name,(section,url) in added_registrations.items():
    assert subprocess.check_output(['git','config','--file','.gitmodules','--get',f'submodule.{section}.path']).decode().strip() == name, ('registration path',section)
    assert subprocess.check_output(['git','config','--file','.gitmodules','--get',f'submodule.{section}.url']).decode().strip() == url, ('registration url',section)
declared_rows = subprocess.check_output([
    'git','config','--file','.gitmodules','--get-regexp',r'^submodule\..*\.path$'
]).decode().splitlines()
declared_paths = {row.split(None,1)[1] for row in declared_rows}
assert set(submodule_pins) <= declared_paths, sorted(set(submodule_pins) - declared_paths)
external_paths = checkpoint_paths | set(submodule_pins) | registration_paths
assert len(external_paths) == 156, len(external_paths)
subprocess.check_call(['git','merge-base','--is-ancestor',
                       '01498ac721db7c44f18fcf9591ffbbf30ba245e2',checkpoint])
checkpoint_changes = set(subprocess.check_output([
    'git','diff','--name-only','01498ac721db7c44f18fcf9591ffbbf30ba245e2',checkpoint
]).decode().splitlines())
assert checkpoint_changes - allowed == external_paths, sorted((checkpoint_changes - allowed) ^ external_paths)
for name in sorted(external_paths):
    if name.startswith('references/'):
        entry = subprocess.check_output(['git','ls-tree',checkpoint,'--',name]).decode().split()
        expected = submodule_pins[name]
        assert entry == ['160000','commit',expected,name], ('checkpoint gitlink',name,entry,expected)
        actual = subprocess.check_output(['git','-C',name,'rev-parse','HEAD']).decode().strip()
        assert actual == expected, ('checkout',name,actual,expected)
        staged = subprocess.check_output(['git','ls-files','--stage','--',name]).decode().split()
        assert staged == ['160000',expected,'0',name], ('root gitlink',name,staged,expected)
        assert not subprocess.check_output(['git','-C',name,'status','--porcelain']), ('dirty submodule',name)
    elif name not in registration_paths:
        expected = subprocess.check_output(['git','show',f'{checkpoint}:{name}'])
        assert (root/name).read_bytes() == expected, ('checkpoint content',name)
external_drift = set(subprocess.check_output([
    'git','diff','--name-only',checkpoint,'--',*sorted(external_paths - set(submodule_pins) - registration_paths)
]).decode().splitlines())
assert not external_drift, sorted(external_drift)
story_changed = changed - external_paths
assert story_changed | untracked <= allowed, sorted((story_changed | untracked) - allowed)
print(f'protected-path verifier: {len(pins)} hashes, AD-13 UNAPPROVED, {len(story_changed | untracked)} allowed paths, {len(external_paths)} pinned external paths')
