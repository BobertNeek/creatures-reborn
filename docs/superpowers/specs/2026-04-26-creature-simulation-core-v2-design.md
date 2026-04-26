# Creature Simulation Core v2 Design Spec

**Date:** 2026-04-26
**Repo:** Creatures Reborn
**Status:** Design baseline for later implementation plans

## Purpose

Creature Simulation Core v2 is the target architecture for making Creatures Reborn feel closer to the deep artificial-life promise of Creatures 3 and Docking Station while using a modern, modular, inspectable simulation stack.

This spec is not an implementation plan. It captures the research baseline, maps it to the current repo, and defines the direction for future subsystem-specific implementation plans.

The central design goal is not to replace the classic systems with a black-box AI creature. The goal is to preserve what made C3/DS powerful: readable genetics, breedable variation, chemistry-driven needs, lobe-and-tract brains, environmental cellular automata, and emergent behavior from many coupled systems. Modern technology should make those systems clearer, richer, easier to debug, and easier to evolve.

## Research Baseline

### Classic C3/DS System Shape

Creatures 3 and Docking Station are best understood as a coupled artificial-life runtime:

- CAOS agents provide world objects, scripts, events, classifiers, and interaction surfaces.
- Genomes define creature body, instincts, brain layout, biochemical organs, reactions, emitters, receptors, appearance, drives, and lifespan traits.
- Brains use lobes, neurons, dendrites, tracts, state variables, and SVRules rather than one opaque monolithic neural network.
- Biochemistry models chemicals, reactions, organs, receptors, emitters, half-lives, drives, toxins, hormones, and life support.
- Cellular automata fields carry environmental state such as heat, light, nutrients, smells, and other room-based gradients.
- Breeding combines genomes, mutation, gene expression timing, sex, age, and viability constraints into a heritable ecosystem.

The most important lesson is that each subsystem is individually legible but behavior emerges from their feedback loops. A creature does not decide from brain state alone. It decides from brain state under drives, chemistry, instincts, stimuli, world fields, agent affordances, and genetic variation.

### CAOS And Agents

CAOS in C3/DS is both a scripting language and a world object model. Agents have classifiers, event scripts, attributes, object variables, and interaction verbs. The scripting system makes the world extensible without requiring every object behavior to be compiled into the core engine.

For Creatures Reborn, the durable lesson is the separation between:

- agent identity and classification,
- world object state,
- event dispatch,
- creature perception and interaction,
- authored object behavior.

Modern implementation should keep the agent system strongly typed internally while preserving CAOS-like data-driven extensibility at the boundary. The simulation core should not depend on Godot scene details to understand what an object affords.

### Genetics And Breeding

C3/DS genomes are not just cosmetic DNA. Genes affect anatomy, appearance, brain construction, instincts, emitters, receptors, reactions, organs, drives, and development. Mutation and crossover matter because genes are executable biological configuration.

Important classic properties to preserve:

- A genome must be inspectable as genes, not only bytes.
- A gene must have expression timing, sex dependency, mutation behavior, and meaningful payload.
- Crossover must preserve enough structure that offspring are usually viable but still surprising.
- Mutation must be explicit, seed-driven, and observable.
- Genetic changes should produce phenotypic explanations: what changed, where, and likely behavioral effect.

Community genome work such as CFF, TWB, and related C3/DS breeds shows that small biochemical and instinct changes can produce major survival differences. Hunger/fullness, thermoregulation, immune behavior, oxygen, wound handling, fertility, social behavior, and navigation are especially important.

### Brain Internals

The classic brain is a modular neural system built from lobes and tracts. Lobes contain neurons. Tracts connect lobes through dendrites. SVRules update neuron and dendrite state. Instincts provide genetically defined initial training impulses.

This has several valuable properties:

- The brain is inspectable at lobe, neuron, dendrite, and rule level.
- The genome can alter structure, dynamics, and initial behavior.
- Learning is local and biologically themed rather than one global optimizer.
- Different brain regions can have different update rules and meanings.

Modern upgrades should therefore extend the brain through modules, not flatten it into a single generic model. Strong candidates for future modules include:

- neuromodulated Hebbian learning,
- differentiable-plasticity-style local learning,
- reservoir or recurrent modules for short-term dynamics,
- episodic or place-memory modules,
- action arbitration modules that sit beside SVRule lobes,
- learned perception encoders that feed classic lobe inputs without owning the whole creature.

