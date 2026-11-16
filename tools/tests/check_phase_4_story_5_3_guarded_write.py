"""Explicit, expensive Story 5.3 guarded-write proof using genuine business lanes.

Run with run-with-temp. Only GitHub HTTP facts are synthetic. Every evaluation
and consumption executes the unchanged complete business plan in fresh scratch.
This driver is intentionally outside unittest discovery and the fixture workflow.
"""

import argparse
from contextlib import contextmanager
import datetime as dt
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
from unittest import mock

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools"))
import phase_4_story_5_3 as A
import phase_4_story_completion as S
import phase_4_transition as C
import phase_4_transport_fixture as HTTP

SPEC = "_bmad-output/implementation-artifacts/spec-5-3-guarded-write-proof-fixture.md"
BUSINESS = "src/Hexalith.EventStore/Authentication/ConfigureJwtBearerOptions.cs"
MESSAGE = "test(governance): bind private story 5.3 guarded write fixture\n"


def private_tree(root, entries):
    """Store actual canonical blobs and trees only in the disposable repository."""
    nested = {}
    for row in A.validate_entries(entries):
        node = nested
        parts = row["path"].split("/")
        for part in parts[:-1]:
            node = node.setdefault(part, {})
        node[parts[-1]] = row
        if row["mode"] != "160000":
            value = A.read_source(root, row)
            if A.blob_sha(value) != row["git_sha"]:
                value = value.replace(b"\r\n", b"\n")
            C.require(A.store_object(root, "blob", value) == row["git_sha"], "SOURCE_MISMATCH", "private blob identity mismatch")

    def store(node):
        value = b""
        for name, item in sorted(node.items(), key=lambda pair: (pair[0] + ("/" if "git_sha" not in pair[1] else "")).encode()):
            tree = "git_sha" not in item
            oid = store(item) if tree else item["git_sha"]
            value += ("40000" if tree else item["mode"]).encode() + b" " + name.encode() + b"\0" + bytes.fromhex(oid)
        return A.store_object(root, "tree", value)
    return store(nested)


def validate_message(source, logs):
    """Validate the exact authored message with this repository's pinned CLI."""
    pinned = json.loads((source / "package.json").read_text())["devDependencies"]["@commitlint/cli"]
    package = source / "node_modules/@commitlint/cli"
    C.require(package.is_dir(), "INPUT_INVALID", "prerequisite:pinned-commitlint-cli-missing", 2)
    C.require(json.loads((package / "package.json").read_text())["version"] == pinned,
              "INPUT_INVALID", "prerequisite:commitlint-cli-version-mismatch", 2)
    process = subprocess.run(["node", str(package / "cli.js"), "--config", str(source / "commitlint.config.mjs"), "--verbose"],
                             input=MESSAGE, text=True, capture_output=True, cwd=source, timeout=60)
    (logs / "commitlint.txt").write_text(process.stdout + process.stderr)
    C.require(process.returncode == 0, "INPUT_INVALID", "fixture commit message failed pinned commitlint; see commitlint.txt", 2)


def snapshot(source, private, logs):
    """Capture all current private inputs without changing shared Git state."""
    manifest = A.inventory(source)
    manifest["runtime"] = A.runtime_inventory(source)
    A.materialize(source, private, manifest)
    A.assert_stable(source, manifest, diagnostic=True)
    copied = C.parse((private / A.INVENTORY).read_bytes())
    # FrontComposer supplies only its pinned Pact bytes, but authenticates the
    # complete root gitlink index before selecting those bytes on each capture.
    for repository in copied["metadata"][1:]:
        if repository["path"].endswith("Hexalith.FrontComposer"):
            destination = private / repository["path"]
            A.write_index(destination, A.index_entries(source / repository["path"]))
            (destination / ".git/HEAD").write_text(repository["head"] + "\n")
            with (destination / ".git/config").open("a") as stream:
                stream.write('[remote "origin"]\nurl = ' + repository["origin"] + '\n')
    before = copied["metadata"][0]["head"]
    previous = A.verified_object(private, "commit", before)
    before_tree = re.match(rb"tree ([0-9a-f]{40})\n", previous)[1].decode()
    (private / SPEC).write_text("---\nstatus: 'in-progress'\nstory_key: '" + A.STORY_IDENTITY + "'\n---\n\nDisposable guarded-write proof only.\n")
    tracker = (private / S.TRACKER).read_bytes()
    tracker, count = re.subn(rb"(?m)^(  " + A.STORY_IDENTITY.encode() + rb": )[a-z-]+", rb"\g<1>in-progress", tracker)
    C.require(count == 1, "STORY_IDENTITY_MISSING", "private tracker lacks canonical Story 5.3")
    (private / S.TRACKER).write_bytes(tracker)
    entries = [{key: row[key] for key in ("path", "mode", "git_sha")} for row in copied["entries"] if row["path"] != SPEC]
    entries.append({"path": SPEC, "mode": "100644", "git_sha": "0" * 40})
    entries = [{key: row[key] for key in ("path", "mode", "git_sha")}
               for row in A.bind_entries(private, A.validate_entries(entries), exact=False)]
    validate_message(source, logs)
    tree = private_tree(private, entries)
    head = A.store_object(private, "commit", ("tree " + tree + "\nparent " + before
        + "\nauthor Fixture <fixture@example.test> 0 +0000\ncommitter Fixture <fixture@example.test> 0 +0000\n\n" + MESSAGE).encode())
    A.write_index(private, entries)
    (private / ".git/HEAD").write_text(head + "\n")
    with (private / ".git/config").open("a") as stream:
        stream.write('[remote "origin"]\nurl = ' + copied["metadata"][0]["origin"] + '\n')
    return head, tree, before, before_tree, entries


