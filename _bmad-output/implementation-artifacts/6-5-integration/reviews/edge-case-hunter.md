[
  {
    "location": "_bmad-output/implementation-artifacts/6-5-integration/verify.py:90-114",
    "trigger_condition": "Source-pin rows disappear, duplicate, change labels, or contain incorrect D-import hashes.",
    "guard_snippet": "require(abc_rows == expected_abc_rows and d_rows == manifest_d_rows, 'document-input-pin-set')",
    "potential_consequence": "The gate accepts incomplete or contradictory source provenance."
  },
  {
    "location": "_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md:1285",
    "trigger_condition": "Publication remains unresolved after a previous resume outcome's fixed deletion deadline.",
    "guard_snippet": "if terminal_verification_requires(original_closure_sources): retain_sources_and_original_charge()",
    "potential_consequence": "Deleting historical closure evidence makes C5 terminal verification permanently unavailable."
  },
  {
    "location": "_bmad-output/implementation-artifacts/6-5-integration/verify.py:116-118",
    "trigger_condition": "Every required block runs.",
    "guard_snippet": "next(ART.glob(...)) also selects existing -2.md execution records, which contain no model.",
    "potential_consequence": "Unmodified checkouts can reject valid inputs because directory enumeration selects the execution record.",
    "kind": "claim",
    "confidence": "high"
  },
  {
    "location": "_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md:1069",
    "trigger_condition": "Every publication seam has one exact algorithm and bound.",
    "guard_snippet": "D3 deletes historical closure/authentication sources at fixed deadlines; C5 still requires their complete bytes.",
    "potential_consequence": "Implementers cannot satisfy both reclamation and terminal-verification rules for long-lived unresolved operations.",
    "kind": "claim",
    "confidence": "high"
  }
]
