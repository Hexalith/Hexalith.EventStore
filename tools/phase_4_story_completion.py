"""Guard new spec and tracker completion writes against authoritative NFR coverage.

Only an ordinary, unambiguously unguarded slice can complete without assurance.
Governed claims always use fresh shared consumption and complete story-owned NFR
evidence from their registered adapter.
"""

import datetime as dt
from contextlib import ExitStack
import json
import os
from pathlib import Path
import re
import stat
import subprocess
import sys
import tempfile

import phase_4_transition as C
from phase_4_gate_decision import Parser

PRD = "_bmad-output/planning-artifacts/prd.md"
EPICS = "_bmad-output/planning-artifacts/epics.md"
TRACKER = "_bmad-output/implementation-artifacts/sprint-status.yaml"
CUSTOMIZATION = "_bmad/custom/bmad-build.toml"
SCHEMA = "hexalith.eventstore.phase-4-story-completion.v1"
CLAIM_KEYS = {"story_key", "story_id", "spec_path", "target", "scope", "nfr_ids",
              "high_risk_nfr_ids", "coverage_sha256"}
STORY_KEY = re.compile(r"([1-9][0-9]*)-([1-9][0-9]*)-[a-z0-9]+(?:-[a-z0-9]+)*")
NFR = re.compile(r"\bNFR([1-9][0-9]*)(?:\s*[-–—]\s*NFR([1-9][0-9]*))?\b")


def ordered(values):
    return sorted(values, key=lambda value: int(value[3:]))


def frontmatter(content, identity_required=True):
    """Read scalar identity/lifecycle fields, refusing duplicates or YAML aliases."""
    text = content.decode("utf-8")
    match = re.match(r"\A---\r?\n(.*?)\r?\n---(?:\r?\n|$)", text, re.S)
    C.require(match is not None, "STORY_IDENTITY_MISSING", "spec needs frontmatter")
    rows = {"story_key": [], "status": []}
    for line in match[1].splitlines():
        if not line.strip() or line.lstrip().startswith("#"):
            continue
        if line[0].isspace():
            C.require(not re.match(r"(?:story_key|status)[ \t]*:", line.lstrip())
                      and not line.lstrip().startswith(("'", '"', "?", "<<:")),
                      "STORY_IDENTITY_MISSING", "unsupported nested identity structure")
            continue
        # Only plain mapping keys are supported. Quoted/escaped/explicit keys,
        # tags and merge keys cannot hide a second YAML identity or status.
        entry = re.fullmatch(r"([A-Za-z_][A-Za-z0-9_-]*)[ \t]*:[ \t]*(.*)", line)
        C.require(entry is not None and entry[1] != "<<", "STORY_IDENTITY_MISSING",
                  "unsupported frontmatter mapping key")
        if entry[1] in rows:
            rows[entry[1]].append(entry[2])
    fields = {}
    for name, values in rows.items():
        if name == "story_key" and not identity_required and not values:
            continue
        C.require(len(values) == 1, "STORY_IDENTITY_MISSING", "spec needs one scalar " + name)
        scalar = re.fullmatch(r"(?:'([a-z0-9-]+)'|\"([a-z0-9-]+)\"|([a-z0-9-]+))(?:[ \t]+#[^\r\n]*)?[ \t]*", values[0])
        C.require(scalar is not None, "STORY_IDENTITY_MISSING", "invalid scalar " + name)
        fields[name] = next(value for value in scalar.groups() if value is not None)
    return fields


def tracker_rows(content):
    """Accept only unique story rows inside the development_status mapping."""
    text = content.decode("utf-8")
    sections = re.findall(r"^development_status:\s*\r?\n((?:[ \t].*\r?\n|\r?\n)*)", text, re.M)
    C.require(len(sections) == 1, "STORY_IDENTITY_MISSING", "tracker needs one development_status mapping")
    rows = re.findall(r"^  ([1-9][0-9]*-[1-9][0-9]*-[a-z0-9-]+): ([a-z-]+)\s*(?:#.*)?$", sections[0], re.M)
    C.require(len(rows) == len(dict(rows)), "STORY_IDENTITY_MISSING", "duplicate tracker story identity")
    return dict(rows)


