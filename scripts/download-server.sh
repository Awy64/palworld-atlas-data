#!/usr/bin/env bash
set -euo pipefail

steamcmd="${1:?steamcmd path required}"
install_dir="${2:?server install directory required}"
mkdir -p "$install_dir"
"$steamcmd" \
  +force_install_dir "$install_dir" \
  +login anonymous \
  +app_update 2394010 validate \
  +quit

test -f "$install_dir/Pal/Content/Paks/Pal-LinuxServer.pak"

