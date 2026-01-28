# Publishing to GitHub - Complete Guide

## Prerequisites ?

You already have:
- ? Git repository initialized
- ? Remote configured: https://github.com/Rolling2405/FireStickScreenSaverEnforcer
- ? Code ready to publish

## Step 1: Build Release Package

Run the build script:

```powershell
.\build-release.ps1
```

This will:
- Clean previous builds
- Restore NuGet packages
- Build Release configuration with self-contained .NET runtime
- Create `FireStickScreenSaverEnforcer-v1.0.0-win-x64.zip`

**Output:** ~150-200 MB ZIP file ready for distribution

## Step 2: Commit & Push to GitHub

### Option A: Using Visual Studio (Recommended)

1. **View** ? **Git Changes** (`Ctrl+0, Ctrl+G`)
2. Review changed files
3. Stage all changes (click **+** next to "Changes")
4. Commit message:
   ```
   Release v1.0.0: Initial stable release
   
   - Self-contained WinUI 3 app with bundled .NET 10 runtime
   - ADB tools included for portable deployment
   - Separate IP/Port fields for cleaner UI
   - Auto-save settings to settings.json
   ```
5. Click **Commit All and Push**

### Option B: Using PowerShell Script

```powershell
.\git-push.ps1
```

Follow the prompts to commit and push.

## Step 3: Create GitHub Release

1. Go to: https://github.com/Rolling2405/FireStickScreenSaverEnforcer/releases/new

2. **Choose a tag:**
   - Type: `v1.0.0`
   - Click "Create new tag: v1.0.0 on publish"

3. **Release title:**
   ```
   v1.0.0 - Initial Release
   ```

4. **Description:**
   Copy the content from `RELEASE_NOTES.md` (already created for you)

5. **Attach binaries:**
   - Drag and drop `FireStickScreenSaverEnforcer-v1.0.0-win-x64.zip`

6. **Set as latest release:** ? (checked)

7. Click **Publish release**

## Step 4: Verify

After publishing, check:

- ? Release appears at: https://github.com/Rolling2405/FireStickScreenSaverEnforcer/releases
- ? ZIP file is downloadable
- ? README shows the download link correctly

## Testing the Release

Download the ZIP you just published and test:

1. Extract to a test folder (e.g., `C:\Temp\FireTV-Test`)
2. Run `FireStickScreenSaverEnforcer.exe`
3. Verify it runs without requiring .NET installation
4. Verify platform-tools folder is present

## Future Updates

For future releases:

1. Update version in:
   - `FireStickScreenSaverEnforcer.App.csproj` (line 23)
   - `CHANGELOG.md` (add new section)
   - `build-release.ps1` (line 11)

2. Run `.\build-release.ps1`
3. Commit and push
4. Create new GitHub release with new tag (e.g., `v1.1.0`)

---

## Quick Reference

| File | Purpose |
|------|---------|
| `build-release.ps1` | Build and package for release |
| `git-push.ps1` | Quick commit and push helper |
| `RELEASE_NOTES.md` | Template for GitHub release description |
| `CHANGELOG.md` | Version history |

---

**Questions?** Open an issue or check the [README](README.md).
