# Creatures Reborn Art Direction

## Direction

Creatures Reborn should feel like a closer tribute to Creatures 3 and Docking Station without directly remastering their assets. The target is cozy biotech: warm nursery spaces, readable machine silhouettes, organic rooms, soft alien science, and expressive pet-sim creatures.

## Runtime Shape

- Use generated 2D images for metarooms, agent sprites, food, eggs, props, UI icons, and concept sheets.
- Keep Norns as true 3D runtime geometry. The default model is an original Godot procedural mesh assembled from named parts; regenerated texture maps and DNA-driven material/proportion variation are applied at runtime.
- Treat generated Norn images as concept and texture direction unless a later milestone explicitly replaces the model geometry.
- Keep metaroom floors visually clear. Detailed backgrounds must not hide creature feet, food, eggs, stairs, or elevator pads.

## Asset Rules

- Every generated asset needs a manifest record in `art/manifest.json`.
- Every generation prompt should be saved under `art/prompts/`.
- Runtime paths must be stable before replacing code references.
- Prefer transparent PNGs for agent, food, egg, prop, and UI sprites.
- Do not overwrite preserved current art until the replacement imports in Godot and passes visual QA.

## Visual QA

- Asset has readable silhouette at in-game scale.
- Background art keeps walkable surfaces clear.
- Colors distinguish creatures, food, eggs, hand pointer, and UI from the room.
- Image dimensions match the manifest or prompt note.
- Godot can load the file from the recorded `res://` runtime path.
