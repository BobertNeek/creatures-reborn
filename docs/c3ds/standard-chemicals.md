# C3/DS Standard Chemicals

`StandardChemicalCatalog` documents all 256 chemical slots used by C3/DS-compatible biochemistry. Every slot has a stable id, token, display name, category, source, and known/unknown status. Named standard chemicals use the Creatures Wiki C3 chemical list as the first public reference.

Research anchor:

- C3 Chemical List: https://creatures.wiki/C3_Chemical_List

## Compatibility Notes

- Chemical meaning is genome-dependent. A chemical id can have different effects in breeds with different reactions, receptors, emitters, and half-lives.
- Unlisted public chemicals are represented as `Unknownase` rather than treated as errors.
- Stimulus genes use their own chemical numbering. `GenePayloadCodec.StimulusChemicalToBiochemical` converts stimulus chemical ids to biochemical ids with the documented C3/DS formula.
- `ChemicalCatalog` remains the compact runtime/debug catalog used by existing UI. `StandardChemicalCatalog` is the complete C3/DS reference catalog for import compatibility.

## Important Standard Ranges

| Range | Meaning |
| --- | --- |
| 1-13 | digestion and food metabolism |
| 24-36 | waste, respiration, water, energy, ATP, ADP |
| 39-54 | reproductive hormones and sex chemistry |
| 66-81 | toxins |
| 82-89 | antigens |
| 92-100 | medicines |
| 102-109 | antibodies |
| 131-145 | drive backups |
| 148-162 | drive chemicals |
| 165-184 | CA smell gradients |
| 187-195 | stress chemicals |
| 198-213 | brain, reinforcement, navigation, and REM chemicals |

