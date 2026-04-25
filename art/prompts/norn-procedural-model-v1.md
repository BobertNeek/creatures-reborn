# Norn Procedural Model V1

Purpose: experimental runtime 3D model for Creatures Reborn Norns. It remains available for iteration, but normal play uses the legacy imported GLB until a better authored/generated 3D replacement is ready.

Style target: original, closer tribute to Creatures 3 / Docking Station. Rounded pet-sim proportions, warm amber fur, cream skin areas, teal markings, glossy expressive eyes, dark soft hair tuft, readable baby-like head shape, squat limbs, and a tail that reads clearly in side-view.

Runtime contract:
- Built from named Godot 3D mesh parts in `src/Godot/NornModelFactory.cs`.
- Part names intentionally mirror the previous GLB mesh names so `NornAppearanceApplier` and `NornBillboardSprite` can keep one tint/scale/animation map.
- `CreatureAppearance` drives sex dimorphism, age scale, body part proportions, material tint, eye color, hair scale, and mutation-visible variation.
- Enable this model only when `UseProceduralModel` is true.

QA notes:
- Must be true 3D geometry, not sprite sheets.
- Must keep feet visible on Treehouse floors.
- Must support male/female comparison screenshots.
- Must not require external GLB texture imports when it becomes production-ready.
