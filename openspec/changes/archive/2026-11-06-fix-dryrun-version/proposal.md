# Proposal

## Why

The release dry run, the release workflow's only test before v1.0.0, failed on its first run: Velopack's packer refuses the version `0.0.0-dryrun.<run>` because it requires at least 0.0.1, and a pre-release of 0.0.0 sorts below that. Every step before packing passed. Until the version is fixed, the dry run can never produce an installer.

## What Changes

- The dry run's version becomes `0.1.0-dryrun.<run number>`: a pre-release that the packer accepts (above 0.0.1) and that still sorts below every real release from 1.0.0 on.
- The release preparation script and its self-test produce and check that version.
- The docs that name the dry-run version follow: `docs/architecture.md`, the release line in `CLAUDE.md`, and the comments in `scripts/pack.ps1` and `.github/workflows/release.yml`.
- `docs/roadmap.md` [D] records the failed first dry run, its cause and this fix.

## Scope

**In:**
- The dry-run version scheme, its self-test, and the docs and comments that name it.
- Re-running the dry run after the fix, with the owner's approval to push (task 8.4 of `delivery-unpackaged`).

**Out:**
- Release versions (still the tag's `X.Y.Z`) and local check versions (still `0.0.<n>`).
- Any other change to the release workflow, packing or the package scan.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `development-workflow`: the "Release dry run" requirement states which version a dry-run installer carries.

## Guardrails touched

- **10** (living docs): the docs that name the dry-run version are updated in the same change.
- **11** (public safety): unchanged; the final public-safety check still runs.

## User-visible

No. Dry-run installers are workflow artifacts that are never published, so there is no `CHANGELOG.md` entry.

## Roadmap

Item **[D] Delivery** in `docs/roadmap.md` (its pending dry run).

## Impact

- **Code:** `scripts/release-prep.ps1` (version and `-SelfTest`); comments in `scripts/pack.ps1` and `.github/workflows/release.yml`.
- **Docs:** `docs/architecture.md`, `CLAUDE.md`, `docs/roadmap.md`.
