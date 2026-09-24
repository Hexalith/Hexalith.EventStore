#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

build_args=(
  tests/Hexalith.EventStore.Contracts.Tests/Hexalith.EventStore.Contracts.Tests.csproj
  --configuration Release
  -m:1
)
if [[ -n "${HEXALITH_BUILDS_SOURCE:-}" ]]; then
  build_args+=("-p:Hexalith4BuildPackageProps=${HEXALITH_BUILDS_SOURCE}/Props/Directory.Packages.props")
fi
dotnet build "${build_args[@]}"

runner="tests/Hexalith.EventStore.Contracts.Tests/bin/Release/net10.0/Hexalith.EventStore.Contracts.Tests"
for test_class in \
  Oq8V5CandidateTests \
  TrustedPublishingReleaseTests \
  ContainerPublishingGovernanceTests \
  ReleasePackageManifestTests; do
  "$runner" -class "*$test_class"
done
"$runner" -method '*PublicationAuthorityFixturesPassWithoutSkippedCases'
