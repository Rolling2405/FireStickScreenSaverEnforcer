# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.3.0] - 2026-02-11

### Added

- Dream/screensaver system enforcement: The app now periodically checks and automatically re-enables the Fire TV Dream/screensaver system (`screensaver_enabled`, `screensaver_activate_on_sleep`, `screensaver_activate_on_dock`) to prevent the "black screen" issue on Fire OS 6 devices when screensaver is set to "Never".
- Amazon Photos fallback: If `screensaver_components` is missing or empty, the app sets it to Amazon Photos for a consistent screensaver experience.

### Changed

- Integrated Dream/screensaver checks into the existing enforcement loop; self-heal runs immediately when enforcement starts and on the configured interval.
- Logging improved for Dream enforcement and ADB errors; avoid spamming logs when no change is needed.

### Packaging

- Published as a portable, unpackaged WinUI 3 app for Windows x64 (.NET 10, self-contained).


## [1.0.0] - 2025-01-XX

### Added

- Initial release
- WinUI 3 desktop application for Windows 11 x64
- Automatic enforcement of 1-minute screensaver timeout on Fire TV Stick
- Configurable check interval (10-600 seconds)
- Activity log with timestamps
- Portable deployment (no installer required)
- Settings persistence in `settings.json`
- ADB file validation on startup
- Graceful error handling for connection failures
- Auto-scroll log display

### Technical

- Built with .NET 10 and Windows App SDK 1.8
- Unpackaged deployment (no MSIX/certificate required)
- Uses local `platform-tools` folder for ADB binaries
