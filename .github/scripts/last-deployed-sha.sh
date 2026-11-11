#!/usr/bin/env bash
# Prints the commit of the most recent *successful* deployment to a GitHub environment, or nothing if there is none.
#
# Usage: last-deployed-sha.sh <owner/repo> <environment>   (needs GH_TOKEN with deployments: read)
# GitHub records a deployment for every job that uses the environment. When a deployment succeeds, older ones are
# marked inactive, so the newest deployment whose latest status is "success" is the one running in AWS. Runs that
# skipped or failed the deploy job leave no successful deployment behind, so their changes stay in the next diff.
set -euo pipefail

repo="${1:?usage: last-deployed-sha.sh <owner/repo> <environment>}"
environment="${2:?usage: last-deployed-sha.sh <owner/repo> <environment>}"

gh api "repos/$repo/deployments?environment=$environment&per_page=30" --jq '.[] | "\(.id) \(.sha)"' |
  while read -r id sha; do
    state="$(gh api "repos/$repo/deployments/$id/statuses?per_page=1" --jq '.[0].state // ""')"
    if [[ "$state" == success ]]; then
      echo "$sha"
      break
    fi
  done
