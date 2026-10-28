# B2S Pro Changelog

All notable B2S Pro changes are documented here. Release packages and checksums are published with each corresponding GitHub release.

## Designer 1.0.8 / Server 3.0.9 — 2026-10-27

**Feature Update — Dynamic Lighting and Infinity Controls**

### October 28 maintenance refresh — versions unchanged

- Designer: export Dynamic Front Lighting as a clean alpha-only field so stationary canvas details are not stamped into moving artwork, and save the optional linked snippet target through project and DirectB2S/B2SPro export paths.
- Server: apply Dynamic Front Lighting only to its linked snippet when a target is saved, preserve the moving artwork's opaque surface and colors, and retain the established all-eligible-snippets behavior when no target is saved.
- Server: rotate new one-image wheels continuously from their single persistent image instead of generating duplicate rotated frames or full-size temporary images.
- Server: render native-wheel Dynamic Front Lighting from only the affected overlap and reuse its pixel work buffers, removing the measured full-memory cleanup stalls while keeping the rendered lighting pixel-for-pixel unchanged.
- Preserve untouched legacy rotation and non-native moving/static Dynamic Front Lighting paths.

### October 27 package correction — versions unchanged

- Store identical OCR, help, ID Tester, and Infinity artwork once in the Designer runtime ZIP instead of duplicating them in both x64 and x86 folders.
- Update the complete setup to install the shared payload plus the selected Designer architecture. The current setup continues to accept the earlier self-contained package layout.
- Keep Server runtime code, package layout, and server-only installation behavior unchanged.

### Designer

- Add Dynamic Front Lighting for moving snippets, including optional masked light visibility and accurate Designer preview/export support.
- Add the Infinity Lights generator with photorealistic bulbs, masks, adjustable lighting controls, reusable animation frames, effect playlists, clockwise, counterclockwise, pulse, inward, outward, bounce, and strobe effects.
- Add normal B2S command, ROM lamp, ROM solenoid, ROM switch, inverted, mixed, and always-running triggers to generated Infinity animations. Preserve trigger settings when reopening and editing an Infinity group.

### Server

- Render Dynamic Front Lighting against moving snippets while preserving normal legacy backglass lighting and animation paths.
- Read and run the generated Infinity frames as ordinary B2S animations, using the existing trigger and stop systems.

### Updater

- Show the update classification, release title, installed and available versions, and component-specific release notes before downloading an update.
- Preserve the fixed-name setup download path used by existing supported update checkers.

## Designer 1.0.7 / Server 3.0.8 — 2026-10-22

- Add a setup option to disable automatic update notifications for both Designer and Server. Manual **Check for Updates** remains available, and rerunning setup with the option cleared re-enables automatic notifications.
- Stop the physics trigger pipe and mark the backglass unavailable when the separate Server EXE exits. Tables with a PUP pack and no B2S backglass no longer stall on repeated 100 ms connections to a receiver that has already exited.
- Present physics-ball positions once per completed server frame and map masks with the saved backglass scale, preserving ball size, movement, and masking across display sizes.
- Restore the established legacy mechanical-snippet rotation route while keeping native one-image rotation limited to files that explicitly enable it.
- Poll only the ROM switches used by the loaded backglass unless the tester explicitly requests the complete switch range.
- Detect conflicting B2S runtime copies below the selected Visual Pinball folder, include only those runtime files in verified recovery, and retire them during installation. Preserve ScreenRes files, settings, plugins, tables, backglasses, backups, and unrelated files.
- Server release metadata advances to 3.0.8. Designer remains 1.0.7.

## Designer 1.0.7 / Server 3.0.7 — 2026-10-19

- Add Restore Previous Setup to both installers. Back up and verify only existing server files replaced or removed by the update; record newly installed server files so restore can remove them.
- Preserve existing settings, plugins, projects, tables and previous backups without copying them into recovery. Designer rollback copies are temporary during installation.
- Verify recovery files before restoring and preserve current affected files as a safety backup. Keep backup/install progress separate from completed download progress.
- Server release metadata advances to 3.0.7; runtime behavior and compatibility identifiers are unchanged. Designer remains 1.0.7.
- Controlled package install/restore checks passed for x64, x86 and server-only setup. Live Windows registration and a full VPX rollback were not exercised by these checks. Recovery does not restore Windows registration or shortcuts.

