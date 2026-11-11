#!/usr/bin/env bash
# Tests for changed-artifacts.sh, run in CI (repo-hygiene job). Each case commits files to a throwaway git
# repository and checks the script's output for that commit range.
# Usage: .github/scripts/changed-artifacts.test.sh
set -euo pipefail

script="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/changed-artifacts.sh"
repo="$(mktemp -d)"
trap 'rm -rf "$repo"' EXIT
cd "$repo"
git init -q
git config user.email test@example.invalid
git config user.name test
git config core.autocrlf false
git commit -q --allow-empty -m root

failures=0

# commit <path>...: commits a change to each path and prints the new commit's SHA.
commit() {
  for path in "$@"; do
    mkdir -p "$(dirname "$path")"
    echo "$RANDOM" >>"$path"
  done
  git add -A
  git commit -q -m "change $*"
  git rev-parse HEAD
}

# expect <case> <base> <head> <expected output on one line>
expect() {
  local actual
  actual="$(bash "$script" "$2" "$3" | tr '\n' ' ' | sed 's/ $//')"
  if [[ "$actual" == "$4" ]]; then
    echo "ok:   $1"
  else
    echo "FAIL: $1"
    echo "      expected: $4"
    echo "      actual:   $actual"
    failures=$((failures + 1))
  fi
}

all='users-api=true tasks-api=true web=true infra=true deploy=true'
none='users-api=false tasks-api=false web=false infra=false deploy=false'

head="$(commit README.md)"
expect "empty base: everything changed" "" "$head" "$all"
expect "all-zero base (first push): everything changed" "0000000000000000000000000000000000000000" "$head" "$all"
expect "unknown base: everything changed" "1234567890abcdef1234567890abcdef12345678" "$head" "$all"

base="$(git rev-parse HEAD)"; head="$(commit README.md docs/notes.md)"
expect "docs only: nothing to deploy" "$base" "$head" "$none"

base="$head"; head="$(commit .github/workflows/pr-review.yml)"
expect "review workflow only: nothing to deploy" "$base" "$head" "$none"

base="$head"; head="$(commit web/src/app/app.ts)"
expect "web only" "$base" "$head" 'users-api=false tasks-api=false web=true infra=false deploy=true'

base="$head"; head="$(commit src/Users/TaskManager.Users.Api/Program.cs)"
expect "users API only" "$base" "$head" 'users-api=true tasks-api=false web=false infra=false deploy=true'

base="$head"; head="$(commit src/Tasks/TaskManager.Tasks.Domain/TaskItem.cs)"
expect "tasks API only" "$base" "$head" 'users-api=false tasks-api=true web=false infra=false deploy=true'

for shared in src/BuildingBlocks/TaskManager.ApiCommon/X.cs Directory.Packages.props Directory.Build.props global.json; do
  base="$head"; head="$(commit "$shared")"
  expect "shared $shared: both APIs" "$base" "$head" 'users-api=true tasks-api=true web=false infra=false deploy=true'
done

base="$head"; head="$(commit infra/TaskManager.Infra/ApiConstruct.cs)"
expect "infra code" "$base" "$head" 'users-api=false tasks-api=false web=false infra=true deploy=true'

base="$head"; head="$(commit .github/workflows/ci.yml)"
expect "ci workflow counts as infra" "$base" "$head" 'users-api=false tasks-api=false web=false infra=true deploy=true'

base="$head"; head="$(commit tests/TaskManager.Users.Api.Tests/X.cs src/Users.txt web.md)"
expect "look-alike paths: nothing to deploy" "$base" "$head" "$none"

base="$head"; commit README.md >/dev/null; head="$(commit web/src/main.ts)"
expect "range of several commits: union of changes" "$base" "$head" 'users-api=false tasks-api=false web=true infra=false deploy=true'

if ((failures > 0)); then
  echo "$failures case(s) failed."
  exit 1
fi
echo "All cases passed."
