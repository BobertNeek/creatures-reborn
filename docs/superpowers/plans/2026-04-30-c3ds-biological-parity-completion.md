# C3/DS Biological Parity Completion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` task-by-task. Use test-first changes for every runtime behavior change.

**Goal:** Build a measurable C3/DS `.gen` biology parity track before adding more modern intelligence.

**Architecture:** Keep C3/DS raw genome bytes as source of truth. Add fixture discovery, parity reports, genome-authored stimulus runtime, and parity traces for biochemistry and brain without enabling modern helper loops in C3/DS mode.

**Tech Stack:** .NET 8 `src/Sim`, xUnit, existing genome/biochemistry/brain runtime, Godot-free parity APIs.

---

## Tasks

- [x] Set up isolated worktree `feature/c3ds-biological-parity-completion`.
- [x] Verify baseline with `dotnet test tests\Sim.Tests\Sim.Tests.csproj -p:UseSharedCompilation=false`.
- [x] Add fixture discovery and parity report tests.
- [x] Implement `C3DsFixtureSet`, `C3DsParityReport`, and `C3DsParityReportWriter`.
- [x] Add genome-authored stimulus table tests.
- [x] Implement `GenomeStimulusTable`, `StimulusGeneDefinition`, and `StimulusApplicationTrace`.
- [x] Add biochemistry parity trace tests and public snapshots.
- [x] Add brain parity trace tests and SVRule parity case coverage.
- [x] Expand gated fixture tests to import/report every configured `.gen`.
- [x] Update C3/DS parity docs with implemented and remaining gaps.
- [x] Run full verification, commit, merge to `master`, and push.
