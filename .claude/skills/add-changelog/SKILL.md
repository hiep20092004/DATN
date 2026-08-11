---
name: add-changelog
description: Add or update CHANGELOG.md entries following Keep a Changelog 1.0.0 and SemVer 2.0.0. Use when the user wants to record a change, cut a release, or bump the version in the changelog.
---

# add-changelog

Maintain `CHANGELOG.md` at repo root per [Keep a Changelog 1.0.0](https://keepachangelog.com/en/1.0.0/)
and [SemVer 2.0.0](https://semver.org/spec/v2.0.0.html). Keep edits minimal — touch only the
changelog (and `bundleVersion` when cutting a release).

## Versioning model (no git tags)

This project does NOT use git tags. A version is defined by its **start commit** — the
commit whose subject is the bare version string (e.g. `0.4.6`) that bumped `bundleVersion`.
A version contains every commit from its start commit up to (but not including) the next
version's start commit.

Find a version's commits:
```
git log --no-merges --format="%s" <start-of-X>..<start-of-next> | grep -v "^<next-version>$"
```
The version heading's link ref points to its start commit:
```
[X.Y.Z]: https://github.com/SonatGameProduct/water_flow_out_factory/commit/<full-sha>
```
Get the start commit + date for a version:
```
git log --format="%H %ai" --grep="^X\.Y\.Z$" -1
```

`--no-merges` skips merge commits. When a range contains a **merge from another branch**,
expand its second parent so the merged-in work is summarized in detail, not as one line:
```
git log -1 --format="%P" <merge-sha>                          # first-parent second-parent
git log --no-merges --format="%h %s" <first-parent>..<second-parent>
```
Skip squashed third-party/SDK commits (e.g. `sonat_sdk_v2`) as chore noise.

## Two modes

### 1. Generate a version section (default for an existing bump)
1. Resolve the version's start commit and the next version's start commit.
2. List the commits in that range (command above), excluding the next bump commit.
3. Summarize into a `## [X.Y.Z] - YYYY-MM-DD` section (date = start commit's date).
4. Add/refresh the `[X.Y.Z]` link ref pointing to the start commit.

### 2. Record an in-progress change
This project does NOT use an `## [Unreleased]` section. Work committed after the latest
version bump belongs to that latest version until the next bump lands. Append the
human-readable line under the matching category of the **latest version section** (the one
whose start commit is the most recent bump). Create the category sub-heading only if missing.

**Always reconcile against the full range, never just the one change you were handed.**
Even when the user points at a single commit/merge, scan everything since the latest bump
and fill every gap — do not stop at the item mentioned:
```
git log --no-merges --format="%h %s" <latest-bump>..HEAD
```
Expand any merge in that range (see the merge command above — a merge's children appear on
the branch, not in `--no-merges` of the mainline). Then diff that commit list against the
bullets already in the latest version section; add any user/dev-facing commit that is
missing. Skip only `[chore]`/`[refactor]` (non-user-visible) and squashed SDK noise.

## Categories (exact spelling, this order only)

`Added` · `Changed` · `Deprecated` · `Removed` · `Fixed` · `Security`

## Map existing commit prefixes → category

The repo already uses bracket commit prefixes. Use them to classify when summarizing commits:

| Commit prefix | Category |
|---|---|
| `[feat]`     | Added (or Changed if it modifies existing behavior) |
| `[fix]`      | Fixed |
| `[refactor]` | Changed (only if user-visible; otherwise omit) |
| `[art]`      | Changed (only if player-facing; otherwise omit) |
| `[chore]`    | Usually omit (build/deps) unless user-facing → Changed |

Removed/Deprecated/Security have no prefix — infer from the change description.

## SemVer rules (pre-1.0 aware — project is currently 0.x)

- **MAJOR** (`X`): incompatible / breaking change. Pre-1.0 (`0.y.z`), breaking changes
  bump MINOR instead, per SemVer §4.
- **MINOR** (`Y`): new backward-compatible functionality (`Added`, sometimes `Changed`).
- **PATCH** (`Z`): backward-compatible bug fixes (`Fixed`).
- If unsure which to bump, ask the user once with a concrete recommendation.

## Writing rules

- One bullet per change, present-tense imperative, player/dev-facing language — not raw commit text.
- No empty released sections; omit categories with no entries.
- `YYYY-MM-DD` dates only. Do NOT use an `## [Unreleased]` section — in-progress work lives
  under the latest version section until the next bump.
- Don't reorder or rewrite already-released entries.
