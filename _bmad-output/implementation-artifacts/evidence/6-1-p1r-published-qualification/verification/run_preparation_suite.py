"""Run the existing preparation suite with a marked synthetic source binding.

Usage: run_preparation_suite.py TOOLS_DIR. Used only because concurrent work left a tracked source file
deleted but unstaged, so the real binding (correctly) refuses. Process controls remain real; this proves
the extracted receipt validators keep their behaviour, not any source or package qualification.
"""
import copy, json, signal, subprocess, sys, tempfile, time, unittest
from pathlib import Path
from unittest import mock
TOOLS = Path(sys.argv[1])
sys.path.insert(0, str(TOOLS)); sys.path.insert(0, str(TOOLS / "tests"))
import p1r_qualification as p
BINDING = {"repository": str(p.ROOT), "fixture": "hexalith-p1r-synthetic-fixture",
           "python": {"path": str(Path(sys.executable).resolve()), "sha256": "0" * 64, "version": list(sys.version_info[:3])},
           "main": {"head": "0" * 40, "diff_sha256": "1" * 64, "gitlinks": {},
                    "files": [{"path": "synthetic.cs", "sha256": "2" * 64, "git_blob": None, "index_blob": None}]},
           "dependencies": [], "retained_inputs": [{"path": "synthetic.md", "sha256": "3" * 64}]}
DRIVER = r"""
import copy, json, sys
from pathlib import Path
from unittest import mock
sys.path.insert(0, sys.argv[1])
import p1r_qualification as p
import importlib.util
spec = importlib.util.spec_from_file_location("cli", Path(sys.argv[1]) / "p1r-qualification.py"); cli = importlib.util.module_from_spec(spec); spec.loader.exec_module(cli)
binding = json.loads(sys.argv[2])
with mock.patch.object(p, "source_binding", side_effect=lambda root=None: copy.deepcopy(binding)):
    raise SystemExit(cli.main(sys.argv[3:]))
"""
real_popen = subprocess.Popen
def popen(args, *a, **k):
    # Route the suite's real CLI invocations through the same patched binding.
    if isinstance(args, list) and len(args) > 1 and str(args[1]).endswith("tools/p1r-qualification.py"):
        args = [args[0], "-c", DRIVER, str(TOOLS), json.dumps(BINDING), *args[2:]]
    return real_popen(args, *a, **k)
with mock.patch.object(p, "source_binding", side_effect=lambda root=p.ROOT: copy.deepcopy(BINDING)), \
        mock.patch.object(subprocess, "Popen", side_effect=popen):
    suite = unittest.defaultTestLoader.loadTestsFromName("test_p1r_qualification")
    result = unittest.TextTestRunner(verbosity=1).run(suite)
sys.exit(0 if result.wasSuccessful() else 1)
