# Bundling Windows App SDK Runtime with Your App

## ? Yes, You Can Bundle It!

I've created a solution that bundles the runtime installer with your app and auto-installs it on first launch.

---

## What I've Added

### 1. RuntimeBootstrapper.cs
**Location:** `FireStickScreenSaverEnforcer.App\Services\RuntimeBootstrapper.cs`

This service:
- ? Checks if Windows App SDK Runtime is installed
- ? Prompts user to install if missing
- ? Auto-runs the bundled installer with admin elevation
- ? Restarts the app after installation

### 2. Updated App.xaml.cs
Now checks for the runtime on startup and prompts for installation if needed.

---

## How to Use

### Step 1: Download the Runtime Installer

```powershell
# Download the installer
$url = "https://aka.ms/windowsappsdk/1.8/latest/windowsappruntimeinstall-x64.exe"
$output = "windowsappruntimeinstall-x64.exe"
Invoke-WebRequest -Uri $url -OutFile $output
```

### Step 2: Add to Your Publish Folder

Copy `windowsappruntimeinstall-x64.exe` to:
```
FireStickScreenSaverEnforcer.App\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish\
```

### Step 3: Update .csproj to Include the Installer

Add this to your `.csproj`:

```xml
<!-- Bundle Windows App SDK Runtime Installer -->
<ItemGroup>
  <None Include="windowsappruntimeinstall-x64.exe" Condition="Exists('windowsappruntimeinstall-x64.exe')">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

### Step 4: Rebuild

```powershell
dotnet publish -c Release -r win-x64 --self-contained true
```

---

## How It Works for Users

### First Launch (Without Runtime):

1. User extracts ZIP and runs `FireStickScreenSaverEnforcer.exe`
2. App detects missing runtime
3. Shows dialog: "Windows App SDK Runtime Required"
4. User clicks "Install"
5. App auto-runs the bundled installer (requests admin permission)
6. Installer completes (~1 minute)
7. App shows "Installation Complete - Please restart"
8. App restarts automatically
9. **App now works!**

### Subsequent Launches:

- Runtime check passes instantly
- App launches normally
- No prompts or delays

---

## Benefits

| Approach | User Experience | File Size | Complexity |
|----------|----------------|-----------|------------|
| **Without Bundling** | ? Must download separately | 99 MB | ? Simple |
| **With Bundling** | ? One-click install | 108 MB | ?? Medium |
| **MSIX Package** | ? Fully automatic | 150 MB | ??? Complex |

---

## Alternative: Download on Demand

If you don't want to bundle the 9MB installer, you can download it on first launch:

```csharp
public static async Task<bool> DownloadAndInstallRuntimeAsync()
{
    const string runtimeUrl = "https://aka.ms/windowsappsdk/1.8/latest/windowsappruntimeinstall-x64.exe";
    var tempPath = Path.Combine(Path.GetTempPath(), "windowsappruntimeinstall-x64.exe");
    
    // Download
    using var client = new HttpClient();
    var bytes = await client.GetByteArrayAsync(runtimeUrl);
    await File.WriteAllBytesAsync(tempPath, bytes);
    
    // Install
    var process = Process.Start(new ProcessStartInfo
    {
        FileName = tempPath,
        UseShellExecute = true,
        Verb = "runas"
    });
    
    await Task.Run(() => process?.WaitForExit());
    return process?.ExitCode == 0;
}
```

---

## Recommendation

**For v1.0.1:** Bundle the installer and use the RuntimeBootstrapper I created.

**Pros:**
- ? One-click setup for users
- ? No separate downloads needed
- ? Professional user experience
- ? Only adds 9MB to your ZIP

**Cons:**
- ?? Increases ZIP size to ~108 MB (was 99 MB)
- ?? Requires admin elevation on first launch

---

## Next Steps

1. **Test the RuntimeBootstrapper:**
   ```powershell
   # Build with the new code
   dotnet build
   
   # Run from Visual Studio to test the dialog
   ```

2. **Download the installer:**
   ```powershell
   Invoke-WebRequest -Uri "https://aka.ms/windowsappsdk/1.8/latest/windowsappruntimeinstall-x64.exe" -OutFile "FireStickScreenSaverEnforcer.App\windowsappruntimeinstall-x64.exe"
   ```

3. **Update .csproj** to include it in builds

4. **Rebuild release:**
   ```powershell
   dotnet publish -c Release -r win-x64 --self-contained true
   ```

5. **Test on a clean Windows 11 PC** (without runtime)

---

## Testing Checklist

- [ ] Build completes successfully
- [ ] Runtime check works (false on clean machine)
- [ ] Dialog appears on first launch
- [ ] Installer runs with admin elevation
- [ ] App restarts after installation
- [ ] Runtime check passes after install
- [ ] App launches normally
- [ ] ZIP includes the installer EXE

---

## User Documentation Update

Update your README/release notes:

```markdown
## One-Click Setup

The app will automatically install required dependencies on first launch:

1. Extract the ZIP
2. Run FireStickScreenSaverEnforcer.exe
3. Click "Install" when prompted (requires administrator permission)
4. App will restart and work normally

No manual downloads or configuration needed!
```

---

**Your app will now have a professional, seamless setup experience!** ??
