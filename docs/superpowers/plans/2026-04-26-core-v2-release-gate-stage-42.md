# Core v2 Stage 42: Docs, Tooling, And Release Gate

## Goal

Close the 42-stage Core v2 umbrella by documenting the implemented subsystem surfaces and adding a repeatable release-gate command script.

## Constraints

- No unresolved marker strings in Core v2 docs or `src/Sim`.
- Final verification must include full simulation tests and project build.
- Godot smoke is required when a Godot executable is available because this feature branch includes the Debug HUD observability stage.
- The release docs must cover Genome v2, Biochemistry Trace, Brain Snapshot, Creature Trace, CA Fields, Agent Affordances, Lab Runner, Evolution Hooks, and Authoring.
- Push only the isolated feature branch.

## Implementation Steps

- Add red tests for subsystem doc coverage, release-gate script coverage, and multi-creature trace review.
- Add a Core v2 implementation notes document.
- Add a `tools/run_core_v2_release_gate.ps1` helper.
- Run focused tests, full simulation tests, build, marker scan, Godot smoke if available, commit, and push.