## 1.0.7 / Server 3.0.6 — 2026-10-11

### October 19 update — versions unchanged

- Keep selection masks aligned with the original artwork when adding or resizing a speaker panel. Extend the mask canvas with transparent space instead of stretching its existing pixels; preserve masks through Undo/Redo and project/runtime exports.

### October 18 update — versions unchanged

- Let the first left-click activate the main toolbar/menu and perform its normal action when returning from another window or floating panel. Preserve right-click behavior. Confirmed in local user testing.

- Remember the Auto Save toolbar setting across restarts. Turning it off keeps it off until explicitly enabled again; new installations retain the default of on. Local restart behavior confirmed by the user.

- Keep resized snippets at their chosen canvas scale when rotating with the drag handle, including grouped picture-animation frames. Preserve source-image detail and existing unresized rotation behavior.
- Canvas shrink-and-rotate behavior confirmed in local user testing. Designer remains 1.0.7 and Server remains 3.0.6.

### October 17 update — versions unchanged

- Refresh Create Backglass, Preview/Test, and Import DMD command availability when opening or switching projects, without first opening their menus. Preserve the existing destination and DMD restrictions.
- Embed the illustrated B2S Pro guide, including the revised examples and screenshots, with no separate help download. Keep the original B2S Help available as a separate choice and remove the superseded Pro introduction from that legacy guide.
- Add Lamp to physics launcher triggers and Lamp/Solenoid to motion-path Stop and Resume. Preserve those values through project saves, exported backglasses, trough editing, and both Server runtimes.
- Preserve existing B2S/SW routing and the separate legacy and one-image animation formats.

### October 13 update — versions unchanged

- Keep the full toolbar accessible with a smaller logo, a permanent progress bar in the upper menu strip, and Lamp filter under View. Remove the visible Undo/Redo buttons and zoom percentage box while preserving their existing commands and internal zoom state.
- Give Manage Animations separate B2S, Lamp, Solenoid, and SW source choices, a command ID, and inversion without overlapping the preview or timing controls. Preserve existing GI and mixed legacy joins.
- Add B2S and SW sources to snippet rotation settings. Keep source choices separate from continuous/triggered mode.
- Route SW input through the existing controller connection for lights, flashers, snippets, animation start/stop, native rotation, motion-path entry/exit/stop/resume, trough drains, pivots, and physics launchers on Backglass and DMD. Retain every object assigned to the same switch.
- Preserve SW values through project saves and exported backglasses while retaining existing B2S, Lamp, Solenoid, GI, and legacy animation mappings. SW operation confirmed in local VPX testing.
- Remove invalid editable-selection queries from the illumination panel's selection-only dropdowns during startup.
- Allow manual editing of the Server folder in both setup programs, retaining automatic detection and the Change button. Both setup changes confirmed in local user testing.

### Confirmed local UI update — versions unchanged

- Restore window dragging from the logo and blank header/menu/toolbar areas. A stationary click keeps the window maximized; an actual drag restores and moves it between monitors.
- Replace the visible speaker-panel dropdown with Choose Template, followed by Import Image, Height, Save to My Template and My Saved Speaker Grills. Preserve the existing template selection and preview behavior.

### Initial release

- Add an Import DMD Image toolbar shortcut and default GIF/PNG sequence imports to the active canvas.
- Render DMD ball, motion-path, pivot, masked and behind-canvas artwork using the existing animation model, preserving ordinary legacy DMD drawing.
- Update both runtime packages and setup programs; preserve the B2S.Server COM identity and VPX connection.

## 1.0.6 / Server 3.0.5 — 2026-10-10

- Apply Text Settings changes to every selected text box and illuminate the whole selected group in the live preview, preserving each box's lettering and routing.
- Restore legacy text with an old black Dodge Color value automatically, without requiring brightness-slider changes or resaving the backglass.
- Restore cached reel artwork during dragging and keep all score displays responsive while preserving their layer order, including stationary displays above the selected score.
- Keep complete tilted reel previews, including 3D glass, behind the canvas when Behind Canvas is selected.
- Avoid redundant full-canvas repaints when adjusting score rotation and perspective; retain full-quality final rendering after release.
- Retain bundled offline OCR/PDF imports, digit proportions, and the previously confirmed transparent credit-pointer repaint correction.
- Update both setup programs and release metadata. Server behavior remains unchanged from 3.0.4.

