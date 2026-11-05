# development-workflow Specification

## Purpose

Defines how mordWare is developed and verified: enforced module boundaries, an automated build-and-test check with bounded run time, a single roadmap that answers "what's next", and the changelog of record.

## Requirements

### Requirement: Module boundaries are checked automatically
The automated test suite SHALL fail when the app's architecture rules are broken:
- Modules depend only on the shared core and shared infrastructure, never on the app or on another module's internals.
- Domain code depends on neither the database framework nor the UI framework.
- Shared core depends on no module.

#### Scenario: Module references another module
- **WHEN** code in one module references a type internal to another module
- **THEN** the architecture tests fail and name the offending dependency

#### Scenario: Domain code uses the database framework
- **WHEN** a domain type references the database framework or the UI framework
- **THEN** the architecture tests fail

#### Scenario: Clean skeleton passes
- **WHEN** the architecture tests run against the solution as delivered by this change
- **THEN** they all pass

### Requirement: Tests cannot reach the real data location
The automated test suite SHALL fail if test code depends on the component that resolves an installed edition's real data location.

#### Scenario: Test uses the real paths provider
- **WHEN** a test project references the real data-location resolver
- **THEN** the architecture tests fail

### Requirement: Continuous integration builds and tests
Every push to the main branch and every pull request SHALL trigger an automated run that builds the solution and runs the test suite. Every job in that run SHALL have a time limit.

#### Scenario: Failing test fails the run
- **WHEN** a pushed commit contains a failing test
- **THEN** the automated run is reported as failed

#### Scenario: Hung job is stopped
- **WHEN** a job exceeds its time limit
- **THEN** it is cancelled and the run is reported as failed

### Requirement: Roadmap answers "what's next"
The repository SHALL contain one roadmap document that lists every planned item in order. Each item SHALL show its status, its change name once it has one, its parked notes and its port references. The change that completes an item SHALL update the roadmap.

#### Scenario: Next step is derivable
- **WHEN** no change is in progress and the owner asks what's next
- **THEN** the first roadmap item not marked done identifies the next item and whether it needs exploring or proposing

#### Scenario: Completing an item updates the roadmap
- **WHEN** a change that completes a roadmap item is archived
- **THEN** that same change marks the item done in the roadmap

### Requirement: Changelog of record
The repository SHALL keep a changelog with an "Unreleased" section. Every user-visible change SHALL add an entry there.

#### Scenario: User-visible change logged
- **WHEN** a change that alters what the user sees or can do is archived
- **THEN** the changelog's Unreleased section contains an entry describing it
