#!/usr/bin/env bash
#
# fusion-ctl — one command for the day-to-day management of a Fusion Dedicated
# server. Wraps the three systemd user units so you never have to remember them.
#
#   fusion-ctl start|stop|restart|status   manage the whole stack (Xvfb, Steam, relay)
#   fusion-ctl enable|disable              start at boot
#   fusion-ctl logs                        follow the server log
#   fusion-ctl panel                       how to reach the control panel
#   fusion-ctl login                       one-time Steam sign-in (steam-login.sh)
#   fusion-ctl update                      pull the repo and reinstall
#   fusion-ctl uninstall                   remove everything (asks first)
#
set -euo pipefail

INSTALL_DIR="${FUSION_INSTALL_DIR:-$HOME/fusiondedicated}"
REPO_DIR="${FUSION_REPO_DIR:-$HOME/fusion-dedicated}"
UNITS=(fusion-server fusion-steam fusion-xvfb)

say()  { printf '\033[36m==>\033[0m %s\n' "$1"; }
die()  { printf '\033[31m  ✗\033[0m %s\n' "$1" >&2; exit 1; }

have_unit() { systemctl --user cat "${1}.service" >/dev/null 2>&1; }

require_installed() {
    have_unit fusion-server || die "not installed — run install.sh from the repository first"
}

# Acts on all three units in dependency order (relay, Steam, display).
order() {
    local action="$1"
    case "$action" in
        start|restart) for u in fusion-xvfb fusion-steam fusion-server; do systemctl --user "$action" "$u"; done ;;
        *)             for u in fusion-server fusion-steam fusion-xvfb; do systemctl --user "$action" "$u"; done ;;
    esac
}

panel_url() {
    local port="8778" host="localhost"

    if [ -r "$INSTALL_DIR/server.json" ]; then
        port=$(grep -oE '"DashboardPort"[[:space:]]*:[[:space:]]*[0-9]+' \
            "$INSTALL_DIR/server.json" | grep -oE '[0-9]+' | head -1) || true
        host=$(grep -oE '"DashboardHost"[[:space:]]*:[[:space:]]*"[^"]+"' \
            "$INSTALL_DIR/server.json" | sed -E 's/.*"([^"]+)"$/\1/' | head -1) || true
    fi

    if [ "$host" = "+" ] || [ "$host" = "*" ]; then
        echo "http://$(hostname -I 2>/dev/null | awk '{print $1}'):${port}/  (listening on every interface)"
    else
        echo "http://localhost:${port}/  — from another machine, tunnel first:"
        echo "    ssh -L ${port}:localhost:${port} $USER@$(hostname -I 2>/dev/null | awk '{print $1}')"
    fi
}

case "${1:-help}" in
    start|stop|restart)
        require_installed
        say "$1ing Fusion Dedicated"
        order "$1"
        ;;
    status)
        require_installed
        for u in "${UNITS[@]}"; do
            systemctl --user status "$u" --no-pager -l || true
            echo
        done
        echo "Panel: $(panel_url)"
        ;;
    enable|disable)
        require_installed
        say "$1ing autostart at boot"
        for u in "${UNITS[@]}"; do systemctl --user "$1" "$u"; done
        if [ "$1" = "enable" ] && [ "$(loginctl show-user "$USER" -p Linger --value)" != "yes" ]; then
            echo "  ! Also run:  sudo loginctl enable-linger $USER"
            echo "    (without lingering, user services only run while you are logged in)"
        fi
        ;;
    logs)
        require_installed
        exec journalctl --user -u fusion-server -f -n 50
        ;;
    panel)
        require_installed
        panel_url
        ;;
    login)
        [ -x "$INSTALL_DIR/steam-login.sh" ] || die "steam-login.sh not found in $INSTALL_DIR"
        exec "$INSTALL_DIR/steam-login.sh" "${2:-}"
        ;;
    update)
        [ -d "$REPO_DIR/.git" ] || die "repository not found at $REPO_DIR — set FUSION_REPO_DIR or clone it there"
        say "Pulling the latest changes"
        git -C "$REPO_DIR" pull --ff-only
        say "Reinstalling"
        "$REPO_DIR/install.sh"
        echo
        echo "  Restart to pick up the new build:  fusion-ctl restart"
        ;;
    uninstall)
        if [ -x "$REPO_DIR/uninstall.sh" ]; then
            exec "$REPO_DIR/uninstall.sh"
        elif [ -f "$INSTALL_DIR/uninstall.sh" ]; then
            exec "$INSTALL_DIR/uninstall.sh"
        else
            die "uninstall.sh not found — see the README's Uninstall section"
        fi
        ;;
    help|--help|-h|*)
        sed -n '2,13p' "$0" | sed 's/^# \{0,1\}//'
        ;;
esac
