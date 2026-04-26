# Core v2 Stage 40: Lineage And Lab Runner

## Goal

Add a pure `src/Sim` deterministic experiment runner for repeatable creature lab runs.

## Constraints

- Lab execution is explicit and offline; it does not affect normal gameplay defaults.
- All stochastic work uses injected `IRng` implementations seeded from `LabRunConfig`.
- Runs track founders, optional explicit births, deaths, mutation/crossover reports, chemical summaries, brain metrics, and behavior metrics.
- World presets provide deterministic environmental inputs without Godot dependencies.
- Keep the initial runner narrow enough to support future richer evolution labs without becoming a god object.

## Research Mapping

- C3/DS breeding and mutation are meaningful only when lineage and chemical/behavior outcomes are observable.
- NEAT-like or module experiments need fixed seeds, stable candidate populations, and repeatable metrics.
- Chemical and brain snapshots from earlier stages become lab metrics rather than manual-only debug surfaces.

## Implementation Steps

- Add tests for deterministic repeated runs.
- Add tests for founder lineage and final organism metrics.
- Add tests for explicit offline breeding, crossover reports, and mutation reports.
- Add tests for config validation and world preset recording.
- Add `LineageRecord`, `LabRunConfig`, `LabRunner`, `LabRunMetrics`, and supporting metric records.
- Run focused tests, full simulation tests, build, marker scan, diff checks, commit, and push.
