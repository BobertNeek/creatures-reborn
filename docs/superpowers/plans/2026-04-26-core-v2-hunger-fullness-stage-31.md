# Core v2 Hunger And Fullness Implementation Plan, Stage 31

**Scope:** Make hunger/fullness more organism-like while preserving deterministic, traceable simulation behavior.

**Governing spec:** `docs\superpowers\specs\2026-04-26-creature-simulation-core-v2-design.md`

**Umbrella plan:** `docs\superpowers\plans\2026-04-26-creature-simulation-core-v2-staged-implementation.md`

## Constraints

- Keep the work inside `E:\Creatures Reborn\CreaturesReborn`.
- Do not use `_archive\Darwinbots Unity`.
- Do not touch or stage `scenes\VerticalSlice.tscn`.
- Keep changes in pure `src\Sim`.
- Preserve determinism; no new random paths.
- Every behavior change must be visible through `BiochemistryTrace`.

## Stage 31: Hunger And Fullness Rework

- Treat glycogen, muscle, and adipose as the current fullness stores for carbohydrate, protein, and fat hunger.
- Add traceable metabolism that nudges each hunger chemical toward the inverse of its storage chemical.
- Add traceable storage-to-ATP conversion when ATP is low.
- Make eating reward depend on current hunger and fullness, so hungry creatures are reinforced more strongly than full creatures.
- Preserve existing C3/DS-style stimulus IDs and food-kind mappings.

## Verification

- Add failing tests in `NornLifeLoopTests` before implementation.
- Run `dotnet test tests\Sim.Tests\Sim.Tests.csproj --filter NornLifeLoopTests`.
- Run `dotnet test tests\Sim.Tests\Sim.Tests.csproj`.
- Run the unresolved-marker scan across touched source, tests, and this plan.
- Run `git diff --check`.
