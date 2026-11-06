# Spec Delta

## MODIFIED Requirements

### Requirement: Release dry run
The release process SHALL offer a manually started dry run that performs every release step except publishing, and keeps the installer it produced as a downloadable result of the run. The dry-run installer SHALL carry a pre-release version that names the dry run and its run number, that the packing step accepts, and that sorts below every real release version.

#### Scenario: Dry run publishes nothing
- **WHEN** the owner starts the release dry run
- **THEN** it builds, tests, packs and scans the installer, makes it downloadable from the run, and creates no release and no tag

#### Scenario: Dry run version
- **WHEN** the owner starts the release dry run with run number n
- **THEN** its installer is packed with version `0.1.0-dryrun.n`, which is a pre-release lower than 1.0.0, and packing does not refuse it
