#!/usr/bin/env python3
"""Corrective-release identity binder retained for the shared publisher contract.

The EventStore Release workflow does not call this helper. Semantic Release
selects the version, and the production environment approves publication.
"""

import importlib.util
import os
import re
import sys
from pathlib import Path


def main():
    candidate = sys.argv[1:2] == ["candidate"]
    if not candidate and sys.argv[1:2] != ["check"]:
        raise ValueError("Expected candidate or check mode.")
    del sys.argv[1]
    contract = Path(os.environ.get("HEXALITH_RELEASE_CONTRACT_DIRECTORY", ".hexalith/release")).resolve()
    sys.path.insert(0, str(contract))
    spec = importlib.util.spec_from_file_location("publication_preflight", contract / "publication_preflight.py")
    publisher = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(publisher)
    arguments = publisher._parse_arguments()
    try:
        if arguments.source_ci_workflow != "ci.yml":
            publisher._fail("source-workflow-invalid", "CI proof cannot be bypassed.")
        if arguments.contract_directory.resolve() != contract:
            publisher._fail("contract-mismatch", "Identity must use the installed approved publisher contract.")
        if os.environ.get("GITHUB_EVENT_NAME") != "workflow_dispatch":
            publisher._fail("run-identity-invalid", "Publication requires an explicit workflow dispatch.")
        source_proof = None
        for workflow in ("ci.yml", "p1r-qualification.yml", "commitlint.yml"):
            proof = publisher.prove_current_green_source(
                arguments.repository, arguments.source_sha, arguments.source_branch,
                workflow, os.environ.get("GITHUB_TOKEN", ""),
            )
            if workflow == "ci.yml":
                source_proof = proof
        identity = publisher.build_publication_identity(arguments, source_proof)
        identity_bytes = publisher._canonical_bytes(identity)
        digest = publisher._sha256_bytes(identity_bytes)
        candidate_path = Path(os.environ.get(
            "HEXALITH_RELEASE_CANDIDATE_IDENTITY", ".hexalith/release-candidate/publication-identity.json"))
        if candidate:
            # Exclusive creation: never silently replace a previously exposed candidate.
            directory = publisher.workspace_output_directory(str(candidate_path.parent))
            publisher.workspace_make_directory(directory)
            candidate_path = directory / candidate_path.name
            with candidate_path.open("xb") as output:
                output.write(identity_bytes)
            with candidate_path.with_suffix(".sha256").open("x", encoding="utf-8") as output:
                output.write(digest + "\n")
            if os.environ.get("GITHUB_OUTPUT"):
                with open(os.environ["GITHUB_OUTPUT"], "a", encoding="utf-8") as output:
                    output.write(f"candidate-identity-sha256={digest}\n")
                    output.write(f"candidate-run-attempt={identity['run']['attempt']}\n")
            print(f"Read-only publication candidate identity: {digest}")
        else:
            expected = os.environ.get("HEXALITH_RELEASE_CANDIDATE_SHA256", "")
            if re.fullmatch(r"[0-9a-f]{64}", expected) is None:
                publisher._fail("candidate-missing", "The same-run candidate identity hash is required.")
            if digest != expected or publisher.workspace_read_bytes(candidate_path) != identity_bytes:
                publisher._fail("candidate-mismatch", "Publication differs from the exposed same-run candidate.")
            authority = publisher.validate_publication_authority(
                arguments, identity, os.environ.get("GITHUB_TOKEN", ""))
            # Retain the approved engine's consumption protocol, and also reject a
            # reused nonce or identity attached to a replacement authority comment.
            for comment in publisher._github_json_array(
                    arguments.authority_issue_url + "/comments", os.environ.get("GITHUB_TOKEN", "")):
                author = comment.get("user") if isinstance(comment, dict) else None
                if not isinstance(author, dict) or author.get("login") != "github-actions[bot]":
                    continue
                try:
                    body = publisher._embedded_json(comment.get("body", ""))
                except (AttributeError, TypeError, ValueError):
                    continue
                if (isinstance(body, dict)
                        and body.get("schema") == publisher.AUTHORITY_CONSUMPTION_SCHEMA
                        and (body.get("nonce") == authority["nonce"]
                             or body.get("identity_sha256") == digest)):
                    publisher._fail("authority-replayed", "Publication nonce or identity was already consumed.")
    except (publisher.PreflightError, OSError) as error:
        print(f"[release-identity] {getattr(error, 'code', 'candidate-unavailable')}: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