class FinalPreparationEdit:
    """Edit business source only at this writer destination's final preparation."""

    def __init__(self, factory, destination, source, ready):
        self.factory, self.destination, self.source, self.ready = factory, destination, source, ready
        self.reached = 0
        self.temporary = None
        self.edited = source.read_bytes() + b"\n// Concurrent guarded-write proof edit must survive.\n"

    def __call__(self, *args, **kwargs):
        stream = self.factory(*args, **kwargs)
        if kwargs.get("prefix") == ".story-completion-" and Path(kwargs.get("dir", ".")).resolve() == self.destination.parent.resolve():
            try:
                C.require(self.reached == 0 and self.ready(), "STORY_EVIDENCE_FAILED", "final preparation requires fresh genuine PASS evidence")
                self.temporary = Path(stream.name)
                self.source.write_bytes(self.edited)
                self.reached += 1
            except BaseException:
                stream.close()
                Path(stream.name).unlink(missing_ok=True)
                raise
        return stream


def expected_write(original, target):
    """The positive control permits only the selected lifecycle fields."""
    if target == "spec":
        return original.replace(b"status: 'in-progress'", b"status: 'done'", 1)
    updated = re.sub(rb"(?m)^(  " + A.STORY_IDENTITY.encode() + rb": )in-progress", rb"\g<1>done", original)
    return re.sub(rb"(?m)^(last_updated:[ \t]*)[^\r\n]+", lambda match: match[1] + dt.date.today().isoformat().encode(), updated)


def assert_preserved(edit, destination, original):
    """Require the reached boundary, preserved edit/label and exact cleanup."""
    C.require(edit.reached == 1 and edit.temporary is not None and not edit.temporary.exists()
              and not list(destination.parent.glob(".story-completion-*"))
              and destination.read_bytes() == original and edit.source.read_bytes() == edit.edited,
              "STORY_EVIDENCE_FAILED", "concurrent-edit preservation or temporary cleanup failed")


@contextmanager
def platform(state_path):
    """Replace only the authenticated HTTP transport, never verifier decisions."""
    environment = {key: value for key, value in os.environ.items() if not key.startswith(("GH_", "GITHUB_", "PHASE4_"))}
    environment.update(PHASE4_FIXTURE_STATE=str(state_path), GH_TOKEN=os.urandom(32).hex())
    with mock.patch.dict(os.environ, environment, clear=True), mock.patch("urllib.request.build_opener", return_value=HTTP.Transport()):
        yield


