# Core v2 Passive Brain Module Implementation Plan, Stage 37

**Scope:** Add a diagnostic passive brain module that reads the port registry and emits snapshots without changing brain behavior.

**Governing spec:** `docs\superpowers\specs\2026-04-26-creature-simulation-core-v2-design.md`

**Umbrella plan:** `docs\superpowers\plans\2026-04-26-creature-simulation-core-v2-staged-implementation.md`

## Constraints

- Keep the work inside `E:\Creatures Reborn\CreaturesReborn`.
- Do not use `_archive\Darwinbots Unity`.
- Do not touch or stage `scenes\VerticalSlice.tscn`.
- Keep changes in pure `src\Sim`.
- The diagnostic module must be disabled by default.
- The module must be passive: read brain/chemical ports and create snapshots, but not write neuron state or chemicals.
- Existing `IBrainModule` tests must remain valid.

## Stage 37: Passive Brain Module Example

- Add `DiagnosticBrainModule` implementing `IBrainModule`.
- Add `DiagnosticBrainModuleSnapshot` and `BrainPortReading`.
- Add narrow brain read helpers for port-based module reads.
- Snapshot default brain ports: drive values, reward/punishment/instinct chemicals, and current motor decision.
- Prove registering the module does not alter normal brain outputs.

## Verification

- Add failing tests before production code.
- Run `dotnet test tests\Sim.Tests\Sim.Tests.csproj --filter BrainDiagnosticModuleTests`.
- Run `dotnet test tests\Sim.Tests\Sim.Tests.csproj`.
- Run the unresolved-marker scan across touched source, tests, and this plan.
- Run `git diff --check`.