### October 11 update — versions unchanged

- Add one FIT toolbar button with a cyan square and four-way arrow, using the existing Zoom to Window command.
- Preserve the saved Lamp Properties window size when reopening it.
- Keep blank menu, toolbar, and logo clicks from restoring a maximized Designer window.
- Hide the duplicate Reels & LEDs and Illumination menu tabs while retaining the toolbar commands; keep only dark and illuminated image exports visible in Backglass.
- Replace the white progress bar with a permanently visible neon cyan bar, purple frame, percentage display, and an active-work pulse.

### October 11 speaker-panel update — versions unchanged

- Add the Add Speaker Grill toolbar command to append a separate panel without moving original artwork, lights, or scores.
- Set the existing grill height automatically; retain the Server's existing grill show/hide behavior.
- Embed eight speaker-panel templates with a picture-and-name picker, custom image import, height adjustment, undo, and safe panel removal.
- Add Save to My Templates and My Saved Templates for persistent local image collections, including images added directly to the folder.
- Refresh Layers when Current Image switches between Backglass and DMD.

### October 11 COM registration update — versions unchanged

- Limit Server COM exposure to the existing B2S.Server contract, preserving its ProgID, class/interface/event identifiers, and callable interface.
- Carry over legacy B2S ProgID and orphan CLSID cleanup from the upstream February 14 registration update, with explicit protection for the active B2S.Server identifiers.
- Verify cleanup with in-memory stores and confirm the local VPX connection. Actual registry cleanup remains untested.

## 1.0.5 / Server 3.0.4 — 2026-10-10

- Correct credit-pointer image trails on transparent B2S Pro canvases by clearing repaint edges fully and preserving the cleared clip region. Confirmed in VPX with Gottlieb 300; legacy rendering retains its existing path.

- Import one PDF or image sheet into EM reel, EM credit reel, or LED image sets, recognize 0–9, and preview the extracted artwork before saving.
- Include offline OCR and PDF tools in the existing Designer package; setup installs them automatically without a separate download or tool installation.
- Name and delete imported image sets, hide unwanted built-in sets, and restore built-in sets from each category's import menu.
- Generate credit reel values through a selected maximum from 1–99, retaining zero for reset and preserving the original digit artwork.
- Preserve sheet-imported digit proportions when resizing in the Designer and Server, with an optional saved setting that leaves legacy rendering unchanged.
- Make eligible reel dragging and resizing faster while restoring the full-quality rendering when the drag ends.

## 1.0.4 / Server 3.0.3 — 2026-09-29

- Add a trough option that releases every loaded ball to gravity on one trigger, keeps each dropped ball live where it settles, and returns one randomly selected available ball from that position through the normal entry trigger and path.
- Add local Load Balls, Drop All Balls, and Test Trigger controls that use the saved trough path, gravity, boundaries, collision response, selected ball artwork, and rolling behavior.
- Keep gravity-trough boundary edits synchronized across every ball in the group, and preserve the new trough settings through project saves and DirectB2S exports.
- Add a separate drain-all option that keeps every occupied ball ordered and non-overlapping along the trough centers and exact authored exit path.

### October 2 update — versions unchanged

- Remove the gravity-trough return-capture zone and hidden parking behavior. Released balls remain visible physics objects and are reused from their actual resting positions.
- Keep gravity-trough ball artwork round while preserving its visible authored area on non-uniformly scaled backglass displays. Normal troughs and other physics artwork retain their existing scaling behavior.
- Dispose temporary illuminated-reel export images promptly during DirectB2S creation.
- Existing 1.0.4 / 3.0.3 installations need to rerun setup manually to receive this same-version update.

## 1.0.3 / Server 3.0.2 — 2026-09-23

### September 28 update — versions unchanged

- Remove the Screen Resolution Identifier's window-position lock option and the Server's corresponding monitor-lock runtime behavior. VPX playfield display selection remains independent of B2S window placement.
- Rebuild and package the current Screen Resolution Identifier with every release build so setup installs the source-matched utility instead of retaining a baseline copy.
- Existing 1.0.3 / 3.0.2 installations need to rerun setup manually to receive this same-version update.

### September 27 update — versions unchanged

