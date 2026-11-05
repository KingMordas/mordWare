# Tasks

## 1. License text and notice

- [ ] 1.1 Replace `LICENSE` with the official GPL-3.0 text downloaded from `https://www.gnu.org/licenses/gpl-3.0.txt`, unmodified (design decision 1).
  - Verify: its SHA-256 equals that of a second, independent download; it starts with "GNU GENERAL PUBLIC LICENSE / Version 3, 29 June 2007"; it names no private person.
- [ ] 1.2 Rewrite the README's terms section:
  - "Copyright (C) 2026 KingMordas", "GNU General Public License v3.0 only (GPL-3.0-only)", the no-warranty statement and the hobby / spare-time notice;
  - a short "Contributions" section: not accepted, forks are welcome under the license;
  - no "or any later version" anywhere.
  - Verify spec scenarios "Visitor reads the terms", "No later-version grant" and "Visitor wants to contribute" by reading the README; `git grep -n -i "later version"` finds nothing outside `LICENSE`.

## 2. Living docs

- [ ] 2.1 Update `CLAUDE.md`:
  - the overview says "free, open-source (GPL-3.0-only)", "closed to contribution", and that the owner may also distribute the app under other terms (for example through the Store);
  - guardrail 1 adds GPL-3.0 compatibility (design decision 4).
  - Verify there is no MIT claim about mordWare itself.
- [ ] 2.2 Update `docs/roadmap.md`:
  - Direction: GPL-3.0-only and closed to contribution; a Store edition remains possible later.
  - [R] scope: note that MIT was replaced by GPL-3.0-only before publication (change `switch-license-gpl3`).
  - Foundation: a parked note for the About section's legal notices (design decision 3).
  - [D]: parked notes for the source offer in each release and the license text in the installer.
  - Verify all four notes are present.
- [ ] 2.3 Update the context line of `openspec/config.yaml` to say GPL-3.0-only. Verify `openspec instructions proposal --change switch-license-gpl3 --json` still loads every rule without a warning.
- [ ] 2.4 Verify no MIT claim about mordWare remains: `git grep -n -E "\(MIT\)|MIT license|MIT-licensed"` lists only third-party tools (gitleaks, Velopack, LiveCharts2, FlaUI, SQLite3MC) and archived changes.

## 3. Repository settings and final checks

- [ ] 3.1 Verify spec scenario "Outside pull request" through the GitHub API (read-only): Issues are disabled, and pull-request creation is limited to collaborators. Record the result in generic terms.
- [ ] 3.2 Run `scripts/check-public-safety.ps1 -All` with the local deny list, and `openspec validate switch-license-gpl3 --strict`. Verify both pass.

## Workflow follow-up

- `/opsx:archive` this change: sync the `public-repository` spec, then make one Conventional Commit (`docs: ...`) as the public identity, passing the hooks.
- With the owner's OK, push to `main` and confirm that CI is green and that GitHub's About panel detects the license as GPL-3.0.
- Then the owner switches the repository to public and enables secret scanning and push protection. Next on the roadmap: [S4].
