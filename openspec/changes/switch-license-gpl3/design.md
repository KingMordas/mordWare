# Design

## Context

See proposal.md (Why). The repository is private and holds an MIT `LICENSE` that has never been published. MIT is named in `README.md`, `CLAUDE.md` (overview), `docs/roadmap.md` (Direction, [R] scope) and the context line of `openspec/config.yaml`; the `public-repository` spec requires MIT. Every dependency is MIT or Apache-2.0. The owner has already limited pull requests to collaborators and turned Issues off.

## Goals / Non-Goals

**Goals:**
- An unambiguous GPL-3.0-only grant, with the standard text, that any reader or tool recognizes.
- No remaining MIT claim about mordWare itself. Third-party tools keep their own licenses: gitleaks, Velopack, LiveCharts2 and FlaUI are MIT and are named as such.

**Non-Goals:**
- Building the About screen or the release workflow. Their GPL obligations are recorded as parked notes for Foundation and [D].
- Choosing a contributor license agreement: there are no contributors.

## Decisions

### 1. How "only" is stated

`LICENSE` holds the official GPL-3.0 text from gnu.org (`gpl-3.0.txt`), unmodified, because the text says it may not be changed. The grant is stated where people and tools look:
- **README:** a "License" section that says "Copyright (C) 2026 KingMordas. Licensed under the GNU General Public License v3.0 only (GPL-3.0-only)" and repeats the no-warranty statement.
- **SPDX identifier `GPL-3.0-only`:** it appears in the README, and GitHub's license detection reads `LICENSE` as GPL-3.0.

The text's own "how to apply" appendix suggests "either version 3 ... or any later version". That clause is deliberately omitted everywhere.
- **Alternative considered:** append a "this program is licensed only under version 3" note to `LICENSE`. Rejected: changing the file breaks license detection and the text forbids changes. The README statement plus the SPDX id are the established way.

### 2. No per-file headers

Source files get no license header. The repository-level `LICENSE` and the README notice cover them, and the owner is the sole author.
- **Alternative considered:** an SPDX header in every source file. Rejected for now: it is noise across many files and adds nothing while there is one author. It can be added later by a script if the project ever needs it.

### 3. Obligations parked for later items

- **Foundation (Settings/About).** GPL-3.0 section 5(d): an interactive program shows "Appropriate Legal Notices": the copyright, that there is no warranty, that the program is GPL-3.0-only, and how to view the license and get the source (the GitHub repository). Localized in en-US and it-IT (guardrail 3).
- **[D] (release workflow).** GPL-3.0 section 6: the source must be available to whoever receives a binary. A GitHub Release attaches the tagged source automatically, and the release notes link to it. The installer also ships the `LICENSE` text next to `THIRD-PARTY-NOTICES`.

### 4. Dependency rule

Guardrail 1 gains "and GPL-3.0-compatible": MIT, BSD, Apache-2.0, MPL-2.0 and LGPL are fine; GPL-2.0-only and proprietary or "non-commercial" licenses are not. All current dependencies already comply, so nothing is replaced.

## Risks / Trade-offs

- [Someone redistributes a modified mordWare under GPL-3.0] → Allowed by design. They must publish their full source under the same license, so nobody can sell a closed copy.
- [A future dependency is GPL-incompatible] → The license check in guardrail 1 catches it before adoption.
- [The README notice and `LICENSE` drift apart] → Both are short and part of this change's checks. The public-safety scan does not police licensing; the spec scenario does.
- [The owner later wants commercial terms (for example on the Store)] → As sole copyright holder, the owner can license a Store edition under different terms. Nothing here prevents that.
