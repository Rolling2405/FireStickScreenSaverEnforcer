# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0] - 2025-01-27

### Added

- Initial release
- WinUI 3 desktop application for Windows 11 x64
- Automatic enforcement of 1-minute screensaver timeout on Fire TV Stick
- Separate IP address and port input fields for cleaner UI
- Configurable check interval (10-600 seconds, default 30)
- Real-time activity log with timestamps and auto-scroll
- Portable deployment (no installer required)
- Settings persistence in `settings.json` next to executable
- ADB tools bundled (adb.exe, AdbWinApi.dll, AdbWinUsbApi.dll)
- ADB file validation on startup with helpful error messages
- Graceful error handling for connection failures (continues retrying)
- Start/Stop enforcement with proper cancellation
- Port validation (1-65535)
- Modern Windows 11 UI with Mica backdrop effect

### Technical

- Built with .NET 10 and Windows App SDK 1.8
- Self-contained deployment (includes .NET runtime)
- Unpackaged deployment (no MSIX/certificate required)
- Uses local `platform-tools` folder for ADB binaries
- CRLF line endings enforced via .gitattributes
