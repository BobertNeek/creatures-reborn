# Biology, Stillbirth, Chemical Reinforcement, And Evolutionary Brain Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bring Creatures Reborn up to a C3/DS-like artificial-life level where genetics builds chemistry and brain structure, chemicals drive learning, hard-invalid offspring are stillborn, and natural selection in the game world shapes future creatures.

**Architecture:** Preserve C3/DS compatibility as the foundation. Add a simulation-safety validator that rejects only world-breaking organisms, not weak organisms. Build chemical reinforcement as the learning language between body and brain, then add larger hand-wired brain blueprints and offline/accelerated evolution tools that use the same survival rules as the game.

**Tech Stack:** .NET 8 `src/Sim`, xUnit, existing C3/DS genome/biochemistry code, existing `IBrainModule`, existing `LabRunner`, Godot only for hatch/stillborn presentation and debug UI.

---

## Stage Order

- [ ] Compatibility baseline lock.
- [ ] Genome simulation safety model.
- [ ] Stillborn hatch outcome.
- [ ] C3/DS biology parity pass.
- [ ] Chemical reinforcement bus.
- [ ] Brain learning integration.
- [ ] Minimum simulatable brain interface.
- [ ] Fallible biology and immortal-vegetable prevention.
- [ ] Larger hand-wired brain blueprint.
- [ ] Genetic control of modern extensions.
- [ ] In-game natural selection metrics.
- [ ] Accelerated ecology runner.
- [ ] Chemical RL training schema.
- [ ] Debugging and observability.
- [ ] Documentation.

## Execution Rules

- Use a dedicated git worktree and narrow commits.
- Keep `src/Sim` Godot-free.
- Use tests before production code for behavior changes.
- Preserve C3/DS `.gen` import compatibility.
- Treat hard-invalid offspring as stillborn, not deleted.
- Allow weak but simulatable organisms to hatch.
- Keep modern learning and RL systems opt-in and inspectable.
- Do not use `_archive\Darwinbots Unity`.

