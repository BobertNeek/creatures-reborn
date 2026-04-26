# Creature Simulation Core v2 Staged Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build Creature Simulation Core v2 as a sequence of inspectable, C3/DS-compatible simulation upgrades.

**Architecture:** Preserve classic Creatures systems instead of replacing them with a black-box model: typed genomes configure brain, biochemistry, organs, stimuli, appearance, and breeding; chemistry and CA fields drive organism state; modern neural systems plug in only as inspectable brain modules. The first implementation slice is behavior-preserving Genome v2 observability.

**Tech Stack:** .NET 8, C#, xUnit, Godot integration only after pure `src/Sim` APIs exist.

---

## Source Scope

- Active repo: `E:\Creatures Reborn\CreaturesReborn`.
- Governing design spec: `docs/superpowers/specs/2026-04-26-creature-simulation-core-v2-design.md`.
- Excluded source material: `_archive\Darwinbots Unity` and all other archived non-Creatures projects.
- Existing user WIP: `scenes\VerticalSlice.tscn` must not be staged, reverted, formatted, or merged into this work.

## Research Anchors

- C3/DS genomes expose switch-on time, silent genes, variants, sex-specific expression, brain lobe/tract genes, receptors, emitters, reactions, half-lives, stimuli, instincts, appearance, and organs.
- C3/DS brains are built from lobes, neurons, dendrites, tracts, STW/LTW state, reinforcement paths, chemical inputs, and SVRule opcodes.
- C3/DS biochemistry is genetically interpreted, reaction-driven, organ-hosted, half-life-decayed, and life-force-sensitive.
- C3/DS room CA fields model spatial environmental state such as heat, light, nutrients, radiation, and smells.
- Modernization should prioritize tools, snapshots, traces, typed metadata, reproducible lab runs, and inspectable module boundaries.

## Global Execution Rules

- [ ] Run `git status --short --branch` before each stage.
- [ ] Run `dotnet test tests\Sim.Tests\Sim.Tests.csproj` before each commit.
- [ ] Stage only intended files for the current stage.
- [ ] Keep `src\Sim` Godot-free.
- [ ] Preserve raw `.gen` byte compatibility unless a later behavior-changing plan explicitly says otherwise.
- [ ] Use injected RNG only for stochastic paths.
- [ ] Push and merge carefully after verified stage commits.

## Stage 1: Plan Artifact And Worktree Discipline

**Files:**
- Create: `docs\superpowers\plans\2026-04-26-creature-simulation-core-v2-staged-implementation.md`

- [ ] Create this plan artifact.
- [ ] Work in an isolated branch or worktree when unrelated user WIP exists.
- [ ] Verify the active worktree is clean except intended files.

## Stage 2: Genome Raw Scan API

**Files:**
- Create: `src\Sim\Genome\GeneRecord.cs`
- Create: `src\Sim\Genome\GeneDecoder.cs`
- Test: `tests\Sim.Tests\GenomeObservabilityTests.cs`

- [ ] Add `GeneHeader`, `GenePayload`, `GenePayloadKind`, `GeneIdentity`, `GeneFamilyIdentity`, and `GeneRecord`.
- [ ] Add `GeneDecoder.Decode(Genome genome)` and `GeneDecoder.DecodeRaw(ReadOnlySpan<byte> raw)`.
- [ ] Decode from `Genome.AsSpan()` without moving the existing genome read pointer.
- [ ] Capture marker offset, total length, type, subtype, id, generation, switch-on age, flags, mutability, variant, payload bytes, raw bytes, and display name.
- [ ] Preserve existing `Genome`, `GenomeReader`, `GenomeWriter`, and runtime consumers.

## Stage 3: Genome Validation

**Files:**
- Create: `src\Sim\Genome\GeneValidator.cs`
- Test: `tests\Sim.Tests\GenomeObservabilityTests.cs`

