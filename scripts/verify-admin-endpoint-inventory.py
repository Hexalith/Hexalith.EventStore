#!/usr/bin/env python3
"""Check the Story 5.2 Admin action inventory against its three sources.

Controller attributes, the exhaustive map in AdminActionSecurityMetadataTests,
and the public table in docs/brownfield/api-contracts.md must name the same
action, policy, tenant filter, and encoded-body limit. The script only reads
those files.
"""

from __future__ import annotations

import re
import sys
from dataclasses import dataclass
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CONTROLLERS = ROOT / "src/Hexalith.EventStore.Admin.Server/Controllers"
EXPECTED = (
    ROOT
    / "tests/Hexalith.EventStore.Admin.Server.Tests/Authorization/AdminActionSecurityMetadataTests.cs"
)
CONTRACTS = ROOT / "docs/brownfield/api-contracts.md"

POLICY_RANK = {"ReadOnly": 0, "Operator": 1, "Admin": 2}
POLICY_DOC = {"ReadOnly": "AdminReadOnly", "Operator": "AdminOperator", "Admin": "AdminFull"}
LIMIT_DOC = {
    None: "N/A (bodyless)",
    "OrdinaryJsonBody": "1 MiB",
    "BackupImportJsonBody": "10 MiB",
}

# Hand-read from AdminConsistencyController, plus the two route-constraint
# shapes that a line-oriented parser is most likely to flatten wrong.
HAND_ACTIONS = {
    ("AdminConsistencyController", "GetCheckResult"): (
        "GET",
        "/api/v1/admin/consistency/checks/{checkId}",
        "Admin",
        False,
        None,
        None,
        False,
    ),
    ("AdminConsistencyController", "GetChecks"): (
        "GET",
        "/api/v1/admin/consistency/checks",
        "ReadOnly",
        True,
        None,
        None,
        False,
    ),
    ("AdminConsistencyController", "TriggerCheck"): (
        "POST",
        "/api/v1/admin/consistency/checks",
        "Operator",
        True,
        "OrdinaryJsonBody",
        "ConsistencyCheckRequest",
        True,
    ),
    ("AdminConsistencyController", "CancelCheck"): (
        "POST",
        "/api/v1/admin/consistency/checks/{checkId}/cancel",
        "Admin",
        False,
        None,
        None,
        False,
    ),
    ("AdminBackupsController", "TriggerBackup"): (
        "POST",
        "/api/v1/admin/backups/{tenantId}",
        "Admin",
        True,
        None,
        None,
        False,
    ),
    ("AdminStreamsController", "GetEventDetailAsync"): (
        "GET",
        "/api/v1/admin/streams/{tenantId}/{domain}/{aggregateId}/events/{sequenceNumber}",
        "ReadOnly",
        True,
        None,
        None,
        False,
    ),
}


@dataclass(frozen=True)
class Action:
    controller: str
    name: str
    method: str
    path: str
    policy: str
    tenant_filter: bool
    body_limit: str | None
    body_type: str | None
    nullable_body: bool
    advertises_413: bool

    def key(self) -> tuple[str, str]:
        return (self.controller, self.name)

    def route_key(self) -> tuple[str, str]:
        return (self.method, self.path)


def strip_route_constraints(template: str) -> str:
    result: list[str] = []
    index = 0
    while index < len(template):
        if template[index] != "{":
            result.append(template[index])
            index += 1
            continue
        end = index + 1
        while template[end] not in ":}":
            end += 1
        name = template[index + 1 : end]
        if template[end] == ":":
            depth = 0
            end += 1
            while not (template[end] == "}" and depth == 0):
                if template[end] == "(":
                    depth += 1
                elif template[end] == ")":
                    depth -= 1
                end += 1
        if template[end] != "}":
            raise ValueError(f"unclosed route parameter in {template!r}")
        result.append("{" + name + "}")
        index = end + 1
    return "".join(result)


def join_route(prefix: str, template: str) -> str:
    path = "/" + prefix.strip("/")
    if template:
        path += "/" + strip_route_constraints(template).strip("/")
    return path


def attribute_value(line: str, pattern: str) -> re.Match[str] | None:
    return re.fullmatch(pattern, line.strip())


def policies_in(lines: list[str]) -> list[str]:
    found: list[str] = []
    for line in lines:
        match = attribute_value(line, r"\[Authorize(?:\(Policy = AdminAuthorizationPolicies\.(\w+)\))?\]")
        if match and match.group(1):
            found.append(match.group(1))
    return found


