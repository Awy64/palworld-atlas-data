#!/usr/bin/env bash
set -euo pipefail

steamcmd="${1:?steamcmd path required}"
"$steamcmd" +login anonymous +app_info_update 1 +app_info_print 2394010 +quit 2>&1 \
  | python3 -c 'import re,sys; text=sys.stdin.read(); match=re.search(r"\"public\"\s*\{.*?\"buildid\"\s*\"(\d+)\"", text, re.S); print(match.group(1) if match else "")'