def prove(source, logs):
    executions, targets = [], []
    real_run = A.run
    current = {"target": None, "phase": None}

    def evidence(root, manifest, *args, **kwargs):
        label = str(len(executions) + 1) + "-" + current["target"] + "-" + current["phase"]
        lane_logs = logs / label
        lane_logs.mkdir()
        print("Starting genuine business execution " + label, flush=True)
        report = real_run(root, manifest, *args, logs=lane_logs, **kwargs)
        A.validate_report(report, A.summary(manifest))
        C.require(report["result"] == "PASS", "STORY_EVIDENCE_FAILED",
                  "genuine evidence failed: " + label + ": " + ", ".join(report["gaps"]))
        executions.append({"target": current["target"], "phase": current["phase"], "report": report})
        print("Completed genuine business execution " + label + ": PASS", flush=True)
        return report

    with tempfile.TemporaryDirectory(prefix="story-5-3-guarded-write-") as directory:
        private = Path(directory) / "checkout"
        private.mkdir()
        head, tree, before, before_tree, entries = snapshot(source, private, logs)
        identities = {"head_sha": head, "root_tree": tree, "previous_head": before, "previous_tree": before_tree,
                      "root_gitlinks": [row for row in entries if row["mode"] == "160000"]}
        (logs / "private-identities.json").write_bytes(C.canonical(identities) + b"\n")
        for target in ("spec", "tracker"):
            current.update(target=target, phase="evaluate")
            request, state = HTTP.story_completion_facts(private, SPEC, target, head, tree, before, before_tree, entries)
            state_path = Path(directory) / (target + "-http.json")
            state_path.write_bytes(C.canonical(state))
            (logs / (target + "-request.json")).write_bytes(C.canonical(request) + b"\n")
            destination = private / (SPEC if target == "spec" else S.TRACKER)
            original = destination.read_bytes()
            with platform(state_path), mock.patch.object(A, "run", side_effect=evidence):
                candidate = C.Control(private, request, C.GitHub()).evaluate()
                C.require(candidate["result"] == "PASS" and candidate["story_evidence"] == executions[-1]["report"],
                          "STORY_EVIDENCE_FAILED", "evaluation lacks genuine passing evidence")
                state = C.parse(state_path.read_bytes())
                HTTP.bind_story_candidate(state, request, candidate)
                state_path.write_bytes(C.canonical(state))
                (logs / (target + "-candidate.json")).write_bytes(C.canonical(candidate) + b"\n")
                current["phase"] = "positive-write"
                positive = S.complete(private, SPEC, target, request, 7, 2, write=True, api=C.GitHub())
                C.require(positive["disposition"] == "assured-story-slice" and positive["verification"]["phase"] == "consume"
                          and positive["story_evidence"] == executions[-1]["report"]
                          and destination.read_bytes() == expected_write(original, target),
                          "STORY_EVIDENCE_FAILED", "positive write changed unexpected lifecycle bytes")
                C.require(not list(destination.parent.glob(".story-completion-*")), "STORY_EVIDENCE_FAILED", "positive completion left a temporary file")
                destination.write_bytes(original)
                C.require(destination.read_bytes() == original, "SOURCE_MISMATCH", "private lifecycle restoration failed")
                stable = A.inventory(private, C.GitHub(), head)
                stable["runtime"] = A.runtime_inventory(private)
                A.assert_stable(private, stable)
                current["phase"] = "concurrent-write"
                previous_count = len(executions)
                business_original = (private / BUSINESS).read_bytes()
                edit = FinalPreparationEdit(S.tempfile.NamedTemporaryFile, destination, private / BUSINESS,
                    lambda: len(executions) == previous_count + 1 and executions[-1]["report"]["result"] == "PASS")
                try:
                    with mock.patch.object(S.tempfile, "NamedTemporaryFile", side_effect=edit):
                        S.complete(private, SPEC, target, request, 7, 2, write=True, api=C.GitHub())
                except C.Refusal as error:
                    C.require(error.code == "SOURCE_MISMATCH", "STORY_EVIDENCE_FAILED", "concurrent write failed before source boundary: " + error.code)
                else:
                    raise C.Refusal("STORY_EVIDENCE_FAILED", "concurrent business edit was accepted")
                assert_preserved(edit, destination, original)
                targets.append({"target": target, "positive": "PASS", "concurrent": "SOURCE_MISMATCH", "boundary_reached": edit.reached,
                                "edit_preserved": True, "destination_preserved": True, "temporary_removed": True,
                                "inventory": A.summary(stable)})
                (private / BUSINESS).write_bytes(business_original)
                A.assert_stable(private, stable)
                (logs / (target + "-outcome.json")).write_bytes(C.canonical(targets[-1]) + b"\n")
                print("Completed " + target + ": positive write PASS; concurrent SOURCE_MISMATCH; "
                      "edit and destination preserved; temporary removed", flush=True)
            shutil.copyfile(state_path, logs / (target + "-synthetic-http.json"))
    C.require(len(executions) == 6, "STORY_EVIDENCE_FAILED", "both targets require three fresh business executions")
    return {"result": "PASS", "platform_facts": "synthetic; no live approval, seal or CI delivery", "business_executions": executions,
            "private_inputs": identities, "targets": targets, "parent_story_9_2": "in-progress"}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__, allow_abbrev=False)
    parser.add_argument("--logs", type=Path, required=True)
    args = parser.parse_args(argv)
    logs = args.logs.absolute()
    created = False
    try:
        C.require(os.environ.get("TMPDIR") and Path(os.environ["TMPDIR"]).is_dir(), "INPUT_INVALID", "run this driver through run-with-temp", 2)
        C.require(not logs.exists() and not any(path.is_symlink() for path in (logs, *logs.parents))
                  and not logs.is_relative_to(Path(os.environ["TMPDIR"]).resolve()), "INPUT_INVALID", "logs need a new persistent directory without symbolic parents", 2)
        logs.mkdir(parents=True, mode=0o700)
        created = True
        result = prove(ROOT, logs)
        code = 0
    except (C.Refusal, OSError, ValueError, KeyError, TypeError, subprocess.SubprocessError) as error:
        result = {"result": "FAIL", "code": getattr(error, "code", "INPUT_INVALID"), "diagnostic": str(error),
                  "platform_facts": "synthetic; no live approval, seal or CI delivery", "parent_story_9_2": "in-progress"}
        code = 1
    if created:
        (logs / "guarded-write-result.json").write_bytes(C.canonical(result) + b"\n")
    print(json.dumps(result, sort_keys=True), flush=True)
    return code


if __name__ == "__main__":
    raise SystemExit(main())
