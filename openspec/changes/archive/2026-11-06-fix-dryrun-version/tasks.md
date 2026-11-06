# Tasks

## 1. Dry-run version

- [x] 1.1 In `scripts/release-prep.ps1`, make the dry-run version `0.1.0-dryrun.<run>` (code and help text) and update its `-SelfTest` to expect `0.1.0-dryrun.42`; verify `pwsh -File scripts/release-prep.ps1 -SelfTest` passes and fails when the expected value is flipped locally.
- [x] 1.2 Verify scenario "Dry run version" locally, without PROD: run `release-prep.ps1 -DryRun -RunNumber 1` on the repository, then `pack.ps1 -Edition Dev -Version <the printed version>`; check that vpk accepts it and the package scan passes, then delete the local pack output. (Scenario "Dry run publishes nothing" is unchanged and re-checked by the re-run dry run in the follow-up.)
- [x] 1.3 Update the comments that name the dry-run version in `scripts/pack.ps1` and `.github/workflows/release.yml`; verify `git grep -n "0.0.0-dryrun"` finds nothing outside archived changes.

## 2. Docs

- [x] 2.1 Update `docs/architecture.md` (Packing: versions; Releases: release-prep step) and the release line in `CLAUDE.md` to `0.1.0-dryrun.<run number>`; verify both match `release-prep.ps1`.
- [x] 2.2 Update `docs/roadmap.md` [D]: record that dry run #1 failed at packing because vpk requires a version of at least 0.0.1, and that this change sets the dry-run version to `0.1.0-dryrun.<run>`; keep the dry run marked pending; verify the "what's next" procedure still reads correctly.

## 3. Checks

- [x] 3.1 Run `scripts/check-public-safety.ps1 -All` with the local deny list, and `-SelfTest`; verify both pass before archive.

## Workflow follow-up

- `/opsx:archive` (syncs the `development-workflow` delta and ends with one `fix:` commit on `main`).
- With the owner's approval: push `main`, start the release dry run again, check its artifact (PROD installer and full package only, version `0.1.0-dryrun.<n>`, notes with the license line and source link, scan report), and record the result in `docs/roadmap.md` [D] (task 8.4 of `delivery-unpackaged`).
