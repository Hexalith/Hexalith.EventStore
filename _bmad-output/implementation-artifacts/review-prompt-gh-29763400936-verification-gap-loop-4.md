# Verification-gap review prompt — loop 4

Use the repository-local `bmad-review-verification-gap` skill and read its
`SKILL.md` completely. Review the current working tree for
`spec-gh-29763400936-fix-release-post-publish-status.md` against baseline
`f435d968eae603bf377809925f78f25fdac5f4f5`.

Inspect the complete tracked and untracked diff without staging or editing.
Determine whether every changed runtime and CI behavior has reliable proof,
especially:

- the actual pinned Undici transport and selected-origin egress guard;
- the installed plugin version, exact production plugin options, and isolated
  issue-like/ordinary lifecycle cases;
- exact stale-failure cleanup GraphQL versus forbidden run-ID lookups and
  numeric issue/PR mutations;
- the semantic-release governance job's uniqueness, unconditional/blocking
  behavior, SHA-pinned actions, Node 22, `npm ci`, and fixture ordering;
- required publication failures, every frozen matrix row, documentation, and
  recorded verification results.

Local evidence currently reports: fixture passed; Release build had zero
warnings/errors; `ContainerPublishingGovernanceTests` passed 9/9;
`ReleasePackageManifestTests` passed 28/28; and actionlint/whitespace checks
passed. Verify those claims rather than trusting them.

Return only evidence-backed verification gaps. For each, include file and line,
the impacted behavior, existing evidence, missing proof, a concrete
demonstration, and consequence. Do not modify files, perform remote operations,
or recommend reverting unrelated concurrent work.
