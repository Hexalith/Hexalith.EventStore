"""Focused mutation controls for the two review supplement scripts."""
import copy
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

HERE = Path(__file__).resolve().parent
PACKET = HERE.parent / 'runs-2026-10-21-runtime-recovery'
REPOSITORY = next(p for p in HERE.parents if (p / 'Hexalith.EventStore.slnx').exists())


def module(name, filename):
    spec = importlib.util.spec_from_file_location(name, HERE / filename)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


verifier = module('supplement_verifier', 'verify-supplement.py')
source = module('source_comparison', 'compare-source-tree.py')


class ReviewVerifierTests(unittest.TestCase):
    def mutate(self, filename, mutation, operation, message):
        original_load = verifier.load
        def altered(path):
            data = original_load(path)
            if path.name == filename:
                data = copy.deepcopy(data)
                mutation(data)
            return data
        with patch.object(verifier, 'load', side_effect=altered):
            with self.assertRaisesRegex(verifier.ValidationFailure, message):
                operation()

    def test_missing_final_artifacts_fail_closed(self):
        with tempfile.TemporaryDirectory() as temporary:
            repo = Path(temporary)
            (repo / 'Hexalith.EventStore.slnx').touch()
            supplement = repo / 'review'; supplement.mkdir()
            packet = repo / 'runs-2026-10-21-runtime-recovery'; packet.mkdir()
            with self.assertRaisesRegex(verifier.ValidationFailure, 'index.json'):
                verifier.verify(supplement)
            (packet / 'index.json').write_text('{}')
            with self.assertRaisesRegex(verifier.ValidationFailure, 'recovery-timelines.json'):
                verifier.verify(supplement)

    def test_index_cannot_omit_files_or_expand_exclusions(self):
        with tempfile.TemporaryDirectory() as temporary:
            directory = Path(temporary)
            (directory / 'index.json').write_text('{}')
            (directory / 'required').write_text('evidence')
            with self.assertRaisesRegex(verifier.ValidationFailure, 'membership'):
                verifier.check_index(directory, {'files': {}, 'excludedFromIndex': ['index.json']}, ['index.json'])
            with self.assertRaisesRegex(verifier.ValidationFailure, 'exclusions'):
                verifier.check_index(directory, {'files': {}, 'excludedFromIndex': ['index.json', 'required']}, ['index.json'])

    def test_original_and_current_handoff_identity_mismatches_fail(self):
        for key in ['originalSpecSha256', 'currentSpecSha256', 'originalReportSha256', 'originalHandoffSha256']:
            with self.subTest(key=key):
                self.mutate('handoff-binding.json', lambda d: d.update({key: '0' * 64}), lambda: verifier.check_handoff(HERE, PACKET, REPOSITORY), 'binding mismatch|handoff mismatch')

    def test_candidate_build_patch_and_toolchain_mismatches_fail(self):
        binding = verifier.load(HERE / 'handoff-binding.json')
        for filename, change, message in [
            ('final-runtime-identity.json', lambda d: d.update(candidateSha256='0' * 64), 'candidate mismatch'),
            ('final-candidate-build.json', lambda d: d['command'].__setitem__(d['command'].index('-o') + 1, '/tmp/wrong'), 'source/output mismatch'),
            ('final-runtime-patch-identity.json', lambda d: d.update(patchSha256='0' * 64), 'patch digest mismatch'),
            ('final-runtime-identity.json', lambda d: d.update(toolchainSha256='0' * 64), 'toolchain mismatch'),
        ]:
            with self.subTest(filename=filename, message=message):
                self.mutate(filename, change, lambda: verifier.check_provenance(PACKET, binding), message)

    def test_scenario_candidate_and_endpoint_mismatches_fail(self):
        identity = verifier.load(PACKET / 'final-runtime-identity.json')
        self.mutate('recovery-timelines.json', lambda d: d['timelines'][0].update(candidateSha256='0' * 64), lambda: verifier.check_scenarios(PACKET, identity), 'timeline candidate mismatch')
        self.mutate('manifest.json', lambda d: d.update(SecondSidecarExecutableSha256='0' * 64), lambda: verifier.check_scenarios(PACKET, identity), 'scenario runtime identity mismatch')
        self.mutate('manifest.json', lambda d: d['Observations'].update(RestoredSecondInternalGrpcPort=1), lambda: verifier.check_scenarios(PACKET, identity), 'endpoint mismatch')

    def test_optimized_wrapper_execution_is_rejected(self):
        result = subprocess.run([sys.executable, '-O', '-B', str(HERE / 'verify-supplement.py')], capture_output=True, text=True)
        self.assertEqual(result.returncode, 1)
        self.assertIn('optimized Python execution is forbidden', result.stderr)

    def test_original_verifier_disables_environment_optimization_and_writes(self):
        self.assertEqual(verifier.original_command(PACKET), [sys.executable, '-E', '-B', str(PACKET / 'verify-packet.py')])

    def test_source_inventory_rejects_unexpected_changes_additions_removals_and_modes(self):
        entry = {'kind': 'file', 'mode': 0o644, 'sha256': 'unchanged', 'bytes': 1}
        archive = {'original': entry}
        for current in [dict(archive, extra=entry), {}, {'original': dict(entry, sha256='changed')}, {'original': dict(entry, mode=0o755)}]:
            with self.subTest(current=current), self.assertRaises(source.ValidationFailure):
                source.compare(archive, current, {})

    def test_source_inventory_accepts_only_exact_retained_change(self):
        original = {'kind': 'file', 'mode': 0o644, 'sha256': 'archive', 'bytes': 1}
        patched = dict(original, sha256='retained')
        self.assertEqual(len(source.compare({'router': original}, {'router': patched}, {'router': patched})), 1)
        with self.assertRaises(source.ValidationFailure):
            source.compare({'router': original}, {'router': dict(patched, sha256='other')}, {'router': patched})


if __name__ == '__main__':
    unittest.main(verbosity=2)
