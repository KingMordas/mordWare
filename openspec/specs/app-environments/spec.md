# app-environments Specification

## Purpose

Lets the owner run a development edition and the real (production) edition of mordWare on the same PC without either one ever reading or writing the other's data.

## Requirements

### Requirement: Two distinct editions
mordWare SHALL exist as two editions: PROD, shown as "mordWare", and DEV, shown as "mordWare (DEV)". The edition SHALL be fixed when the app is built and SHALL NOT change at run time. Both editions SHALL be able to run on the same Windows account at the same time, without either one starting, replacing or reading the other.

#### Scenario: Both editions run side by side
- **WHEN** the installed PROD edition is running and the DEV edition is started from its build output on the same account
- **THEN** both run at the same time, each window shows its own edition's name, and closing one does not affect the other

#### Scenario: Both editions installed side by side
- **WHEN** the PROD edition is installed and the check-only DEV installer is then run on the same account
- **THEN** both remain installed, each appears in the Start menu under its own name, and starting one does not start or replace the other

#### Scenario: Uninstalling DEV leaves PROD intact
- **WHEN** both a check-only DEV installation and the PROD edition are installed, and the DEV one is uninstalled
- **THEN** the PROD edition stays installed and its data is untouched

#### Scenario: No run-time switch
- **WHEN** a build of either edition is started with any setting, argument or environment value
- **THEN** it runs as the edition it was built as

### Requirement: DEV edition is always recognizable
The DEV edition SHALL show a visible DEV marker in its main window, and its window and taskbar icon SHALL differ from PROD's. Wherever the DEV edition is installed for a check, its Start menu entry SHALL be named "mordWare (DEV)" and use the DEV icon. The PROD edition SHALL show no DEV marker.

#### Scenario: DEV window shows the marker
- **WHEN** the DEV edition starts
- **THEN** its main window shows a DEV marker and the DEV icon, in both English and Italian

#### Scenario: PROD window shows no marker
- **WHEN** the PROD edition starts
- **THEN** no DEV marker is shown anywhere in its window, and the window and taskbar show the PROD icon

#### Scenario: Check-only DEV install is labeled
- **WHEN** the check-only DEV installer is run
- **THEN** its Start menu entry is "mordWare (DEV)" with the DEV icon, separate from any "mordWare" entry

### Requirement: Debug and test runs are always DEV
A build produced for debugging or local testing SHALL always be the DEV edition. Only a release build SHALL produce the PROD edition.

#### Scenario: Debug run starts DEV
- **WHEN** the owner starts the app under a debugger or from the development script
- **THEN** the running app is the DEV edition and shows the DEV marker

#### Scenario: Release build produces PROD
- **WHEN** the app is built in the release configuration
- **THEN** the built app is the PROD edition, and its installer installs it as "mordWare"

#### Scenario: Automated check of the edition per configuration
- **WHEN** the automated test suite runs against a debug build and against a release build
- **THEN** it confirms the debug build is DEV and the release build is PROD, and fails otherwise

### Requirement: Per-edition data locations
Each edition SHALL keep its data in a folder of its own under the account's local application data: "mordWare Data" for PROD and "mordWare Data (DEV)" for DEV. Neither folder SHALL be inside the other or inside an installation folder. No setting, debug run or test run SHALL make the DEV edition resolve the PROD data location, or the reverse.

#### Scenario: Editions resolve different data folders
- **WHEN** each edition resolves its data folder
- **THEN** PROD resolves "mordWare Data", DEV resolves "mordWare Data (DEV)", the two are different, and neither is inside the other or inside an installation folder

#### Scenario: DEV from the build output uses the DEV folder
- **WHEN** the DEV edition runs from its build output, or from a check-only DEV installation
- **THEN** it reads and writes only the "mordWare Data (DEV)" folder

#### Scenario: Automated tests never touch real data
- **WHEN** the automated test suite runs on the owner's PC with both editions' data folders present
- **THEN** every test that needs a data folder uses a temporary folder created for that run, and neither edition's data folder is created, read or modified

### Requirement: Main window follows the app language
The main window title SHALL be shown in the app's selected language (English or Italian). Until a language setting exists, the selected language SHALL default to English (US).

#### Scenario: Default language
- **WHEN** the app starts for the first time
- **THEN** the window title is shown in US English

#### Scenario: Italian resources exist
- **WHEN** the app's language is Italian
- **THEN** the window title and the DEV marker are shown in Italian

### Requirement: DEV is never published
Only the PROD edition SHALL be published. A DEV installer SHALL exist only as a build made locally for checking installation and updates, and SHALL never be produced by the automated build or attached to a release.

#### Scenario: Release contains only PROD
- **WHEN** a release is published, or the release dry run produces its installer
- **THEN** it contains only the PROD edition, and no DEV installer or DEV update package exists in it

#### Scenario: Installer checks never use PROD
- **WHEN** an installation, upgrade or update check is run on the owner's PC
- **THEN** it uses the check-only DEV installer, and the PROD installation and the "mordWare Data" folder are not touched
