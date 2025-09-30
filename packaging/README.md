# Packaging: MSI (WiX v4) for IT-managed deployment

This MSI pipeline produces a per-user installer that matches our existing install layout and uninstall entries, supports optional Start Menu shortcut and autostart, registers file associations under HKCU, and seeds the UpdateChannel from a property.

Contents:
- packaging/msi/Product.wxs — WiX authoring (per-user install to %LOCALAPPDATA%\Programs\GGs.Desktop)
- packaging/build-msi.ps1 — Builds the MSI (publishes, harvests files, generates file-association authoring, builds)
- packaging/Install-GGsDesktopMSI.ps1 — Installs the MSI per-user, accepts Channel/Autostart/NoShortcut
- packaging/common/Seed-UpdateChannel.ps1 — Helper to seed UpdateChannel (also usable with MSIX)

## Build the MSI

1) From repo root, build in Release (self-contained or framework-dependent):

PowerShell:

  .\packaging\build-msi.ps1 -Configuration Release -SelfContained -Channel stable -FileAssociations .ggs,.ggprofile

Notes:
- The script uses a local dotnet tool manifest to fetch the WiX v4 CLI (wix). No admin needed.
- It publishes the desktop app to packaging/artifacts/publish.win-x64 by default, harvests that folder into WiX authoring, and builds the MSI to packaging/artifacts.
- The CHANNEL parameter is embedded for HKCU\Software\GGs\Desktop\UpdateChannel.

## Install the MSI per-user

PowerShell (passive UI):

  .\packaging\Install-GGsDesktopMSI.ps1 -MsiPath .\packaging\artifacts\GGs.Desktop.msi -Channel stable -Autostart -NoShortcut:$false

Silent:

  .\packaging\Install-GGsDesktopMSI.ps1 -MsiPath .\packaging\artifacts\GGs.Desktop.msi -Channel beta -Autostart -Silent

The installer writes uninstall info under HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\GGs.Desktop to match the existing script-based layout.

## File Associations

- Provide extensions via -FileAssociations (e.g., .ggs,.ggprofile).
- Associations are written under HKCU:\Software\Classes and are removed automatically on uninstall.
- Default icons point to the installed GGs.Desktop.exe. The app should handle command-line "%1" for opening associated files.

## Update Channel Seeding

- The MSI seeds HKCU:\Software\GGs\Desktop\UpdateChannel using the public property CHANNEL (default: stable).
- If you need to change the channel after install (or when using MSIX), run:

  .\packaging\common\Seed-UpdateChannel.ps1 -Channel dev

## Start Menu and Autostart

- Set -NoShortcut to suppress Start Menu shortcut creation.
- Set -Autostart to add HKCU Run entry (GGsDesktop) to auto-launch on logon.

## Signing

- For production, sign the MSI with your code-signing certificate (signtool.exe) in your CI pipeline after build.

## CI pipeline (GitHub Actions sketch)

- Use a Windows runner, setup .NET SDK, run the build script, then sign and upload the artifact.
- Example (pseudo):

  - name: Build MSI
    run: |
      pwsh -File packaging/build-msi.ps1 -Configuration Release -SelfContained -Channel stable -FileAssociations .ggs,.ggprofile

  - name: Sign MSI
    run: |
      signtool sign /fd SHA256 /f $Env:CODE_SIGN_PFX /p $Env:CODE_SIGN_PASSWORD packaging/artifacts/GGs.Desktop.msi

  - name: Upload Artifact
    uses: actions/upload-artifact@v4
    with:
      name: ggs-desktop-msi
      path: packaging/artifacts/*.msi

## MSIX (Windows Application Packaging Project)

We include a Windows Application Packaging Project that packages the WPF app as an MSIX with manifest-based file associations for .ggs and .ggprofile. The manifest declares a full-trust desktop process, file associations, and a disabled-by-default StartupTask. Because MSIX cannot write registry at install time, the Channel is seeded post-install via a script.

Files:
- packaging/msix/GGs.Desktop.Package/GGs.Desktop.Package.wapproj
- packaging/msix/GGs.Desktop.Package/Package.appxmanifest
- packaging/msix/GGs.Desktop.Package/Assets/* (placeholder images; replace with real PNGs)
- packaging/build-msix.ps1 — builds the MSIX (uses MSBuild if available or MakeAppx fallback)
- packaging/Install-GGsDesktopMSIX.ps1 — installs the .msix and seeds Channel via registry

Build with MSBuild (preferred when VS/SDK installed):

  pwsh -File packaging/build-msix.ps1 -Configuration Release -UseMsBuild

Fallback (MakeAppx; requires Windows 10 SDK):

  pwsh -File packaging/build-msix.ps1 -Configuration Release

Install per-user and seed Channel:

  .\packaging\Install-GGsDesktopMSIX.ps1 -PackagePath .\packaging\artifacts\msix\GGs.Desktop.msix -Channel stable

Notes:
- Replace placeholder PNGs in packaging/msix/GGs.Desktop.Package/Assets with valid images (Square150x150Logo.png, Square44x44Logo.png, StoreLogo.png).
- If you sign the MSIX, import the certificate to CurrentUser\TrustedPeople (or use the -CertPath parameter in the install script).
- Autostart for MSIX is declared via StartupTask and should be toggled by the app’s Settings UI at runtime; it cannot be enabled by the installer.

