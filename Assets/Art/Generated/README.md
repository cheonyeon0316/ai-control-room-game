# DATA LEAK generated art kit

A cohesive raster art kit based on `AI_관제_지휘_게임_SPEC_v0.1.md`, especially sections 6–7, 14–17, and 21–24. The direction combines low-poly industrial spaces, CCTV surveillance, analog-monitor texture and a dark tactical control room. Cyan marks active/confirmed states; amber marks risk; red is reserved for critical failure.

## UI

- `UI/Screens/ControlRoom_Commercial_UI_Mockup.png` — polished widescreen gameplay UI concept generated with the CCTV room feeds, complete HUD screen and component atlas as visual references; includes Korean mission, objective, log and command text.
- `UI/Screens/ControlRoom_HUD_Complete.png` — full gameplay screen reference with mission/time/risk header, 4-camera view, field-unit/objectives sidebar, log/evidence/help tabs, dispatch target, command field, send action, response line and interpreter mode.
- `UI/Screens/Mission_Modal_UI_States.png` — briefing, pause, success and failure modal designs.
- `UI/Screens/Debrief_Evidence_UI_States.png` — command-profile debrief, evidence database with all four reliability states, facility/help map and field-photo viewer.
- `UI/ControlRoom_UI_Atlas.png` — initial reusable HUD parts.
- `UI/Components/Command_UI_Components_Atlas.png` — target/input/send controls, interpreter mode, command statuses, risk levels, objectives, evidence/reliability cards, tabs and pause/close controls.

The screen images are visual references, not interactive Unity canvases. Runtime text and changing values should be rendered by the existing UI with the project's Noto Sans CJK font. Some generated sample values are placeholders. The room feeds, floor plan and prop/material atlases are art references and image assets; the pack does not create 3D mesh prefabs or change runtime scene scripts.

## Environment / CCTV

- `Environment/Layouts/Mission01_Facility_Level_Layout.png` — top-down blockout matching Mission 01's topology: Entrance south of Main Hall; Laboratory east; Storage north; Server Room east of Storage. Includes CCTV coverage, field-agent start, guard patrol and prop placement.
- `Environment/CCTV/CCTV_Reference_Feeds_2x2.png` — CAM 01 Main Hall, CAM 02 Lab Entrance, CAM 03 Storage and CAM 04 Server Room interior-view references.
- `Environment/CCTV/CCTV_Overlay_Animation_Sheet.png` — subtle scanline, sensor-noise, signal-tear and recovery overlay ideas; crop its free-form bands manually if used as sprites.
- `Environment/Materials/Facility_Floor_Wall_Tiles.png` — six surface samples: entrance epoxy, hall concrete, lab ceramic, storage concrete/hazard edge, server raised floor and industrial wall panels. Crop each sample and check its edge before using as a seamless tile.
- `Environment/Props/Facility_Props_Atlas.png` — doors, keypad, lab bench, secure cabinet, server racks, terminal, crates, kiosk, CCTV camera, light, shelf, utility cabinet, chair and cart.
- `Environment/Props/Evidence_Clues_Atlas.png` — USB, keycard, access log, CCTV still, envelope, memo, badge, keypad and related evidence props.
- `Environment/Signs/Room_Signs_Decals_Atlas.png` — room and camera plaques, authorized-personnel/CCTV/purge warnings, exit arrows, hazard stripe, direction arrows and evidence markers.

## Characters and effects

- `Characters/FieldAgent_SpriteSheet.png` — 1536×1024; 8 columns × 4 rows; 192×256 px cells. Rows: idle, walk, sneak, investigate/pick-up.
- `Characters/Guard_SpriteSheet.png` — 1536×1024; 8 columns × 4 rows; 192×256 px cells. Rows: idle, patrol walk, search/radio, alert/radio response.
- `VFX/ControlRoom_VFX_SpriteSheet.png` — 1536×1024; 4 columns × 4 rows; 384×256 px cells. Rows: CCTV glitch, CRT sweep, risk pulse, command confirmation.

## Unity use

Importer metadata is included: character, UI-component, prop, sign/decal, CCTV-overlay and VFX atlases are set to **Sprite (2D and UI) / Multiple** with alpha enabled; screen boards, CCTV feed board and level layout are set to **Sprite / Single** for preview; the floor/wall atlas remains a repeating texture. Character sheets slice on an 8×4 grid (192×256 px); the original VFX sheet slices on a 4×4 grid (384×256 px). Review frame bounds in Sprite Editor before making clips. UI, prop and decal atlases are visually organized but not all boundaries are cell-aligned, so slice those manually. The screen boards and level map are visual references, not interactive canvases, drop-in prefabs or 3D meshes.

## Runtime integration

The playable Unity mission uses these generated assets at runtime:

- `Environment/Materials/Facility_Floor_Wall_Tiles.png` supplies the entrance, hall, laboratory, storage, server-room and wall materials. The runtime crops each named surface sample from the atlas and tiles it across the matching geometry.
- `Characters/FieldAgent_SpriteSheet.png` animates the field-unit portrait in the live operations sidebar. `Characters/Guard_SpriteSheet.png` supplies the opposing-security portrait in the mission briefing.
- `UI/Components/Command_UI_Components_Atlas.png` frames the target selector, command input and send control, and supplies color-coded command-status icons.
- `VFX/ControlRoom_VFX_SpriteSheet.png` animates the header pulse when operational risk rises above LOW.

Runtime copies live in `Assets/Resources/GeneratedArt/` so Unity includes them in standalone and WebGL builds. The full-screen UI boards, CCTV feed boards, prop atlases and sign atlases remain art references; their baked placeholder text and views are not substituted for live gameplay state.
