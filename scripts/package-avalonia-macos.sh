#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 3 || $# -gt 4 ]]; then
  echo "Usage: $0 <version> <publish-directory> <output-directory> [commit-sha]" >&2
  exit 64
fi

version="$1"
publish_directory="$2"
output_directory="$3"
commit_sha="${4:-unknown}"

if [[ ! "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+([+-][0-9A-Za-z.-]+)?$ ]]; then
  echo "Invalid version: $version" >&2
  exit 64
fi
if [[ ! "$commit_sha" =~ ^([0-9a-fA-F]{7,64}|unknown)$ ]]; then
  echo "Invalid commit SHA: $commit_sha" >&2
  exit 64
fi

script_directory="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_directory/.." && pwd)"
publish_path="$(cd "$publish_directory" && pwd)"
mkdir -p "$output_directory"
output_path="$(cd "$output_directory" && pwd)"

executable="$publish_path/Arvrel.Desktop"
if [[ ! -f "$executable" ]]; then
  echo "Published macOS host is missing: $executable" >&2
  exit 1
fi

short_version="$(sed -E 's/^([0-9]+\.[0-9]+\.[0-9]+).*/\1/' <<<"$version")"
build_version="$(tr -cd '0-9.' <<<"$short_version")"
if [[ -z "$build_version" ]]; then
  build_version='1.0.0'
fi

stage_root="$(mktemp -d)"
cleanup() {
  hdiutil detach "$stage_root/mount" -quiet 2>/dev/null || true
  rm -rf "$stage_root"
}
trap cleanup EXIT

app_name='ARVREL.app'
app_root="$stage_root/$app_name"
contents_root="$app_root/Contents"
macos_root="$contents_root/MacOS"
resources_root="$contents_root/Resources"
mkdir -p "$macos_root" "$resources_root"

cp -a "$publish_path"/. "$macos_root"/
chmod +x "$macos_root/Arvrel.Desktop"

for legal_file in \
  LICENSE \
  README.md \
  SECURITY.md \
  SUPPORT.md \
  THIRD-PARTY-NOTICES.md; do
  [[ -f "$repo_root/$legal_file" ]] || {
    echo "Required package document is missing: $repo_root/$legal_file" >&2
    exit 1
  }
  cp "$repo_root/$legal_file" "$resources_root/$legal_file"
done

icon_source="$repo_root/Asset/icon/web-app-manifest-512x512.png"
iconset="$stage_root/ARVREL.iconset"
mkdir -p "$iconset"
for size in 16 32 128 256 512; do
  sips -z "$size" "$size" "$icon_source" \
    --out "$iconset/icon_${size}x${size}.png" >/dev/null
  double_size=$((size * 2))
  sips -z "$double_size" "$double_size" "$icon_source" \
    --out "$iconset/icon_${size}x${size}@2x.png" >/dev/null
done
iconutil -c icns "$iconset" -o "$resources_root/ARVREL.icns"

plist_template="$repo_root/packaging/avalonia/macos/Info.plist"
plist_path="$contents_root/Info.plist"
sed \
  -e "s/__FULL_VERSION__/$version/g" \
  -e "s/__SHORT_VERSION__/$short_version/g" \
  -e "s/__BUILD_VERSION__/$build_version/g" \
  "$plist_template" > "$plist_path"

plutil -lint "$plist_path"
codesign --force --deep --sign - "$app_root"
codesign --verify --deep --strict "$app_root"

zip_name="ARVREL-Avalonia-v${version}-osx-arm64.app.zip"
zip_path="$output_path/$zip_name"
ditto -c -k --sequesterRsrc --keepParent "$app_root" "$zip_path"
unzip -t "$zip_path" >/dev/null

dmg_source="$stage_root/dmg"
mkdir -p "$dmg_source"
cp -a "$app_root" "$dmg_source/$app_name"
ln -s /Applications "$dmg_source/Applications"

dmg_name="ARVREL-Avalonia-v${version}-osx-arm64.dmg"
dmg_path="$output_path/$dmg_name"
hdiutil create \
  -volname "ARVREL $short_version" \
  -srcfolder "$dmg_source" \
  -ov \
  -format UDZO \
  "$dmg_path" >/dev/null
hdiutil verify "$dmg_path" >/dev/null

file "$macos_root/Arvrel.Desktop" | grep -q 'Mach-O 64-bit executable arm64'
plutil -extract CFBundleIdentifier raw "$plist_path" | grep -qx 'io.github.masarray.arvrel'
plutil -extract CFBundleExecutable raw "$plist_path" | grep -qx 'Arvrel.Desktop'

manifest_name="ARVREL-Avalonia-v${version}-osx-arm64-manifest.json"
manifest_path="$output_path/$manifest_name"
cat > "$manifest_path" <<JSON
{
  "schemaVersion": 1,
  "product": "ARVREL Avalonia",
  "version": "$version",
  "runtimeIdentifier": "osx-arm64",
  "commit": "$commit_sha",
  "selfContained": true,
  "packages": [
    "$zip_name",
    "$dmg_name"
  ],
  "bundleIdentifier": "io.github.masarray.arvrel",
  "minimumSystemVersion": "12.0",
  "executable": "ARVREL.app/Contents/MacOS/Arvrel.Desktop",
  "signing": {
    "codeSignature": "ad-hoc",
    "developerIdConfigured": false,
    "notarized": false
  },
  "capabilities": {
    "internalLaboratory": true,
    "pcapReplay": true,
    "liveCapture": "not included; no BPF/libpcap backend is implemented"
  }
}
JSON

checksum_path="$output_path/SHA256SUMS-osx-arm64.txt"
(
  cd "$output_path"
  shasum -a 256 "$zip_name" "$dmg_name" "$manifest_name" > "$(basename "$checksum_path")"
)

for required in "$zip_path" "$dmg_path" "$manifest_path" "$checksum_path"; do
  [[ -s "$required" ]] || {
    echo "macOS package output is missing or empty: $required" >&2
    exit 1
  }
done

echo "Created macOS Avalonia package set in $output_path"
