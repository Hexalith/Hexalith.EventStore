# Platform production-profile lookup

At the owner's request, inspected the existing root-declared
`references/Hexalith.Platform` checkout read-only on 2026-10-08. No approved
`deploy/dapr/production-profile.yaml` or AD-26 ratification record was found.
This lookup supplies no production authority.

The checkout was clean on `main` at
`60540283722895d76c32ecfbe2db956b6a4b0c83`, one commit behind its cached
`origin/main`, `f5a0d72f9b72e88008562147a7085da0607d55f7`. The cached newer commit
changes custody actors/tests and submodule references; it adds no production
profile. No fetch, pull, submodule initialization or update was performed, so
this finding is limited to the local checkout and cached remote revision.
The locally cached `origin/story/4-4-live-evidence` and
`origin/story/4-4-repository-controls` refs were also present. A scoped
`git log --all` lookup of `production-profile.yaml`/`.yml` paths returned no
historical commits across the cached refs.

The Platform architecture spine's production-profile row (line 237) assigns
the ratified template to EventStore AD-26. Its first-staging prerequisite row
(line 498) still calls for profile ratification, catalog/secret-contract
instances, durable-broker selection and qualification, and component/topology
evidence. The production actor-history custody implementation plan (line 38)
lists Platform Story 4.9's ratified profile/digest contribution as backlog and
identifies the EventStore AD-26 template as upstream.

Read-only checks included `git status --short --branch`,
`git rev-parse HEAD origin/main`, `git show --stat origin/main`, scoped
`git ls-tree -r --name-only origin/main -- deploy deployment deployments`,
and filename/text searches for the profile, AD-26 and ratification in ordinary
Platform source/documentation, excluding nested references and skill content.
The scoped deploy tree listing returned no paths. Ordinary planning documents
contain requirements for a profile, rather than an approved profile instance.

Dormant EventStore implementation and local verification continue. The missing
profile remains a final serving-qualification dependency; M1–M8, O01–O20 and
activation dispositions are unchanged.

On 2026-10-09, a second read-only check found the checkout still at
`60540283722895d76c32ecfbe2db956b6a4b0c83`, now three commits behind its
cached `origin/main` at `f6f95cdf5a63e264ada564c4cbb5a5b40b0d5a9a`.
The two additional cached commits concern deletion capability/security denial
recording and deletion batch coordination. A scoped deploy-tree listing at that
cached tip still found no production profile; `git log --all` still found no
history for `deploy/dapr/production-profile.yaml` or `.yml`. No fetch, pull or
submodule update was performed. This remains a local/cached-ref finding, not
a claim about every remote revision.
