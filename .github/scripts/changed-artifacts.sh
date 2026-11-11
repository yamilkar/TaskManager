#!/usr/bin/env bash
# Which deployable artifacts changed between two commits.
#
# Usage: changed-artifacts.sh <base-sha> <head-sha>
# Prints key=value lines, ready for "$GITHUB_OUTPUT":
#   users-api, tasks-api, web, infra  each artifact (true/false)
#   deploy                            true when any of them changed
# A missing, all-zero or unknown base (first push, manual run) marks everything as changed.
set -euo pipefail

base="${1:-}"
head="${2:?usage: changed-artifacts.sh <base-sha> <head-sha>}"

everything=false
if [[ -z "$base" || "$base" =~ ^0+$ ]] || ! git cat-file -e "${base}^{commit}" 2>/dev/null; then
  everything=true
  files=""
else
  files="$(git diff --name-only "$base" "$head")"
fi

# Shared code and build settings feed both APIs.
shared='src/BuildingBlocks/|Directory\.(Build|Packages)\.props$|global\.json$'

changed() {
  if $everything || grep -qE "$1" <<<"$files"; then echo true; else echo false; fi
}

users_api="$(changed "^(src/Users/|${shared})")"
tasks_api="$(changed "^(src/Tasks/|${shared})")"
web="$(changed '^web/')"
infra="$(changed '^(infra/|\.github/workflows/ci\.yml$)')"

deploy=false
for flag in "$users_api" "$tasks_api" "$web" "$infra"; do
  [[ "$flag" == true ]] && deploy=true
done

echo "users-api=$users_api"
echo "tasks-api=$tasks_api"
echo "web=$web"
echo "infra=$infra"
echo "deploy=$deploy"
