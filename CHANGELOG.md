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
- Corrected behind-canvas lighting so transparent score windows transmit light while opaque black artwork keeps its contrast.
- Transparent pixels anywhere in the imported backglass canvas now display over a neutral-gray editor backing in the main, drag, animation, and light/flasher preview views, leaving visible headroom for light brightness and temperature while opaque black artwork remains black.

### EM score reels

- Added optional realistic 3D EM reel rendering with adjustable backlight brightness, the same 2000–6500 K color-temperature range used by illumination, exaggerated drum depth, and glass reflection.
- Kept black digit ink dark while allowing the reel material around it to transmit the simulated backlight.
- Composited the reel behind the backglass canvas and aligned one continuous reflection across the complete score display so each digit reads as part of the same glass-covered window.
- Stored the new settings as optional project and `directB2S` attributes; existing backglasses without them continue through the original rendering path unchanged.
- Carried the Designer's neutral transparent-canvas backing into new B2S Pro exports and the server's custom runtime paint layer, so score-window and other transparent openings no longer fall back to black; legacy files without the opt-in marker retain their established backing.
- Made Add Reel repeat the selected or latest reel's complete configuration—including illumination, 3D backlight, rotation, perspective, and behind-canvas settings—while assigning the next unique reel number.
- Kept repeated reels functionally independent by assigning the first unused Player 1–4 route, the next non-overlapping score-digit range, and the matching player rollover illumination ID instead of duplicating Player 1's routing.
- Made the Reel Lighting & 3D window retain every displayed setting when it is closed and reopened instead of rolling live adjustments back.

### Animation and motion

- Redesigned the animation section for more natural motion and easier setup.
- Added one-picture rotation animation and animated-GIF import in addition to traditional frame sequences.
- Added editable motion paths, pivot animation, mechanical-wheel behavior, and score rotation.
- Added physics boundaries, bumpers, switches, flippers, and launcher behavior.
- Added Trough Animation with live ball-count updates and fully visible first/last ball handles.
- Added custom PNG artwork per trough ball while preserving a consistent diameter.
- Added optional rolling, stable per-ball rotation phases, clean redraws, and feeder respawn direction control.

### Documentation and release

- Added an illustrated B2S Pro help section while retaining all original help topics.
- Embedded the compiled help into B2S Pro.
- Updated B2S Pro branding, About information, credits, and versioning.
- Standardized Designer version 1.0.1 and Server version 3.0.0 release names.
- Preserved established legacy backglass behavior and file compatibility.
