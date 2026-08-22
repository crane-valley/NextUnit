#!/usr/bin/env bash
# Blocks a MAJOR version bump, or a release cut sooner than MIN_INTERVAL_DAYS after the previous
# tag, unless the pull request body carries an explicit justification token. Run locally with:
#   PR_BODY="$(cat body.txt)" .github/scripts/major-bump-guard.sh base.props head.props
# The tag lookup reads the git repository in the current working directory.
set -euo pipefail

MIN_INTERVAL_DAYS=30
MIN_TOKEN_CHARS=20
MAJOR_TOKEN='MAJOR-JUSTIFICATION:'
CADENCE_TOKEN='RELEASE-CADENCE-EXCEPTION:'
TAG_PATTERN='^v[0-9]+\.[0-9]+\.[0-9]+$'

fail() {
  echo "::error::$*"
  exit 1
}

# Sets VERSION_OUT rather than printing, so that fail() exits the script instead of a
# command-substitution subshell that would swallow the ::error:: annotation.
read_version() {
  local file=$1 label=$2 matches count=0 value
  [ -f "$file" ] || fail "$label Directory.Build.props not found: $file"
  matches=$(grep -oE '<Version>[^<]*</Version>' "$file" || true)
  [ -z "$matches" ] || count=$(printf '%s\n' "$matches" | wc -l | tr -d '[:space:]')
  # Demand exactly one declaration. MSBuild lets a later write win, so guessing which of several
  # elements is the shipped version would let the guard check a version nobody releases.
  [ "$count" -eq 1 ] || fail "$label Directory.Build.props must declare exactly one <Version> element, found $count"
  value=${matches#<Version>}
  value=${value%</Version>}
  printf '%s' "$value" | grep -qE '^[0-9]+\.[0-9]+\.[0-9]+$' \
    || fail "$label <Version> is malformed, expected MAJOR.MINOR.PATCH: '$value'"
  VERSION_OUT=$value
}

# The body is attacker-controlled text: it is only ever matched against an anchored literal
# prefix, never printed back into the log, expanded, or executed.
has_token() {
  local token=$1 line rest
  line=$(printf '%s' "${PR_BODY:-}" | tr -d '\r' | grep -m 1 -E "^${token}" || true)
  [ -n "$line" ] || return 1
  rest=${line#"$token"}
  rest=${rest//[[:space:]]/}
  [ ${#rest} -ge "$MIN_TOKEN_CHARS" ]
}

BASE_PROPS=${1:-}
HEAD_PROPS=${2:-}
if [ -z "$BASE_PROPS" ] || [ -z "$HEAD_PROPS" ]; then
  fail "usage: major-bump-guard.sh <base-Directory.Build.props> <head-Directory.Build.props>"
fi

read_version "$BASE_PROPS" base
base_version=$VERSION_OUT
read_version "$HEAD_PROPS" head
head_version=$VERSION_OUT

IFS=. read -r base_major base_minor base_patch <<<"$base_version"
IFS=. read -r head_major head_minor head_patch <<<"$head_version"

if [ "$base_version" = "$head_version" ]; then
  bump_kind='none'
elif [ "$head_major" -gt "$base_major" ]; then
  bump_kind='major'
elif [ "$head_major" -eq "$base_major" ] && [ "$head_minor" -gt "$base_minor" ]; then
  bump_kind='minor'
elif [ "$head_major" -eq "$base_major" ] && [ "$head_minor" -eq "$base_minor" ] && [ "$head_patch" -gt "$base_patch" ]; then
  bump_kind='patch'
else
  # A backwards or sideways move is still a release-shaped change, so it faces the cadence rule,
  # but it is not a MAJOR bump and must not be waved through by a MAJOR justification.
  bump_kind='other'
fi

latest_tag=
latest_ts=0
while IFS= read -r tag; do
  [ -n "$tag" ] || continue
  ts=$(git log -1 --format=%ct "$tag^{commit}" 2>/dev/null || true)
  [ -n "$ts" ] || continue
  if [ "$ts" -gt "$latest_ts" ]; then
    latest_ts=$ts
    latest_tag=$tag
  fi
done < <(git tag --list 2>/dev/null | grep -E "$TAG_PATTERN" || true)

days_since=
if [ -n "$latest_tag" ]; then
  days_since=$(( ( $(date +%s) - latest_ts ) / 86400 ))
fi

major_token_found=no
cadence_token_found=no
has_token "$MAJOR_TOKEN" && major_token_found=yes || true
has_token "$CADENCE_TOKEN" && cadence_token_found=yes || true

summary=$(printf '%s\n' \
  "### Major bump guard" \
  "" \
  "- base version: \`$base_version\`" \
  "- head version: \`$head_version\`" \
  "- bump kind: \`$bump_kind\`" \
  "- last release tag: \`${latest_tag:-none found}\`" \
  "- days since last release tag: \`${days_since:-unknown}\`" \
  "- \`$MAJOR_TOKEN\` present: \`$major_token_found\`" \
  "- \`$CADENCE_TOKEN\` present: \`$cadence_token_found\`")
printf '%s\n' "$summary"
[ -z "${GITHUB_STEP_SUMMARY:-}" ] || printf '%s\n' "$summary" >>"$GITHUB_STEP_SUMMARY"

if [ "$bump_kind" = 'none' ]; then
  echo "Version unchanged at $head_version; guard satisfied."
  exit 0
fi

if [ "$bump_kind" = 'major' ] && [ "$major_token_found" = no ]; then
  fail "MAJOR bump $base_version -> $head_version requires a PR body line starting with '$MAJOR_TOKEN' followed by at least $MIN_TOKEN_CHARS non-space characters."
fi

# Fail closed: without a tag there is no cadence baseline, and silently passing would let the
# rule evaporate exactly when the checkout is shallow or the tag fetch is broken.
[ -n "$latest_tag" ] || fail "No release tag matching $TAG_PATTERN found; the release cadence rule cannot be evaluated. Check out with fetch-depth 0 and fetch tags before running this guard."

if [ "$days_since" -lt "$MIN_INTERVAL_DAYS" ]; then
  [ "$cadence_token_found" = yes ] || fail "Version change $base_version -> $head_version comes $days_since day(s) after $latest_tag, under the $MIN_INTERVAL_DAYS-day release cadence rule; it requires a PR body line starting with '$CADENCE_TOKEN' followed by at least $MIN_TOKEN_CHARS non-space characters."
fi

echo "Guard satisfied for $bump_kind bump $base_version -> $head_version."
