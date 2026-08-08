# Edge Case Hunter Result

```json
[
  {
    "location": "tests/Hexalith.EventStore.Contracts.Tests/Packaging/Fixtures/semantic-release-github-success.mjs:globalThis.fetch→SelectedOriginDispatcher",
    "trigger_condition": "Plugin or dependency uses native globalThis.fetch",
    "guard_snippet": "Patch globalThis.fetch to allow only selectedOrigin beside Undici dispatcher",
    "potential_consequence": "Live GitHub egress bypasses Undici-only origin allow-list",
    "kind": "deletion",
    "confidence": "high"
  },
  {
    "location": "semantic-release-github-success.mjs:245-254 (SelectedOriginDispatcher.dispatch)",
    "trigger_condition": "Plugin HTTP uses a private Undici Agent",
    "guard_snippet": "Assert plugin Agent identity equals getGlobalDispatcher() before success()",
    "potential_consequence": "Origin allow-list never observes plugin GitHub calls"
  },
  {
    "location": "semantic-release-github-success.mjs:324-327 (external Undici probe)",
    "trigger_condition": "undiciFetch rejects for a non-guard reason",
    "guard_snippet": "assert.rejects(..., err => /Undici egress blocked/.test(err.message))",
    "potential_consequence": "Probe can pass while the origin dispatcher guard is inactive"
  },
  {
    "location": "semantic-release-github-success.mjs:195-198 (getSRIssues response)",
    "trigger_condition": "Cleanup GraphQL returns non-empty issue nodes",
    "guard_snippet": "Seed stale issue nodes; assert issue/comment mutations stay 418",
    "potential_consequence": "Stale-failure cleanup mutations escape the allow-list proof"
  },
  {
    "location": "semantic-release-github-success.mjs:278-302 (histories)",
    "trigger_condition": "Commits use Fixes #N, (#N), or issue URL forms",
    "guard_snippet": "Add classic issue-closing histories beside fix/gh-<run-id>",
    "potential_consequence": "Alternate parsers may still attempt reference resolution"
  },
  {
    "location": "ReleasePackageManifestTests.cs:AssertSemanticReleaseGovernanceJobIsBlocking",
    "trigger_condition": "Governance checkout drops persist-credentials: false",
    "guard_snippet": "Require with: persist-credentials: false on pinned checkout step",
    "potential_consequence": "GITHUB_TOKEN persists into npm ci and fixture node steps"
  },
  {
    "location": "ReleasePackageManifestTests.cs:AssertSemanticReleaseGovernanceJobIsBlocking",
    "trigger_condition": "Job gains contents: write or pull-requests: write",
    "guard_snippet": "Assert governance job omits write permissions and token env overrides",
    "potential_consequence": "Fixture holds a writable token despite successCommentCondition false"
  }
]
```
