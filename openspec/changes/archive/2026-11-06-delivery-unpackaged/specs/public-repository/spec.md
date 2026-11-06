# Spec Delta

## MODIFIED Requirements

### Requirement: Open-source license and as-is notice
The repository SHALL carry the GNU General Public License version 3 as its only license, GPL-3.0-only with no "or any later version" grant, with the copyright held under the public project identity. It SHALL also carry one additional permission under GPL-3.0 section 7, kept apart from the license text, that allows combining mordWare with the named Microsoft platform runtime it needs to run on Windows. Its front page SHALL state that mordWare is a free hobby project developed in spare time and provided as-is, without warranty or guaranteed support.

#### Scenario: Visitor reads the terms
- **WHEN** someone opens the repository's front page
- **THEN** they see that mordWare is licensed GPL-3.0-only with an additional permission for the Microsoft platform runtime, free, a spare-time hobby project, and provided as-is without warranty, and the license file is the unmodified GPL-3.0 text and names no private person

#### Scenario: No later-version grant
- **WHEN** someone reads the project's license statement
- **THEN** it names version 3 of the GPL only and does not offer "any later version"

#### Scenario: The additional permission is narrow
- **WHEN** someone reads the additional permission
- **THEN** it names the Microsoft components it covers, grants permission only to combine with and convey them, and grants nothing for any other non-free component

#### Scenario: Platform runtime data is disclosed
- **WHEN** someone reads the front page's privacy statement
- **THEN** it says that mordWare itself sends no data, and that the bundled Microsoft platform runtime may record usage data that Windows handles according to the user's Windows diagnostic-data setting
