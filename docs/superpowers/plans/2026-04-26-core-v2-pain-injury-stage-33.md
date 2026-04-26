# Core v2 Pain, Injury, And Recovery Implementation Plan, Stage 33

**Scope:** Add traceable pain/injury coupling and simple chemistry-supported recovery in pure `src\Sim`.

**Governing spec:** `docs\superpowers\specs\2026-04-26-creature-simulation-core-v2-design.md`

**Umbrella plan:** `docs\superpowers\plans\2026-04-26-creature-simulation-core-v2-staged-implementation.md`

## Constraints

- Keep the work inside `E:\Creatures Reborn\CreaturesReborn`.
- Do not use `_archive\Darwinbots Unity`.
- Do not touch or stage `scenes\VerticalSlice.tscn`.
- Keep changes in pure `src\Sim`.
- Preserve existing organ-hosted injury and repair behavior.
- Every chemistry behavior change must be visible through `BiochemistryTrace`.

## Stage 33: Pain, Injury, And Recovery

- Use `ChemID.Pain` and `ChemID.Injury` as the core injury loop.
- Make wall bumps and falls inject injury as well as pain/fear.
- Keep organ injury loci active by leaving `Organ.LocInjuryToApply` as the organ damage path.
- Add traceable injury metabolism that raises pain from injury.
- Add traceable recovery that uses ATP and endorphin to reduce injury and pain.

## Verification

- Add failing tests in `NornLifeLoopTests` before implementation.
- Run `dotnet test tests\Sim.Tests\Sim.Tests.csproj --filter NornLifeLoopTests`.
- Run `dotnet test tests\Sim.Tests\Sim.Tests.csproj`.
- Run the unresolved-marker scan across touched source, tests, and this plan.
- Run `git diff --check`.
