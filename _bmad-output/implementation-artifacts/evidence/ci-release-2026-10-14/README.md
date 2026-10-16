# CI and release verification — 2026-10-14

Release: [v3.122.0](https://github.com/Hexalith/Hexalith.EventStore/releases/tag/v3.122.0).
Source: `1dac529c4690910c89d4090784a7d46cb9433a9d`.

All seven push workflows and Release succeeded for the released source. Release run `38588597583` used the supported `bypass-validation=true` input and the authenticated required-reviewer account approved its production deployment, without changing environment protections. No local tests ran; GitHub ran its configured CI suites.

`nuget-verification.json` records downloaded packages for all fourteen manifest IDs, one version, matching nuspec repository commits and matching GitHub/NuGet uncompressed payloads excluding NuGet's added signature. This compares payloads; it does not claim cryptographic signature verification. NuGet indexing and negative CDN caching delayed availability. The final DomainService download used a fresh query on the same public flat-container object; its successful response URL and content hash are retained separately.

The release evidence confirms the EventStore OCI index contains linux/amd64 and linux/arm64, both revision/version labels match the source, and both workflow smoke checks passed. Those smokes used Development and establish publication continuity; they do not claim production readiness.

The temporary verifier was run with `python3 /tmp/eventstore-verify-release.py --version 3.122.0 --sha 1dac529c4690910c89d4090784a7d46cb9433a9d --github-assets /tmp/eventstore-release-3.122.0-assets --output /tmp/eventstore-release-verification.json` and finished with exit 0 and fourteen verified packages. The asset origin is the GitHub release above, and each NuGet origin is recorded in the JSON.
