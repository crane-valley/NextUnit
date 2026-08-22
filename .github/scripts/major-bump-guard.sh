#!/usr/bin/env bash
# Blocks a MAJOR version bump, or a release cut sooner than MIN_INTERVAL_DAYS after the previous
# tag, unless the pull request body carries an explicit justification token. Run locally with:
#   PR_BODY="$(cat body.txt)" .github/scripts/major-bump-guard.sh base-tree/ head-tree/
# where each argument is a directory holding a checkout that contains Directory.Build.props.
# The tag lookup reads the git repository in the current working directory.
set -euo pipefail

MIN_INTERVAL_DAYS=30
MIN_TOKEN_CHARS=20
MAJOR_TOKEN='MAJOR-JUSTIFICATION:'
CADENCE_TOKEN='RELEASE-CADENCE-EXCEPTION:'
# Stable releases only. An rc or preview tag days before its GA is the normal shape of a release,
# not the back-to-back cutting this guard exists to catch.
TAG_PATTERN='^v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$'
# The grammar release.yml enforces on the published package, so a version this guard accepts is a
# version that can actually ship. Components are capped at nine digits: a longer one overflows the
# shell integer comparison below, which returns status 2 and would silently skip the MAJOR check.
VERSION_PATTERN='^(0|[1-9][0-9]{0,8})\.(0|[1-9][0-9]{0,8})\.(0|[1-9][0-9]{0,8})(-(alpha|beta|rc|preview)(\.(0|[1-9][0-9]{0,8}))?)?$'

fail() {
  echo "::error::$*"
  exit 1
}

flatten() {
  printf '%s' "$1" | tr -d '\r' | tr '\n' ' '
}

# Ask MSBuild what the version is, from the projects that actually pack.
#
# Earlier revisions parsed Directory.Build.props directly and were bypassed four separate times
# in review: a second declaration carrying an attribute, the same one split across lines, an
# <Import> or <Sdk> pulling the value in from elsewhere, and finally <version> in different
# letter case, since MSBuild property names are case-insensitive. Each fix closed one construct
# and the next review round found another, because reimplementing MSBuild's evaluation rules in
# a parser is an open-ended job. The evaluator is authoritative and already installed in CI.
#
# Evaluating Directory.Build.props on its own was not enough either: reserved properties such as
# MSBuildProjectExtension differ between evaluating that file as the project and evaluating the
# .csproj that imports it, so a property conditioned on one of them reads one version here and
# ships another. This evaluates the same project files release.yml packs, in the same Release
# configuration, and requires every one of them to agree. The list comes from release.yml so that
# adding a package there cannot silently leave it unguarded.
#
# Evaluating a pull request's own project files runs its MSBuild logic. That is not a new
# exposure here: strict-build in this same workflow already runs dotnet build over the same tree.
packed_projects() {
  local tree=$1 workflow="$1/.github/workflows/release.yml"
  [ -f "$workflow" ] || return 1
  grep -oE 'src/[A-Za-z.]+/[A-Za-z.]+\.csproj' "$workflow" | sort -u
}