- [ ] Add `GeneValidator`, `GeneValidationIssue`, `GeneValidationCode`, and `GeneValidationSeverity`.
- [ ] Validate raw byte structure without throwing for valid genomes.
- [ ] Report bad marker, truncated marker, truncated header, missing end marker, unknown gene type, unknown subtype, impossible length, invalid variant, and conflicting sex-link flags.
- [ ] Keep valid starter-compatible genomes validation-clean.

## Stage 4: Genome Family Summaries

**Files:**
- Create: `src\Sim\Genome\GenomeSummary.cs`
- Test: `tests\Sim.Tests\GenomeObservabilityTests.cs`

- [ ] Add `GenomeSummary.Create(Genome genome)`.
- [ ] Group genes by top-level family and subtype.
- [ ] Surface brain lobe/tract, biochemistry receptor/emitter/reaction/half-life/inject/neuroemitter, creature stimulus/instinct/appearance/pigment/pose/gait, and organ counts.
- [ ] Keep counts consistent with existing `Genome.CountGeneType`.

## Stage 5: Genome Diff

**Files:**
- Create: `src\Sim\Genome\GenomeDiff.cs`
- Test: `tests\Sim.Tests\GenomeObservabilityTests.cs`

- [ ] Add `GenomeDiff`, `GeneDiffRecord`, and `GeneDiffKind`.
- [ ] Compare genes by type, subtype, id, generation, and raw bytes.
- [ ] Report added, removed, changed, duplicated, and reordered genes.
- [ ] Treat clone-generation duplicate families as biologically meaningful duplicates.

## Stage 6: Phenotype Summary

**Files:**
- Create: `src\Sim\Genome\PhenotypeSummary.cs`
- Test: `tests\Sim.Tests\GenomeObservabilityTests.cs`

- [ ] Add `PhenotypeSummary`, `PhenotypeSection`, and `PhenotypeSummarizer`.
- [ ] Summarize designer-readable sections: brain structure, organ chemistry, stimulus learning, appearance, reproduction.
- [ ] Do not feed phenotype summaries back into runtime behavior yet.

## Stage 7: Crossover And Mutation Reports

**Files:**
- Create: `src\Sim\Genome\CrossoverReport.cs`
- Test: `tests\Sim.Tests\GenomeObservabilityTests.cs`

- [ ] Add `CrossoverReport.Create(...)`.
- [ ] Add `MutationReport.FromParentAndChild(...)`.
- [ ] Report parent monikers, child moniker, child summary, legacy crossover count, legacy mutation count, and parent-child diffs.
- [ ] Preserve existing crossover and mutation rates.

## Stage 8: Genome Tests And Guardrails

**Files:**
- Test: `tests\Sim.Tests\GenomeObservabilityTests.cs`

- [ ] Verify the new API with synthetic C3/DS-style genes.
- [ ] Verify valid genomes produce no validation issues.
- [ ] Verify malformed raw bytes report issues instead of corrupting runtime state.
- [ ] Verify diff and reports do not mutate genome bytes.
- [ ] Run the full simulation test suite.

## Remaining Stages

9. Chemical catalog.
10. Half-life metadata.
11. Biochemical reaction metadata.
12. Organ metadata.
13. Chemical delta trace.
14. Biochemistry determinism tests.
15. Stimulus trace integration.
16. Brain snapshot.
17. SVRule introspection.
18. Brain port registry.
19. Brain module metadata.
20. Learning trace.
21. Creature tick trace.
22. Creature pipeline tests.
23. CA channel catalog.
24. CA snapshots.
25. CA producer model.
26. CA consumer/query model.
27. Agent affordance catalog.
28. Interaction effects.
29. Agent script bridge.
30. Godot debug read-only UI.
31. Hunger and fullness rework.
32. Sleep and fatigue loop.
33. Pain, injury, and recovery.
34. Temperature and comfort.
35. Immune, toxin, oxygen hooks.
36. Reward/punishment learning tuning.
37. Passive brain module example.
38. Plasticity module experiment.
39. Evolutionary module/lab hook.
40. Lineage and lab runner.
41. Authoring and modding surface.
42. Docs, tooling, and release gate.

Each remaining stage gets its own detailed task plan before code changes.