def story_list(text):
    """Expand explicit same-epic story ranges in PRD coverage cells."""
    if text.strip() in ("", "—", "-", "None"):
        return set()
    result = set()
    for item in text.split(","):
        match = re.fullmatch(r"\s*([1-9][0-9]*)\.([1-9][0-9]*)(?:[-–—]([1-9][0-9]*)\.([1-9][0-9]*))?\s*", item)
        C.require(match is not None, "COVERAGE_AMBIGUOUS", "unsupported PRD story mapping: " + item)
        epic, start = int(match[1]), int(match[2])
        end = int(match[4]) if match[4] else start
        C.require((not match[3] or int(match[3]) == epic) and start <= end <= start + 100,
                  "COVERAGE_AMBIGUOUS", "invalid story range")
        result.update(f"{epic}.{number}" for number in range(start, end + 1))
    return result


def nfrs(text):
    C.require(not re.search(r"\bNFR[0-9]+\s*[-–—]\s*[0-9]", text),
              "COVERAGE_AMBIGUOUS", "unsupported NFR mapping syntax")
    result = set()
    for match in NFR.finditer(text):
        start, end = int(match[1]), int(match[2] or match[1])
        C.require(1 <= start <= end <= 19, "COVERAGE_AMBIGUOUS", "unknown or reversed NFR range")
        result.update(f"NFR{number}" for number in range(start, end + 1))
    # Unsupported shorthand must never silently produce a smaller denominator.
    remainder = NFR.sub("", text)
    C.require(not re.search(r"\bNFR[0-9]", remainder),
              "COVERAGE_AMBIGUOUS", "unsupported NFR mapping syntax")
    return result


def coverage(prd, epics, story_id):
    """Reconcile the story declaration with primary/supporting PRD coverage."""
    prd_text, epics_text = prd.decode("utf-8"), epics.decode("utf-8")
    sections = re.findall(r"^### 11\.2 High-Risk NFR Story Coverage\r?\n(.*?)(?=^### |\Z)", prd_text, re.M | re.S)
    C.require(len(sections) == 1, "COVERAGE_AMBIGUOUS", "missing or duplicate PRD NFR coverage authority")
    rows = re.findall(r"^\| (NFR[0-9]+) \| ([^|]*)\| ([^|]*)\| ([^|]*)\|\s*$", sections[0], re.M)
    C.require(len(rows) == 19 and {row[0] for row in rows} == {f"NFR{n}" for n in range(1, 20)},
              "COVERAGE_AMBIGUOUS", "PRD coverage needs exactly NFR1-NFR19")
    primary, supporting, inferred = set(), set(), set()
    for nfr, owners, supporters, caveat in rows:
        if story_id in story_list(owners):
            primary.add(nfr)
        if story_id in story_list(supporters):
            supporting.add(nfr)
        # Caveats are free prose, but the interior inventory itself is a closed list.
        if caveat.startswith(("Range-only interior:", "Range-only interiors:")):
            inventory = caveat.split(":", 1)[1].split(";", 1)[0].split(". ", 1)[0].rstrip(".")
            if story_id in story_list(inventory):
                inferred.add(nfr)
    heading = r"^### Story " + re.escape(story_id) + r": [^\r\n]+\r?\n(.*?)(?=^### Story |^## Epic |\Z)"
    stories = re.findall(heading, epics_text, re.M | re.S)
    C.require(len(stories) == 1, "STORY_IDENTITY_MISSING", "story needs exactly one canonical epics section")
    declarations = re.findall(r"^(?:\*\*Requirements coverage:\*\*|Requirements coverage:)[ \t]+([^\r\n]+)", stories[0], re.M)
    C.require(len(declarations) == 1, "COVERAGE_AMBIGUOUS", "story needs one explicit requirements coverage declaration")
    declared = nfrs(declarations[0])
    # PRD supporting-boundary tables supplement §11.2 (for example Story 3.17).
    for line in prd_text.splitlines():
        cells = line.split("|")
        if len(cells) >= 4 and re.match(r"\s*Story " + re.escape(story_id) + r"(?![0-9.])", cells[2]):
            supporting.update(nfrs(cells[1]))
    mapped = primary | supporting | inferred
    C.require(declared == mapped, "COVERAGE_MISMATCH",
              "PRD/epics disagree for story " + story_id + ": PRD=" + ",".join(ordered(mapped))
              + "; epics=" + ",".join(ordered(declared)))
    return {"primary": ordered(primary), "supporting": ordered(supporting),
            "range_only": ordered(inferred), "nfr_ids": ordered(declared)}


