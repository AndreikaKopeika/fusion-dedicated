#!/usr/bin/env bash
#
# Removes Fusion Dedicated: stops the services, deletes the systemd user units
# and — after asking — the installed files.
#
# The Steam account and its cached credentials are deliberately untouched. If you
# want those gone too, sign out through the Steam client itself.
#
set -euo pipefail

INSTALL_DIR="${FUSION_INSTALL_DIR:-$HOME/fusiondedicated}"
UNIT_DIR="$HOME/.config/systemd/user"
UNITS=(fusion-server fusion-steam fusion-xvfb)

say()  { printf '\033[36m==>\033[0m %s\n' "$1"; }
ok()   { printf '\033[32m  ✓\033[0m %s\n' "$1"; }
warn() { printf '\033[33m  !\033[0m %s\n' "$1"; }

command -v systemctl >/dev/null || { echo "systemctl not found — nothing to uninstall here."; exit 0; }

say "Stopping services"
for unit in "${UNITS[@]}"; do
    systemctl --user disable --now "$unit" >/dev/null 2>&1 \
        && ok "$unit stopped" || warn "$unit was not installed"
done

say "Removing unit files"
for unit in "${UNITS[@]}"; do
    rm -f "$UNIT_DIR/$unit.service"
done
systemctl --user daemon-reload
systemctl --user reset-failed 2>/dev/null || true
ok "units removed"

if [ -d "$INSTALL_DIR" ]; then
    echo
    warn "The installed files in $INSTALL_DIR include your server.json"
    echo "      (ban list, ranks and the learned mod catalogue live there)."
    read -rp "      Delete $INSTALL_DIR as well? [y/N] " reply
    if [[ "$reply" =~ ^[Yy]$ ]]; then
        rm -rf "$INSTALL_DIR"
        ok "$INSTALL_DIR removed"
    else
        echo "      Kept. Remove it later with:  rm -rf $INSTALL_DIR"
    fi
else
    ok "$INSTALL_DIR is already gone"
fi

LINGER="$(loginctl show-user "$USER" -p Linger --value 2>/dev/null || echo no)"
if [ "$LINGER" = "yes" ]; then
    echo
    echo "  Lingering is still on, which is harmless. To switch it off:"
    echo "      sudo loginctl disable-linger $USER"
fi

echo
echo "Done. Steam itself was not touched."
