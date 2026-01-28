# Fire TV Screensaver Timeout Enforcer v1.0.0

**First stable release** of the portable Windows app that enforces 1-minute screensaver timeout on Fire TV devices via ADB.

## ?? What's New

This is the initial release featuring:

- ? **Fully Portable** - No installation required, runs from any folder
- ? **Self-Contained** - .NET 10 runtime is bundled, no dependencies
- ? **ADB Included** - All required tools (adb.exe, DLLs) are bundled
- ? **Clean UI** - Separate IP and Port fields for easy configuration
- ? **Auto-Save Settings** - Remembers your Fire TV IP and preferences
- ? **Real-Time Logging** - See exactly what's happening with timestamps
- ? **Smart Retry** - Continues working even if connection temporarily fails

## ?? Download

**Windows 11 x64 only**

Download `FireStickScreenSaverEnforcer-v1.0.0-win-x64.zip` below, extract to any folder, and run `FireStickScreenSaverEnforcer.exe`.

**File size:** ~150-200 MB (includes .NET runtime and ADB tools)

## ?? Quick Start

1. **Enable ADB** on your Fire TV:
   - Settings ? My Fire TV ? About ? Click "Build" 7 times
   - Settings ? My Fire TV ? Developer Options ? Enable "ADB Debugging"

2. **Find your Fire TV IP**:
   - Settings ? My Fire TV ? About ? Network
   - Note the IP address (e.g., 192.168.1.50)

3. **Run the app**:
   - Enter your Fire TV's IP and port (default 5555)
   - Click "Start Enforcing"
   - Authorize the connection on your Fire TV (first time only)

## ?? Requirements

- Windows 11 x64
- Fire TV Stick with ADB debugging enabled
- Same local network (Fire TV and PC on same WiFi/LAN)

## ?? What It Does

The app automatically:
1. Connects to your Fire TV via ADB every 30 seconds (configurable)
2. Checks the current screensaver timeout setting
3. Forces it to 60000ms (1 minute) if Fire OS reverted it to 5 minutes

Fire OS loves to reset this setting after sleep/wake - this app keeps it at 1 minute permanently.

## ?? Known Issues

None reported yet! Please open an issue if you find any problems.

## ?? Full Changelog

See [CHANGELOG.md](https://github.com/Rolling2405/FireStickScreenSaverEnforcer/blob/master/CHANGELOG.md)

## ?? Acknowledgments

- Google for Android platform-tools (ADB)
- Microsoft for WinUI 3 and Windows App SDK

---

**Need help?** Check the [README](https://github.com/Rolling2405/FireStickScreenSaverEnforcer#readme) for detailed setup instructions and troubleshooting.
