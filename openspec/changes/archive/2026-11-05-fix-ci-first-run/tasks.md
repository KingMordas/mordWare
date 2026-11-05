# Tasks

## 1. CI restore for Release

- [x] 1.1 In `.github/workflows/ci.yml`, make the `build-test` job restore with `-p:Configuration=Release` before the Release build, with a comment naming the ReadyToRun reason.
  - Verify: in a fresh clone (no build output), `dotnet restore MordWare.slnx -p:Configuration=Release`, then `dotnet build MordWare.slnx -c Release --no-restore -p:GenerateAppxPackageOnBuild=false`, ends with 0 errors.
- [x] 1.2 Update the CI section of `docs/architecture.md` (the restore step) in the same group. Verify the description matches the workflow.

## 2. Safety checks without a private deny list

- [x] 2.1 In `scripts/check-public-safety.ps1`, make the deny-list loader always return an array (empty when the file is missing or has no entries), and keep the note that private words are not checked.
  - Verify spec scenario "No private deny list": in a fresh clone without the deny list, `-History`, `-All` and `-SelfTest` pass in PowerShell 7, `-All` passes in Windows PowerShell 5.1, and the note is printed.
- [x] 2.2 Re-run the same three modes in the working repository with the deny list present. Verify all pass, so the fix changes nothing when the list exists.

## 3. Architecture tests read the right build

- [x] 3.1 In `tests/MordWare.Tests.Architecture/MordWareArchitecture.cs`, make the build-output lookup search the project's `bin` folder for the test's own configuration both directly (`bin\<configuration>\`) and under a platform folder (`bin\<platform>\<configuration>\`). Take the newest match, never read the AppX layout, and keep the "not found" error naming the configuration.
  - Verify spec scenario "Release build is checked too": in the fresh clone, `dotnet test --project tests/MordWare.Tests.Architecture -c Release --no-build` after the Release build passes all architecture tests.
  - Result (2026-11-05): finding the output was not enough. The Release App is x86 ReadyToRun (native) code, which `Assembly.LoadFile` refuses in a 64-bit test process. The App and unit-test DLLs are now loaded by path through ArchUnitNET's metadata reader (`LoadFilteredDirectory`), which needs no runtime load. In a fresh clone: Release restore and build 0 errors and 0 warnings; the full suite in Release 23/23 passed (architecture 5/5). The rules fail when they select nothing, so this proves the Release App was read.
- [x] 3.2 Verify the existing scenarios still hold in Debug: `dotnet test --project tests/MordWare.Tests.Architecture` passes in the working repository.
- [x] 3.3 Update the architecture-tests "Loading" note in `docs/architecture.md` to say the outputs are found under `bin\<configuration>\` or `bin\<platform>\<configuration>\`. Verify it matches the code.

## 4. Roadmap and final checks

- [x] 4.1 In `docs/roadmap.md`: under [R], record that the first CI run exposed these three defects and that this change fixed them; under [D], add the parked note that the solution maps the App to x86 in every configuration, to decide with the platforms. Verify both notes are present.
- [x] 4.2 Run the whole suite in Release in the fresh clone, the way CI does: `dotnet test --solution MordWare.slnx -c Release --no-build`. Verify 0 failures, then delete the clone.
- [x] 4.3 Run `scripts/check-public-safety.ps1 -All` with the local deny list, and `openspec validate fix-ci-first-run --strict`. Verify both pass.

## Workflow follow-up

- `/opsx:archive` this change: one Conventional Commit (`fix(ci): ...`) authored as the public identity, passing the hooks.
- With the owner's OK, push to `main` and confirm that the CI run is green in both jobs. This covers spec scenario "Clean history passes" for the published history.
- Then the owner switches the repository to public, and enables secret scanning and push protection.
