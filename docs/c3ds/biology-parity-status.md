# Biology Parity Status

Creatures Reborn keeps C3/DS `.gen` bytes as the source of truth. Imported genomes default to C3/DS biochemistry mode, raw genes are preserved, standard gene schemas are decoded, and modern extensions live in a separate inherited overlay.

Implemented compatibility foundations:

- Raw `.gen` import strips the file-level `dna3` header and preserves gene bytes.
- Standard brain, creature, biochemistry, and organ payload schemas are cataloged.
- C3/DS biochemistry mode disables modern helper loops.
- Initial concentration and half-life genes can switch on at lifecycle age transitions.
- Safety validation rejects malformed, unsimulatable, or world-breaking genomes without rejecting weak but living organisms.

Remaining parity work:

- Late receptor, emitter, organ, neuroemitter, lobe, and tract expression should be applied incrementally.
- Stock-genome parity fixtures should be expanded when local C3/DS data is configured.
- More C3/DS-specific organ failure and receptor flag edge cases should be covered with golden tests.
