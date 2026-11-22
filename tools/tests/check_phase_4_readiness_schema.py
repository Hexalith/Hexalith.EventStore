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
        for source in (candidate, ready):
            wrong_gate = copy.deepcopy(source)
            wrong_gate['binding']['readiness']['gates']['G-BASELINE']['gate'] = 'G-NFR18'
            self.assertFalse(validator.is_valid(wrong_gate))
        for key in ('required_level', 'achieved_level', 'downstream_minimum'):
            with self.subTest(key=key):
                self.assertFalse(validator.is_valid(dict(ready, **{key: 'none'})))
                self.assertFalse(validator.is_valid(dict(ready, **{key: 'independent'})))


if __name__ == '__main__':
    unittest.main()
