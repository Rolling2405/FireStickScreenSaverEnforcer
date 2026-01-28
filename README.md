# Fire TV Screensaver Timeout Enforcer

A portable Windows desktop application that keeps your Fire TV Stick screensaver timeout forced to 1 minute (60 seconds) by periodically checking and correcting the setting via ADB.

## Why This App?

Fire OS has a frustrating behavior: it keeps reverting the screensaver timeout to 5 minutes (300000ms) after wake/resume events. This app runs in the background and automatically enforces your preferred 1-minute timeout by monitoring and correcting the setting at regular intervals.

## Features

- ?? **Automatic Enforcement**: Periodically checks and corrects the screensaver timeout
- ?? **Easy Setup**: Simple IP address configuration
- ?? **Configurable Interval**: Check every 10-600 seconds (default: 30 seconds)
- ?? **Activity Log**: See exactly what's happening with timestamped entries
- ?? **Portable**: Runs from any folder, no installation required
- ?? **No Certificate Required**: Unpackaged deployment, no MSIX signing needed

## Requirements

- **Windows 11** (x64)
- **Fire TV Stick** with ADB debugging enabled
- **Same local network** (Fire TV and PC must be on the same LAN)
- **.NET 10 Runtime** (or build as self-contained)

## How It Works

The app uses ADB (Android Debug Bridge) to communicate with your Fire TV Stick:

1. **Connects** to the Fire TV via `adb connect <ip>:5555`
2. **Reads** the current timeout: `adb shell settings get system screen_off_timeout`
3. **Corrects** if needed: `adb shell settings put system screen_off_timeout 60000`
4. **Repeats** at the configured interval

The target timeout is 60000ms (1 minute / 60 seconds).

## Setup Instructions

### 1. Enable ADB Debugging on Fire TV

1. On your Fire TV, go to **Settings** ? **My Fire TV** ? **About**
2. Click on **Build** 7 times rapidly to enable Developer Options
3. Go back to **My Fire TV** ? **Developer Options**
4. Enable **ADB Debugging**
5. (Optional but recommended) Enable **Apps from Unknown Sources** for network authorization

### 2. Find Your Fire TV's IP Address

1. On your Fire TV, go to **Settings** ? **My Fire TV** ? **About** ? **Network**
2. Note the IP address (e.g., `192.168.1.50`)
3. For best results, assign a static IP to your Fire TV in your router settings

### 3. Download Platform-Tools

You need the Android platform-tools (containing `adb.exe`):

1. Download from: https://developer.android.com/studio/releases/platform-tools
2. Extract the ZIP file
3. Copy these 3 files to a `platform-tools` folder next to the app executable:
   - `adb.exe`
   - `AdbWinApi.dll`
   - `AdbWinUsbApi.dll`

Your folder structure should look like:
```
FireStickScreenSaverEnforcer/
??? FireStickScreenSaverEnforcer.exe
??? platform-tools/
?   ??? adb.exe
?   ??? AdbWinApi.dll
?   ??? AdbWinUsbApi.dll
??? (other app files)
```

### 4. First-Time ADB Authorization

The first time you connect via ADB, your Fire TV will show an authorization prompt:

1. Run the app and click **Start Enforcing**
2. Look at your Fire TV screen for the "Allow USB debugging?" prompt
3. Check "Always allow from this computer"
4. Click **OK**

If you miss this prompt, the app will keep retrying until authorized.

## Building from Source

### Prerequisites

- **Visual Studio 2022** (17.x or later) or **Visual Studio 2025/2026**
- **.NET 10 SDK**
- **Windows App SDK** workload installed
- **Windows 11 SDK** (10.0.19041.0 or later)

### Build Steps

1. Clone the repository
2. Open `FireStickScreenSaverEnforcer.sln` in Visual Studio
3. Select **x64** as the target platform
4. Build the solution (Ctrl+Shift+B)
5. The output will be in `bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64\`

### Publish as Self-Contained

For a fully portable deployment that doesn't require .NET 10 to be installed:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true
```

## Running the App

1. Navigate to the output folder (or published folder)
2. Ensure `platform-tools` folder contains the ADB files
3. Run `FireStickScreenSaverEnforcer.exe`
4. Enter your Fire TV's IP address (e.g., `192.168.1.50:5555`)
5. Set your preferred check interval (default: 30 seconds)
6. Click **Start Enforcing**

Settings are automatically saved to `settings.json` next to the executable.

## Troubleshooting

### "Missing ADB files" Error

Make sure you have copied `adb.exe`, `AdbWinApi.dll`, and `AdbWinUsbApi.dll` to the `platform-tools` folder next to the executable.

### "Connection failed" or "device offline"

- Verify the Fire TV IP address is correct
- Ensure both devices are on the same network
- Check that ADB debugging is enabled on Fire TV
- Try restarting the Fire TV
- If you previously authorized ADB, try revoking and re-authorizing

### Windows Firewall Prompt

When first connecting, Windows Firewall may ask to allow `adb.exe`. Click **Allow** for private networks.

### ADB Authorization Prompt Not Appearing

1. Run `adb disconnect` then `adb connect <ip>:5555` manually from command line
2. The prompt should appear on the Fire TV
3. If not, try: Settings ? My Fire TV ? Developer Options ? Revoke USB debugging authorizations
4. Then try connecting again

### Fire TV IP Address Changes

If your Fire TV gets a new IP address (DHCP), you'll need to update the app. Consider assigning a static IP in your router's DHCP settings.

### Timeout Keeps Reverting Anyway

This is expected behavior - that's why this app exists! The app will catch and correct the reversion within your configured interval. If it happens too often, reduce the check interval.

## Security Considerations

?? **ADB Debugging Security Warning**

Enabling ADB debugging on your Fire TV has security implications:

- Any device on your network can potentially connect to your Fire TV
- An authorized computer has significant control over the device
- Consider disabling ADB when not actively needed

This app only modifies the `screen_off_timeout` setting and does not access any personal data.

## File Structure

```
FireStickScreenSaverEnforcer/
??? FireStickScreenSaverEnforcer.exe    # Main application
??? settings.json                        # Your saved settings (auto-created)
??? platform-tools/                      # ADB binaries (you must add these)
?   ??? adb.exe
?   ??? AdbWinApi.dll
?   ??? AdbWinUsbApi.dll
??? (runtime files)
```

## Project Structure (Source)

```
FireStickScreenSaverEnforcer.App/
??? App.xaml / App.xaml.cs              # Application entry point
??? MainWindow.xaml / MainWindow.xaml.cs # Main UI and logic
??? Models/
?   ??? AppSettings.cs                   # Settings model
??? Services/
?   ??? AdbRunner.cs                     # ADB command execution
?   ??? SettingsService.cs               # Settings persistence
??? platform-tools/                      # (Add ADB files here for development)
```

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

### Platform-Tools License

The Android platform-tools (adb.exe, etc.) are provided by Google under their own license terms. You must download them directly from Google and comply with their license. This repository does not include platform-tools binaries.

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for version history.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## Acknowledgments

- Google for providing the Android platform-tools
- Microsoft for WinUI 3 and Windows App SDK