evaluate_one() {
  local project=$1 label=$2 output body count value
  output=$(dotnet msbuild "$project" -getProperty:Version -p:Configuration=Release -nologo 2>&1) \
    || fail "$label $project could not be evaluated by MSBuild: $(flatten "$output")"
  # The whole of stdout has to be the version. Accepting just its last non-blank line would let
  # whatever MSBuild printed ahead of that line through unexamined.
  body=$(printf '%s' "$output" | tr -d '\r' | grep -v '^[[:space:]]*$' || true)
  count=$(printf '%s' "$body" | grep -c '' || true)
  [ -n "$body" ] && [ "$count" -eq 1 ] \
    || fail "$label $project did not evaluate to a single value: $(flatten "$output")"
  value=${body#"${body%%[![:space:]]*}"}
  EVALUATED=${value%"${value##*[![:space:]]}"}
}

# Sets VERSION_OUT rather than printing, so that fail() exits the script instead of a
# command-substitution subshell that would swallow the ::error:: annotation.
read_version() {
  local tree=$1 label=$2 projects project agreed=
  projects=$(packed_projects "$tree") \
    || fail "$label tree has no .github/workflows/release.yml to take the packed project list from."
  [ -n "$projects" ] || fail "$label release.yml lists no packable project, so there is no version to check."
  while IFS= read -r project; do
    [ -n "$project" ] || continue
    [ -f "$tree/$project" ] || fail "$label $project is packed by release.yml but missing from the tree."
    evaluate_one "$tree/$project" "$label"
    if [ -z "$agreed" ]; then
      agreed=$EVALUATED
    elif [ "$EVALUATED" != "$agreed" ]; then
      # Packages that disagree would publish one tag over several different versions.
      fail "$label packages do not agree on a version: $project evaluates to '$EVALUATED', an earlier one to '$agreed'."
    fi
  done <<<"$projects"
  # Anything MSBuild did not resolve to a shippable version, an empty property included, stops
  # here rather than being guessed at.
  printf '%s' "$agreed" | grep -qE "$VERSION_PATTERN" \
    || fail "$label version does not match this repository's version scheme: '$agreed'"
  VERSION_OUT=$agreed
}

# The body is attacker-controlled text: it is only ever matched against an anchored literal
# prefix, never printed back into the log, expanded, or executed. Every matching line is checked,
# so an empty token line left in a template cannot mask a real justification further down.
has_token() {
  local token=$1 line rest
  while IFS= read -r line; do
    rest=${line#"$token"}
    rest=${rest//[[:space:]]/}
    [ ${#rest} -lt "$MIN_TOKEN_CHARS" ] || return 0
  done < <(printf '%s' "${PR_BODY:-}" | tr -d '\r' | grep -E "^${token}" || true)
  return 1
}

BASE_TREE=${1:-}
HEAD_TREE=${2:-}
if [ -z "$BASE_TREE" ] || [ -z "$HEAD_TREE" ]; then
  fail "usage: major-bump-guard.sh <base-tree-directory> <head-tree-directory>"
fi
command -v dotnet >/dev/null 2>&1 || fail "the .NET SDK is required to evaluate the version but dotnet was not found."

read_version "$BASE_TREE" base
base_version=$VERSION_OUT
read_version "$HEAD_TREE" head
head_version=$VERSION_OUT

IFS=. read -r base_major base_minor base_patch <<<"${base_version%%-*}"
IFS=. read -r head_major head_minor head_patch <<<"${head_version%%-*}"

if [ "$base_version" = "$head_version" ]; then
  bump_kind='none'
elif [ "$head_major" -gt "$base_major" ]; then
  bump_kind='major'
elif [ "$head_major" -lt "$base_major" ]; then
  # Dropping the MAJOR is as large a compatibility statement as raising it, and it is what an
  # accidentally removed <Version> looks like once the SDK default of 1.0.0 takes over, so it
  # needs the same justification rather than passing as an ordinary change.
  bump_kind='major-decrease'
elif [ "$head_major" -eq "$base_major" ] && [ "$head_minor" -gt "$base_minor" ]; then
  bump_kind='minor'
elif [ "$head_major" -eq "$base_major" ] && [ "$head_minor" -eq "$base_minor" ] && [ "$head_patch" -gt "$base_patch" ]; then
  bump_kind='patch'
else
  # A prerelease move, or a backwards one, is still a release-shaped change, so it faces the
  # cadence rule, but it is not a MAJOR bump and must not be waved through by a MAJOR justification.
  bump_kind='other'
fi

latest_tag=
latest_ts=0
# One for-each-ref rather than a git log per tag: both dates this loop needs are ref fields, so
# the lookup costs one git process however many tags the repository carries.
while IFS=' ' read -r tag objecttype creator commit_ts; do
  [[ $tag =~ $TAG_PATTERN ]] || continue
  # An annotated tag cut today can point at an old commit, so take the later of the tagger date
  # and the tagged commit's date. A lightweight tag has no creation date of its own: creatordate
  # reports the commit date and there is no dereferenced date, so a lightweight tag cut today on
  # an old commit still reads as old. That residual gap is accepted because this repository tags
  # the release commit itself, and closing it would mean calling the Releases API, which the
  # local run cannot do.
  case $objecttype in
    commit) ts=${creator:-0} ;;
    # An annotated tag that peels to nothing tags a blob or a tree, which is not a release at
    # all. Its tagger date must not become the cadence baseline.
    tag)
      [ -n "$commit_ts" ] || continue
      ts=${creator:-0}
      [ "$commit_ts" -le "$ts" ] || ts=$commit_ts
      ;;
    *) continue ;;
  esac
  [ "$ts" -gt 0 ] || continue
  if [ "$ts" -gt "$latest_ts" ]; then
    latest_ts=$ts
    latest_tag=$tag
  fi
done < <(git for-each-ref --format='%(refname:strip=2) %(objecttype) %(creatordate:unix) %(*committerdate:unix)' refs/tags 2>/dev/null || true)

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

if [ "$head_major" != "$base_major" ] && [ "$major_token_found" = no ]; then
  fail "MAJOR change $base_version -> $head_version requires a PR body line starting with '$MAJOR_TOKEN' followed by at least $MIN_TOKEN_CHARS non-space characters."
fi

# Fail closed: without a tag there is no cadence baseline, and silently passing would let the
# rule evaporate exactly when the checkout is shallow or the tag fetch is broken.
[ -n "$latest_tag" ] || fail "No release tag matching $TAG_PATTERN found; the release cadence rule cannot be evaluated. Check out with fetch-depth 0 and fetch tags before running this guard."

if [ "$days_since" -lt "$MIN_INTERVAL_DAYS" ]; then
  [ "$cadence_token_found" = yes ] || fail "Version change $base_version -> $head_version comes $days_since day(s) after $latest_tag, under the $MIN_INTERVAL_DAYS-day release cadence rule; it requires a PR body line starting with '$CADENCE_TOKEN' followed by at least $MIN_TOKEN_CHARS non-space characters."
fi

echo "Guard satisfied for $bump_kind bump $base_version -> $head_version."
