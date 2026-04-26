# Core v2 Stage 39: Evolutionary Module And Lab Hooks

## Goal

Add genome-derived offline hooks for future NEAT-like topology and module experiments without changing live creature behavior.

## Constraints

- Hooks are read-only views over decoded genome data.
- Candidates are offline-only and disabled by default.
- No module is registered into a gameplay brain by this stage.
- Candidate IDs are deterministic and do not use hidden randomness.
- Generated module options require explicit opt-in before they can shadow or augment a lobe.
- Keep `src/Sim` Godot-free.

## Research Mapping

- C3/DS genomes remain the source of brain structure through lobe and tract genes.
- NEAT-style future work needs stable innovation identifiers for candidate topology/module changes.
- Modern brain modules must be inspectable and opt-in, so this stage emits candidates rather than mutating live creatures.

## Implementation Steps

- Add tests for genome lobe/tract anchor extraction.
- Add tests for deterministic offline-only candidates and explicit plasticity option creation.
- Add tests that hook generation does not mutate genome bytes or runtime cursor behavior.
- Add `GenomeEvolutionHookSet` and related anchor/candidate records under `src/Sim/Lab`.
- Run focused tests, full simulation tests, build, marker scan, diff checks, commit, and push.
