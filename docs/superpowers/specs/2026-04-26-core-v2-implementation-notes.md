# Core v2 Implementation Notes

**Date:** 2026-04-26
**Branch:** `feature/core-v2-immune-oxygen`
**Scope:** Creature Simulation Core v2 stages 1-42

## Summary

Core v2 now has the major compatibility and observability surfaces needed before deeper artificial-life tuning continues. The implementation keeps classic Creatures-style systems as the base: raw-compatible genomes, chemical-organ biochemistry, lobe/tract/SVRule brains, CA room fields, CAOS-like agent affordances, and deterministic lab tooling.

The new modern systems are inspectable and opt-in. Plasticity and evolution hooks do not replace the classic brain, and lab tooling runs offline with explicit seeded configuration.

## Genome v2

Genome v2 adds read-only typed inspection over the existing raw `.gen` compatibility layer.

- `GeneRecord`, `GeneHeader`, and `GenePayload` expose gene identity, switch-on age, flags, mutability, variant, raw bytes, and payload kind.
- `GeneDecoder` scans raw genome bytes without moving the runtime genome cursor.
- `GeneValidator` reports structural issues without affecting runtime loading.
- `GenomeSummary`, `GenomeDiff`, `PhenotypeSummary`, `CrossoverReport`, and `MutationReport` make breeding and mutation explainable.

The raw genome remains the source of truth for loading, writing, crossover, mutation, and existing creature boot paths.

## Biochemistry Trace

Biochemistry now has named metadata and optional trace output.

- `ChemicalCatalog` names the current 256-slot chemical space and categorizes energy, drive, reinforcement, hormone, toxin, immune, injury, smell, organ, environment, and unknown chemicals.
- Half-life, reaction, and organ views expose current genome-hosted chemistry without changing reaction math.
- `BiochemistryTrace` and `ChemicalDelta` explain changes from direct sets, stimuli, decay, reactions, receptors, emitters, metabolism, fatigue, injury, environment, respiration, immune response, and toxins.
- Hunger/fullness, sleep/fatigue, pain/injury/recovery, temperature comfort, and oxygen/immune/toxin hooks now feed traceable chemistry.

This preserves the classic model where organism behavior comes from chemical state, organ state, receptors, emitters, reactions, and half-life decay.

## Brain Snapshot

Brain v2 observability keeps the lobe/tract/SVRule substrate visible.

- `BrainSnapshot`, `LobeSnapshot`, `NeuronSnapshot`, `TractSnapshot`, and `DendriteSnapshot` copy brain structure and sampled state for debugging.
- `SVRuleDisassembler` exposes opcode sequences for rule inspection.
- `BrainPortRegistry` names drive, motor, chemical, lobe input, and lobe output ports.
- `BrainModuleDescriptor` describes passive and shadowing modules.
- `LearningTrace` records instinct and reinforcement paths without changing learning math.
- `DiagnosticBrainModule` proves passive module inspection.
- `PlasticityBrainModule` adds an explicitly enabled local Hebbian plasticity experiment with full weight/state snapshots.

The module boundary is deliberately narrow. A module can augment or shadow a lobe only when registered and configured by a caller.

## Creature Trace

Creature ticks now have an optional traceable pipeline.

- `CreatureTickTrace` records context, stimulus, biochemistry, drives, brain, learning, motor, action, reproduction, and age stages.
- Trace options can include biochemistry deltas, brain snapshots, and learning traces.
- Tracing is optional and behavior-preserving; same-seed runs remain deterministic with tracing enabled or disabled.

The creature remains a coordinator for organism subsystems rather than the owner of detailed genome, brain, chemistry, world, or agent behavior.

## CA Fields

World CA fields are now named, queryable, and snapshot-friendly.

- `CaChannelCatalog` maps the current 20 channels to tokens, display names, categories, source links, output links, and descriptions.
- `CaSnapshot`, `RoomCaSnapshot`, and `MetaRoomCaSnapshot` capture room and metaroom field values.
- `CaProducer`, `CaEmission`, `CaConsumer`, and `CaQuery` expose CAOS-like `EMIT`, source, highest-room, lowest-room, and consumer concepts.

The current CA implementation still uses the existing room field math; the new surface makes fields inspectable and usable by tools and future ecology systems.

## Agent Affordances

Agent interaction now has a typed CAOS-like affordance layer.

- `AgentAffordanceCatalog` covers eat, push, pull, hit, activate, pickup, drop, approach, retreat, play, and mate.
- `InteractionEffect`, `InteractionContext`, and `InteractionResult` describe stimulus, chemical, CA emission, script event, and debug-reason outcomes.
- `AgentManager.FireAffordance` bridges affordances to the existing script system without replacing the scriptorium.

This keeps authored agent behavior separate from Godot presentation and makes creature-facing interaction surfaces inspectable.

## Evolution Hooks

Evolution hooks are offline and deterministic.

- `GenomeEvolutionHookSet` derives lobe and tract anchors from decoded brain genes.
- `EvolutionModuleCandidate` creates stable innovation IDs for future NEAT-like and module experiments.
- Plasticity options generated from candidates are disabled by default and require explicit opt-in before shadowing or writing target inputs.

The hooks create experiment candidates. They do not mutate live creatures or register gameplay modules invisibly.

## Lab Runner

The lab runner provides a pure `src/Sim` path for repeatable artificial-life experiments.

- `LabRunConfig` defines seed, ticks, founders, world preset, and optional explicit first-pair breeding.
- `LabRunner` loads seeded populations, ticks a pure `GameWorld`, applies deterministic environment and air-quality signals, and records metrics.
- `LineageRecord`, `LabRunMetrics`, chemical summaries, brain metrics, behavior metrics, environment metrics, crossover reports, and mutation reports provide reproducible outputs.

This gives future breeding, survival, and module experiments a shared metric surface.

## Authoring

The authoring surface is C#-first until external schemas are justified.

- `AuthoringDefinitionBundle` groups chemical metadata, CA presets, agent affordances, and lab configs.
- `AuthoringValidator` returns actionable `AuthoringValidationIssue` records with severity, code, path, and message.
- `CaPresetDefinition` can apply validated CA values to pure `Room` instances.

Invalid definitions report issues instead of affecting simulation state.

## Release Gate

The Core v2 release gate is:

1. `dotnet test tests\Sim.Tests\Sim.Tests.csproj`
2. `dotnet build CreaturesReborn.csproj`
3. Core v2 marker scan over `docs\superpowers` and `src\Sim`
4. `tools\run_godot_smoke.ps1` when Godot is available
5. One multi-creature trace review using biochemistry, brain snapshot, and learning trace options

The helper script `tools\run_core_v2_release_gate.ps1` runs the repeatable command portion. The multi-creature trace review is covered by `CoreV2ReleaseGateTests`.
