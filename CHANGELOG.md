# B2S Pro Changelog

All notable B2S Pro changes are documented here. Release packages and checksums are published with each corresponding GitHub release.

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
- Added separate complete B2S Pro and server-only setup programs, each with a verified GitHub-release mode and a three-file offline mode.
- Updated B2S Pro branding, About information, credits, and versioning.
- Standardized Designer version 1.0.1 and Server version 3.0.0 release names.
- Preserved established legacy backglass behavior and file compatibility.
