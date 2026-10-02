# Generated game art

Generated from `AI_관제_지휘_게임_SPEC_v0.1.md` (sections 21–24). All three PNGs are 1536×1024 RGBA images with transparent exterior areas.

## Files

- `UI/ControlRoom_UI_Atlas.png` — dark industrial CCTV HUD atlas: monitor bezels, status/risk indicators, camera and recording icons, team/data/log tabs, target selector, command field, send control, warning and confirmation panels. The parts are laid out as a visual atlas rather than a uniform grid; slice individual pieces in Unity's Sprite Editor. Text is intentionally omitted so the game can render localized labels with its own font.
- `Characters/FieldAgent_SpriteSheet.png` — consistent low-poly field agent, 8 columns × 4 rows; each cell is 192×256 px. Rows from top to bottom: idle breathing, walk cycle, crouched sneak, investigate/pick-up action. Slice as a grid, then create one animation clip per row.
- `VFX/ControlRoom_VFX_SpriteSheet.png` — 4 columns × 4 rows; each cell is 384×256 px. Rows from top to bottom: CCTV glitch, CRT scan sweep, amber risk pulse, cyan command confirmation. Slice as a grid and animate each row left to right.

## Unity import

Set each texture to **Sprite (2D and UI)** with **Sprite Mode: Multiple** and keep alpha transparency enabled. Use Sprite Editor's Grid slicing for the character (192×256 px) and VFX (384×256 px). Slice the UI atlas manually around each isolated component. Suggested filter mode is Bilinear for the detailed metallic UI; use Point only if the project adopts a pixel-art treatment.

The art follows the spec's dark industrial / CCTV / analog-monitor direction. Cyan marks active/confirmed UI, amber marks risk, and red is reserved for critical warnings. These are generated prototype assets; verify frame boundaries and tune each slice in the Sprite Editor before wiring animations into the scene.
