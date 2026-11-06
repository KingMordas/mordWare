# Design

## Context

See proposal.md (Why). The current check (`scripts/check-public-safety.ps1`, `-Package`, documented in `docs/architecture.md`, "Public-safety checks"):

- `Test-OwnPackageBytes` reports at most one `this-user` finding per file (the first match of the profile path, user name or computer name), without its matched text (`Raw` is empty).
- `Test-VelopackDerivedBytes` subtracts every finding whose `Rule|Raw` also occurs in the pinned vendor binaries, except `this-user` and `private-word`, which always block.
- On a GitHub runner, `%USERPROFILE%` is the runner's standard user folder, the same path Velopack's vendor binaries embed from their own build, so the three derived binaries always fail there. On the owner's PC the path differs, which is why local packs passed.

## Goals / Non-Goals

**Goals:**
- The derived-binary check treats a this-PC hit like the other rules: it blocks only if the vendor binary lacks that exact text.

**Non-Goals:**
- No change for own files, byte-identical third-party files or the repository modes. No CI-specific switch.

## Decisions

### D1. this-PC findings carry their text, one finding per distinct match

`Test-OwnPackageBytes` records each distinct this-PC match (profile path, user name or computer name) as its own finding, with the matched text in `Raw` (never printed; `Detail` stays masked). That makes the vendor comparison exact, and a new, different match can no longer hide behind a first one that the vendor also has (the same lesson as the user-path fix in `delivery-unpackaged`). Own files are unaffected: any this-PC finding there still blocks.

### D2. Only private words always block in derived binaries

In `Test-VelopackDerivedBytes`, a finding blocks when its rule is `private-word`, or when its `Rule|Raw` does not occur in the pinned vendor binaries. `this-user` joins user-path, e-mail and package-hash in that comparison. The vendor binaries are computed with the same function on the same machine, so their this-PC hits are the text of the build profile as it appears in Velopack's upstream bytes.

*Why this is safe:* the vendor binaries are the pinned vpk package from the NuGet cache. A string that occurs in them byte for byte was put there by Velopack's build, not by ours. Private words stay unconditional because they are the owner's own words, which have no business in any shipped file.

*Alternatives considered:*
- Skip the this-PC rule on GitHub Actions: rejected, it would also stop checking our own files there.
- Compare only the masked `Detail`: rejected, masked values can collide.

### D3. Self-test

`-SelfTest` gains two cases with a vendor folder whose `stub.exe` contains the current profile path:
- a derived `*_ExecutionStub.exe` with that same text passes;
- one that adds the current computer name (absent from the vendor sample) fails with a `this-user` finding.

The existing private-word, user-path and clean cases stay. The case data is assembled at run time, so the script never contains a real path.

## Risks / Trade-offs

- [A build PC whose profile path happens to equal text inside Velopack's binaries passes for that text] → That text is identical to upstream content, so nothing of the build PC is added. Any other this-PC text, and every private word, still blocks.
- DEV/PROD (guardrail 7) and data are not touched: the scan only reads packed files.

## Migration Plan

Land on `main`, push with the owner's approval, start the dry run again (#3). Rollback: revert the commit.