The compatibility rule is simple: classic SVRule lobe behavior remains the base substrate until a specific module intentionally shadows or augments a lobe.

### Biochemistry

C3/DS biochemistry turns creature behavior into organism behavior. Hunger, pain, fear, tiredness, reward, punishment, hormones, toxins, immune response, aging, injury, and organ failure all flow through chemical state.

Modern implementation should treat chemistry as a reaction network:

- chemicals have concentration, decay, category, and debug metadata,
- reactions consume and produce chemicals under rate rules,
- organs host reactions, receptors, and emitters,
- brain loci and body loci can read or write chemical effects,
- drives are derived from chemistry rather than maintained as unrelated counters,
- disease and injury are chemical and organ perturbations, not separate gameplay flags.

The key upgrade is observability. Every major chemical change should be explainable by source: decay, reaction, receptor, emitter, stimulus, agent interaction, aging, injury, or reproduction.

### Cellular Automata And Environment

C3/DS room cellular automata fields provide spatial gradients and environmental memory. They let the world hold information that creatures can sense and react to without every decision being scripted.

Creatures Reborn should keep this concept and expand it carefully. Candidate field groups:

- physical: temperature, heat source, light, rainfall,
- ecology: nutrient water, minerals, food scent, plant density,
- danger: toxin, radiation, predator, injury risk,
- navigation: home scent, object scent, creature scent, social density,
- comfort: sleep safety, crowding, noise, humidity.

The design principle is that fields should be useful to creatures and agents, not decorative. Each new field needs at least one producer, one consumer, and one debug visualization before it becomes part of the core.

### Chaos And Determinism

Classic Creatures behavior feels alive partly because many systems are noisy and coupled. Modern implementation should keep chaos, but make it seed-driven and controllable.

Rules for future stochastic behavior:

- all randomness flows through injected RNG interfaces,
- lab mode uses fixed seeds and repeatable initial state,
- wild mode can use generated seeds but still logs them,
- mutation, perception noise, behavior noise, world events, and stochastic reactions each have identifiable RNG streams,
- debug logs can explain important stochastic decisions when enabled.

This matches the current repo's direction of explicit RNG and keeps testing possible even when behavior is probabilistic.

## Current Repo Mapping

### `src\Sim\Genome`

Current role:

- Reads and writes C3/DS-style genome data.
- Preserves the `dna3`, `gene`, and `gend` structure.
- Implements crossover and mutation mechanics over raw gene bytes.
- Exposes gene type identification.

V2 direction:

- Add typed gene decoding beside raw byte preservation.
- Keep raw compatibility while introducing validated gene records.
- Produce genome diffs and phenotype summaries.
- Make mutation/crossover reports inspectable.

The existing raw genome layer should remain the compatibility floor. Typed genes should wrap it rather than replace it abruptly.

### `src\Sim\Brain`

Current role:

- Builds lobes, tracts, instincts, and SVRule-driven components from genome data.
- Supports neuron and dendrite state update.
- Provides `IBrainModule` as an extensibility interface.
- Allows a module to shadow a specific lobe token.

V2 direction:

- Keep SVRule as the default classic runtime.
- Treat `IBrainModule` as the upgrade boundary for modern brain modules.
- Add brain snapshots, lobe summaries, tract summaries, and debug timelines.
- Define stable brain input and output ports so Godot and creature code do not reach through internals unnecessarily.
- Replace current incomplete script-offset mapping with an explicit brain-locus table when the surrounding CAOS/script model is ready.

The current module hook is valuable and should be preserved. Future neural upgrades should plug into it instead of bypassing the brain architecture.

### `src\Sim\Biochemistry`

Current role:

- Tracks 256 chemical concentrations.
- Applies half-life decay.
- Loads genome-defined neuroemitters and organs.
- Runs organ reactions, receptors, and emitters.
- Connects biochemical loci to brain and creature state.

V2 direction:

- Promote chemical definitions into named metadata records.
- Emit chemical delta traces for debugging.
- Model drives as chemical-derived organism state.
- Add richer organ health, injury, repair, immune, oxygen, temperature, and reproduction pathways.
- Keep concentrations bounded and deterministic under fixed seeds.

The current organ/reaction/receptor/emitter shape is already close to the desired substrate. The main upgrade is typed metadata, explanation, and expanded biology.

### `src\Sim\Creature`

Current role:

- Composes genome, biochemistry, brain, motor faculty, drives, age, appearance, stimuli, and reproduction rules.
- Ticks creature state in a consistent order.
- Connects world context into creature drives and brain inputs.
- Applies food, wall-bump, egg-laying, and other life-loop stimuli.

