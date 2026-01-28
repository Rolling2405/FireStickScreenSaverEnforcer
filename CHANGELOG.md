# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