def effective_policy(class_policies: list[str], method_policies: list[str]) -> str:
    chosen = class_policies + method_policies
    unknown = [policy for policy in chosen if policy not in POLICY_RANK]
    if unknown:
        raise ValueError(f"unclassified Admin policy {unknown}")
    if not chosen:
        raise ValueError("action has no named Admin policy")
    return max(chosen, key=lambda policy: POLICY_RANK[policy])


def class_attributes(lines: list[str], class_line: int) -> list[str]:
    collected: list[str] = []
    for line in reversed(lines[:class_line]):
        stripped = line.strip()
        if stripped.startswith("["):
            collected.append(stripped)
            continue
        if stripped == "" or stripped.startswith("///") or stripped.startswith("//"):
            continue
        break
    collected.reverse()
    return collected


def attributes_above(lines: list[str], signature: int) -> list[str]:
    collected: list[str] = []
    index = signature - 1
    while index >= 0:
        stripped = lines[index].strip()
        if stripped == "" or stripped.startswith("///") or stripped.startswith("//"):
            index -= 1
            continue
        if stripped.startswith("["):
            collected.append(stripped)
            index -= 1
            continue
        break
    collected.reverse()
    return collected


def signature_text(lines: list[str], signature: int) -> str:
    chunk = [lines[signature]]
    if re.search(r"\)\s*\{", lines[signature]):
        return lines[signature]
    for line in lines[signature + 1 :]:
        chunk.append(line)
        if re.search(r"\)\s*\{", line):
            break
    return "\n".join(chunk)


def body_parameter(signature: str) -> tuple[str | None, bool]:
    found = re.findall(r"\[FromBody\]\s+([\w.]+)(\?)?", signature)
    if len(found) > 1:
        raise ValueError(f"multiple [FromBody] parameters in {signature!r}")
    if not found:
        return None, False
    return found[0][0], found[0][1] == "?"


def http_binding(attributes: list[str]) -> tuple[str, str] | None:
    for line in attributes:
        match = attribute_value(line, r"\[(Http(?:Get|Post|Put|Delete|Patch))(?:\(\"(.*)\"\))?\]")
        if match:
            return match.group(1).removeprefix("Http").upper(), match.group(2) or ""
    return None


def parse_controller(path: Path) -> list[Action]:
    lines = path.read_text(encoding="utf-8").splitlines()
    if any("[AllowAnonymous" in line for line in lines):
        raise ValueError(f"{path.name} declares AllowAnonymous")
    class_line = next(index for index, line in enumerate(lines) if line.startswith("public class "))
    class_match = re.fullmatch(r"public class (\w+)\(.*", lines[class_line])
    if class_match is None:
        raise ValueError(f"{path.name} has no primary-constructor controller declaration")
    controller = class_match.group(1)
    header = class_attributes(lines, class_line)
    route = next(
        match.group(1)
        for line in header
        if (match := attribute_value(line, r"\[Route\(\"(.*)\"\)\]"))
    )
    class_policies = policies_in(header)
    actions: list[Action] = []
    for index, line in enumerate(lines):
        match = re.match(r"\s*public async Task<IActionResult> (\w+)\(", line)
        if not match:
            continue
        attributes = attributes_above(lines, index)
        binding = http_binding(attributes)
        if binding is None:
            raise ValueError(f"{controller}.{match.group(1)} has no HTTP verb")
        method, template = binding
        body_type, nullable = body_parameter(signature_text(lines, index))
        limit_match = next(
            (
                found
                for item in attributes
                if (found := attribute_value(item, r"\[RequestSizeLimit\(AdminRequestSizeLimits\.(\w+)\)\]"))
            ),
            None,
        )
        advertises_413 = any("StatusCodes.Status413PayloadTooLarge" in item for item in attributes)
        tenant_filter = any(
            attribute_value(item, r"\[ServiceFilter\(typeof\(AdminTenantAuthorizationFilter\)\)\]")
            for item in attributes
        )
        actions.append(
            Action(
                controller,
                match.group(1),
                method,
                join_route(route, template),
                effective_policy(class_policies, policies_in(attributes)),
                tenant_filter,
                None if limit_match is None else limit_match.group(1),
                body_type,
                nullable,
                advertises_413,
            )
        )
    return actions


def parse_expected(text: str) -> dict[tuple[str, str], tuple[str, bool, str | None]]:
    rows = re.findall(
        r"\(typeof\((\w+)\), \"(\w+)\", AdminAuthorizationPolicies\.(\w+), (true|false), "
        r"(null|AdminRequestSizeLimits\.(\w+))\)",
        text,
    )
    return {
        (controller, action): (policy, flag == "true", limit or None)
        for controller, action, policy, flag, _, limit in rows
    }


