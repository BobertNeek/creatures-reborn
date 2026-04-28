# C3/DS Biochemistry Compatibility

C3/DS-compatible biochemistry is genome-authored. The engine reads half-lives, initial concentrations, neuroemitters, organs, reactions, receptors, and emitters from `.gen` data, then advances the 256 chemical concentrations deterministically each tick.

Research anchors:

- Biochemistry: https://creatures.wiki/Biochemistry
- C3 Chemical List: https://creatures.wiki/C3_Chemical_List
- OpenC2E: https://openc2e.github.io/

## Compatibility Mode

`BiochemistryCompatibilityMode.C3DS` runs the classic substrate only:

- neuroemitters
- organ updates
- reactions
- receptors
- emitters
- half-life decay

`BiochemistryCompatibilityMode.ModernExtensions` keeps the existing Creatures Reborn helper loops for extra metabolism, fatigue, sleep, toxin, oxygen, temperature, and recovery behavior. Imported C3/DS genomes default to `C3DS` so modern behavior does not silently change original biology.

## Import Reports

`C3DsGenomeImporter` produces a `GenomeCompatibilityReport` with:

- validation issues
- supported standard genes
- raw-preserved genes whose payloads are unknown or nonstandard

This lets third-party breeds load without data loss while still exposing compatibility gaps to tools and tests.

