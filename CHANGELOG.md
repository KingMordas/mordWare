# Changelog

All user-visible changes to mordWare are recorded here. The `## [Unreleased]` section is the changelog of record:
every user-visible change adds an entry in the same change. Entries follow [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added

- mordWare opens an empty window, in US English (Italian ready).
- mordWare installs for your Windows account through an installer, without administrator rights, with a Start menu entry and an installed-apps entry. A newer installer upgrades it in place, and uninstalling keeps your data in `%LOCALAPPDATA%\mordWare Data`.
- Every installation carries the license, its additional permission for the Microsoft platform runtime, and the third-party notices.
- A separate development edition, "mordWare (DEV)", with its own icon, an orange DEV badge and its own data folder (`%LOCALAPPDATA%\mordWare Data (DEV)`), which never shares data with the real "mordWare".

### Changed

- The development edition runs from its build output and is no longer installed as a Windows package; Windows Developer Mode is no longer needed.
- mordWare is built for x64 only, and no longer bundles the Windows ML, AI, Search and Widgets runtimes it does not use.