- Give grouped trough balls a fixed collision bounce based on arrival speed, with reduced force transferred through adjacent balls and exact return to their authored slots.
- Stagger trough compaction by 48 ms per following ball so ejected balls no longer move in lockstep.
- Reuse identical layered snippet backdrops while exporting artwork lights, cutting repeated full-canvas bitmap work on image-heavy backglasses.
- Dispose generated save images and animation-preview timers promptly, and guarantee the Layers list resumes painting after every rebuild.
- Ignore malformed or NUL-filled Step 2 preview telemetry instead of allowing an intermittent conversion error to interrupt the preview.
- Make each new reel or LED score frame inherit the selected or latest frame's reusable display configuration, including digit count, while assigning a new frame ID and leaving player/start-digit routing unassigned.
- Add independent grill brightness with exact numeric entry, plus a reset that restores the current session's pre-brightness image at its native dimensions and clears both brightness adjustments.
- Existing 1.0.3 installations need to rerun setup manually to receive this same-version update.

### September 26 update — versions unchanged

- Advance physics-linked flippers and balls together in 1 ms steps in the Designer test and Server, so delayed screen updates do not alter the calculated hit.
- Render physics-linked Server pivots once after each batch of physics steps instead of forcing a redraw for every substep.
- Keep Quick Selection brush feathering inside the visible brush diameter and add Photoshop-style Shift-click straight lines from the previous brush point.
- Existing 1.0.3 installations need to rerun setup manually to receive this same-version update.

### September 24 update — versions unchanged

- Physics preview keeps the ball selection mask fixed in backglass coordinates while the ball moves and rotates, including feathered edges.
- Pivot Animation adds Random strength (%) for flipper impacts, saved in projects and exports and applied by the shared preview/Server physics implementation. Zero preserves fixed strength.
- Existing 1.0.3 / 3.0.2 installations need to rerun setup manually to receive this same-version update.

- Preserve original snippet image resolution in the Designer and Server, with a compatible fallback for legacy backglasses.
- Reload larger backglass images at their original resolution by default; resizing to the existing canvas is an explicit choice.
- Add live ball testing in the physics boundary editor using the shared Server physics implementation, with firing, pause, reset, and live adjustments.
- Improve physics preview rendering performance and keep ball rotation consistent with runtime rendering.
- Add individual circular bumper bounce settings, saved with projects and exports and used by both the preview and Server.
- Update Designer, Server, and setup release versions; About and Server version displays continue to derive their labels from release metadata.

## 1.0.2 / Server 3.0.1 — 2026-09-20

- Added Help → Check for Updates for manual checks at any time, independent of the once-per-day automatic check.
- Setup hides only the designated runtime configuration files, update checker, and installation log in each product folder. Files remain installed; applications, DLLs, tools, and backups remain visible.
- Designer About and the Server settings version display now read release metadata automatically, keeping their displayed versions aligned with future releases.
- Reduced light-rendering work with equivalent blur output, bounded image caches, and coalesced Light dialog canvas updates.
- Kept artwork stationary during light dragging and rotation previews. Removed the Light dialog Reset button while retaining Flasher Reset.
- Avoided duplicate editor illumination generation and misleading save-progress updates during lighting edits.
- Skipped unchanged recovery snapshots and deferred recovery during dragging or modal editing. Recovery encoding remains synchronous.
- Made preview process-exit waiting asynchronous.
- Retained elapsed physics time across delayed callbacks using bounded fixed steps. Moving-flipper and cross-computer cabinet behavior still require validation.
- Disposed failed rotation preparations and allowed retry after unavailable parent windows; protected newer preparations from stale completions.
- Incremented release file and package versions so existing installations can detect the update. Runtime assembly identities and COM identifiers are unchanged.

## 1.0.1 / Server 3.0.0 update — 2026-09-16

- Both setup programs remember Visual Pinball, Designer, and Server installation paths after successful installation and prefill them on subsequent updates. Server-only updates preserve the saved Designer location.
- Installed program files, supporting files, installation logs, and backup folders are visible. Updating clears the previous Hidden/System attributes from installer-managed program files and backups.
- Existing executable filenames and VPX/server interfaces remain unchanged.

## 1.0.1 / Server 3.0.0 update — 2026-09-15

