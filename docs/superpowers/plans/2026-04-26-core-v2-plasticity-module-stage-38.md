# Core v2 Stage 38: Plasticity Module Experiment

## Goal

Add an experimental brain module that proves modern local-learning upgrades can coexist with classic SVRule lobes without becoming a black-box creature controller.

## Constraints

- Disabled by default and never registered automatically.
- Only shadows a lobe when explicitly enabled and configured with a shadow token.
- Uses deterministic local Hebbian-style plasticity; no hidden RNG.
- Exposes learned weights, recent deltas, source state, target state, and module outputs through snapshot APIs.
- Keeps `src/Sim` Godot-free.
- Does not change existing creature behavior unless callers explicitly register and enable the module.

## Research Mapping

- C3/DS brain compatibility is preserved by keeping lobes, tracts, SVRules, STW/LTW, and reinforcement as the base substrate.
- Differentiable-plasticity-style modernization is represented as inspectable local weight updates, not a whole-creature opaque network.
- The module uses named lobe tokens and snapshot output so future lab tooling can compare classic and experimental learning paths.

## Implementation Steps

- Add red tests for disabled registration, explicit shadowing, deterministic Hebbian updates, copied typed snapshots, and generic brain snapshot export.
- Add `PlasticityBrainModuleOptions`, `PlasticityBrainModuleSnapshot`, and `PlasticityBrainModule`.
- Add generic module snapshot records so `BrainSnapshot` can expose inspectable module state.
- Run focused tests, then full simulation tests and build.
- Commit and push only Stage 38 files on the isolated feature branch.
