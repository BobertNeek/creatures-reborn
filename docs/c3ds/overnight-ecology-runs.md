# Overnight Ecology Runs

`EcologyRunner` is a deterministic headless runner for accelerated survival trials. It wraps the pure sim lab runner, uses seeded replay, records an evolution journal, and summarizes generation count, living population, stillborn count, deaths, reproduction count, and extinction.

Current use:

1. Create an `EcologyRunConfig` with seed, generation count, ticks per generation, founders, world preset, and population policy.
2. Run `new EcologyRunner().Run(config)`.
3. Save or inspect the returned journal and generation summaries.

The runner is intentionally pure `src/Sim`. Godot visuals and UI do not own biology.
