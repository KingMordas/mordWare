# Spec Delta

## Purpose

Defines what the public mordWare repository guarantees to anyone who reads, uses or forks it: an open-source license with an as-is notice, and content that never exposes secrets, personal data, machine-specific details or the internals of the earlier web app.

## ADDED Requirements

### Requirement: Open-source license and as-is notice
The repository SHALL carry the MIT license, with the copyright held under the public project identity, and SHALL state on its front page that mordWare is a free hobby project developed in spare time and provided as-is, without warranty or guaranteed support.

#### Scenario: Visitor reads the terms
- **WHEN** someone opens the repository's front page
- **THEN** they see that mordWare is MIT-licensed, free, a spare-time hobby project, and provided as-is without warranty, and the license file names no private person

### Requirement: Commits carry only the public identity
Every commit that is published SHALL carry the public project identity as author and committer, with a no-reply address, and SHALL never carry the owner's real name or personal e-mail.

#### Scenario: Published history shows the public identity
- **WHEN** anyone lists the authors and committers of the published history
- **THEN** every entry shows only the public project identity and its no-reply address

### Requirement: Content stays free of secrets and personal data
Published content SHALL contain no secrets: no keys, tokens, certificates, signing material, recovery keys or connection strings with real values. It SHALL also contain no personal data: no real names or contacts, and no real vehicle, house or finance records, in code, docs, fixtures or images.

#### Scenario: A secret is staged
- **WHEN** a commit is attempted with a staged change containing something that looks like a private key or access token
- **THEN** the commit is refused and the offending file and line are named

#### Scenario: Synthetic fixtures only
- **WHEN** a test or a screenshot needs data
- **THEN** the data is synthetic, and the screenshot comes from the DEV edition

### Requirement: Content is system-independent
Published content SHALL describe nothing specific to the owner's PC: no absolute user-folder paths, no user or machine names, no local folder layout and no installed-package identifiers. Paths SHALL be repository-relative, and external references SHALL be public addresses.

#### Scenario: A local path is staged
- **WHEN** a commit is attempted with a staged change containing an absolute path into a Windows user folder
- **THEN** the commit is refused and the offending file and line are named

#### Scenario: Owner-specific words are caught without being published
- **WHEN** a staged change contains a word from the owner's private deny list (for example, their real name)
- **THEN** the commit is refused, and the deny list itself is never part of the repository

### Requirement: No details of the earlier web app
Published content SHALL contain no details of the earlier web app that mordWare replaces: no location, structure, file, spec or type names, data model, hosting, backup or import mechanics, and no statement about who uses it. Behavior SHALL be specified as what mordWare does, on its own terms. The project's public website address MAY appear as the product's front page.

#### Scenario: Ported behavior is specified on its own
- **WHEN** a change specifies behavior ported from the earlier web app
- **THEN** its artifacts describe mordWare's behavior and cite no file, spec, path or internal detail of the earlier app

### Requirement: Safety checks run before commit and in CI
A check SHALL run automatically before every commit and in every CI run. In CI it SHALL scan the full published history. A failing check SHALL block the commit or fail the run, and bypassing it SHALL NOT be part of any workflow.

#### Scenario: CI scans history
- **WHEN** a push reaches the repository and any commit in its history contains a detectable secret
- **THEN** the CI run fails and names the finding

#### Scenario: Clean history passes
- **WHEN** CI runs on the history as published by this change
- **THEN** the safety scan passes

#### Scenario: Bypass attempt by the assistant is blocked
- **WHEN** the coding assistant tries to commit while skipping hooks, or tries to change the repository's visibility
- **THEN** the action is blocked, and only the owner can perform it

### Requirement: Going public requires a full review
The repository SHALL become public only after a full-history safety scan passes, a manual review against the published checklist is complete, and the owner gives explicit approval.

#### Scenario: Visibility change
- **WHEN** the repository is about to be made public
- **THEN** the full-history scan result and the completed checklist exist, and the owner has approved the step explicitly
