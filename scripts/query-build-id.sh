#!/usr/bin/env bash
set -euo pipefail

steamcmd="${1:?steamcmd path required}"
for attempt in 1 2 3; do
  output="$({ "$steamcmd" +login anonymous +app_info_update 1 +app_info_print 2394010 +quit || true; } 2>&1)"
  build_id="$(python3 -c 'import re,sys; text=sys.stdin.read(); match=re.search(r"\"public\"\s*\{.*?\"buildid\"\s*\"(\d+)\"", text, re.S); print(match.group(1) if match else "")' <<<"$output")"
  if [ -n "$build_id" ]; then
    printf '%s\n' "$build_id"
    exit 0
  fi
  if [ "$attempt" -lt 3 ]; then sleep 2; fi
done

echo "SteamCMD did not return Palworld's public build ID after three attempts." >&2
exit 1
