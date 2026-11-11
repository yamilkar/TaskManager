#!/usr/bin/env bash
# Post-deploy smoke test: checks that the site, both APIs, DynamoDB access, auth, rate-limit client IP and CORS work.
#
# Usage: smoke-test.sh <site-url> <users-api-url> <tasks-api-url>
# Prints one "ok:" or "FAIL:" line per check (status codes only, never auth response bodies) and exits 1 on any failure.
set -uo pipefail

site="${1:?usage: smoke-test.sh <site-url> <users-api-url> <tasks-api-url>}"
users="${2:?usage: smoke-test.sh <site-url> <users-api-url> <tasks-api-url>}"
tasks="${3:?usage: smoke-test.sh <site-url> <users-api-url> <tasks-api-url>}"
site="${site%/}" users="${users%/}" tasks="${tasks%/}"

failures=0
pass() { echo "ok:   $1"; }
fail() { echo "FAIL: $1"; failures=$((failures + 1)); }

# status <curl args...>: prints the HTTP status code (000 when unreachable).
status() { curl -s -o /dev/null -w '%{http_code}' --max-time 30 "$@" || true; }

expect_status() { # expect_status <name> <expected> <curl args...>
  local name="$1" expected="$2" actual
  shift 2
  actual="$(status "$@")"
  if [[ "$actual" == "$expected" ]]; then pass "$name ($actual)"; else fail "$name: expected $expected, got $actual"; fi
}

expect_app_shell() { # expect_app_shell <name> <url>
  local body
  body="$(curl -s --max-time 30 -w '\n%{http_code}' "$2" || true)"
  if [[ "${body##*$'\n'}" == 200 && "$body" == *"<app-root"* ]]; then pass "$1"; else fail "$1: no Angular app shell (status ${body##*$'\n'})"; fi
}

# 1-2. The client, including a deep link that only exists in the Angular router.
expect_app_shell "site serves the app" "$site/"
expect_app_shell "deep link /tasks serves the app" "$site/tasks"

# 3. config.json points the client at these APIs.
config="$(curl -s --max-time 30 "$site/config.json" || true)"
if [[ "$(jq -r '.usersApiUrl // empty' <<<"$config" 2>/dev/null)" == "$users" &&
      "$(jq -r '.tasksApiUrl // empty' <<<"$config" 2>/dev/null)" == "$tasks" ]]; then
  pass "config.json points at both APIs"
else
  fail "config.json does not point at $users and $tasks"
fi

# 4. Each API is up and can reach its DynamoDB table.
expect_status "users API ready" 200 "$users/health/ready"
expect_status "tasks API ready" 200 "$tasks/health/ready"

# 5. Login reaches the handler: 401 for an unknown user. A 503 would mean the client IP never reached the rate limiter.
expect_status "login rejects an unknown user" 401 -X POST "$users/api/v1/auth/login" \
  -H 'Content-Type: application/json' \
  -d "{\"email\":\"smoke-$RANDOM$RANDOM@example.invalid\",\"password\":\"not-a-real-password\"}"

# 6. The Tasks API requires a token.
expect_status "tasks API requires a token" 401 "$tasks/api/v1/tasks"

# 7. Reviewers can browse both APIs (Scalar UI over the OpenAPI document).
for api in "$users" "$tasks"; do
  expect_status "API docs page $api/scalar/v1" 200 "$api/scalar/v1"
  expect_status "OpenAPI document $api/openapi/v1.json" 200 "$api/openapi/v1.json"
done

# 8. The browser on the site may call both APIs (CORS preflight).
for api in "$users/api/v1/auth/login" "$tasks/api/v1/tasks"; do
  allowed="$(curl -s -o /dev/null -D - --max-time 30 -X OPTIONS "$api" \
    -H "Origin: $site" \
    -H 'Access-Control-Request-Method: POST' \
    -H 'Access-Control-Request-Headers: authorization,content-type' |
    tr -d '\r' | awk -F': ' 'tolower($1) == "access-control-allow-origin" { print $2 }' || true)"
  if [[ "$allowed" == "$site" ]]; then pass "CORS allows the site on $api"; else fail "CORS on $api: allowed origin '${allowed}'"; fi
done

if ((failures > 0)); then
  echo "$failures check(s) failed."
  exit 1
fi
echo "All checks passed."
