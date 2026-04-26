# Core v2 Sleep And Fatigue Implementation Plan, Stage 32

**Scope:** Add a deterministic, chemistry-derived fatigue and sleep pressure loop in pure `src\Sim`.

**Governing spec:** `docs\superpowers\specs\2026-04-26-creature-simulation-core-v2-design.md`

**Umbrella plan:** `docs\superpowers\plans\2026-04-26-creature-simulation-core-v2-staged-implementation.md`

## Constraints

- Keep the work inside `E:\Creatures Reborn\CreaturesReborn`.
- Do not use `_archive\Darwinbots Unity`.
- Do not touch or stage `scenes\VerticalSlice.tscn`.
- Keep changes in pure `src\Sim`.
- Do not add a standalone sleep flag; sleep state is derived from chemicals and exposed through sensorimotor loci.
- Every chemistry behavior change must be visible through `BiochemistryTrace`.

## Stage 32: Sleep And Fatigue Loop

- Use `ChemID.Tiredness` and `ChemID.Sleepiness` as the sleep pressure chemicals.
- Raise tiredness while awake.
- Raise sleepiness as tiredness accumulates.
- Derive the sensorimotor asleep locus from sleep pressure.
- Raise the involuntary sleep locus when sleep pressure crosses the threshold.
- While chemically asleep, reduce tiredness and sleepiness and allow a small ATP recovery.

## Verification

- Stabilize the stateful test suite by disabling xUnit test parallelization.
- Add failing tests in `NornLifeLoopTests` before implementation.
- Run `dotnet test tests\Sim.Tests\Sim.Tests.csproj --filter NornLifeLoopTests`.
- Run `dotnet test tests\Sim.Tests\Sim.Tests.csproj`.
- Run the unresolved-marker scan across touched source, tests, and this plan.
- Run `git diff --check`.
