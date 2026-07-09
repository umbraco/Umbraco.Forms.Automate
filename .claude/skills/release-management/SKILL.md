---
name: release-management
description: Creates a release branch for this Umbraco.Automate satellite - bumps the Directory.Packages.props Automate package ranges and version.json to a stable version for the chosen Umbraco CMS major line. Use when preparing a new release.
allowed-tools: Bash, Read, Edit, AskUserQuestion
---

# Release Manager (satellite)

Prepares a release branch for this repo. This is a **light** version of the core Umbraco.Automate
`release-management` skill — this repo has a single product, no changelog, and no release manifest,
so there is no version-bump analysis, cascade, or manifest generation to do.

## Branch model

- `main` = current CMS major line (**v18**)
- `support/17.x` = previous CMS major line (**v17**)

Releases are cut as `release/YYYY.MM.N` branches. `N` is a single counter shared across **both** lines
in a given month (e.g. `release/2026.07.1` for v18, `release/2026.07.2` for v17 in the same month) —
not a separate counter per line.

## Workflow

1. **Ask which line to release** using AskUserQuestion:
   - v18 (from `main`)
   - v17 (from `support/17.x`)

2. **Fetch latest state:**
   ```bash
   git fetch origin --tags
   ```

3. **Determine the next release number for the current month.** Release branches are deleted after
   merge-back (see `/post-release-cleanup`), so there is no live `release/*` branch or date-based tag
   to list. Instead scan merge commit messages on both lines for this month's releases:
   ```bash
   git log origin/main origin/support/17.x --merges --oneline \
     --grep="Merge release/$(date +%Y.%m)\." -E
   ```
   Find the highest `N` used this month across both lines and use `N+1`. If none this month, `N=1`.

4. **Confirm the branch name** with the user (default: computed name, e.g. `release/2026.07.3`).

5. **Create and check out the release branch** from the correct base:
   ```bash
   git checkout -b release/<name> origin/main            # v18
   git checkout -b release/<name> origin/support/17.x    # v17
   ```

6. **Bump `Directory.Packages.props`:** every `PackageVersion` for `Umbraco.Automate`, `.Core`, or
   `.Testing` → range `[X.0.0, X.999.999)` where `X` is the target line's major version (18 or 17).
   NuGet resolves a range to its floor, so this is what pulls the stable `X.0.0` package rather than a
   prerelease. Floating `X.0.0-*` does NOT work here — it resolves to the highest *prerelease*, never
   the stable release. Leave product-dependency ranges (e.g. `Umbraco.Commerce.Cms.Startup`) untouched
   — those are managed separately.

7. **Bump `version.json` `"version"`** → stable `X.0.0` (drop any `-beta`/prerelease suffix).

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
   - CI (azure-pipelines.yml) validates on push (Build + Test + SBOM)
   - Once green, manually push the package to MyGet
   - Then run /post-release-cleanup to merge back, tag, and create the GitHub Release
   ```

## Notes

- Always run from repository root.
- Do not add changelog generation, release manifests, or dependency cascade logic — none of that
  exists in this repo, and adding it is out of scope for this skill.
- If `Directory.Packages.props` has no matching `Umbraco.Automate*` entries for some reason, stop and
  ask the user rather than guessing.
