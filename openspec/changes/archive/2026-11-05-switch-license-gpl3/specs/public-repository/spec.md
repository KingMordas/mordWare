# Spec Delta

## MODIFIED Requirements

### Requirement: Open-source license and as-is notice
The repository SHALL carry the GNU General Public License version 3 as its only license, GPL-3.0-only with no "or any later version" grant, with the copyright held under the public project identity. Its front page SHALL state that mordWare is a free hobby project developed in spare time and provided as-is, without warranty or guaranteed support.

#### Scenario: Visitor reads the terms
- **WHEN** someone opens the repository's front page
- **THEN** they see that mordWare is licensed GPL-3.0-only, free, a spare-time hobby project, and provided as-is without warranty, and the license file is the unmodified GPL-3.0 text and names no private person

#### Scenario: No later-version grant
- **WHEN** someone reads the project's license statement
- **THEN** it names version 3 of the GPL only and does not offer "any later version"

## ADDED Requirements

### Requirement: No outside contributions
The project SHALL be open source but closed to contribution: its front page SHALL state that contributions are not accepted, and only the owner SHALL be able to open pull requests or change the code.

#### Scenario: Visitor wants to contribute
- **WHEN** someone reads the repository's front page looking for how to contribute
- **THEN** it states that mordWare does not accept contributions, and that forks are welcome under the license

#### Scenario: Outside pull request
- **WHEN** someone without write access tries to open a pull request or an issue on the repository
- **THEN** the repository does not let them
