# Core v2 Temperature And Comfort Implementation Plan, Stage 34

**Scope:** Connect room cellular automata fields to creature temperature comfort in pure `src\Sim`.

**Governing spec:** `docs\superpowers\specs\2026-04-26-creature-simulation-core-v2-design.md`

**Umbrella plan:** `docs\superpowers\plans\2026-04-26-creature-simulation-core-v2-staged-implementation.md`

## Constraints

- Keep the work inside `E:\Creatures Reborn\CreaturesReborn`.
- Do not use `_archive\Darwinbots Unity`.
- Do not touch or stage `scenes\VerticalSlice.tscn`.
- Keep changes in pure `src\Sim`.
- Reuse existing CA channel definitions for temperature, light, and radiation.
- Do not add hidden randomness; this stage is deterministic.
- Every chemistry change from environment coupling must be visible through `BiochemistryTrace`.

## Stage 34: Temperature And Comfort

- Add a small creature environment context that can be built from a `Room`.
- Map `CaIndex.Temperature` to hotness/coldness sensorimotor loci and chemicals.
- Map `CaIndex.Light` to the light-level sensorimotor locus.
- Map `CaIndex.Radiation` to the radiation sensorimotor locus plus conservative fear/punishment stress.
- Queue hotness, coldness, comfort, and fear drive inputs from the environment response.
- Keep neutral rooms restorative: reduce hotness/coldness without adding punishment.
- Keep the API usable by Godot later without moving biological logic into Godot nodes.

## Verification

- Add failing tests for hot, cold, neutral, and radiation/light room environments.
- Run `dotnet test tests\Sim.Tests\Sim.Tests.csproj --filter CreatureEnvironmentTests`.
- Run `dotnet test tests\Sim.Tests\Sim.Tests.csproj`.
- Run the unresolved-marker scan across touched source, tests, and this plan.
- Run `git diff --check`.