- Added independent, once-daily update checks for B2S Pro Designer and B2S Server. Checks stay silent when current or offline, run without elevation, and offer the correct verified setup only when a newer GitHub release exists.
- Cleaned the public Server package to ship `ScreenResTemplate.txt` instead of an active `ScreenRes.txt` and to use `B2S-Pro-Changelog.md` as its only changelog. Internal Server runtime files remain installed but hidden.
- Simplified Pivot Animation to direct marker dragging with zoom and pan; removed redundant point-setting and fit commands. Hinges can be authored outside the snippet box, and pivots can also start automatically with the backglass.
- Consolidated bumpers and rotatable switch zones into Boundaries. Switch zones retain their authored angle and trigger when crossed from any direction.
- Simplified launcher placement to the draggable origin and direction marker while preserving exact saved coordinates and pivot-following ball behavior.
- Restored per-light blinker controls to the modern Light editor without restoring Global Mask controls. Lights and flashers retain the shared artwork-pixel renderer and multi-object mask workflow.
- Preserved the exact authored Layers order across save, reload, recovery, and legacy fallback; removed the unused Layers search field and its saved hidden filter state.
- Removed unreachable old project/backup menu paths and uncompiled duplicate source. B2S Pro continues to save only `.B2SPro` while retaining `.directB2S` import compatibility.
- Completed successful ID Tester restores now remove their temporary tester and verified backup instead of accumulating completed test files.
- Reduced every public release to the two setup programs plus the verified Designer and Server runtime ZIPs and their checksum files. Complete setup now installs atomically from those same two runtime packages, eliminating redundant complete-build, source-package, tester, and checksum-list assets.

## 1.0.1 / Server 3.0.0 update — 2026-09-14

- Added opt-in automatic pivot animation. A held solenoid, lamp, or B2S ID swings the snippet between its selected limits; release returns it to the unrotated editor pose.
- Added an automatic swing preview and the option to attach a physics-ball launcher to a pivot snippet. The ball follows the pivot while held, launches at its current angle, and returns to the ball position authored in the editor.
- Showed the full backglass in the pivot editor with the snippet at its authored position and size. Added zoom and pan; the artwork stays fixed while either point marker is placed or dragged for fine alignment.
- Preserved the original trigger, pivot, and launcher paths for backglasses that do not enable these options.

## 1.0.1 / Server 3.0.0 update — 2026-09-13

- Changed automatic recovery to write a clearly marked `_AutoRecovery_` `.B2SPro` copy in the project folder instead of overwriting the saved backglass. Normal Save or Don't Save removes the temporary copy; a newer matching copy can be restored after an interruption.
- Added the toolbar ID Tester. It launches the exact-name VPX table, displays only active lamp, solenoid, GI, and switch IDs, shows the running backglass name, and restores the table's original backglass after testing.
- Added switch-ID display support to B2S Pro Server while retaining legacy lamp, solenoid, and GI behavior.
- Added an illustrated help section for the embedded B2S Pro ID Tester.
- Corrected installer completion so progress stops and the window closes after acknowledgment; simplified the existing-installation confirmation while retaining the update and backup choice.

## 1.0.1 — 2026-09-06

First public B2S Pro baseline.

### Workspace and editing

- Redesigned the main workspace, toolbar, menus, and object-editing flow.
- Added persistent sizing and placement for tool and editor windows.
- Added a comprehensive Layers window with reordering, visibility, locking, naming, opacity, masks, placement, and direct editing.
- Improved selection, dragging, redraw behavior, and multi-object operations.
- Added backglass brightness access to the primary toolbar area.
- Added direct numeric entry beside adjustable sliders throughout the lighting, flasher, mask, reel, Quick Selection, Dream7, illumination, and Layers tools for precise repeatable settings.

### Snippets and masks

- Added **Make Snippet** for copying a selected piece of the current image into a movable or stationary cover snippet.
- Added Quick Selection masks with adjustable radius, feather, shift edge, smooth, contrast, and inversion behavior.
- Added rotation handles and consistent rotated bounding-box behavior.

### Lamps and flashers

