# app-environments Specification

## Purpose

Lets the owner run a development edition and the real (production) edition of mordWare on the same PC without either one ever reading or writing the other's data.

## Requirements

### Requirement: Two distinct editions
mordWare SHALL exist as two editions with distinct installed identities: PROD, shown as "mordWare", and DEV, shown as "mordWare (DEV)". Both SHALL be installable on the same Windows account at the same time, each with its own Start menu entry.

#### Scenario: Both editions installed side by side
- **WHEN** the PROD edition is installed and the DEV edition is then installed on the same account
- **THEN** both remain installed, each appears in the Start menu under its own name, and starting one does not start or replace the other

#### Scenario: Uninstalling DEV leaves PROD intact
- **WHEN** both editions are installed and the DEV edition is uninstalled
- **THEN** the PROD edition stays installed and its data is untouched

### Requirement: DEV edition is always recognizable
The DEV edition SHALL show a visible DEV marker in its main window, and its Start menu entry and taskbar icon SHALL differ from PROD's. The PROD edition SHALL show no DEV marker.

#### Scenario: DEV window shows the marker
- **WHEN** the DEV edition starts
- **THEN** its main window shows a DEV marker, in both English and Italian

#### Scenario: PROD window shows no marker
- **WHEN** the PROD edition starts
- **THEN** no DEV marker is shown anywhere in its window

### Requirement: Debug and test runs are always DEV
A build produced for debugging or local testing SHALL always be the DEV edition. Only a release build SHALL produce the PROD edition.

#### Scenario: Debug run starts DEV
- **WHEN** the owner starts the app under a debugger
- **THEN** the running app is the DEV edition and shows the DEV marker

#### Scenario: Release build produces PROD
- **WHEN** a release package is built
- **THEN** it installs as the PROD edition, shown as "mordWare"

### Requirement: Per-edition data locations
Each edition SHALL keep its data in a location that belongs to that edition alone. No setting, debug run or test run SHALL make the DEV edition resolve the PROD data location, or the reverse.

#### Scenario: Editions resolve different data folders
- **WHEN** both editions are installed and each resolves its data folder
- **THEN** the two folders are different, and neither is inside the other

#### Scenario: Automated tests never touch real data
- **WHEN** the automated test suite runs on the owner's PC with both editions installed
- **THEN** every test that needs a data folder uses a temporary folder created for that run, and neither edition's data folder is created, read or modified

### Requirement: Main window follows the app language
The main window title SHALL be shown in the app's selected language (English or Italian). Until a language setting exists, the selected language SHALL default to English (US).

#### Scenario: Default language
- **WHEN** the app starts for the first time
- **THEN** the window title is shown in US English

#### Scenario: Italian resources exist
- **WHEN** the app's language is Italian
- **THEN** the window title and the DEV marker are shown in Italian
