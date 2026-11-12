#!/usr/bin/env bash
#
# Drift guard for the llms.txt / llms-full.txt pair (see CLAUDE.md, "LLM documentation").
# Runs on CI and locally: `bash scripts/check-llms-docs.sh`. Prints every finding and exits
# non-zero if there was at least one.
#
# Checks:
#   1. Every packable src/ project carries llms.txt and llms-full.txt.
#   2. The H1 of both files is the PackageId, and the blockquote summary is identical in both.
#   3. Every repository link in the pair and in the root llms.txt is on the main branch, points to a
#      path that exists, and never to PublicAPI.Unshipped.txt.
#   4. Every public type and member named in the PublicAPI files is mentioned in llms-full.txt.
#   5. The root llms.txt links every package's llms-full.txt.
#
# Ported from Josupeit.Practices, with one change that matters: this library's public surface is
# made of records, and the compiler gives every record members nobody writes or documents -
# <Clone>$, EqualityContract, PrintMembers, Deconstruct, Equals, GetHashCode, ToString, the
# equality operators and the init accessors of positional properties. Those are skipped below, so
# the coverage check asks only for names a person declared.
#
# Line endings are deliberately not checked: the repository stores LF and the working copy follows
# .gitattributes, so CRLF is an editor concern handled there and in .editorconfig.

set -u

cd "$(dirname "$0")/.."

failures=0

fail() {
  printf 'llms-docs: %s\n' "$1" >&2
  failures=$((failures + 1))
}

repository='https://codeberg.org/DerPeit/Josupeit.Mail.Milter/'
repository_pattern="$(printf '%s' "$repository" | sed 's/[.]/\\./g')"
raw_prefix="${repository}raw/branch/main/"
tree_prefix="${repository}src/branch/main/"

# Pulls a property value out of a csproj; empty when the property is absent.
csproj_property() {
  tr -d '\r' < "$1" | sed -n "s|.*<$2>\(.*\)</$2>.*|\1|p" | head -1
}

# The blockquote is the first line starting with '> ' up to the next blank line.
quote() {
  tr -d '\r' < "$1" | awk '/^> /{q=1} q&&/^$/{exit} q{print}'
}

# Prints the simple name of every type and member declared in a PublicAPI file, one per line,
# deduplicated. Operators, removed entries and compiler-generated record members carry no name
# anyone documents.
api_names() {
  tr -d '\r' < "$1" | awk '
    /^#nullable/ || /^\*REMOVED\*/ || /\.operator / || /^$/ { next }
    # Delegate types get Invoke/BeginInvoke/EndInvoke from the compiler; no document names them.
    /\.(Invoke|BeginInvoke|EndInvoke)\(/ { next }
    # What the compiler adds to every record.
    /\.<Clone>\$\(/ || /\.(EqualityContract|PrintMembers|Deconstruct|Equals|GetHashCode|ToString)[.(]/ { next }
    {
      sub(/ -> .*$/, "")
      sub(/ = .*$/, "")     # a constant or enum member is listed with its value
      sub(/\(.*$/, "")
      sub(/\.(get|set|init|add|remove)$/, "")
      while (gsub(/<[^<>]*>/, "")) { }
      n = split($0, parts, ".")
      name = parts[n]
      sub(/^~/, "", name)   # a finalizer is listed as ~Type
      print name
    }' | sort -u
}

# Every repository link in a file must be on main, resolve in the tree, and avoid the Unshipped file.
check_links() {
  local file="$1" url

  while read -r url; do
    case "$url" in
      *PublicAPI.Unshipped*) fail "$file: links $url (never link the Unshipped file)" ;;
      "$raw_prefix"*) [ -e "${url#"$raw_prefix"}" ] || fail "$file: $url does not exist in the tree" ;;
      "$tree_prefix"*) [ -e "${url#"$tree_prefix"}" ] || fail "$file: $url does not exist in the tree" ;;
      *) fail "$file: $url is not on the main branch" ;;
    esac
  done < <(grep -o "$repository_pattern[^ )]*" "$file" | tr -d '\r' | sed 's/[.,;:]$//' | sort -u)
}

for csproj in src/*/*.csproj; do
  dir="$(dirname "$csproj")"
  name="$(basename "$dir")"

  [ "$(csproj_property "$csproj" IsPackable)" = "false" ] && continue

  # ---- 1. Existence ----------------------------------------------------------------------------
  index="$dir/llms.txt"
  full="$dir/llms-full.txt"
  missing=0

  for file in "$index" "$full"; do
    [ -s "$file" ] || { fail "$name: $file is missing or empty"; missing=1; }
  done

  [ "$missing" -eq 0 ] || continue

  # ---- 2. H1 and blockquote --------------------------------------------------------------------
  package_id="$(csproj_property "$csproj" PackageId)"
  [ -n "$package_id" ] || package_id="$name"

  for file in "$index" "$full"; do
    h1="$(head -1 "$file" | tr -d '\r' | sed 's/^\xEF\xBB\xBF//')"
    [ "$h1" = "# $package_id" ] || fail "$file: first line is '$h1', expected '# $package_id'"
  done

  [ -n "$(quote "$full")" ] || fail "$full: no blockquote summary"
  [ "$(quote "$index")" = "$(quote "$full")" ] || fail "$name: blockquote summary differs between llms.txt and llms-full.txt"

  # ---- 3. Links --------------------------------------------------------------------------------
  for file in "$index" "$full"; do
    check_links "$file"
  done

  # ---- 4. API coverage -------------------------------------------------------------------------
  for api in "$dir"/PublicAPI.Shipped*.txt "$dir"/PublicAPI.Unshipped*.txt; do
    [ -f "$api" ] || continue

    names="$(api_names "$api")"
    [ -n "$names" ] || continue

    while read -r member; do
      fail "$full: does not mention '$member' ($(basename "$api"))"
    done < <(comm -23 <(printf '%s\n' "$names") <(grep -o -w -F -f <(printf '%s\n' "$names") "$full" | sort -u))
  done
done

# ---- 5. Root index -----------------------------------------------------------------------------
if [ -s llms.txt ]; then
  check_links llms.txt

  for full in src/*/llms-full.txt; do
    grep -q -F "$raw_prefix$full" llms.txt || fail "llms.txt: does not link $full"
  done
else
  fail "llms.txt: root index is missing"
fi

if [ "$failures" -gt 0 ]; then
  printf 'llms-docs: %d finding(s)\n' "$failures" >&2
  exit 1
fi

echo "llms-docs: all checks passed"
