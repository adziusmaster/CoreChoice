#!/usr/bin/env bash
#
# promo.sh — create, list and revoke CoreChoice tester promo codes.
#
# Codes grant analyses ("coins", default 10). A single code can be redeemed by many
# testers, but each device may redeem a given code only once. Codes can be given an
# expiry and can be revoked at any time.
#
# Requirements:
#   - curl and python3 on PATH.
#   - ADMIN_SECRET must match the server's Admin__Secret (set in deploy/.env as ADMIN_SECRET).
#
# Usage:
#   export ADMIN_SECRET=your-secret
#   export CORECHOICE_API=https://api.corechoice.lechdigital.nl   # optional, this is the default
#
#   ./deploy/promo.sh gen                          # generate a code using the settings below
#   ./deploy/promo.sh create [--analyses N] [--days D] [--code X]
#   ./deploy/promo.sh list
#   ./deploy/promo.sh revoke CODE
#
# Examples:
#   ./deploy/promo.sh gen                          # quickest: uses the CONFIG block below
#   ./deploy/promo.sh create                       # random 5-char code, 10 analyses, never expires
#   ./deploy/promo.sh create --analyses 10 --days 30
#   ./deploy/promo.sh create --code TEST1 --days 14
#   ./deploy/promo.sh list
#   ./deploy/promo.sh revoke TEST1
set -euo pipefail

# Auto-load deploy/.env (git-ignored) so ADMIN_SECRET etc. are available without exporting.
_SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if [ -f "$_SCRIPT_DIR/.env" ]; then
  set -a
  # shellcheck disable=SC1091
  . "$_SCRIPT_DIR/.env"
  set +a
fi

# ============================================================================
#  >>> EDIT HERE to generate a code, then run:  ./deploy/promo.sh gen  <<<
# ============================================================================
# The admin secret. Either paste it here, or leave empty and `export ADMIN_SECRET=...`
# before running. It must match the server's Admin__Secret (deploy/.env ADMIN_SECRET).
ADMIN_SECRET="${ADMIN_SECRET:-}"

# How many analyses each redemption of the generated code grants.
GEN_ANALYSES=10

# Days until the generated code expires. Use 0 for "never expires".
GEN_DAYS=30

# Leave empty for a random, easy-to-read 5-char code (recommended),
# or set a custom 5-character code, e.g. GEN_CODE="TEST1".
GEN_CODE=""
# ============================================================================

API="${CORECHOICE_API:-https://api.corechoice.lechdigital.nl}"
API="${API%/}"

die() { echo "error: $*" >&2; exit 1; }

[ -n "${ADMIN_SECRET:-}" ] || die "ADMIN_SECRET is not set. export ADMIN_SECRET=... (must match the server's Admin__Secret)."
command -v curl >/dev/null || die "curl is required."
command -v python3 >/dev/null || die "python3 is required."

# pretty <json> — pretty-print a JSON payload, or echo raw text on failure.
pretty() { python3 -m json.tool 2>/dev/null || cat; }

cmd="${1:-}"; shift || true

case "$cmd" in
  gen)
    # Generate a code using the CONFIG block at the top of this file.
    body=$(ANALYSES="$GEN_ANALYSES" DAYS="$GEN_DAYS" CODE="$GEN_CODE" python3 - <<'PY'
import json, os
b = {}
if os.environ.get("ANALYSES"): b["coins"] = int(os.environ["ANALYSES"])
if os.environ.get("DAYS"):     b["expiresInDays"] = int(os.environ["DAYS"])
if os.environ.get("CODE"):     b["code"] = os.environ["CODE"]
print(json.dumps(b))
PY
)
    echo "Generating code (analyses=$GEN_ANALYSES, days=$GEN_DAYS, code=${GEN_CODE:-random})..."
    curl -fsS -X POST "$API/api/admin/promo" \
      -H "X-Admin-Secret: $ADMIN_SECRET" \
      -H "Content-Type: application/json" \
      -d "$body" | pretty
    ;;

  create)
    analyses=""
    days=""
    code=""
    while [ $# -gt 0 ]; do
      case "$1" in
        --analyses) analyses="${2:-}"; shift 2 ;;
        --days)     days="${2:-}"; shift 2 ;;
        --code)     code="${2:-}"; shift 2 ;;
        *) die "unknown option '$1' for create" ;;
      esac
    done
    body=$(ANALYSES="$analyses" DAYS="$days" CODE="$code" python3 - <<'PY'
import json, os
b = {}
if os.environ.get("ANALYSES"): b["coins"] = int(os.environ["ANALYSES"])
if os.environ.get("DAYS"):     b["expiresInDays"] = int(os.environ["DAYS"])
if os.environ.get("CODE"):     b["code"] = os.environ["CODE"]
print(json.dumps(b))
PY
)
    curl -fsS -X POST "$API/api/admin/promo" \
      -H "X-Admin-Secret: $ADMIN_SECRET" \
      -H "Content-Type: application/json" \
      -d "$body" | pretty
    ;;

  list)
    curl -fsS "$API/api/admin/promo" -H "X-Admin-Secret: $ADMIN_SECRET" | pretty
    ;;

  revoke)
    code="${1:-}"
    [ -n "$code" ] || die "usage: promo.sh revoke CODE"
    curl -fsS -X POST "$API/api/admin/promo/$code/revoke" \
      -H "X-Admin-Secret: $ADMIN_SECRET" | pretty
    ;;

  ""|-h|--help|help)
    cat <<'USAGE'
promo.sh — create, list and revoke CoreChoice tester promo codes.

Quickest way to generate a code:
  1. Open deploy/promo.sh and edit the CONFIG block at the top
     (ADMIN_SECRET, GEN_ANALYSES, GEN_DAYS, optional GEN_CODE).
  2. Run:  ./deploy/promo.sh gen

Other commands:
  ./deploy/promo.sh gen                          generate a code from the CONFIG block
  ./deploy/promo.sh create [--analyses N] [--days D] [--code X]
  ./deploy/promo.sh list                         list all codes + redemption counts
  ./deploy/promo.sh revoke CODE                  disable a code

ADMIN_SECRET must match the server's Admin__Secret (deploy/.env ADMIN_SECRET).
Override the backend with CORECHOICE_API (defaults to https://api.corechoice.lechdigital.nl).
USAGE
    ;;

  *)
    die "unknown command '$cmd' (try: gen, create, list, revoke, help)"
    ;;
esac