def derive(read, spec_path, target):
    """Construct the claim from fixed planning sources, spec identity and tracker."""
    C.require(C.M.repository_path(spec_path) is not None and spec_path.startswith("_bmad-output/implementation-artifacts/")
              and spec_path.endswith(".md") and target in ("spec", "tracker"),
              "STORY_IDENTITY_MISSING", "invalid spec path or completion target")
    metadata = frontmatter(read(spec_path))
    key = metadata["story_key"]
    match = STORY_KEY.fullmatch(key)
    C.require(match is not None, "STORY_IDENTITY_MISSING", "canonical tracker story_key required")
    filename = re.match(r"(?:spec-)?([1-9][0-9]*)-([1-9][0-9]*)-", Path(spec_path).name)
    C.require(filename is None or filename.groups() == match.groups(), "STORY_IDENTITY_MISSING",
              "spec filename conflicts with its canonical story identity")
    story_id = match[1] + "." + match[2]
    rows = tracker_rows(read(TRACKER))
    aliases = [row for row in rows if row.startswith(match[1] + "-" + match[2] + "-")]
    C.require(aliases == [key], "STORY_IDENTITY_MISSING", "spec and tracker must bind one canonical story identity")
    mapping = coverage(read(PRD), read(EPICS), story_id)
    return {"story_key": key, "story_id": story_id, "spec_path": spec_path, "target": target,
            "scope": "slice", "nfr_ids": mapping["nfr_ids"],
            "high_risk_nfr_ids": ordered(set(mapping["nfr_ids"]) & C.HIGH_RISK_NFRS),
            "coverage_sha256": C.digest(C.canonical(mapping))}


def validate_claim(claim):
    C.closed(claim, CLAIM_KEYS)
    C.require(isinstance(claim["story_key"], str) and STORY_KEY.fullmatch(claim["story_key"])
              and isinstance(claim["story_id"], str) and re.fullmatch(r"[1-9][0-9]*\.[1-9][0-9]*", claim["story_id"])
              and isinstance(claim["spec_path"], str) and C.M.repository_path(claim["spec_path"])
              and claim["target"] in ("spec", "tracker") and claim["scope"] == "slice"
              and C.sha(claim["coverage_sha256"]), "STORY_IDENTITY_MISSING", "invalid canonical completion claim")
    for name in ("nfr_ids", "high_risk_nfr_ids"):
        C.require(isinstance(claim[name], list) and all(isinstance(value, str) and re.fullmatch(r"NFR(?:[1-9]|1[0-9])", value)
                  for value in claim[name]) and len(claim[name]) == len(set(claim[name])),
                  "COVERAGE_AMBIGUOUS", "invalid derived NFR inventory")


def bind(control):
    """Derive instead of trusting caller inventories; seal all coverage authorities."""
    request = control.request
    claim = request["story"]
    C.require(request["subject_mode"] == "files" and request["gate"] is None,
              "COVERAGE_MISMATCH", "story completion cannot substitute AD-26 or a gate-pass subject")
    subjects = {entry["path"] for entry in request["subjects"]}
    C.require({PRD, EPICS, TRACKER, claim["spec_path"]} <= subjects,
              "SUBJECT_MISMATCH", "completion subject must bind spec, tracker, PRD and epics")
    expected = derive(control.local, claim["spec_path"], claim["target"])
    C.require(claim == expected, "COVERAGE_MISMATCH", "canonical story claim differs from derived coverage")
    if "nfr_ids" in request:
        C.require(set(request["nfr_ids"]) == set(claim["nfr_ids"]), "COVERAGE_MISMATCH", "caller NFR inventory differs from derived coverage")
    C.require(claim["high_risk_nfr_ids"], "COVERAGE_MISMATCH", "transition needs derived high-risk NFR coverage")
    return expected


