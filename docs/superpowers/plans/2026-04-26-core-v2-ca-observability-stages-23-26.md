# Core v2 CA Observability Implementation Plan, Stages 23-26

**Scope:** Build the first read-only Cellular Automata v2 surface in pure `src\Sim`, preserving existing room CA math and agent emission behavior.

**Governing spec:** `docs\superpowers\specs\2026-04-26-creature-simulation-core-v2-design.md`

**Umbrella plan:** `docs\superpowers\plans\2026-04-26-creature-simulation-core-v2-staged-implementation.md`

## Constraints

- Keep the work inside `E:\Creatures Reborn\CreaturesReborn`.
- Do not use `_archive\Darwinbots Unity`.
- Do not touch or stage `scenes\VerticalSlice.tscn`.
- Keep `src\Sim` Godot-free.
- Preserve current CA diffusion, decay, and agent emission behavior.
- Add read-only catalog, snapshot, producer, consumer, and query APIs before any CA behavior changes.

## Stage 23: CA Channel Catalog

- Add `CaChannelDefinition`, `CaChannelCategory`, and `CaChannelCatalog`.
- Map exactly the current `CaIndex.Count` 20 channels.
- Name current physical, source, ecology, danger, and scent channels.
- Include explicit source/output links for existing physical/environmental source pairs.

## Stage 24: CA Snapshots

- Add `CaSnapshot`, `MetaRoomCaSnapshot`, `RoomCaSnapshot`, and channel-value records.
- Add snapshot extension methods for `Room`, `MetaRoom`, and `GameMap`.
- Ensure snapshots copy channel values and do not alias live room CA arrays.

## Stage 25: CA Producer Model

- Add `CaProducer`, `CaEmission`, and `CaEmissionKind`.
- Map existing `Agent.EmitCaIndex`, `Agent.EmitCaAmount`, and `Agent.CurrentRoom` into a typed producer view.
- Do not change `Agent.Tick` or CA emission math.

## Stage 26: CA Consumer And Query Model

- Add `CaQuery`, `CaConsumer`, and `RoomCaReading`.
- Provide read-only helpers for direct channel reads plus highest/lowest room queries.
- Preserve first-room tie behavior by relying on stable iteration order.

## Verification

- Add `tests\Sim.Tests\CaObservabilityTests.cs` first and confirm it fails because APIs are missing.
- Run `dotnet test tests\Sim.Tests\Sim.Tests.csproj --filter CaObservabilityTests`.
- Run `dotnet test tests\Sim.Tests\Sim.Tests.csproj`.
- Run the unresolved-marker scan across touched source, tests, and this plan.
- Run `git diff --check`.
