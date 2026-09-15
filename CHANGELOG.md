# B2S Pro Changelog

All notable B2S Pro changes are documented here. Release packages and checksums are published with each corresponding GitHub release.

## 1.0.1 / Server 3.0.0 update — 2026-09-15

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
- Preserved the original Add Reel behavior while carrying only the selected or latest reel's five optional 3D settings—enabled, brightness, color temperature, depth, and glass reflection—into the next reel window.
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