V2 direction:

- Make the creature tick pipeline explicit as a documented organism lifecycle.
- Separate body state, drive state, stimulus intake, decision output, and motor output into narrow interfaces.
- Keep creature as coordinator, not a god object.
- Add per-tick organism traces for brain, chemistry, perception, stimulus, action, and reproduction.

The creature object should remain the place where organism subsystems meet, but future work should avoid putting detailed subsystem behavior directly into it.

### `src\Sim\World`

Current role:

- Owns map, metarooms, rooms, room navigation, world time, agents, creature population defaults, and CA field updates.
- Defines 20 CA channels aligned with C3/DS-style fields.

V2 direction:

- Make CA fields named, described, visualizable, and queryable by creatures and agents.
- Add producers and consumers for each field.
- Separate world topology from environmental field simulation.
- Add deterministic world snapshots for lab runs.

World simulation should become the shared substrate for ecology, navigation, and environmental sensing.

### `src\Sim\Agent`

Current role:

- Defines agents, classifiers, attributes, script events, catalog entries, and an agent manager.
- Maintains a scriptorium keyed by classifier and event.

V2 direction:

- Strengthen the CAOS-like event model without recreating the entire CAOS language immediately.
- Define typed affordances that creatures can perceive and act on.
- Make object interactions produce stimulus and chemical effects through data rather than scattered direct calls.
- Support future authored agent behavior through a constrained script or rule layer.

The agent system is the natural place to preserve the CAOS spirit while keeping the engine maintainable.

### Godot Integration

Current role:

- Hosts visual creature nodes, metaroom nodes, food, eggs, gadgets, incubators, UI, imports, pose animation, and debug HUD.
- Bridges player interaction and visual state into simulation objects.

V2 direction:

- Keep Godot as presentation, input, and scene composition layer.
- Keep long-lived biological state in `src\Sim`.
- Add debug panels and visual overlays that read snapshots from the simulation core.
- Avoid encoding core biological behavior only in Godot nodes.

The design boundary should be clear: Godot can present and initiate interactions, but the simulation core owns organism truth.

## V2 Principles

### Preserve Legibility

Every important system should be inspectable at the level designers and advanced players care about:

- genes and expression,
- chemicals and reactions,
- lobes and tracts,
- drives and stimuli,
- CA fields,
- lineage and mutation history.

Modern AI techniques are acceptable only when their inputs, outputs, and training or update rules can be inspected enough to support debugging and breeding.

### Keep Genetics Executable

Genes should remain the source of creature variation. Configuration files can define defaults and metadata, but the genome must decide the organism. If a trait affects survival, behavior, development, or reproduction, it should have a path to heritability.

### Build Compatibility Layers

Classic C3/DS data compatibility and modern typed systems should coexist:

- raw genome bytes stay loadable,
- typed gene views decode the bytes,
- modern modules can augment old lobes,
- agent behavior can become more data-driven without requiring immediate full CAOS support.

This reduces rewrite risk and allows testable vertical slices.

### Prefer Coupled Small Systems Over One Large Model

Emergence should come from many focused systems interacting:

- chemistry changes drives,
- drives bias brain state,
- brain selects actions,
- actions affect agents and world fields,
- world fields alter perception and chemistry,
- breeding carries successful configurations forward.

This makes behavior easier to debug than a single all-purpose model and keeps the classic artificial-life character.

### Make Chaos Explicit

The simulation should support surprising behavior without hidden randomness. Every stochastic path should be reproducible in lab mode and logged enough to diagnose.

### Design For Tools Early

Deep artificial life needs strong tooling. Future implementation plans should treat debug tools as first-class, not as optional polish.

Minimum long-term tool surfaces:

- genome browser and gene diff,
- biochemistry graph and chemical timeline,
- brain lobe and tract viewer,
- CA field overlay,
- lineage tree,
- organism tick trace,
- deterministic experiment runner.

## Target Architecture

### Genome v2

Genome v2 should introduce a typed decoding layer over the existing raw genome model.

Core concepts:

- `RawGenome`: byte-compatible source representation.
- `GeneRecord`: typed view with header, type, subtype, expression metadata, and payload.
- `GeneDecoder`: converts raw genes into typed records.
- `GeneValidator`: checks structural validity, missing dependencies, impossible values, and dangerous ranges.
- `GenomeDiff`: compares parents, offspring, and mutations.
- `PhenotypeSummary`: human-readable summary of likely expressed effects.

