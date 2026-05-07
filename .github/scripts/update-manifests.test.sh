#!/usr/bin/env bash
set -euo pipefail

# Test harness for update-manifests.py.
# Creates fixture manifests in a temp dir, runs the script with mock checksums,
# asserts the resulting files have the expected shape.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPT="$SCRIPT_DIR/update-manifests.py"

TEST_TMPDIR="$(mktemp -d)"
trap 'rm -rf "$TEST_TMPDIR"' EXIT

cd "$TEST_TMPDIR"

# Fixture: empty-versions manifest.json
cat > manifest.json <<'EOF'
[
  {
    "guid": "b4a6d7e2-8f3c-4a1e-9d5b-2c7f0e8a1b3d",
    "name": "Tracearr SSE",
    "description": "Server-Sent Events endpoint for real-time playback and session notifications.",
    "overview": "Streams playback start, progress, pause, stop, and session connect/disconnect events as Server-Sent Events. Built for the Tracearr scrobbler. Works with any SSE client.",
    "owner": "Connor Gallopo - Tracearr",
    "category": "General",
    "imageUrl": "https://raw.githubusercontent.com/Tracearr/Media-Server-SSE/main/assets/icon.png",
    "versions": []
  }
]
EOF

# Fixture: empty-versions emby-packages.xml
cat > emby-packages.xml <<'EOF'
<?xml version="1.0" encoding="utf-8"?>
<Packages>
  <PackageInfo>
    <name>Tracearr SSE</name>
    <guid>a3d8f1e6-2b7c-4e9a-8f5d-1c6b0a3e7f92</guid>
    <owner>Connor Gallopo - Tracearr</owner>
    <description>Server-Sent Events endpoint for real-time playback and session notifications.</description>
    <overview>Streams playback start, progress, pause, stop, and session connect/disconnect events as Server-Sent Events. Built for the Tracearr scrobbler. Works with any SSE client.</overview>
    <category>General</category>
    <thumbImage>https://raw.githubusercontent.com/Tracearr/Media-Server-SSE/main/assets/icon.png</thumbImage>
    <targetSystem>Server</targetSystem>
    <versions></versions>
  </PackageInfo>
</Packages>
EOF

# Helpers: read XML values via Python stdlib (no xmlstarlet dependency).
emby_count() {
  python3 -c "import xml.etree.ElementTree as ET; t=ET.parse('emby-packages.xml'); print(len(t.findall('.//PackageInfo/versions/version')))"
}
emby_first_field() {
  python3 -c "import xml.etree.ElementTree as ET; t=ET.parse('emby-packages.xml'); print(t.find('.//PackageInfo/versions/version[1]/$1').text)"
}

# First run: add 0.1.0
"$SCRIPT" 0.1.0 abc123 def456 2026-05-07T12:00:00Z

# Assert manifest.json has one version, version=0.1.0.0, checksum=abc123
JF_COUNT=$(jq '.[0].versions | length' manifest.json)
[ "$JF_COUNT" = "1" ] || { echo "FAIL: jellyfin versions count = $JF_COUNT, expected 1"; exit 1; }

JF_VERSION=$(jq -r '.[0].versions[0].version' manifest.json)
[ "$JF_VERSION" = "0.1.0.0" ] || { echo "FAIL: jellyfin version = $JF_VERSION, expected 0.1.0.0"; exit 1; }

JF_CS=$(jq -r '.[0].versions[0].checksum' manifest.json)
[ "$JF_CS" = "abc123" ] || { echo "FAIL: jellyfin checksum = $JF_CS, expected abc123"; exit 1; }

JF_SRC=$(jq -r '.[0].versions[0].sourceUrl' manifest.json)
[ "$JF_SRC" = "https://github.com/Tracearr/Media-Server-SSE/releases/download/v0.1.0/Tracearr.Sse.Jellyfin_0.1.0.zip" ] || { echo "FAIL: jellyfin sourceUrl wrong: $JF_SRC"; exit 1; }

# Assert emby-packages.xml has one version, versionStr=0.1.0.0, checksum=def456
EMBY_COUNT=$(emby_count)
[ "$EMBY_COUNT" = "1" ] || { echo "FAIL: emby versions count = $EMBY_COUNT, expected 1"; exit 1; }

EMBY_VERSION=$(emby_first_field versionStr)
[ "$EMBY_VERSION" = "0.1.0.0" ] || { echo "FAIL: emby version = $EMBY_VERSION, expected 0.1.0.0"; exit 1; }

EMBY_CS=$(emby_first_field checksum)
[ "$EMBY_CS" = "def456" ] || { echo "FAIL: emby checksum = $EMBY_CS, expected def456"; exit 1; }

# Second run: add 0.2.0 — must prepend (newest first)
"$SCRIPT" 0.2.0 ghi789 jkl012 2026-06-15T08:00:00Z

JF_COUNT2=$(jq '.[0].versions | length' manifest.json)
[ "$JF_COUNT2" = "2" ] || { echo "FAIL: jellyfin versions count after 2nd run = $JF_COUNT2, expected 2"; exit 1; }

JF_FIRST=$(jq -r '.[0].versions[0].version' manifest.json)
[ "$JF_FIRST" = "0.2.0.0" ] || { echo "FAIL: jellyfin first version = $JF_FIRST, expected 0.2.0.0 (newest first)"; exit 1; }

EMBY_FIRST=$(emby_first_field versionStr)
[ "$EMBY_FIRST" = "0.2.0.0" ] || { echo "FAIL: emby first version = $EMBY_FIRST, expected 0.2.0.0 (newest first)"; exit 1; }

EMBY_COUNT2=$(emby_count)
[ "$EMBY_COUNT2" = "2" ] || { echo "FAIL: emby versions count after 2nd run = $EMBY_COUNT2, expected 2"; exit 1; }

# Third run: omit timestamp, verify default is current UTC ISO-8601
"$SCRIPT" 0.3.0 mno345 pqr678

JF_TS=$(jq -r '.[0].versions[0].timestamp' manifest.json)
echo "$JF_TS" | grep -qE '^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z$' || { echo "FAIL: jellyfin default timestamp wrong format: $JF_TS"; exit 1; }

EMBY_TS=$(emby_first_field timestamp)
echo "$EMBY_TS" | grep -qE '^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z$' || { echo "FAIL: emby default timestamp wrong format: $EMBY_TS"; exit 1; }

JF_COUNT3=$(jq '.[0].versions | length' manifest.json)
[ "$JF_COUNT3" = "3" ] || { echo "FAIL: jellyfin versions count after 3rd run = $JF_COUNT3, expected 3"; exit 1; }

# Validate JSON and XML are still well-formed
jq -e . manifest.json > /dev/null || { echo "FAIL: manifest.json invalid JSON"; exit 1; }
python3 -c "import xml.etree.ElementTree as ET; ET.parse('emby-packages.xml')" || { echo "FAIL: emby-packages.xml invalid XML"; exit 1; }

echo "PASS"
