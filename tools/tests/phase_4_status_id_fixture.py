"""The two CI producers for the status-link gate, represented at the HTTP boundary."""

import json
import xml.etree.ElementTree as ET

import phase_4_status_id_gate as S
import phase_4_tenant_fixture as F
import test_phase_4_transition as T


# Use the reviewed schema's actual reporter labels independently of the adapter's expected-case logic.
REPORT_SCHEMAS = json.loads((T.ROOT / "tools/phase-4-transition.schema.json").read_bytes())["$defs"][
    "status_qualification_result"]["properties"]["reports"]["properties"]


def trx(assembly):
    root = ET.Element("TestRun", xmlns=S.TRX_NAMESPACE)
    definitions = ET.SubElement(root, "TestDefinitions")
    results = ET.SubElement(root, "Results")
    path = f"/fixture/tests/{assembly}/bin/Release/net11.0/{assembly}.dll"
    labels = REPORT_SCHEMAS[assembly + ".trx"]["properties"]["passing_cases"]["items"]["enum"]
    for index, label in enumerate(labels, 1):
        cls, method = label.partition("(")[0].partition(" [")[0].rsplit(".", 1)
        uid = str(index)
        unit = ET.SubElement(definitions, "UnitTest", id=uid, storage=path, name=label)
        ET.SubElement(unit, "Execution", id=uid)
        ET.SubElement(unit, "TestMethod", className=cls, name=method, codeBase=path)
        ET.SubElement(results, "UnitTestResult", testId=uid, executionId=uid, testName=label, outcome="Passed")
    return ET.tostring(root)


class Qualification(F.Qualification):
    def __init__(self, fixture):
        self.f = f = fixture
        self.files, self.logs, self.runs = {}, {}, {}
        rows = json.loads((f.root / T.MATRIX).read_text())["gates"]
        f.row = next(row for row in rows if row["gate"] == S.GATE)
        for path in (*f.row["subject_paths"], S.PATH, S.T.PATH):
            dest = f.root / path
            dest.parent.mkdir(parents=True, exist_ok=True)
            if not dest.exists():
                dest.write_bytes((T.ROOT / path).read_bytes() if path in (S.PATH, S.T.PATH, S.WORKFLOW)
                                 else ("fixture subject " + path).encode())
            f.blob(T.HEAD, path, dest.read_bytes())
        f.request.update(gate=S.GATE, subjects=[f.file(p) for p in f.row["subject_paths"]], evidence=[], predecessors=[])
        f.put("/activity?ref=refs%2Fheads%2Fmain&per_page=100", [f.activity(T.HEAD, "0" * 40, "2026-01-01T00:00:00Z")])
        files = {artifact: {a + "/" + a + ".trx": trx(a) for a in assemblies} for artifact, assemblies in S.REPORTS.items()}
        self.install_lane(9, "eventstore_ci", T.REPO, S.WORKFLOW, report_files=files)
        self.pin = self.runs["eventstore_ci"]
        self.claim = {"adapter": S.ADAPTER, "adapter_sha256": f.file(S.PATH)["sha256"], "run": self.pin}
        f.request["status_qualification"] = self.claim
        f.approve()
