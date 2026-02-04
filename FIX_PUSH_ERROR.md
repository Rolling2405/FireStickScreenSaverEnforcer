# How to Fix the "Remote Disconnected" Error

## Problem

You attempted to push a **99 MB ZIP file** to GitHub, which exceeds GitHub's file size limit of 100 MB. This causes the "Remote Disconnected" error.

## Solution

The following has been configured:
- ZIP files are now excluded from Git (`.gitignore` updated)
- The ZIP file remains on your computer only
- Upload it to the GitHub Releases page instead

---

## Publishing Instructions

### Step 1: Commit Changes (Without ZIP)

In **GitHub Desktop**:

1. You should see these files ready to commit:
   - `.gitignore` (modified - now excludes ZIP files)
   - Source code files
   - Documentation files
   - **NOT** the ZIP file (it's ignored now)

2. Verify the ZIP file is NOT listed or unchecked:
   - Should NOT see `FireStickScreenSaverEnforcer-v1.0.0-win-x64.zip`

3. Use this commit message:
   ```
   Release v1.0.0 - Initial Release
   
   - WinUI 3 app with self-contained .NET 10
   - Separate IP/Port fields
   - Bundled ADB tools
   - Fixed .pri file copy for WinUI 3
   - Updated .gitignore to exclude release packages
   - Normalized line endings to CRLF
   ```

4. Click **Commit to master**

### Step 2: Push to GitHub

1. Click **Push origin** (top button)
2. Should complete quickly (source code only, ~5-10 MB)

---

### Step 3: Upload ZIP to GitHub Release

The ZIP file goes to the **Releases page**, not Git:

1. Go to: https://github.com/Rolling2405/FireStickScreenSaverEnforcer/releases

2. Edit your v1.0.0 release:
   - Delete the old ZIP (if present)
   - Click **Attach binaries**

3. Upload the new ZIP:
   - Location: `C:\dev\FireStickScreenSaverEnforcer\FireStickScreenSaverEnforcer-v1.0.0-win-x64.zip`
   - Size: 99 MB (fully self-contained)
   - Wait for upload to complete

4. Click **Update release**

---

## What Changed

| Aspect | Before | After |
|--------|--------|-------|
| ZIP approach | Attempted to push to Git | Upload to Releases page |
| Result | Remote disconnected error | Fast push, ZIP on Releases |
| File size | ~99 MB bundled in repo | Separate, not in repo |

---

## Summary

**Git Repository:**
- Contains source code only (~5-10 MB)
- Fast and reliable push
- No large files

**GitHub Release:**
- Contains the 99 MB ZIP file
- Users download from Releases page
- All dependencies included

---

## What NOT to Commit to Git

Never commit these to your repository:
- `.zip` files (Release packages)
- `bin/` folders (Build output)
- `obj/` folders (Intermediate files)
- Large binary files

---

## What TO Commit to Git

Always commit these files:
- Source code (`.cs`, `.xaml`, `.xaml.cs`)
- Project files (`.csproj`, `.sln`)
- Documentation (`.md` files)
- Build scripts (`.ps1` files)
- Configuration files (`.gitignore`, `.gitattributes`)
- ADB tools (files in `platform-tools/` folder)

---

## Recovery: Accidental Large File Commit

If you accidentally commit a large file to Git:

```powershell
# Remove from Git but keep locally
git rm --cached <filename>
git commit -m "Remove large file from tracking"
git push
```

Or run the helper script:
```powershell
.\remove-large-files.ps1
```

---

## You're Ready!

Follow the steps above in GitHub Desktop to publish your release successfully.