The first implementation slice should be read-only and behavior-preserving. It should decode and explain existing genomes without changing creature runtime behavior. Runtime adoption can happen gene family by gene family.

### Biochemistry v2

Biochemistry v2 should keep the 256-slot compatibility model while adding named metadata and traceable reactions.

Core concepts:

- `ChemicalDefinition`: id, token/name, category, normal range, debug color, decay policy.
- `ChemicalState`: current concentration and recent delta history.
- `ReactionDefinition`: inputs, outputs, catalyst or condition, rate, owning organ.
- `OrganSystem`: organ health, energy cost, injury, repair, failure effects.
- `BiochemistryTrace`: per-tick explanation of meaningful concentration changes.

Future biology expansions should be staged around survival loops:

- hunger and fullness,
- sleep and fatigue,
- pain and injury,
- temperature and comfort,
- oxygen and suffocation,
- immune response and toxins,
- reproduction hormones and pregnancy,
- reward, punishment, and learning chemistry.

### Brain v2

Brain v2 should preserve the current lobe/tract/SVRule implementation and formalize extension points.

Core concepts:

- `BrainSnapshot`: read-only view of lobes, tracts, neurons, dendrites, drives, and decisions.
- `BrainPort`: named input/output mapping between body, chemistry, world, and brain.
- `BrainModuleDescriptor`: metadata for modules that augment or shadow lobes.
- `LearningTrace`: record of instinct firing, reward/punishment, dendrite change, and module output.

Module candidates for later plans:

- classic SVRule lobe runtime cleanup and coverage,
- neuromodulated Hebbian module,
- short-term recurrent memory module,
- spatial/place memory module,
- action arbitration module,
- perception encoder module.

The first implementation step should be observability. New behavior modules should come only after snapshots and tests can show what the classic brain is doing.

### Creature Organism Pipeline

The creature tick should be documented and eventually enforced as a narrow pipeline:

1. Gather world context and perception.
2. Apply external stimuli.
3. Update chemistry and organs.
4. Derive drives and brain inputs.
5. Update brain and learning.
6. Select motor/action output.
7. Apply action to world and agents.
8. Update age, reproduction state, and lifecycle.
9. Emit debug trace.

This order can be refined during implementation planning, but future work should avoid hidden side effects that bypass the pipeline.

### World And CA v2

World v2 should turn CA fields into first-class environmental signals.

Core concepts:

- `CaChannelDefinition`: id, name, category, diffusion, decay, visualization color, range.
- `CaProducer`: agent, room, metaroom, weather, or creature source.
- `CaConsumer`: perception, plant growth, comfort, toxicity, navigation, or ecology logic.
- `CaSnapshot`: room-level field values for debugging and deterministic runs.

Each CA field must be justified by gameplay or organism function. A field should not be added unless a future implementation plan includes both a producer and a consumer.

### Agent And Interaction v2

Agent v2 should preserve C3/DS-style extensibility while keeping strongly typed engine boundaries.

Core concepts:

- `AgentClassifier`: stable identity and grouping.
- `AgentAffordance`: actions available to creatures or player.
- `InteractionEffect`: stimulus, chemical, world-field, inventory, or script event effect.
- `AgentEventScript`: constrained future scripting or rule hook.

This gives food, toys, machines, plants, eggs, and gadgets a common interaction model. Creature behavior can then reason about affordances rather than Godot node classes.

### Lab And Evolution Tooling

A modern implementation should include a lab mode for reproducible artificial-life experiments.

Core concepts:

- deterministic run config,
- seed and RNG stream logging,
- initial population definition,
- environment preset,
- metric collection,
- lineage tracking,
- genome and phenotype export,
- replay or snapshot comparison.

Possible later algorithms include MAP-Elites, NEAT-style topology search, novelty search, and constrained mutation sweeps. These should be offline tools first, not hidden in normal gameplay.

## Staged Roadmap

### Stage 1: Observability Without Behavior Changes

Goal: make current systems explainable before making them more complex.

Expected outputs:

- typed genome read-only views,
- genome diff and summary tools,
- chemical metadata and per-tick traces,
- brain snapshots for lobes, tracts, and module outputs,
- CA field debug descriptions,
- deterministic organism trace format.

This stage should preserve existing behavior except for debug-only output.

### Stage 2: Stronger Organism Loops

Goal: improve survival, comfort, and learning loops through chemistry and stimuli.

