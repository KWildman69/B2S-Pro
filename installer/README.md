# B2S Pro Installers

The same tested installation engine produces two separate setup programs:

- `B2SProSetup.exe` installs or updates only B2S Pro Backglass Designer 1.0.10.
  It does not inspect, install, update, register, or otherwise change the Server.
- `B2SServerSetup.exe` installs or updates only B2S Pro Server 3.0.11 for VPX
  players who do not need the Designer.

## Installation model

- The user selects the Visual Pinball folder containing a `VPinballX*.exe`.
- After successful installation, both setups share saved installation paths in
  `%ProgramData%\B2SPro\SetupPaths.txt`. Later runs prefill existing locations;
  server-only updates retain the saved Designer location. Explicit updater
  launch arguments take precedence over remembered locations.
- Both setups offer **Disable automatic update notifications**. The choice is
  stored for the current Windows user and applies to automatic Designer and
  Server checks. Manual **Check for Updates** remains available. Rerun setup
  with the option cleared to re-enable automatic notifications.
- The Server setup automatically detects a server in `B2SServer`, `Tables`, or
  the VPX folder itself. A Change button remains available for nonstandard layouts.
- On a completely fresh Server setup, the installer creates `B2SServer` inside
  the selected Visual Pinball folder, installs the complete Server payload, and
  runs the included registration application.
- The Designer setup installs the Designer to its own user-selected folder. It
  never creates or changes a Server folder. The Server setup never creates or
  changes a Designer folder.
- On an existing Designer installation, setup replaces only `B2SPro.exe` and
  `B2SUpdateChecker.exe`. On an existing Server installation, setup replaces
  only `B2SBackglassServer.dll`, `B2SBackglassServerEXE.exe`, and
  `B2SUpdateChecker.exe`.
- An existing original Designer is never removed or overwritten.
- Existing B2S Pro or B2S Server program files are listed and require an
  explicit overwrite confirmation.
- Both setups save and verify only existing files covered by that installation,
  under `%ProgramData%\B2SPro\Recovery`. Existing protected settings, plugins,
  tables, projects, unrelated program files, and old backups are not included.
  Unreadable selected files, linked paths, insufficient space, or failed checksums
  stop installation. Designer replacement files have temporary rollback copies
  during installation; those copies are discarded after successful installation.
- **Restore Previous Setup** opens a saved `Recovery.json` in its dated recovery
  folder. It shows the original locations and backup date before confirmation.
  Close VPX, Designer, and running backglasses first. Restore verifies the backup,
  saves and verifies only the current copies of affected files as a safety backup,
  restores the previous server files, and removes package files recorded as new
  during that update. Existing settings, plugins, unrelated files and backup folders
  remain. A failed restore attempts to recover the pre-restore files and reports
  the safety backup location if recovery also fails.
- These are **application-file backups**, not a Windows system rollback: registration,
  file associations, shortcuts, filesystem permissions, and settings outside the
  selected folders are not restored. Existing older `B2SPro-Backups` folders are
  replacement-file backups and cannot be selected in the new Restore screen.
- `ScreenRes.txt`, `B2STableSettings.xml`, the `Plugins` and `Plugins64`
  folders, projects, tables, backglasses, and unrelated files are preserved.
- Setup hides the Designer's `B2SPro.exe.config`, `B2SVPinMAMEStarter.exe.config`,
  `B2SUpdateChecker.exe`, and `B2SPro-Install.log`. In the Server folder it hides
  `B2S_ScreenResIdentifier.exe.config`, `B2S_SetUp.exe.config`,
  `B2SBackglassServerEXE.exe.config`, `B2SUpdateChecker.exe`, and
  `B2SServer-Install.log`. These files remain installed and usable. Only the
  Hidden attribute is applied; Explorer can show them when hidden items are enabled.
- Other installed programs, DLLs, tools, and backup folders remain visible.
  Unrelated user files and display settings are preserved.

## Package sources

For private development or offline installation, keep the applicable verified
files together in one folder:

- Designer setup: `B2SProSetup.exe`, `B2S-Pro-Backglass-1.0.10.zip`, and
  `B2S-Pro-Backglass-1.0.10.zip.sha256`.
- Server-only setup: `B2SServerSetup.exe`, `B2S-Pro-Server-3.0.11.zip`, and
  `B2S-Pro-Server-3.0.11.zip.sha256`.

Each package's checksum sidecar is required. Setup verifies every required
package before changing any installed files.

The Designer ZIP stores architecture-neutral OCR, help, ID Tester, and Infinity
artwork once under `common`, with only architecture-specific executables under
`x64` and `x86`. The Designer setup overlays `common` with the selected
architecture in the Designer installation folder. It also continues to accept
the earlier self-contained x64/x86 package layout. A previously downloaded
older combined setup does not understand the compact package and must be
replaced with the current fixed-name `B2SProSetup.exe`. Server setup and the
Server package layout are unchanged.

When the GitHub repository is public, each setup can query the latest release
of `KWildman69/B2S-Pro`. Designer setup downloads only the latest versioned
Designer package; Server setup downloads only the latest versioned Server
package. Each package must have a matching `.sha256` release asset and is
verified before use.

Every GitHub release is restricted to exactly six custom assets: the two setup
programs, the Designer and Server runtime ZIPs, and one `.sha256` sidecar for
each ZIP. GitHub supplies its own automatic source-code links.

Private GitHub releases cannot be downloaded anonymously. No GitHub token is
accepted, requested, or stored by the installer.

## Building

Run `build-installer.ps1`. It uses the .NET Framework 4.8 C# compiler already
included with Windows on the development machine and produces:

By default, generated installers are written outside the Git repository to
`..\B2S-Local-Work\Installer-Dist`. The full release script supplies its own
external staging path.

`dist/B2SServerSetup.exe`

Use `dist/B2SSetup.SelfTest.exe --self-test <designer-build.zip>
<server-build.zip>` to validate both package layouts, x86/x64 selection,
protected-file rules, backup behavior, Designer isolation, and sandbox
installation without elevation or contact with a real Visual Pinball
installation. The self-test runner is a development output and is not
distributed to users.

## Server registration

Both setup programs request administrator permission when they start. The
Designer setup never changes Server registration. Server setup runs the included
official registration application only for a fresh Server installation. Updating
an existing Server in the same folder preserves its established 32-bit and
64-bit registrations. Registration is disabled during the sandbox self-test so
development tests cannot change a live installation.
