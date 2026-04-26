# Core v2 Godot Debug HUD Implementation Plan, Stage 30

**Scope:** Extend the existing Godot `DebugHud` with read-only Core v2 observability surfaces.

**Governing spec:** `docs\superpowers\specs\2026-04-26-creature-simulation-core-v2-design.md`

**Umbrella plan:** `docs\superpowers\plans\2026-04-26-creature-simulation-core-v2-staged-implementation.md`

## Constraints

- Keep biological state in `src\Sim`; the HUD only reads snapshots and catalogs.
- Do not touch or stage `scenes\VerticalSlice.tscn`.
- Do not use `_archive\Darwinbots Unity`.
- Do not change creature behavior, CA math, agent script behavior, or genome loading.
- Avoid scene-file edits in this slice; keep the HUD extension source-only unless a later UI layout pass requires scene wiring.

## Stage 30: Godot Debug Read-Only UI

- Extend `src\Godot\UI\DebugHud.cs`.
- Continue showing existing drive bars, decision text, top chemicals, and lobe activity.
- Add a genome summary panel using `GenomeSummary.Create`.
- Add a named chemical watch using `ChemicalCatalog.Get`.
- Add a brain snapshot summary using `Brain.CreateSnapshot`.
- Add CA room values using `GameMap.CreateCaSnapshot`.
- Add current affordance target text using `AgentAffordanceCatalog.ForAgent`.
- Provide setter methods so world and target references can be wired later without the HUD owning simulation state.

## Verification

- Add failing source coverage in `GodotRuntimeSourceTests`.
- Run `dotnet test tests\Sim.Tests\Sim.Tests.csproj --filter GodotRuntimeSourceTests`.
- Run `dotnet test tests\Sim.Tests\Sim.Tests.csproj`.
- Run `dotnet build CreaturesReborn.csproj`.
- Run `tools\run_godot_smoke.ps1` if the local Godot executable is available.
- Run the unresolved-marker scan across touched files.
- Run `git diff --check`.
