# Core v2 Agent Interaction Implementation Plan, Stages 27-29

**Scope:** Add the first typed, behavior-preserving Agent v2 affordance and interaction surface in pure `src\Sim`.

**Governing spec:** `docs\superpowers\specs\2026-04-26-creature-simulation-core-v2-design.md`

**Umbrella plan:** `docs\superpowers\plans\2026-04-26-creature-simulation-core-v2-staged-implementation.md`

## Constraints

- Keep the work inside `E:\Creatures Reborn\CreaturesReborn`.
- Do not use `_archive\Darwinbots Unity`.
- Do not touch or stage `scenes\VerticalSlice.tscn`.
- Keep `src\Sim` Godot-free.
- Preserve `AgentManager.RegisterScript` and `AgentManager.FireScript`.
- Do not implement a full CAOS VM in this slice.
- Keep the new surface data-oriented and narrow so Godot can read it later without owning biological behavior.

## Stage 27: Agent Affordance Catalog

- Add `AgentAffordanceKind`, `AgentAffordance`, and `AgentAffordanceCatalog`.
- Cover eat, push, pull, hit, activate, pickup, drop, approach, retreat, play, and mate.
- Map affordances to current creature `VerbId`, target script events, stimulus IDs where known, and existing `AgentBehaviour` or `AgentAttr` gates.
- Provide catalog queries by kind and by current `Agent` plus optional `AgentArchetype`.

## Stage 28: Interaction Effects

- Add `InteractionContext`, `InteractionEffect`, `InteractionEffectKind`, and `InteractionResult`.
- Represent stimulus, direct chemical delta, CA emission, script event, and debug-reason effects.
- Keep effects declarative in this slice; creature chemistry and world CA application remain explicit caller choices.

## Stage 29: Agent Script Bridge

- Add an affordance-to-script bridge that wraps the existing scriptorium.
- Preserve `RegisterScript` and `FireScript` behavior.
- Return an `InteractionResult` that records whether the target script fired and which effects were declared.

## Verification

- Add `tests\Sim.Tests\AgentInteractionTests.cs` first and confirm it fails because APIs are missing.
- Run `dotnet test tests\Sim.Tests\Sim.Tests.csproj --filter AgentInteractionTests`.
- Run `dotnet test tests\Sim.Tests\Sim.Tests.csproj`.
- Run the unresolved-marker scan across touched source, tests, and this plan.
- Run `git diff --check`.
