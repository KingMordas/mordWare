# Spec Delta

## MODIFIED Requirements

### Requirement: Safety checks run before commit and in CI
A check SHALL run automatically before every commit and in every CI run. In CI it SHALL scan the full published history. A failing check SHALL block the commit or fail the run, and bypassing it SHALL NOT be part of any workflow.

#### Scenario: CI scans history
- **WHEN** a push reaches the repository and any commit in its history contains a detectable secret
- **THEN** the CI run fails and names the finding

#### Scenario: Clean history passes
- **WHEN** CI runs on the history as published by this change
- **THEN** the safety scan passes

#### Scenario: Bypass attempt by the assistant is blocked
- **WHEN** the coding assistant tries to commit while skipping hooks, or tries to change the repository's visibility
- **THEN** the action is blocked, and only the owner can perform it

#### Scenario: No private deny list
- **WHEN** the checks run where no private deny list exists, as in CI or a fresh clone
- **THEN** every other rule still runs, clean content passes, and the run notes that private words were not checked
