# Core v2 Reward/Punishment Learning Tuning Implementation Plan, Stage 36

**Scope:** Make reward and punishment reinforcement inspectable through `LearningTrace` before changing learning math.

**Governing spec:** `docs\superpowers\specs\2026-04-26-creature-simulation-core-v2-design.md`

**Umbrella plan:** `docs\superpowers\plans\2026-04-26-creature-simulation-core-v2-staged-implementation.md`

## Constraints

- Keep the work inside `E:\Creatures Reborn\CreaturesReborn`.
- Do not use `_archive\Darwinbots Unity`.
- Do not touch or stage `scenes\VerticalSlice.tscn`.
- Keep changes in pure `src\Sim`.
- Preserve existing SVRule, STW/LTW, reward, and punishment math unless trace-backed tests expose a concrete bug.
- Learning trace must be optional and must not alter untraced brain behavior.

## Stage 36: Reward/Punishment Learning Tuning

- Add a `Brain.Update(LearningTrace?)` path that leaves existing `Brain.Update()` behavior unchanged.
- Trace reward and punishment reinforcement at the tract/dendrite level with before/after short-term weights.
- Record tract index, chemical ID, chemical level, dendrite ID, source neuron, destination neuron, and reinforcement kind.
- Integrate optional learning traces into `CreatureTickTrace`.
- Add tests for positive reward and punishment reinforcement paths.
- Add a determinism test proving traced and untraced brain ticks produce the same brain/motor-visible outputs.

## Verification

- Add failing tests before production code.
- Run `dotnet test tests\Sim.Tests\Sim.Tests.csproj --filter BrainLearningTests`.
- Run `dotnet test tests\Sim.Tests\Sim.Tests.csproj`.
- Run the unresolved-marker scan across touched source, tests, and this plan.
- Run `git diff --check`.
