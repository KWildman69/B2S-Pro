# B2S Pro Installers

The same tested installation engine produces two separate setup programs:

- `B2SProSetup.exe` installs B2S Pro Backglass Designer 1.0.1 beside the
  original Designer and installs or updates B2S Pro Server 3.0.0.
- `B2SServerSetup.exe` installs or updates only B2S Pro Server 3.0.0 for VPX
  players who do not need the Designer.

## Installation model

- The user selects the Visual Pinball folder containing a `VPinballX*.exe`.
- The installer automatically detects a server in `B2SServer`, `Tables`, or the
  VPX folder itself. A Change button remains available for nonstandard layouts.
- On a completely fresh setup, the installer creates `B2SServer` inside the
  selected Visual Pinball folder and installs the complete server payload there.
- The complete B2S Pro setup installs the Designer to its own user-selected
  folder. The server-only setup never creates or changes a Designer folder.
- The B2S Server is installed or updated in its own user-selected folder.
- An existing original Designer is never removed or overwritten.
- Existing B2S Pro or B2S Server program files are listed and require an
  explicit overwrite confirmation.
- The B2S Server is mandatory. Declining an existing-server update cancels the
  complete installation rather than leaving mismatched Designer and Server
  versions.
- Every replaced program file is copied to a dated backup first.
- `ScreenRes.txt`, `B2STableSettings.xml`, the `Plugins` and `Plugins64`
  folders, projects, tables, backglasses, and unrelated files are preserved.
- Runtime DLLs, helper executables, `.config` files, installer logs, and backup
  folders are marked Hidden + System after installation. `B2SPro.exe`, useful
  server utilities, documentation, `Projects`, and `ScreenRes.txt` remain
  visible. Protected internal files can still be deliberately revealed through
  Windows Explorer's protected-operating-system-files setting; NTFS access is
  not denied because the applications and future updates still need the files.
- The complete setup creates a `B2S Pro` shortcut on the current user's desktop and in a
  `B2S Pro` Start Menu folder. Reinstalling refreshes both shortcuts without
  changing the original Designer's shortcuts. Server-only setup creates no
  Designer shortcut.

## Package sources

For private development or offline installation, keep the applicable three
files together in one folder:

- Complete setup: `B2SProSetup.exe`, `B2S-Latest-Complete-Build.zip`, and
  `B2S-Latest-Complete-Build.zip.sha256`.
- Server-only setup: `B2SServerSetup.exe`, `B2S-Pro-Server-3.0.0.zip`, and
  `B2S-Pro-Server-3.0.0.zip.sha256`.

The checksum sidecar is required. Setup verifies the package before changing
any installed files.

When the GitHub repository is public, each setup can query the latest release
of `KWildman69/B2S-Pro`. Complete setup downloads the latest complete package;
server-only setup downloads the latest versioned server package. Each package
must have a matching `.sha256` release asset and is verified before use.

Private GitHub releases cannot be downloaded anonymously. No GitHub token is
accepted, requested, or stored by the installer.

## Building

Run `build-installer.ps1`. It uses the .NET Framework 4.8 C# compiler already
included with Windows on the development machine and produces:

`dist/B2SProSetup.exe`

`dist/B2SServerSetup.exe`

Use `dist/B2SSetup.SelfTest.exe --self-test <complete-build.zip>
<server-build.zip>` to validate both package layouts, x86/x64 selection,
protected-file rules, backup behavior, Designer isolation, and sandbox
installation without elevation or contact with a real Visual Pinball
installation. The self-test runner is a development output and is not
distributed to users.

## Server registration

Both setup programs request administrator permission when they start and run
the included official `B2SBackglassServerRegisterApp.exe silent` after the file
transaction completes. Fresh and updated servers are therefore registered
automatically without a second user workflow. The command is disabled during
the sandbox self-test so development tests cannot change a live registration.
