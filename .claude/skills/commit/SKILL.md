---
name: commit
description: Stage and split the current working changes into clean, logically-grouped commits with changelog-friendly messages. Use when the user wants to commit work, split a big diff into multiple commits, or asks how to name commits.
---

# commit

Turn the current working tree into one or more well-scoped commits whose messages map
cleanly onto `CHANGELOG.md` categories. Pairs with the `/add-changelog` skill.

## Procedure

1. Inspect state: `git status --short` and `git diff --stat` (plus `git diff` for hunks).
2. Group changed files by intent — one commit per coherent change. Split when a diff mixes
   a feature, a bug fix, and chore/asset noise. Do NOT bundle unrelated changes.
3. Stage each group explicitly (`git add <paths>` or `-p` for partial hunks), commit, repeat.
4. Never commit `Library/`, generated caches, or local-only files. Respect `.gitignore`.
5. Confirm with the user before committing if the grouping is non-obvious or large.

If on the default branch (`develop`), branch first per repo workflow.

## Understanding merged-in work

When recent history includes a **merge commit** from another branch, do NOT treat the
merge as one opaque change. Expand its second parent to read what actually landed before
writing commit messages or changelog entries:

```
git log -1 --format="%P" <merge-sha>                          # first-parent second-parent
git log --no-merges --format="%h %s" <first-parent>..<second-parent>
```

Summarize from those underlying commits (skip squashed third-party/SDK noise), not from the
merge subject alone.

## Message format (this is the contract `add-changelog` relies on)

```
[prefix] imperative summary, player/dev-facing
```

- Lowercase bracket prefix from the table below — pick the one that decides the changelog category.
- Summary in present-tense imperative, describing the *effect*, not the file touched.
  Good: `[fix] stop two blocks tunneling when picked during snap`
  Bad:  `[fix] BlockMovement.cs update`
- One change per commit. If the summary needs "and", it is probably two commits.
- Optional body (after a blank line) for *why* / context — not a file list.

### Prefix → changelog category

| Prefix       | Changelog category | Use for |
|--------------|--------------------|---------|
| `[feat]`     | Added              | new player- or dev-facing functionality |
| `[change]`   | Changed            | reworks/polish of existing behavior that players or devs notice |
| `[fix]`      | Fixed              | bug fixes |
| `[remove]`   | Removed            | deleted features/systems |
| `[deprecate]`| Deprecated         | still present but slated for removal |
| `[security]` | Security           | security-relevant fixes |
| `[art]`      | Changed (if player-facing) | sprites, models, VFX, BGM |
| `[refactor]` | omit unless user-visible | internal restructure |
| `[chore]`    | omit               | build, deps, tooling, CI, settings |
| `[level]`    | Added              | new/updated level content (batch many levels per commit) |

`[change]`, `[remove]`, `[deprecate]`, `[security]`, `[level]` extend the existing
`[feat]/[fix]/[refactor]/[art]/[chore]` set so each commit lands in exactly one
Keep a Changelog bucket. Anything mapped to "omit" is intentionally kept out of the changelog.

## Version bump commits

A version bump is its own commit with the bare version string as the entire subject:
`0.4.7`. It must change only `bundleVersion` in `ProjectSettings/ProjectSettings.asset`.
Keep it isolated — `add-changelog` treats it as the start-of-version marker.

## End each commit message with

```
Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
```
