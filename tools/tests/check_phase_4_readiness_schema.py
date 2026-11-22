"""Validate readiness producer output and malformed variants with the existing Draft 2020-12 development tool."""

import copy
import json
import unittest

from jsonschema import Draft202012Validator
import test_phase_4_transition as T


class ReadinessSchemaTests(unittest.TestCase):
    def test_gate_keys_and_passing_decision_assurance(self):
        fixture = T.TransitionTests()
        self.addCleanup(fixture.doCleanups)
        fixture.setUp()
        fixture.prepare_readiness()
        fixture.install_synthetic_owners()
        schema = json.loads((T.ROOT / 'tools/phase-4-transition.schema.json').read_text())
        Draft202012Validator.check_schema(schema)
        validator = Draft202012Validator(schema)
        candidate = fixture.readiness_cli()
        fixture.artifact(candidate)
        ready = fixture.readiness_cli('consume')
        for document in (fixture.request, candidate, ready):
            validator.validate(document)
        boundary = copy.deepcopy(candidate)
        files = boundary['binding']['inputs']
        for i in range(len(files), 212):
            files.append({'path': f'boundary/{i}', 'sha256': 'a' * 64})
        validator.validate(boundary)
        files.append({'path': 'boundary/overflow', 'sha256': 'b' * 64})
        self.assertFalse(validator.is_valid(boundary))
        missing_owner = copy.deepcopy(fixture.request)
        del missing_owner['readiness']['gates']['G-STATUS-ID']['owner_request']
        self.assertFalse(validator.is_valid(missing_owner))
        wrong_owner = copy.deepcopy(fixture.request)
        wrong_owner['readiness']['gates']['G-STATUS-ID']['owner_request'] = wrong_owner['readiness']['gates']['G-TENANT']['owner_request']
        self.assertFalse(validator.is_valid(wrong_owner))
        for source in (candidate, ready):
            wrong_gate = copy.deepcopy(source)
            wrong_gate['binding']['readiness']['gates']['G-BASELINE']['gate'] = 'G-NFR18'
            self.assertFalse(validator.is_valid(wrong_gate))
        for key in ('required_level', 'achieved_level', 'downstream_minimum'):
            with self.subTest(key=key):
                self.assertFalse(validator.is_valid(dict(ready, **{key: 'none'})))
                self.assertFalse(validator.is_valid(dict(ready, **{key: 'independent'})))

    def test_status_link_claim_and_authenticated_result_are_closed(self):
        import phase_4_status_id_fixture as F
        import phase_4_gate_decision as D
        fixture = T.TransitionTests()
        self.addCleanup(fixture.doCleanups)
        fixture.setUp()
        F.Qualification(fixture)
        validator = Draft202012Validator(json.loads((T.ROOT / 'tools/phase-4-transition.schema.json').read_text()))
        candidate = fixture.assert_pass()
        fixture.artifact(candidate)
        decision = D.report(fixture.assert_pass('consume'), 'G-STATUS-ID')
        for doc in (fixture.request, candidate, decision):
            validator.validate(doc)
        for report in decision['binding']['status_qualification']['reports']:
            bad = copy.deepcopy(decision)
            cases = bad['binding']['status_qualification']['reports'][report]['passing_cases']
            cases[0] = 'a different case'
            self.assertFalse(validator.is_valid(bad))
            del cases[:]
            cases.append(decision['binding']['status_qualification']['reports'][report]['passing_cases'][0])
            self.assertFalse(validator.is_valid(bad))
        bad = copy.deepcopy(fixture.request)
        del bad['status_qualification']['run']['artifacts']['contracts-test-results']
        self.assertFalse(validator.is_valid(bad))
        for field in ('producers', 'reports'):
            bad = copy.deepcopy(decision)
            bad['binding']['status_qualification'][field]['invented'] = {}
            self.assertFalse(validator.is_valid(bad))
        bad = copy.deepcopy(decision)
        del bad['binding']['status_qualification']
        self.assertFalse(validator.is_valid(bad))
        self.assertFalse(validator.is_valid(dict(decision, gate='G-OQ8')))


if __name__ == '__main__':
    unittest.main()
