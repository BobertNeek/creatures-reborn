# C3/DS Genome Gene Formats

Creatures Reborn treats C3/DS `.gen` files as the compatibility baseline. Raw gene bytes remain the source of truth; typed decoding is an inspection and authoring layer around those bytes.

Research anchors:

- Genetics Kit Manual: https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/1838430/manuals/Genetics_Kit_Manual.pdf?t=1640163808
- GEN files: https://creatures.wiki/GEN_files
- OpenC2E: https://github.com/openc2e/openc2e

## Header

Each C3/DS gene starts with `gene`, followed by type, subtype, id, generation, switch-on age, flags, mutability, variant, then payload bytes. The file ends with `gend`. Version 3 files include a file-level `dna3` token before the gene stream.

Important expression fields:

- switch-on age controls lifecycle expression.
- flags include mutable, duplicable, cuttable, male-only, female-only, and not-expressed.
- variant gates expression for variant-specific genomes.
- generation distinguishes cloned genes.

## Standard Payload Lengths

The schema catalog in `src/Sim/Genome/GeneSchemaCatalog.cs` defines the current compatibility contract:

| Payload | Type/Subtype | Length |
| --- | ---: | ---: |
| Brain lobe | 0/0 | 121 |
| Brain organ | 0/1 | 5 |
| Brain tract | 0/2 | 128 |
| Biochemistry receptor | 1/0 | 8 |
| Biochemistry emitter | 1/1 | 8 |
| Biochemistry reaction | 1/2 | 9 |
| Biochemistry half-lives | 1/3 | 256 |
| Biochemistry initial concentration | 1/4 | 2 |
| Biochemistry neuroemitter | 1/5 | 15 |
| Creature stimulus | 2/0 | 13 |
| Creature genus | 2/1 | 65 |
| Creature appearance | 2/2 | 3 |
| Creature pose | 2/3 | 17 |
| Creature gait | 2/4 | 9 |
| Creature instinct | 2/5 | 9 |
| Creature pigment | 2/6 | 2 |
| Creature pigment bleed | 2/7 | 2 |
| Creature facial expression | 2/8 | 11 |
| Organ | 3/0 | 5 |

Unknown payloads and nonstandard lengths are preserved raw by `C3DsGenomeImporter` and reported in `GenomeCompatibilityReport`.