- Added pixel-level image illumination while protecting black areas to preserve contrast.
- Added a dedicated high-intensity Flasher editor separate from normal lamp lighting.
- Added live full-backglass flasher preview focused on only the selected object for smoother editing.
- Added adjustable brightness, glow, softness, mask refinement, light temperature, and radial-spike splash controls.
- Added a repeating one-second test pulse with a fixed 75 ms flash duration.
- Added mask-aware rendering and corrected light/flasher drag artifacts.
- Added rotation behavior matching snippets for both lamps and flashers.
- Restored the established normal-lamp renderer for sharp text and working Quick Selection masks while keeping flashers on their dedicated artwork renderer.
- Fixed Clear Mask so it removes the mask instead of saving an all-transparent mask that permanently hides the light.
- Fixed normal-light movement and resizing so the cyan rotation stem and handle are fully repainted without leaving trails across the canvas.
- Corrected behind-canvas lighting so transparent score windows transmit light while opaque black artwork keeps its contrast.
- Transparent pixels anywhere in the imported backglass canvas now display over a neutral-gray editor backing in the main, drag, animation, and light/flasher preview views, leaving visible headroom for light brightness and temperature while opaque black artwork remains black.

### EM score reels

- Added optional realistic 3D EM reel rendering with adjustable backlight brightness, the same 2000–6500 K color-temperature range used by illumination, exaggerated drum depth, and glass reflection.
- Kept black digit ink dark while allowing the reel material around it to transmit the simulated backlight.
- Composited the reel behind the backglass canvas and aligned one continuous reflection across the complete score display so each digit reads as part of the same glass-covered window.
- Stored the new settings as optional project and `directB2S` attributes; existing backglasses without them continue through the original rendering path unchanged.
- Carried the Designer's neutral transparent-canvas backing into new B2S Pro exports and the server's custom runtime paint layer, so score-window and other transparent openings no longer fall back to black; legacy files without the opt-in marker retain their established backing.
- Added reusable 3D reel settings—enabled, brightness, color temperature, depth, and glass reflection—which now carry forward with the selected or latest score frame's display configuration.
- Made the Reel Lighting & 3D window retain every displayed setting when it is closed and reopened instead of rolling live adjustments back.
- Raised realistic reel backlight brightness from 200% to 400% across the Designer, saved project and `directB2S` data, and B2S Pro Server runtime.
- Made ROM Player Up triggers switch the realistic 3D reel backlight together with the active player's illumination, leaving inactive-player reel material unlit.
- Kept reel digits visible when legacy Reel Illumination Location is Off; that legacy setting no longer removes an enabled 3D reel display.

### Animation and motion

- Redesigned the animation section for more natural motion and easier setup.
- Added one-picture rotation animation and animated-GIF import in addition to traditional frame sequences.
- Added editable motion paths, pivot animation, mechanical-wheel behavior, and score rotation.
- Added physics boundaries, bumpers, switches, flippers, and launcher behavior.
- Added Trough Animation with live ball-count updates and fully visible first/last ball handles.
- Added custom PNG artwork per trough ball while preserving a consistent diameter.
- Added optional rolling, stable per-ball rotation phases, clean redraws, and feeder respawn direction control.

### Documentation and release

- Made `.B2SPro` the permanent save format. The complete editable project and runtime backglass data now remain together in one file.
- Kept `.directB2S` as a read-only legacy import format. Editing and saving a legacy backglass creates a `.B2SPro` file without renaming or overwriting the original.
- Updated B2S Pro Server to prefer a matching `.B2SPro` file and fall back to the matching legacy `.directB2S` file when no Pro file exists.
- Added seamless Windows associations for both formats: Explorer displays the exact distinct type labels `.B2SPro` and `.directB2S`, and double-clicking either opens that file in B2S Pro.
- Consolidated the File menu into one **Import backglass file** command that shows both supported formats.
- Made normal Save place the `.B2SPro` file in the matching project folder while Save As continues to honor the path selected by the user.
- Added an illustrated B2S Pro help section while retaining all original help topics.
- Embedded the compiled help into B2S Pro.
- Added separate complete B2S Pro and server-only setup programs, each with verified GitHub-release and offline modes.
- Updated B2S Pro branding, About information, credits, and versioning.
- Standardized Designer version 1.0.1 and Server version 3.0.0 release names.
- Preserved established legacy backglass behavior and file compatibility.

October 10, 2026 — same-version Designer update
- Backport text editing, font selection, remembered appearance, Dodge Color, brightness, and uppercase typing fixes to .NET Framework 4.8.
- Add the Text layer category, cyan text rows, and type colors across layer rows.
- Compact the Illumination panel and improve Accurate Light Preview responsiveness.
- Fix repeated Step 2 preview closing and reopening.
- Detect refreshed same-version update packages and record installed package fingerprints.
- Preserve the existing splash screen and Server 3.0.3 runtime.