def adapter_for(claim):
    """Select a fixed adapter from the derived story identity and NFR coverage."""
    if claim["story_id"] == "2.14":
        import phase_4_story_2_14 as adapter
    elif claim["story_id"] == "2.18":
        import phase_4_story_2_18 as adapter
    elif claim["story_id"] == "5.3":
        import phase_4_story_5_3 as adapter
    else:
        return None

    return adapter if adapter.supported(claim) else None


def git(root, *arguments, absent=False):
    """Fixed read-only history access; never accept caller commands or revisions."""
    process = subprocess.run(["git", "--no-replace-objects", "-C", str(root), *arguments], capture_output=True, timeout=15)
    if absent and process.returncode:
        return None
    C.require(process.returncode == 0, "SOURCE_MISMATCH", "cannot read committed completion authority")
    return process.stdout


def complete(root, spec_path, target, request=None, run_id=None, attempt=None, write=False, api=None):
    """The actual completion writer. Failed verification never changes a label."""
    reader = C.Control.__new__(C.Control)
    reader.root = root
    read = reader.local
    path = spec_path if target == "spec" else TRACKER
    original = read(path)
    if (request is None and target == "spec" and frontmatter(original, identity_required=False)["status"] == "done"
            and git(root, "show", "HEAD:" + path, absent=True) == original):
        return {"result": "PASS", "disposition": "historical-unchanged", "claim": None,
                "verification": None, "message": "Historical label preserved; no evidence or closure authority issued."}
    metadata = frontmatter(read(spec_path))
    status = metadata["status"] if target == "spec" else tracker_rows(original).get(metadata["story_key"])
    # A no-op preserves an existing historical label, never supplies fresh proof.
    if (request is None and status == "done" and git(root, "show", "HEAD:" + path, absent=True) == original
            and git(root, "show", "HEAD:" + spec_path, absent=True) == read(spec_path)):
        return {"result": "PASS", "disposition": "historical-unchanged", "claim": None,
                "verification": None, "message": "Historical label preserved; no evidence or closure authority issued."}
    persisted = git(root, "show", "HEAD:" + spec_path, absent=True)
    if persisted is not None:
        C.require(frontmatter(persisted)["story_key"] == metadata["story_key"], "STORY_IDENTITY_MISSING",
                  "persisted spec identity cannot be relabelled to bypass completion coverage")
    else:
        C.require(re.match(r"(?:spec-)?[1-9][0-9]*-[1-9][0-9]*-", Path(spec_path).name),
                  "STORY_IDENTITY_MISSING", "new specs require a matching numeric story filename")
    claim = derive(read, spec_path, target)
    adapter = adapter_for(claim)
    snapshot = {source: read(source) for source in (PRD, EPICS, TRACKER, spec_path)}
    C.require(not (target == "tracker" and claim["story_id"] == "9.2"),
              "BUSINESS_PREREQUISITE_MISSING", "parent Story 9.2 remains in-progress; this delivery closes only its completion-guard slice")
    if request is not None:
        C.require(request.get("transition") == "high-risk-nfr-story-done" and request.get("story") == claim,
                  "COVERAGE_MISMATCH", "request must bind this exact completion target and derived claim")
        C.require(C.positive(run_id) and C.positive(attempt), "SEAL_MISSING", "completion requires a completed evaluation run and attempt")
        control = C.Control(root, request, api or C.GitHub())
        try:
            verification = control.consume(run_id, attempt)
        except C.Refusal as error:
            error.story_evidence = control.story_evidence
            raise
        # Defense at the writer boundary too: assurance never substitutes NFR proof.
        C.require(adapter is not None and control.story_inventory is not None
                  and verification.get("story_evidence", {}).get("result") == "PASS",
                  "ADAPTER_UNAVAILABLE", "complete story-owned NFR evidence required")
    else:
        for source in (PRD, EPICS):
            C.require(git(root, "show", "HEAD:" + source) == read(source), "SOURCE_MISMATCH",
                      "planning source differs from committed authority: " + source)
        C.require(adapter is None, "SEAL_MISSING",
                  "Story " + claim["story_id"] + " completion requires fresh authenticated approval and completed-run consumption")
        C.require(not claim["high_risk_nfr_ids"], "ADAPTER_UNAVAILABLE",
                  "story-owned NFR evidence adapter unavailable: " + ", ".join(claim["high_risk_nfr_ids"])
                  + "; completion also requires fresh authenticated approval and seal")
        verification = None
    # Verify snapshot stability immediately before any write.
    C.require(all(read(source) == content for source, content in snapshot.items())
              and read(path) == original and derive(read, spec_path, target) == claim,
              "SOURCE_MISMATCH", "completion inputs changed during verification")
    if request is not None:
        adapter.assert_stable(root, control.story_inventory)
    if write:
        text = original.decode("utf-8")
        if target == "spec":
            block = re.match(r"\A---\r?\n(.*?)\r?\n---", text, re.S)
            start, end = block.span(1)
            replacement = re.sub(r"(?m)^(status[ \t]*:[ \t]*)(?:'[a-z0-9-]+'|\"[a-z0-9-]+\"|[a-z0-9-]+)",
                                 r"\g<1>'done'", block[1])
            text = text[:start] + replacement + text[end:]
        else:
            text, count = re.subn(r"(?m)^(  " + re.escape(claim["story_key"]) + r": )[a-z-]+", r"\g<1>done", text)
            C.require(count == 1, "STORY_IDENTITY_MISSING", "tracker completion needs one row")
            text = re.sub(r"(?m)^(last_updated:[ \t]*)[^\r\n]+", r"\g<1>" + dt.date.today().isoformat(), text)
        destination = root / path
        temporary = None
        try:
            with tempfile.NamedTemporaryFile(dir=destination.parent, prefix=".story-completion-", delete=False) as stream:
                temporary = Path(stream.name)
                stream.write(text.encode("utf-8"))
            temporary.chmod(destination.stat().st_mode & 0o777)
            C.require(all(read(source) == content for source, content in snapshot.items())
                      and read(path) == original and derive(read, spec_path, target) == claim,
                      "SOURCE_MISMATCH", "completion inputs changed during temporary preparation")
            if request is not None:
                adapter.assert_stable(root, control.story_inventory)
            temporary.replace(destination)
        finally:
            if temporary is not None:
                temporary.unlink(missing_ok=True)
    return {"result": "PASS", "disposition": "assured-story-slice" if request is not None else "ordinary-slice",
            "claim": claim, "verification": verification,
            "story_evidence": verification.get("story_evidence") if verification is not None else None,
            "message": "Slice completion only; no parent-story or whole-NFR closure."}


