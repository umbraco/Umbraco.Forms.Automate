---
name: post-release-cleanup
description: Merges a released release/* branch back into main or support/17.x, tags the merge commit, creates the GitHub Release, bumps version.json for next-cycle nightlies, and deletes the release branch. Use after the release build is green AND the package has been manually pushed to MyGet.
allowed-tools: Bash, Read, Edit, AskUserQuestion
---

# Post-Release Cleanup (satellite)

Run this **after** the release branch's CI build is green **and** the package has been manually
pushed to MyGet. This is the point where a version is actually shipped — nothing in git or CI observes
the manual MyGet push directly, so tagging and creating the GitHub Release happen here, not on branch
push or CI completion.

## Workflow

1. **Confirm the release actually shipped.** Ask the user (AskUserQuestion) to confirm the package for
   this release branch was pushed to MyGet. If not confirmed, stop — do not tag or merge an unpublished
   release.

2. **Identify the release branch and its target:**
   - `release/*` branches cut from `main` merge back into `main` (v18)
   - `release/*` branches cut from `support/17.x` merge back into `support/17.x` (v17)

   If ambiguous, ask the user.

3. **Fetch latest state:**
   ```bash
   git fetch origin --tags
   ```

4. **Merge the release branch into its target:**
   ```bash
   git checkout main                  # or support/17.x
   git pull origin main                # or support/17.x
   git merge --no-ff origin/release/<name> -m "Merge release/<name> into main"
   ```

5. **Read the shipped version** from `version.json` on the merge commit (this is the stable version set
   by `/release-management`, e.g. `18.0.0`).

6. **Tag the merge commit and push the tag:**
   ```bash
   git tag release-<version>
   git push origin release-<version>
   ```
   Tag naming is `release-<version>` (e.g. `release-18.0.0`, `release-17.0.0`) — matches the existing
   tag convention in this repo.

7. **Create the GitHub Release:**
   ```bash
   gh release create release-<version> --target main --generate-notes
   ```
   (`--target support/17.x` for a v17 release.) Use `--generate-notes` — GitHub's auto-generated
   summary of merged PRs since the previous release. Do not hand-write release notes.

8. **Push the merge commit:**
   ```bash
   git push origin main               # or support/17.x
   ```

9. **Patch-bump `version.json` on the target branch** so nightly `--preview.*` builds sort above the
   just-shipped stable version (e.g. `18.0.0` → `18.0.1`). Commit as:
   ```bash
   git commit -am "chore(release): Bump <branch> to <new-version> after <release-branch-name> release"
   git push origin main                # or support/17.x
   ```

10. **Delete the release branch, local and remote:**
    ```bash
    git branch -d release/<name>
    git push origin --delete release/<name>
    ```

11. **Report summary:**
    ```
    ✓ Merged release/2026.07.3 into main
    ✓ Tagged release-18.0.0 and pushed
    ✓ Created GitHub Release release-18.0.0 (auto-generated notes)
    ✓ Bumped main to 18.0.1 for next-cycle nightlies
    ✓ Deleted release/2026.07.3 (local + remote)
    ```

## Notes

- Always run from repository root.
- This is a light version of the core Umbraco.Automate `post-release-cleanup` skill — no changelog
  files, no release manifest, no multi-product tag scanning (single product, one `version.json`).
- Do not tag or create a release for a branch whose package was not actually confirmed on MyGet.
- If no `release/*` branch matches, or the merge conflicts, stop and report — do not force-resolve
  conflicts silently.