def parse_contracts(text: str) -> dict[tuple[str, str], tuple[str, str, str]]:
    section = text.split("### Admin operation policy and request-limit inventory", 1)[1]
    section = section.split("> Admin API serves", 1)[0]
    rows: dict[tuple[str, str], tuple[str, str, str]] = {}
    for line in section.splitlines():
        match = re.match(
            r"^\| `(GET|POST|PUT|DELETE|PATCH) ([^`]+)` \| `([^`]+)` \| ([^|]+) \| ([^|]+) \|",
            line,
        )
        if match:
            rows[(match.group(1), match.group(2))] = (
                match.group(3).strip(),
                match.group(4).strip(),
                match.group(5).strip(),
            )
    return rows


def body_shape_matches(action: Action, shape: str) -> bool:
    if action.body_type is None:
        return shape == "none"
    if action.body_type == "string":
        return "JSON string" in shape
    if action.nullable_body and "nullable" not in shape:
        return False
    return action.body_type in shape


def source_problems(actions: list[Action]) -> list[str]:
    problems: list[str] = []
    for action in actions:
        label = f"{action.controller}.{action.name}"
        has_body = action.body_type is not None
        if has_body != (action.body_limit is not None):
            problems.append(f"{label} body parameter and request-size limit disagree")
        if has_body != action.advertises_413:
            problems.append(f"{label} 413 metadata and body limit disagree")
        if action.body_limit not in LIMIT_DOC:
            problems.append(f"{label} uses unclassified limit {action.body_limit}")
    return problems


def compare(actions: list[Action], expected: dict, contracts: dict) -> list[str]:
    problems = source_problems(actions)
    by_name = {action.key(): action for action in actions}
    by_route = {action.route_key(): action for action in actions}
    if len(by_name) != len(actions):
        problems.append("duplicate controller action names")
    if len(by_route) != len(actions):
        problems.append("duplicate method and path")

    for key, (policy, tenant_filter, body_limit) in expected.items():
        action = by_name.get(key)
        if action is None:
            problems.append(f"expected map has {key[0]}.{key[1]} with no controller action")
            continue
        if (action.policy, action.tenant_filter, action.body_limit) != (policy, tenant_filter, body_limit):
            problems.append(
                f"{key[0]}.{key[1]} source {(action.policy, action.tenant_filter, action.body_limit)} "
                f"!= expected {(policy, tenant_filter, body_limit)}"
            )
    for key in by_name:
        if key not in expected:
            problems.append(f"controller action {key[0]}.{key[1]} is missing from the expected map")

    for route, (policy, shape, limit) in contracts.items():
        action = by_route.get(route)
        if action is None:
            problems.append(f"policy table has {' '.join(route)} with no controller action")
            continue
        if policy != POLICY_DOC[action.policy] or limit != LIMIT_DOC[action.body_limit] or not body_shape_matches(action, shape):
            problems.append(
                f"{action.controller}.{action.name} table {(policy, shape, limit)} "
                f"!= source {(POLICY_DOC[action.policy], action.body_type, LIMIT_DOC[action.body_limit])}"
            )
    for action in actions:
        if action.route_key() not in contracts:
            problems.append(f"{action.controller}.{action.name} {action.method} {action.path} is missing from the policy table")
    return problems


def hand_problems(actions: list[Action]) -> list[str]:
    by_name = {action.key(): action for action in actions}
    problems: list[str] = []
    for key, expected in HAND_ACTIONS.items():
        action = by_name.get(key)
        if action is None:
            problems.append(f"hand unit missing {key[0]}.{key[1]}")
            continue
        actual = (
            action.method,
            action.path,
            action.policy,
            action.tenant_filter,
            action.body_limit,
            action.body_type,
            action.advertises_413,
        )
        if actual != expected:
            problems.append(f"hand unit {key[0]}.{key[1]} parsed {actual} != read {expected}")
    return problems


def main() -> int:
    actions = [action for path in sorted(CONTROLLERS.glob("Admin*Controller.cs")) for action in parse_controller(path)]
    hand = hand_problems(actions)
    if hand:
        print("\n".join(hand), file=sys.stderr)
        return 1
    print(f"hand unit: {len(HAND_ACTIONS)} actions match the controller text")

    problems = compare(
        actions,
        parse_expected(EXPECTED.read_text(encoding="utf-8")),
        parse_contracts(CONTRACTS.read_text(encoding="utf-8")),
    )
    if problems:
        print("\n".join(problems), file=sys.stderr)
        return 1
    print(
        f"inventory: {len(actions)} actions agree across controllers, "
        "AdminActionSecurityMetadataTests, and docs/brownfield/api-contracts.md"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
