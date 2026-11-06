# Design

## Context

See proposal.md (Why). The dry-run version is computed in one place, `scripts/release-prep.ps1` (dry-run mode), and flows to `scripts/pack.ps1` through the workflow's `version` output, following the release process in `docs/architecture.md` ("Packing", "Releases"). vpk 1.2.161 rejects any version below 0.0.1, and under SemVer 2 a pre-release such as `0.0.x-tag` sorts below `0.0.x`.

## Goals / Non-Goals

**Goals:**
- A dry-run version vpk accepts, that is visibly a dry run with its run number, and that sorts below every real release.

**Non-Goals:**
- No change to release versions, local check versions or the workflow's steps.

## Decisions

### D1. `0.1.0-dryrun.<run number>`

- Above 0.0.1 for every run number, so vpk accepts it. Verified locally with a DEV pack at `0.1.0-dryrun.1`: packed, and the package scan passed.
- A pre-release of 0.1.0, so it sorts below 0.1.0 and therefore below 1.0.0 and every real release.
- It keeps the existing shape (`<base>-dryrun.<n>`) and the run number as its last identifier, so runs stay ordered.

*Alternatives considered:*
- `0.0.<run>-dryrun`: run 1 gives `0.0.1-dryrun`, which is below 0.0.1 and is refused.
- `0.0.1+dryrun.<run>`: build metadata is ignored when versions are compared, so every dry run would carry the same version. Rejected.
- `1.0.0-dryrun.<run>`: accepted, but it would sort above the local `0.0.<n>` checks and read like a v1.0.0 candidate. Rejected.

### D2. Self-test

`release-prep.ps1 -SelfTest` asserts `0.1.0-dryrun.42` for run 42. The real proof is the re-run dry run, which packs with the new version.

## Risks / Trade-offs

- [The version is still only proven by one packer] → The re-run dry run (task 8.4 of `delivery-unpackaged`, with the owner's approval to push) is the acceptance check.
- No data, edition or schema is touched: DEV and PROD stay apart exactly as before (guardrail 7), and the dry run packs PROD on GitHub only, never on the owner's PC.

## Migration Plan

Land on `main`, push with the owner's approval, start the dry run again. Rollback: revert the commit.
