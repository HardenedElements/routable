#!/usr/bin/env bash
#
# Builds NuGet packages for the Routable libraries and the basic template.
#
# Environment overrides:
#   VERSION         Full package version. When set, any suffix is ignored.
#   VERSION_SUFFIX  Prerelease suffix appended to the base version. Defaults to the
#                   commit short hash on branches other than "master".
#   OUTPUT_DIR      Directory for the produced .nupkg files. Defaults to "artifacts".
#
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"
cd "$repo_root"

base_version="$(grep -oP '<VersionPrefix>\K[^<]+' Directory.Build.props)"
branch="$(git rev-parse --abbrev-ref HEAD)"
output_dir="${OUTPUT_DIR:-artifacts}"

if [ -n "${VERSION:-}" ]; then
	version="$VERSION"
else
	suffix="${VERSION_SUFFIX:-}"
	if [ -z "$suffix" ] && [ "$branch" != "master" ]; then
		suffix="$(git rev-parse --short HEAD)"
	fi
	if [ -n "$suffix" ]; then
		version="$base_version-$suffix"
	else
		version="$base_version"
	fi
fi

mkdir -p "$output_dir"

echo "Branch:  $branch"
echo "Version: $version"
echo "Output:  $output_dir"

libraries=(
	src/Routable/Routable.csproj
	src/Routable.Kestrel/Routable.Kestrel.csproj
	src/Routable.Views.Simple/Routable.Views.Simple.csproj
)

for project in "${libraries[@]}"; do
	dotnet pack "$project" -c Release -p:Version="$version" --output "$output_dir"
done

# The template ships as a content-only package described by its nuspec. MSBuild packs
# it through the template project, so no separate nuget.exe is required under WSL.
template_dir="$repo_root/src/Routable.Templates"
dotnet pack "$template_dir/Routable.Templates.Basic/Routable.Templates.Basic.csproj" \
	-p:NuspecFile="$template_dir/Routable.Templates.Basic.nuspec" \
	-p:NuspecProperties="version=$version" \
	-p:NuspecBasePath="$template_dir" \
	-p:IsPackable=true \
	--output "$output_dir"
