# Proposal

## Why

Release dry run #2 (`0.1.0-dryrun.2`) packed the installer, but the package scan blocked it. Velopack's own vendor binaries embed their build machine's path (the runner's `<user folder>\.cargo\...`, more than 100 times each), because Velopack is built on GitHub Actions. Our release job runs on GitHub Actions too, so the runner's profile is the same path, and the "this PC" rule fires on Velopack's upstream text in the three Velopack-derived binaries. Until the scan tells that text apart from text about the build PC, no dry run or release can pass.

## What Changes

- In the package scan, a "this PC" hit in a Velopack-derived binary (the `Setup.exe` stub, the shortcut stub, `Squirrel.exe`) whose exact text also occurs in the pinned Velopack vendor binary counts as vendor content and no longer blocks. This is the same rule those binaries already follow for user paths, e-mail addresses and package hashes.
- Unchanged: a private word always blocks; our own files and every other file keep the strict rules.
- The scan's self-test gains a case for it, and a "this PC" hit that the vendor binary does not contain still fails.
- `docs/architecture.md`, `docs/public-safety.md` and `docs/roadmap.md` [D] describe the refined rule and record dry run #2.

## Scope

**In:**
- The derived-binary rule in `scripts/check-public-safety.ps1 -Package`, and its self-test.
- The `development-workflow` requirement "Installer programs are checked against their originals".
- The docs that state the rule, and the roadmap note.

**Out:**
- The rules for our own files, for byte-identical third-party files, and for the repository checks (`-Staged`, `-All`, `-History`).
- The release workflow, packing and versions.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `development-workflow`: "Installer programs are checked against their originals" lets text that names a PC pass when the installer tool's pinned original contains exactly that text; private words still always block.

## Guardrails touched

- **11** (public safety): a scan rule is refined, not weakened for our own content. Text that is byte-for-byte in the pinned upstream binary cannot come from the build PC, and private words still always block. Owner decision 2026-11-06.
- **10** (living docs): the docs that state the rule are updated in the same change.

## User-visible

No. The scan only gates packing; there is no `CHANGELOG.md` entry.

## Roadmap

Item **[D] Delivery** in `docs/roadmap.md` (its pending dry run).

## Impact

- **Code:** `scripts/check-public-safety.ps1` (`-Package` derived-binary check and `-SelfTest`).
- **Docs:** `docs/architecture.md`, `docs/public-safety.md`, `docs/roadmap.md`.
