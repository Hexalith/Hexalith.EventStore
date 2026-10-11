#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
fixture_root="$repo_root/tools/folded-snapshot-compatibility"
old_version="${FOLDED_SNAPSHOT_OLD_VERSION:-3.119.0}"
new_version="99.6.2-local$(date +%s%N)"
work_root="$(mktemp -d)"
trap 'rm -rf "$work_root"' EXIT
package_root="$work_root/packages"
mkdir -p "$package_root" "$work_root/old" "$work_root/current"

for project in Contracts Client ServiceDefaults DomainService Server; do
    dotnet pack "$repo_root/src/Hexalith.EventStore.$project/Hexalith.EventStore.$project.csproj" \
        --configuration Release -m:1 -p:Version="$new_version" -p:NuGetAudit=false \
        -o "$package_root"
done

cat > "$work_root/NuGet.Config" <<EOF
<configuration><packageSources><clear />
<add key="local" value="$package_root" />
<add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
</packageSources></configuration>
EOF

cat > "$work_root/old/LegacyConsumer.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>
<OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
<Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings>
</PropertyGroup><ItemGroup>
<PackageReference Include="Hexalith.EventStore.Server" Version="$old_version" />
</ItemGroup></Project>
EOF
cp "$fixture_root/LegacyConsumer.cs" "$work_root/old/LegacyConsumer.cs"
dotnet restore "$work_root/old/LegacyConsumer.csproj" --configfile "$work_root/NuGet.Config" -p:NuGetAudit=false
dotnet build "$work_root/old/LegacyConsumer.csproj" --configuration Release --no-restore -m:1
dotnet "$work_root/old/bin/Release/net10.0/LegacyConsumer.dll" \
    --emit-old "$work_root/old-snapshot.json" > "$work_root/old-wire.json"

cat > "$work_root/current/CurrentConsumer.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>
<OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
<Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings>
</PropertyGroup><ItemGroup>
<PackageReference Include="Hexalith.EventStore.Server" Version="$new_version" />
</ItemGroup></Project>
EOF
cp "$fixture_root/CurrentConsumer.cs" "$work_root/current/CurrentConsumer.cs"
dotnet restore "$work_root/current/CurrentConsumer.csproj" --configfile "$work_root/NuGet.Config" -p:NuGetAudit=false
dotnet build "$work_root/current/CurrentConsumer.csproj" --configuration Release --no-restore -m:1
dotnet "$work_root/current/bin/Release/net10.0/CurrentConsumer.dll" \
    "$work_root/old/bin/Release/net10.0/LegacyConsumer.dll" "$work_root/old-wire.json" \
    "$work_root/old-snapshot.json" "$work_root/new-snapshot.json"
dotnet "$work_root/old/bin/Release/net10.0/LegacyConsumer.dll" \
    --read-new "$work_root/new-snapshot.json"
