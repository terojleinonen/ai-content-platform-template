#!/usr/bin/env bash
# End-to-end check of a running instance (e.g. `docker compose up`): health, sign-up, sign-in,
# a project, a generation and its usage record. Usage: scripts/smoke-test.sh [base-url]
set -euo pipefail

BASE="${1:-http://localhost:8080}"
JAR="$(mktemp)"
trap 'rm -f "$JAR"' EXIT
EMAIL="smoke-$(date +%s)-$RANDOM@example.test"
PASSWORD="Smoke-test-pw-$RANDOM-1"

step() { printf '\n== %s\n' "$*"; }
fail() { echo "FAIL: $*" >&2; exit 1; }
api() { curl -sS -b "$JAR" -c "$JAR" -H 'Content-Type: application/json' "$@"; }

step "Waiting for $BASE/api/health"
for _ in $(seq 60); do
  curl -sf "$BASE/api/health" >/dev/null && break
  sleep 2
done
curl -sf "$BASE/api/health" | tee /dev/stderr | jq -e '.status == "ok"' >/dev/null || fail "API not healthy"

step "Frontend is served"
curl -sf "$BASE/" | grep -q '<div id="root">' || fail "index.html not served"

step "API requires sign-in"
[ "$(curl -s -o /dev/null -w '%{http_code}' "$BASE/api/projects")" = 401 ] || fail "expected 401 without sign-in"

step "Register and sign in"
api -f -X POST "$BASE/api/auth/register" -d "{\"email\":\"$EMAIL\",\"password\":\"$PASSWORD\"}" >/dev/null || fail "register"
api -f -X POST "$BASE/api/auth/login?useCookies=true" -d "{\"email\":\"$EMAIL\",\"password\":\"$PASSWORD\"}" >/dev/null || fail "login"
api -f "$BASE/api/auth/me" | jq -e --arg e "$EMAIL" '.email == $e' >/dev/null || fail "me"

step "Create a project with a brand voice"
PROJECT=$(api -f -X POST "$BASE/api/projects" -d '{"name":"Smoke test"}' | jq -r .id)
api -f -X PUT "$BASE/api/projects/$PROJECT/brand-voice" -d '{"voice":"Friendly","preferredTerms":["smoke"],"avoidTerms":["cheap"]}' \
  | jq -e '.brandVoice.preferredTerms == ["smoke"]' >/dev/null || fail "brand voice"

step "Generate content (and save it)"
RESULT=$(api -f -X POST "$BASE/api/content/generate" -d "{\"prompt\":\"smoke testing\",\"type\":\"BlogPost\",\"keywords\":[\"smoke\"],\"projectId\":\"$PROJECT\"}")
echo "$RESULT" | jq '{title, wordCount, provider, usage, brandCheck}'
echo "$RESULT" | jq -e '.wordCount > 0 and .brandCheck != null' >/dev/null || fail "generate"
api -f -X POST "$BASE/api/projects/$PROJECT/content" \
  -d "$(echo "$RESULT" | jq '{type: "BlogPost", title, body, keywords: ["smoke"]}')" | jq -e '.id' >/dev/null || fail "save content"

step "Usage was recorded"
api -f "$BASE/api/usage?days=1" | jq -e '.totals.calls >= 1 and (.byProject | any(.label == "Smoke test"))' >/dev/null || fail "usage"

step "Sign out"
[ "$(api -s -o /dev/null -w '%{http_code}' -X POST "$BASE/api/auth/logout")" = 204 ] || fail "logout"
[ "$(api -s -o /dev/null -w '%{http_code}' "$BASE/api/auth/me")" = 401 ] || fail "still signed in after logout"

echo -e "\nSMOKE TEST PASSED"
