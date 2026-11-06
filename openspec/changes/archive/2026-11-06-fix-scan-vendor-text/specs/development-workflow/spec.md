# Spec Delta

## MODIFIED Requirements

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
