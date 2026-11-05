# Proposal

## Why

The first CI run on GitHub, triggered by the initial push of roadmap item [R], failed in both jobs. CI had never run before, so three latent defects surfaced together:

1. **Build.** The App enables ReadyToRun in Release, but CI restored packages without the Release configuration and then built Release with `--no-restore`, so the ReadyToRun runtime pack was missing (NETSDK1094). This dates from bootstrap.
2. **Public safety.** The check script crashes when no private deny list exists, which is always the case in CI. Under strict mode, the empty deny list came back from a function as `$null` instead of an empty array. This dates from [R].
3. **Architecture tests in Release.** The solution builds the App for the x86 platform, so its output lands in `bin\<platform>\<configuration>\`. The tests look only in `bin\<configuration>\`, so all five rules fail in Release. In Debug they can silently read a stale App build left there by `run-dev.ps1`. This dates from bootstrap.

The repository must not go public with a red CI, and the tests must check the build they claim to check.

## What Changes

- **CI** restores for the Release configuration before the Release build.
- **The public-safety script** always treats a missing or empty deny list as an empty list, and still prints its note that private words are not checked.
- **The architecture tests** find the App and unit-test outputs under both `bin\<configuration>\` and `bin\<platform>\<configuration>\`, take the newest, and never read the AppX layout.
- **Specs:** a scenario that the architecture tests pass in Release as well as Debug, and a scenario that the safety checks run, and pass on clean content, where no private deny list exists.
- **Parked note for [D]:** the solution maps the App to x86 in every configuration. [D] decides the platforms when it unpackages the app.

### Scope

- **In:** `.github/workflows/ci.yml` (the restore step), `scripts/check-public-safety.ps1` (the deny-list loading), `tests/MordWare.Tests.Architecture` (the build-output lookup), the two spec scenarios, the roadmap note, and a green CI run on GitHub.
- **Out:**
  - changing the solution's platform mapping or the ReadyToRun setting (that is [D]);
  - any new safety rule;
  - going public (the owner does that after this change).

### Guardrails touched

- **8:** the architecture tests stay as strict as they are; only how they locate the build output changes.
- **10:** living docs (the roadmap and the architecture notes on CI and tests).
- **11:** the safety check works in CI.

### User-visible

No. CI and tests only; nothing changes in the app, so there is no CHANGELOG entry.

### Roadmap item

Follow-up to **[R] Open source and public repository**: it makes the first CI run green before the repository goes public.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `development-workflow`: "Module boundaries are checked automatically" gains a scenario: the checks pass against a Release build too.
- `public-repository`: "Safety checks run before commit and in CI" gains a scenario: the checks run where no private deny list exists.

## Impact

- **Files:** `.github/workflows/ci.yml`, `scripts/check-public-safety.ps1`, `tests/MordWare.Tests.Architecture/MordWareArchitecture.cs`, `docs/architecture.md`, `docs/roadmap.md`.
- **No dependency changes and no app code changes.**
- The fixes to the first two files were drafted while diagnosing the failed run and are still uncommitted. Apply verifies them like any other task.
