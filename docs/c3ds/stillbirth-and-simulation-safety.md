# Stillbirth And Simulation Safety

Stillbirth is a simulation-safety outcome, not a fitness judgment. A stillborn genome is preserved in lineage with parent monikers, raw genome bytes, sex, variant, generation, birth tick, crossover and mutation context, and the safety report that explains why it could not enter the active tick loop.

Severity meanings:

- `HardInvalid`: cannot hatch in normal gameplay because it is malformed, has no engine-readable brain interface, lacks fallible life support, or can corrupt lineage/runtime state.
- `QuarantineOnly`: can be inspected in lab/debug contexts but should not enter normal gameplay by default.
- `WeakButLiving`: can hatch; natural selection decides whether it survives.
- `Info`: diagnostic context only.

Weak creatures are allowed to fail naturally. Bad hunger instincts, poor locomotion, low fertility, fragile chemistry, short lifespan, or strange bounded brain wiring are not stillbirth reasons by themselves.
