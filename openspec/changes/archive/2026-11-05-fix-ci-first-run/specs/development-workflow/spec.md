# Spec Delta

## MODIFIED Requirements

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
