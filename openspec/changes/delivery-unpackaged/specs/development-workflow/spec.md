# Spec Delta

## ADDED Requirements

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
The release process SHALL offer a manually started dry run that performs every release step except publishing, and keeps the installer it produced as a downloadable result of the run.

#### Scenario: Dry run publishes nothing
- **WHEN** the owner starts the release dry run
- **THEN** it builds, tests, packs and scans the installer, makes it downloadable from the run, and creates no release and no tag

### Requirement: Packages are scanned before publishing
Before anything is published, the release run SHALL scan the packed release for personal, secret and machine-specific content. The project's own files SHALL contain none. A third-party file SHALL pass only if it is identical to the file its upstream package ships.

#### Scenario: Local path in the app's own files
- **WHEN** a release package contains a file of the app's own with an absolute user-folder path or a private word
- **THEN** the scan fails, names the file, and nothing is published

#### Scenario: Clean package passes
- **WHEN** the scan runs on a package built by the release process from clean sources
- **THEN** it passes, and reports which third-party files it accepted as identical to upstream

### Requirement: Release notes come from the changelog
Each release's notes SHALL be that version's section of the changelog, followed by the license statement and the link to the version's source. A release run SHALL fail if the changelog has no section for its version.

#### Scenario: Missing changelog section
- **WHEN** a release run starts for a version that has no section in the changelog
- **THEN** it fails before publishing and names the missing section