Expected outputs:

- hunger/fullness rework,
- fatigue and sleep chemistry,
- pain/injury/recovery path,
- temperature comfort and stress,
- richer reward/punishment signals,
- clearer stimulus-to-chemistry-to-brain flow.

This stage should produce more coherent creature behavior while remaining compatible with current genome loading.

### Stage 3: Environment And Ecology

Goal: make rooms and agents participate in life-supporting environmental systems.

Expected outputs:

- named CA fields with producers and consumers,
- food/plant nutrient loops,
- home and object scent navigation,
- crowding and social-density signals,
- CA overlays in debug UI,
- deterministic world snapshots.

This stage should make creature choices depend more on place and ecology.

### Stage 4: Brain Module Expansion

Goal: add modern brain modules only after classic brain behavior is visible.

Expected outputs:

- stable brain port registry,
- module descriptors and lifecycle tests,
- learning traces,
- one behavior-preserving module example,
- one experimental module behind a config flag.

This stage should prove that modern modules can coexist with SVRule lobes rather than replacing them wholesale.

### Stage 5: Breeding, Lineage, And Evolution Lab

Goal: make long-running populations measurable, reproducible, and evolvable.

Expected outputs:

- lineage tree and ancestry records,
- mutation and crossover reports,
- fertility and viability metrics,
- experiment runner,
- population dashboards,
- offline search workflows for generating candidate genomes.

This stage should support both player-facing breeding and developer-facing lab experiments.

### Stage 6: Authoring And Modding Surface

Goal: expose safe, data-driven creation surfaces for agents, genes, world fields, and experiments.

Expected outputs:

- agent affordance definitions,
- interaction effect schemas,
- genome and chemistry metadata files,
- world-field presets,
- validation tooling,
- import/export paths for advanced users.

This stage should be based on proven internal APIs from earlier stages rather than guessed too early.

## Design Constraints For Future Implementation Plans

- Keep files focused and modular. Avoid moving detailed genome, brain, chemistry, world, and agent behavior into one coordinator object.
- Preserve raw C3/DS compatibility where the repo already supports it.
- Add read-only views and traces before changing behavior.
- Keep all stochastic behavior behind injected RNG.
- Add tests at the subsystem boundary where behavior can be deterministic.
- Do not let Godot nodes become the source of biological truth.
- Do not add new CA fields, chemicals, or brain modules without debug visibility.
- Keep implementation plans small enough that each plan produces working, testable software on its own.

## Suggested Follow-On Implementation Plans

After this spec is reviewed, split implementation into separate plans:

1. Genome v2 read-only decoder, validator, diff, and phenotype summary.
2. Biochemistry metadata and trace system.
3. Brain snapshot and port registry.
4. Creature organism tick trace and pipeline documentation.
5. CA channel definitions, producers, consumers, and debug overlay.
6. Agent affordance and interaction-effect model.
7. Lineage and deterministic lab runner.

The recommended first implementation plan is Genome v2 read-only decoder and diff tooling, because it creates a safe foundation for all later genetics, breeding, and lab work without changing runtime behavior.

## Source Notes

Research inputs for this spec include:

- Creatures Wiki: Brain, Biochemistry, CAOS, and Cellular Automata pages.
- Creatures Caves resource archive and community material.
- EemFoo archived Creatures community material.
- Official and community C3/DS Genetics Kit documentation.
- OpenC2E project documentation and source direction.
- Community genome projects and breed notes such as CFF, TWB, and TCB discussions.
- Modern evolutionary and neural-system references including NEAT, differentiable plasticity, and contemporary artificial-life research.

Useful public links:

- https://creatures.wiki/Brain
- https://creatures.wiki/Biochemistry
- https://creatures.wiki/CAOS
- https://creatures.wiki/Cellular_Automata
- https://www.creaturescaves.com/
- https://creaturescaves.com/community.php?section=Resources&view=14
- https://creaturescaves.com/community.php?section=Resources&view=102
- https://eem.foo/
- https://eem.foo/ccarchive/Sites/creatures.treesprite.com/Upgrades4.html
- https://openc2e.github.io/
- https://cdn.cloudflare.steamstatic.com/steam/apps/1838430/manuals/Genetics_Kit_Manual.pdf
- https://nn.cs.utexas.edu/downloads/papers/stanley.ec02.pdf
- https://arxiv.org/abs/1504.04909
- https://proceedings.mlr.press/v80/miconi18a.html
