# Proposal

## Why

The owner wants mordWare public, for unlimited CI minutes, for trust through readable code, and for the GitHub Releases delivery path. But the app stays the owner's: no outside contributions, and the option to sell it or ship it on the Microsoft Store later. MIT would let anyone take a published version, close it and sell a rebranded copy. **GPL-3.0-only** keeps the project open source, forces any redistributed copy to stay open under the same license, and leaves the owner, as sole copyright holder, free to sell, dual-license or relicense.

The repository is still private, so no version has been published under MIT. Switching now leaves no MIT-licensed copy in anyone's hands.

## What Changes

- **License.** `LICENSE` becomes the official, unmodified GPL-3.0 text, with "GPL-3.0-only" stated as the terms (no "or any later version"). The copyright line is `Copyright (C) 2026 KingMordas`.
- **No contributions.** The project is open source but closed to contribution. The README says so. The owner has already set GitHub: pull requests are limited to collaborators (only the owner) and Issues are off.
- **Living docs.** The MIT and "open-source (MIT)" wording in `README.md`, `CLAUDE.md`, `docs/roadmap.md` and `openspec/config.yaml` becomes GPL-3.0-only.
- **Guardrail 1** adds that every dependency must be GPL-3.0-compatible (MIT, BSD and Apache-2.0 are; GPL-2.0-only is not).
- **Parked notes** for the obligations GPL-3.0 adds later:
  - **Foundation:** the app's About section shows the "Appropriate Legal Notices" (copyright, no warranty, the license and where to get the source).
  - **[D]:** each release offers the corresponding source and ships the license text with the installer.

### Scope

- **In:** `LICENSE`, `README.md`, `CLAUDE.md`, `docs/roadmap.md`, `docs/architecture.md` (if it mentions the license), `openspec/config.yaml` (context line), the `public-repository` spec, the roadmap parked notes, then commit, push and green CI.
- **Out:**
  - per-file license headers (design decision 2);
  - the About screen itself (Foundation);
  - the release workflow (D);
  - GitHub settings (done by the owner);
  - switching the repository to public (the owner, after this change).

### Guardrails touched

- **1 (dependencies):** GPL-3.0 compatibility is now part of the license check.
- **10 (living docs):** this whole change.
- **11 (public, as-is):** the as-is wording stays; GPL-3.0 sections 15-16 carry the no-warranty terms.

### User-visible

No. Nothing in the app changes, so there is no CHANGELOG entry. The About screen that will show the license notice comes with Foundation.

### Roadmap item

A follow-up to **[R] Open source and public repository**, done before the repository goes public.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `public-repository`:
  - "Open-source license and as-is notice" now requires GPL-3.0-only instead of MIT.
  - A new requirement says the project accepts no outside contributions.

## Impact

- **Files:** `LICENSE`, `README.md`, `CLAUDE.md`, `docs/roadmap.md`, `openspec/config.yaml`, and `openspec/specs/public-repository/spec.md` (at archive).
- **No code, test, dependency or CI changes.** Every current dependency (MIT or Apache-2.0) is GPL-3.0-compatible.
- **Git:** one commit as the public identity, pushed with the owner's OK; CI must be green before the owner switches the repository to public.
