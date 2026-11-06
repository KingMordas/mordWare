# app-delivery Specification

## Purpose

Defines how a user gets, upgrades and removes mordWare: a per-user installer published with each release, data that outlives the installation, and the legal texts and source offer every release carries.

## Requirements

### Requirement: Per-user installation without administrator rights
mordWare SHALL be delivered as an installer that installs it for the current Windows account only, without administrator rights, and adds a Start menu entry named "mordWare". The installer SHALL NOT add a desktop shortcut.

#### Scenario: Install from the installer
- **WHEN** a user without administrator rights runs the mordWare installer
- **THEN** mordWare is installed for that account, appears in the Start menu as "mordWare" and in the account's list of installed apps, no elevation prompt appears, and no desktop shortcut is created

#### Scenario: Installed app opens its window
- **WHEN** mordWare is started from its Start menu entry after installation
- **THEN** its main window opens and shows the window title in the app language

### Requirement: Upgrade in place
Running the installer of a newer version over an installed mordWare SHALL upgrade it in place, keeping one Start menu entry and one installed-apps entry.

#### Scenario: Newer installer over an older install
- **WHEN** an installed mordWare is upgraded by running a newer version's installer
- **THEN** the newer version runs from the same Start menu entry, and no second entry exists

### Requirement: Data outlives the installation
The app's data SHALL be kept outside the installation folder. Upgrading, reinstalling or uninstalling mordWare SHALL leave the data of both editions untouched.

#### Scenario: Uninstall keeps the data
- **WHEN** an installed mordWare is uninstalled
- **THEN** the program files, the Start menu entry and the installed-apps entry are removed, and the edition's data folder and its contents remain

#### Scenario: Upgrade keeps the data
- **WHEN** an installed mordWare that has written to its data folder is upgraded
- **THEN** the data folder and its contents are unchanged after the upgrade

#### Scenario: DEV data untouched by PROD installs
- **WHEN** the PROD edition is installed, upgraded or uninstalled on an account where the DEV edition has data
- **THEN** the DEV edition's data folder is never created, read, changed or removed by it

### Requirement: Legal texts ship with every release
Every installed mordWare and every published release SHALL include the GPL-3.0 license text, the project's GPL-3.0 section 7 additional permission, and third-party notices that name every redistributed component together with its license terms.

#### Scenario: Installed files carry the legal texts
- **WHEN** someone opens the installation folder of an installed mordWare
- **THEN** it contains the license, the additional permission and the third-party notices, and the notices list every bundled third-party component with its license

### Requirement: Corresponding source offered with every release
Every published release SHALL state that mordWare is licensed GPL-3.0-only and SHALL link to the complete corresponding source of exactly that version.

#### Scenario: Release page offers the source
- **WHEN** someone opens a mordWare release on the releases page
- **THEN** the release notes name the license and link to the source archive of the tag that release was built from

### Requirement: The app opens no network connection of its own
Apart from features that are specified to use the network, mordWare SHALL open no network connection. Until such a feature exists, a running mordWare SHALL open none at all.

#### Scenario: Idle installed app
- **WHEN** the installed mordWare runs with its main window open for several minutes
- **THEN** no network connection owned by the mordWare process is observed

### Requirement: Unsigned installer is explained
The project's front page SHALL explain, before download, that mordWare is not code-signed, which warnings the browser and Windows may show the first time, how to proceed safely, that the installer needs no administrator rights, where the data stays after uninstalling and how to remove it completely.

#### Scenario: Visitor reads how to install
- **WHEN** someone reads the front page's section on getting mordWare
- **THEN** it explains the unsigned-installer warnings, says to download only from the project's releases page, says that no administrator rights are needed, and says where the data stays after uninstalling and how to remove it
