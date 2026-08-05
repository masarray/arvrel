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
  echo "Published Linux host is missing: $executable" >&2
  exit 1
fi
chmod +x "$executable"

stage_root="$(mktemp -d)"
cleanup() {
  rm -rf "$stage_root"
}
trap cleanup EXIT

legal_files=(
  LICENSE
  README.md
  SECURITY.md
  SUPPORT.md
  THIRD-PARTY-NOTICES.md
)

portable_root="$stage_root/ARVREL"
mkdir -p "$portable_root"
cp -a "$publish_path"/. "$portable_root"/
for legal_file in "${legal_files[@]}"; do
  [[ -f "$repo_root/$legal_file" ]] || {
    echo "Required package document is missing: $repo_root/$legal_file" >&2
    exit 1
  }
  cp "$repo_root/$legal_file" "$portable_root/$legal_file"
done
chmod +x "$portable_root/Arvrel.Desktop"

portable_name="ARVREL-Avalonia-v${version}-linux-x64.tar.gz"
portable_path="$output_path/$portable_name"
tar \
  --sort=name \
  --mtime='UTC 2020-01-01' \
  --owner=0 \
  --group=0 \
  --numeric-owner \
  -C "$stage_root" \
  -czf "$portable_path" \
  ARVREL

deb_root="$stage_root/deb"
install_root="$deb_root/opt/arvrel"
mkdir -p \
  "$deb_root/DEBIAN" \
  "$install_root" \
  "$deb_root/usr/bin" \
  "$deb_root/usr/share/applications" \
  "$deb_root/usr/share/icons/hicolor/512x512/apps" \
  "$deb_root/usr/share/doc/arvrel"

cp -a "$publish_path"/. "$install_root"/
chmod +x "$install_root/Arvrel.Desktop"
for legal_file in "${legal_files[@]}"; do
  cp "$repo_root/$legal_file" "$deb_root/usr/share/doc/arvrel/$legal_file"
done
cp "$repo_root/Asset/icon/web-app-manifest-512x512.png" \
  "$deb_root/usr/share/icons/hicolor/512x512/apps/arvrel.png"
cp "$repo_root/packaging/avalonia/linux/arvrel.desktop" \
  "$deb_root/usr/share/applications/arvrel.desktop"
ln -s /opt/arvrel/Arvrel.Desktop "$deb_root/usr/bin/arvrel"

installed_size="$(du -sk "$install_root" | awk '{print $1}')"
cat > "$deb_root/DEBIAN/control" <<CONTROL
Package: arvrel
Version: $version
Section: science
Priority: optional
Architecture: amd64
Installed-Size: $installed_size
Maintainer: Ari Sulistiono <ari.sulistiono@gmail.com>
Homepage: https://masarray.github.io/arvrel/
Depends: libx11-6, libxrandr2, libxi6, libxcursor1, libxinerama1, libfontconfig1, libfreetype6, libgl1, libice6, libsm6
Description: ARVREL virtual protection relay laboratory
 Cross-platform Avalonia shell for deterministic relay and virtual-injection
 experiments. Active physical trip outputs are not provided.
CONTROL

find "$deb_root" -type d -exec chmod 0755 {} +
find "$deb_root" -type f -exec chmod 0644 {} +
chmod 0755 "$install_root/Arvrel.Desktop"
chmod 0755 "$deb_root/usr/share/applications/arvrel.desktop"

deb_name="ARVREL-Avalonia-v${version}-linux-x64.deb"
deb_path="$output_path/$deb_name"
SOURCE_DATE_EPOCH=1577836800 dpkg-deb --root-owner-group --build "$deb_root" "$deb_path"

tar_contents="$stage_root/tar-contents.txt"
deb_contents="$stage_root/deb-contents.txt"
file_description="$stage_root/file-description.txt"
elf_header="$stage_root/elf-header.txt"

tar -tzf "$portable_path" > "$tar_contents"
grep -Fx 'ARVREL/Arvrel.Desktop' "$tar_contents" >/dev/null

dpkg-deb --info "$deb_path" >/dev/null
dpkg-deb --contents "$deb_path" > "$deb_contents"
grep -F './opt/arvrel/Arvrel.Desktop' "$deb_contents" >/dev/null

file "$portable_root/Arvrel.Desktop" > "$file_description"
grep -F 'ELF 64-bit' "$file_description" >/dev/null
readelf -h "$portable_root/Arvrel.Desktop" > "$elf_header"
grep -E 'Machine:.*Advanced Micro Devices X86-64' "$elf_header" >/dev/null

manifest_name="ARVREL-Avalonia-v${version}-linux-x64-manifest.json"
manifest_path="$output_path/$manifest_name"
cat > "$manifest_path" <<JSON
{
  "schemaVersion": 1,
  "product": "ARVREL Avalonia",
  "version": "$version",
  "runtimeIdentifier": "linux-x64",
  "commit": "$commit_sha",
  "selfContained": true,
  "packages": [
    "$portable_name",
    "$deb_name"
  ],
  "installScope": {
    "archive": "portable directory",
    "deb": "/opt/arvrel with /usr/bin/arvrel launcher"
  },
  "executable": "Arvrel.Desktop",
  "signing": {
    "packageSignature": "unsigned",
    "repositorySignature": "not configured"
  },
  "capabilities": {
    "internalLaboratory": true,
    "pcapReplay": true,
    "liveCapture": "not included; no libpcap backend is implemented"
  },
  "runtimeDependencies": [
    "X11",
    "fontconfig",
    "freetype",
    "OpenGL"
  ]
}
JSON

checksum_path="$output_path/SHA256SUMS-linux-x64.txt"
(
  cd "$output_path"
  sha256sum "$portable_name" "$deb_name" "$manifest_name" > "$(basename "$checksum_path")"
)

for required in "$portable_path" "$deb_path" "$manifest_path" "$checksum_path"; do
  [[ -s "$required" ]] || {
    echo "Linux package output is missing or empty: $required" >&2
    exit 1
  }
done

echo "Created Linux Avalonia package set in $output_path"
