# Core v2 Immune, Toxin, Oxygen Hooks Implementation Plan, Stage 35

**Scope:** Add conservative, traceable immune, toxin, air-quality, and oxygen hooks in pure `src\Sim`.

**Governing spec:** `docs\superpowers\specs\2026-04-26-creature-simulation-core-v2-design.md`

**Umbrella plan:** `docs\superpowers\plans\2026-04-26-creature-simulation-core-v2-staged-implementation.md`

**Research anchor:** C3/DS chemistry includes air, oxygen, ATP/ADP, named toxins, antigens 0-7, wounded, and antibodies 0-7; toxins such as ATP decoupler and carbon monoxide disrupt ATP and oxygen paths. Reference: https://creatures.wiki/C3_Chemical_List

## Constraints

- Keep the work inside `E:\Creatures Reborn\CreaturesReborn`.
- Do not use `_archive\Darwinbots Unity`.
- Do not touch or stage `scenes\VerticalSlice.tscn`.
- Keep changes in pure `src\Sim`.
- Add simple hooks only; do not build a full bacteria/disease lifecycle yet.
- Do not make baseline creatures suffocate just because starter genomes do not yet seed oxygen.
- Every chemistry change from respiration, immune response, or toxins must be visible through `BiochemistryTrace`.

## Stage 35: Immune, Toxin, Oxygen Hooks

- Name classic air, oxygen, toxin, wounded, and antibody chemical slots in `ChemID` and `ChemicalCatalog`.
- Add trace sources for respiration, immune response, and toxins.
- Add an explicit air-quality application path that sets the sensorimotor air-quality locus and adjusts air/oxygen.
- Add oxygen-stress behavior only after respiration has been signaled or toxin chemistry is present.
- Add simple toxin effects:
  - ATP decoupler converts ATP to ADP.
  - Carbon monoxide reduces oxygen.
  - Cyanide, glycotoxin, geddonase, muscle toxin, sleep toxin, fever toxin, fear toxin, and wounded produce small direct effects.
  - High toxin load raises injury and queues organ injury.
- Add simple immune effects:
  - Matching antibodies reduce matching antigens.
  - Antigens auto-raise matching antibodies slowly.
  - Antigen side effects follow the C3/DS pattern at a low, traceable rate.

## Verification

- Add failing tests for low air quality, ATP decoupler, antibody neutralization, antigen side effects, and toxin organ stress.
- Run `dotnet test tests\Sim.Tests\Sim.Tests.csproj --filter CreatureImmuneOxygenTests`.
- Run `dotnet test tests\Sim.Tests\Sim.Tests.csproj`.
- Run the unresolved-marker scan across touched source, tests, and this plan.
- Run `git diff --check`.
