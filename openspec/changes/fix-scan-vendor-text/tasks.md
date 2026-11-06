# Tasks

## 1. Package scan

- [ ] 1.1 In `scripts/check-public-safety.ps1`, record every distinct this-PC match in `Test-OwnPackageBytes` as its own finding with its text in `Raw` (design D1), and in `Test-VelopackDerivedBytes` let only `private-word` block unconditionally (design D2); update the comments and the `-Package` help text. Verify `-SelfTest` still passes in Windows PowerShell 5.1 and PowerShell 7.
- [ ] 1.2 Add the two self-test cases of design D3; verify they pass (scenario "Build machine path that the original already contains" and scenario "Build machine path that the original does not contain"), that the existing private-word and user-path cases still pass (scenarios "Private word in an installer program" and "Installer program adds flagged content"), and that the new pass case fails when D2 is reverted locally.
- [ ] 1.3 Verify locally that a DEV pack (`pack.ps1 -Edition Dev -Version 0.0.1`) still passes the scan, then delete the local pack output.

## 2. Docs

- [ ] 2.1 Update the package-scan description in `docs/architecture.md` (Public-safety checks) and the Packages item in `docs/public-safety.md`; verify both match the script.
- [ ] 2.2 Update `docs/roadmap.md` [D]: record that dry run #2 packed `0.1.0-dryrun.2` but the scan blocked the three Velopack-derived binaries on the runner's profile path, and that this change refines the rule; correct the derived-binary sentence in the parked notes; keep the dry run marked pending.

## 3. Checks

- [ ] 3.1 Run `scripts/check-public-safety.ps1 -All` with the local deny list, and `-SelfTest`; verify both pass before archive.

## Workflow follow-up

- `/opsx:archive` (syncs the `development-workflow` delta and ends with one `fix:` commit on `main`).
- With the owner's approval: push `main`, start the release dry run again (#3), check its artifact (PROD installer and full package only, version `0.1.0-dryrun.<n>`, notes with the license line and source link, scan report), and record the result in `docs/roadmap.md` [D].
