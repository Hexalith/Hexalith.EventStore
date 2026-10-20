"""Controlled integration regression; all Git mutations stay in disposable fixtures."""
from pathlib import Path
import sys
import tempfile
import unittest

import p1r_qualification as p
from p1r_isolation_fixture import separate_source_fixture


class CleanSourceFilterBoundaryTests(unittest.TestCase):
    def testPinnedRolesRejectExternalCleanFiltersBeforeExecutingThem(self):
        for role in ('tooling', 'source', 'builds', 'source_builds'):
            for location in ('info', 'configured'):
                with self.subTest(role=role, location=location), tempfile.TemporaryDirectory() as temporary, separate_source_fixture(temporary) as f:
                    root = getattr(f, role)
                    name = 'src/Runtime.cs' if role in ('tooling', 'source') else 'README.md'
                    target = root / name
                    original = target.read_bytes()
                    marker = Path(temporary) / 'filter-executed'
                    script = Path(temporary) / 'filter.py'
                    script.write_text('import pathlib,sys\npathlib.Path(' + repr(str(marker))
                                      + ').touch()\nsys.stdin.buffer.read()\nsys.stdout.buffer.write(' + repr(original) + ')\n')
                    f.git(root, 'config', 'filter.hide.clean', sys.executable + ' ' + str(script))
                    attributes = root / '.git/info/attributes' if location == 'info' else Path(temporary) / 'attributes'
                    attributes.write_text(name + ' filter=hide\n')
                    if location == 'configured':
                        f.git(root, 'config', 'core.attributesFile', str(attributes))
                    target.write_bytes(original + b'// concealed physical change\n')
                    with self.assertRaisesRegex(p.InvalidPacket, 'physical tracked'):
                        p.source_binding(f.tooling, f.pins, f.source, f.source_builds)
                    self.assertFalse(marker.exists(), 'source refusal must precede clean-filter execution')


if __name__ == '__main__':
    unittest.main()
