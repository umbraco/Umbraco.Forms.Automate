---
name: release-management
description: Creates a release branch for this Umbraco.Automate satellite - bumps the Directory.Packages.props Automate package ranges and version.json to a release version (stable, or a pre-release while a CMS major is in pre-release) for the chosen Umbraco CMS major line. Use when preparing a new release.
allowed-tools: Bash, Read, Edit, AskUserQuestion
---

# Release Manager (satellite)

Prepares a release branch for this repo. This is a **light** version of the core Umbraco.Automate
`release-management` skill — this repo has a single product, no changelog, and no release manifest,
so there is no version-bump analysis, cascade, or manifest generation to do.

## Branch model

- `main` = the **v18** line (at v19 GA, `main` is expected to become the v19 line; not decided yet)
- `v19/dev` = the **v19** line while CMS 19 is in pre-release. Long-lived. Pushes to it build but don't
  publish. Each pre-release (`19.0.0-rc1`, `-rc2`, ...) is cut from it as a `release/*` branch.
- `support/17.x` = the **v17** line

Releases are cut as `release/YYYY.MM.N` branches. `N` is a single counter shared across **all** lines
in a given month (e.g. `release/2026.07.1` for v18, `release/2026.07.2` for v17 in the same month) —
not a separate counter per line.

## Workflow

1. **Ask which line to release** using AskUserQuestion:
   - v18 (from `main`)
   - v17 (from `support/17.x`)
   - v19 pre-release (from `v19/dev`) — see "Pre-release path" below

2. **Fetch latest state:**
   ```bash
   git fetch origin --tags
   ```

3. **Determine the next release number for the current month.** Release branches are deleted after
   merge-back (see `/post-release-cleanup`), so there is no live `release/*` branch or date-based tag
   to list. Instead scan merge commit messages on all lines for this month's releases:
   ```bash
   git log origin/main origin/support/17.x origin/v19/dev --merges --oneline \
     --grep="Merge release/$(date +%Y.%m)\." -E
   ```
   Find the highest `N` used this month across all lines and use `N+1`. If none this month, `N=1`.

4. **Confirm the branch name** with the user (default: computed name, e.g. `release/2026.07.3`).

5. **Create and check out the release branch** from the correct base:
   ```bash
   git checkout -b release/<name> origin/main            # v18
   git checkout -b release/<name> origin/support/17.x    # v17
   git checkout -b release/<name> origin/v19/dev         # v19 pre-release
   ```

6. **Bump `Directory.Packages.props`:** every `PackageVersion` for `Umbraco.Automate`, `.Core`, or
   `.Testing` → range `[X.0.0, X.999.999)` where `X` is the target line's major version (18 or 17;
   for a v19 pre-release see "Pre-release path").
   NuGet resolves a range to its floor, so this is what pulls the stable `X.0.0` package rather than a
   prerelease. Floating `X.0.0-*` does NOT work here — it resolves to the highest *prerelease*, never
   the stable release. Leave the product-dependency range (`Umbraco.Forms.Core`) untouched — it is
   managed separately.

7. **Bump `version.json` `"version"`** → stable `X.0.0` (drop any pre-release suffix such as `-rc1`).

8. **Show the diff and commit:**
   ```bash
   git add Directory.Packages.props version.json
   git commit -m "build(deps): Release v<X> against Umbraco.Automate <X>.0.0"
   ```

9. **Report next steps:**
   ```
   ✓ Created release branch: release/2026.07.3
   ✓ Bumped Directory.Packages.props + version.json to 18.0.0

   Next steps:
   - Push: git push -u origin release/2026.07.3
   - CI (azure-pipelines.yml, the shared Umbraco.Build template) runs on push: Build, SBOM and Test,
     then Prerelease pushes the package to MyGet, then Release waits for manual approval and pushes
     it to NuGet.org
   - Approve "Wait for manual approval" in the run's Release stage to push to NuGet.org
   - Once both publish stages are green, run /post-release-cleanup to merge back, tag, and create the GitHub Release
   ```

## Pre-release path (v19, while CMS 19 is in pre-release)

A v19 pre-release is published to **both MyGet and NuGet.org**, labelled `-rcN` (first label
`19.0.0-rc1`). It takes the same pipeline path as a stable release: the `release/*` branch makes it a
public build, the Prerelease stage pushes to MyGet, and the Release stage pushes to NuGet.org after
the manual approval. NuGet.org lists it as a pre-release, so `dotnet add package` skips it unless
`--prerelease` is passed. Steps 1-5 and 8-9 are the same, with the base branch `origin/v19/dev`.
Steps 6 and 7 change:

- **Automate range** → `[<floor>, 19.999.999)`, where `<floor>` is the Automate pre-release this
  build targets (e.g. `19.0.0-rc1`). It must exist on nuget.org: check that the version is listed there,
  not just on MyGet. It stays a pre-release floor, not `19.0.0`, until Automate 19 is stable.
- **`version.json`** keeps its pre-release suffix: set it to `<version>`, this package's own next
  unpublished pre-release (usually the one already in the file). Don't drop the suffix until CMS 19 is
  GA, then use the normal v18/v17 steps with `X` = 19 (floors `[19.0.0, 19.999.999)`, `version.json`
  `19.0.0`).
- **The version must not already be published.** A `release/*` branch is a public build, so it
  produces exactly the `version.json` version (`release/*` is in `publicReleaseRefSpec`, so
  `19.0.0-rc1` stays `19.0.0-rc1`). Check both feeds first. After each pre-release,
  `/post-release-cleanup` moves `version.json` on `v19/dev` to the next pre-release (`19.0.0-rc2`).
- Commit message: `build(deps): Release v19 pre-release <version> against Umbraco.Automate <floor>`.
- **Product entry** (`Umbraco.Forms.Core`) is left alone, as for v18/v17. Its pre-release floor
  (`19.0.0-rc1`) is managed by hand when the Forms RC moves.
- The `nuget.config` here lists nuget.org only, so the pre-release restores from nuget.org. If a
  needed pre-release (CMS, Licenses, Forms, Automate) isn't on nuget.org, stop and ask: don't add
  a feed to the shipped repo without a decision.

## Notes

- Always run from repository root.
- Do not add changelog generation, release manifests, or dependency cascade logic — none of that
  exists in this repo, and adding it is out of scope for this skill.
- If `Directory.Packages.props` has no matching `Umbraco.Automate*` entries for some reason, stop and
  ask the user rather than guessing.
