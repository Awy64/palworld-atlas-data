#!/usr/bin/env bash
set -euo pipefail

install_dir="${1:?steamcmd install directory required}"
mkdir -p "$install_dir"
curl --fail --location --silent --show-error \
  https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz \
  | tar -xz -C "$install_dir"
"$install_dir/steamcmd.sh" +quit >/dev/null

