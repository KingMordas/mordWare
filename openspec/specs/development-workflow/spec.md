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

#### Scenario: Release build is checked too
- **WHEN** the solution is built in Release, as CI builds it, and the architecture tests run against that build
- **THEN** the tests check the app as built in that configuration and all pass, exactly as they do for a Debug build

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

### Requirement: Releases come only from a version tag on main
A release SHALL be published only by the automated release run that a pushed tag of the form `v<major>.<minor>.<patch>` starts, and only when the tagged commit is on the main branch. The release version SHALL be the tag's version.

#### Scenario: Tag on main publishes
- **WHEN** a tag `v1.2.3` pointing at a commit on the main branch is pushed
- **THEN** the release run builds, tests, packs and publishes release 1.2.3, and the installed app reports version 1.2.3

#### Scenario: Tag off main is refused
- **WHEN** a version tag pointing at a commit that is not on the main branch is pushed
- **THEN** the release run fails before packing and nothing is published

#### Scenario: Failing tests block the release
- **WHEN** a release run's tests fail
- **THEN** nothing is packed or published

### Requirement: Release dry run
The release process SHALL offer a manually started dry run that performs every release step except publishing, and keeps the installer it produced as a downloadable result of the run. The dry-run installer SHALL carry a pre-release version that names the dry run and its run number, that the packing step accepts, and that sorts below every real release version.

#### Scenario: Dry run publishes nothing
- **WHEN** the owner starts the release dry run
- **THEN** it builds, tests, packs and scans the installer, makes it downloadable from the run, and creates no release and no tag

#### Scenario: Dry run version
- **WHEN** the owner starts the release dry run with run number n
- **THEN** its installer is packed with version `0.1.0-dryrun.n`, which is a pre-release lower than 1.0.0, and packing does not refuse it

### Requirement: Packages are scanned before publishing
Before anything is published, the release run SHALL scan the packed release for personal, secret and machine-specific content. The project's own files SHALL contain none. A third-party file SHALL pass only if it is identical to the file its upstream package ships, except for the installer tool's own programs (next requirement).

#### Scenario: Local path in the app's own files
- **WHEN** a release package contains a file of the app's own with an absolute user-folder path or a private word
- **THEN** the scan fails, names the file, and nothing is published

#### Scenario: Clean package passes
- **WHEN** the scan runs on a package built by the release process from clean sources
- **THEN** it passes, and reports which third-party files it accepted as identical to upstream

### Requirement: Installer programs are checked against their originals
The installer tool builds its setup program, shortcut stub and updater for each app, so they cannot be identical to upstream. Each SHALL pass the scan only if it contains no flagged content beyond what the tool's pinned original of that program contains, and no private word. Text that names the PC doing the build blocks unless the pinned original contains exactly that text.

#### Scenario: Installer program adds flagged content
- **WHEN** the installer tool's setup program, shortcut stub or updater in a package contains an absolute user-folder path, e-mail address or package hash that the tool's pinned original does not contain
- **THEN** the scan fails, names the file, and nothing is published

#### Scenario: Build machine path that the original already contains
- **WHEN** the package is built on a machine whose user-folder path also appears, character for character, in the installer tool's pinned original of a setup program, shortcut stub or updater
- **THEN** that text does not fail the scan for that program

#### Scenario: Build machine path that the original does not contain
- **WHEN** a setup program, shortcut stub or updater in a package contains the build machine's user-folder path, user name or computer name, and the installer tool's pinned original does not contain that text
- **THEN** the scan fails, names the file, and nothing is published

#### Scenario: Private word in an installer program
- **WHEN** a setup program, shortcut stub or updater in a package contains a private word, even one its pinned original also contains
- **THEN** the scan fails, names the file, and nothing is published

### Requirement: Release notes come from the changelog
Each release's notes SHALL be that version's section of the changelog, followed by the license statement and the link to the version's source. A release run SHALL fail if the changelog has no section for its version.

#### Scenario: Missing changelog section
- **WHEN** a release run starts for a version that has no section in the changelog
- **THEN** it fails before publishing and names the missing section