def _prepare_report(destination, arguments, result, cleanup):
    """Reserve a safe diagnostic file before parsing or changing lifecycle data."""
    options = {name: [] for name in ("--root", "--spec", "--request")}
    for index, argument in enumerate(arguments):
        for name in options:
            if argument == name and index + 1 < len(arguments) and not arguments[index + 1].startswith("--"):
                options[name].append(Path(arguments[index + 1]))
            elif argument.startswith(name + "="):
                options[name].append(Path(argument.split("=", 1)[1]))
    roots = [Path(__file__).resolve().parents[1], *(path.resolve() for path in options["--root"])]
    destination = destination.absolute()
    C.require(not any(part.is_symlink() for part in (destination, *destination.parents)),
              "INPUT_INVALID", "diagnostic path cannot contain symbolic links", 2)
    C.require(not any(destination.resolve().is_relative_to(root) for root in roots),
              "INPUT_INVALID", "completion diagnostics must be outside the repository", 2)
    C.require(not destination.exists() or stat.S_ISREG(destination.stat().st_mode),
              "INPUT_INVALID", "diagnostic destination must be a regular file", 2)
    inputs = [*options["--request"], *(root / path for path in options["--spec"] for root in roots)]
    C.require(not any(destination.resolve() == source.resolve()
                      or destination.exists() and source.exists() and destination.samefile(source)
                      for source in inputs), "INPUT_INVALID", "diagnostic destination collides with an input", 2)
    destination.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile(mode="w", encoding="utf-8", dir=destination.parent,
                                     prefix=".story-completion-report-", delete=False) as stream:
        temporary = Path(stream.name)
        cleanup.callback(temporary.unlink, missing_ok=True)
        stream.write(json.dumps(result, indent=2, sort_keys=True) + "\n")
    return temporary


