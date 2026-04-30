# Biology Parity Status

Creatures Reborn keeps C3/DS `.gen` bytes as the source of truth. Imported genomes default to C3/DS biochemistry mode, raw genes are preserved, standard gene schemas are decoded, and modern extensions live in a separate inherited overlay.

Implemented compatibility foundations:

- Raw `.gen` import strips the file-level `dna3` header and preserves gene bytes.
- Standard brain, creature, biochemistry, and organ payload schemas are cataloged.
- C3/DS biochemistry mode disables modern helper loops.
- Initial concentration and half-life genes can switch on at lifecycle age transitions.
- Late receptor, emitter, organ, neuroemitter, lobe, and tract expression applies incrementally.
- Genome-authored stimulus genes can build the runtime stimulus table; the hardcoded table remains only as fallback for genomes without expressed stimulus genes.
- Fixture discovery can recursively load user-owned C3/DS `.gen` files from `CREATURES_C3DS_FIXTURES`.
- Parity reports can be exported as JSON and Markdown under `artifacts/c3ds-parity` during local fixture runs.
- Biochemistry parity traces expose chemical table size, half-lives, organs, reactions, receptors, emitters, and neuroemitters.
- Brain parity traces expose lobe/tract boot structure, WTA winners, instinct state, module absence, and SVRule opcode/operand inventory.
- Safety validation rejects malformed, unsimulatable, or world-breaking genomes without rejecting weak but living organisms.

Remaining parity work:

- Run the expanded stock-genome fixture suite against local legal C3/DS genome data and triage every generated report issue.
- Add external golden behavior expectations where available; current reports prove internal determinism and coverage, not byte-for-byte C2E equivalence.
- Continue expanding SVRule behavior cases from inventory coverage into per-opcode behavioral parity where stock genomes exercise edge cases.
- Extend parity beyond `.gen` biology into `.creature` import/export and full CAOS engine semantics in a later project.
