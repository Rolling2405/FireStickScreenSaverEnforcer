# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.5.0] - 2026-XX-XX

### Changed
- **HDMI-CEC Prime Pulse:** Now only runs once at startup instead of on every enforcement tick, improving reliability and reducing unnecessary toggling.

## [1.4.0] - 2026-02-19


## [1.4.0] - 2026-02-19

### ✨ New Features
- **HDMI-CEC Prime Pulse (ON→OFF):**  
  The app now automatically performs a “prime pulse” (toggles HDMI-CEC Device Control ON, waits, then OFF) via ADB settings on every enforcement tick. This fixes the issue where the Fire TV Stick home screen appears instead of the screensaver after setting the timeout to “Never.” The app robustly discovers the correct settings key and always leaves HDMI-CEC OFF.

### 🛠 Improvements
- The discovered HDMI-CEC key is cached in settings for faster future runs.
- All CEC and ADB actions are logged in the activity log for transparency and troubleshooting.
- **Maximum enforcement interval reduced to 30 seconds** (previously 600 seconds) for more responsive monitoring while keeping the minimum at 10 seconds.

### 🔒 Security Enhancements
- **Comprehensive Input Validation:**  
  - All user inputs (IP address, port, interval, timeout) are now strictly validated before use
  - IP addresses validated using regex patterns and .NET IPAddress parsing
  - Port numbers validated to be in valid range (1-65535)
  - Interval constrained to 10-30 seconds with validation
  - Settings key names and namespaces validated against safe character sets

- **Output Sanitization:**  
  - All ADB command output sanitized before logging or display
  - Dangerous control characters removed from logs (prevents log injection)
  - Exception messages sanitized for safe display
  - Log output length-limited to prevent flooding

- **Command Injection Prevention:**  
  - Dangerous shell characters (`&`, `|`, `;`, `$`, etc.) removed from all command arguments
  - IP:Port combinations validated before ADB connect
  - Settings values restricted to safe ranges (0/1 for CEC toggles)

- **Enhanced Data Integrity:**  
  - AppSettings model validates all properties on set
  - Invalid settings automatically fall back to safe defaults
  - Cached CEC keys validated on load and save

### 🛠 Security Improvements
- Added centralized `SecurityHelper` class with reusable validation and sanitization methods
- Enhanced error messages with specific validation failure details
- Improved defense-in-depth with multiple validation layers
- All validation uses compiled regex patterns for performance

### 📚 Documentation
- Added comprehensive `SECURITY_ENHANCEMENTS.md` documenting all security features

### 🔧 Technical
- Follows Microsoft Security Development Lifecycle (SDL) best practices
- Aligns with OWASP Top 10 input validation guidelines
- Zero impact on existing functionality - fully backwards compatible
- Uses .NET 10 modern features (partial methods for generated regex)

### 🐞 Bug Fixes
- None specific to this release.

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