def main(argv=None):
    parser = Parser(description=__doc__, allow_abbrev=False)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--spec")
    parser.add_argument("--target", choices=("spec", "tracker"))
    parser.add_argument("--write", action="store_true", help="atomically write done only after successful verification")
    source = parser.add_mutually_exclusive_group()
    source.add_argument("--request", type=Path)
    source.add_argument("--request-env", action="store_true")
    parser.add_argument("--run-id", type=int)
    parser.add_argument("--attempt", type=int)
    parser.add_argument("--result", type=Path)
    arguments = list(sys.argv[1:] if argv is None else argv)
    destination = None
    for index, argument in enumerate(arguments):
        if argument == "--result" and index + 1 < len(arguments) and not arguments[index + 1].startswith("--"):
            destination = Path(arguments[index + 1])
        elif argument.startswith("--result="):
            destination = Path(argument.split("=", 1)[1])
    result = {"schema": SCHEMA, "result": "FAIL", "codes": [], "disposition": "refused",
              "claim": None, "verification": None, "story_evidence": None, "message": ""}
    with ExitStack() as cleanup:
        prepared = None
        try:
            if destination is not None:
                prepared = _prepare_report(destination, arguments, result, cleanup)
            args = parser.parse_args(arguments)
            request = None
            if args.request_env or args.request is not None:
                if args.request_env:
                    content = os.environ.get("PHASE4_REQUEST_JSON", "").encode()
                else:
                    source_reader = C.Control.__new__(C.Control)
                    source_reader.root = args.request.absolute().parent
                    content = source_reader.local(args.request.name)
                C.require(len(content) <= C.MAX_BYTES, "INPUT_INVALID", "request too large", 2)
                request = C.validate_request(C.parse(content))
            C.require(request is not None or args.run_id is None and args.attempt is None,
                      "INPUT_INVALID", "seal coordinates require a canonical transition request", 2)
            spec_path = args.spec or (request or {}).get("story", {}).get("spec_path")
            target = args.target or (request or {}).get("story", {}).get("target")
            C.require(spec_path is not None and target is not None, "STORY_IDENTITY_MISSING", "--spec and --target or canonical request required")
            if Path(spec_path).is_absolute():
                spec_path = Path(spec_path).relative_to(args.root.resolve()).as_posix()
            result.update(complete(args.root.resolve(), spec_path, target, request, args.run_id, args.attempt, args.write))
            code = 0
        except (C.Refusal, OSError, UnicodeError, ValueError, KeyError, TypeError, AttributeError, RecursionError, subprocess.SubprocessError) as error:
            if not isinstance(error, C.Refusal):
                error = C.Refusal("INPUT_INVALID", "malformed or unreadable input: " + type(error).__name__, 2)
            result.update(result="FAIL", codes=[error.code], disposition="refused", message=str(error), verification=None,
                          story_evidence=getattr(error, "story_evidence", None))
            print(error.code + ": " + str(error), file=sys.stderr)
            code = error.exit_code
        try:
            if prepared is not None:
                prepared.write_text(json.dumps(result, indent=2, sort_keys=True) + "\n", encoding="utf-8")
                prepared.replace(destination)
        except (OSError, ValueError):
            result.update(result="FAIL", codes=["INPUT_INVALID"], disposition="refused", message="cannot write diagnostics", verification=None)
            code = 2
        print(json.dumps(result, sort_keys=True))
        return code
