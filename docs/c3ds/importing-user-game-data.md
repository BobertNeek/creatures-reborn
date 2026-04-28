# Importing User C3/DS Game Data

Creatures Reborn supports user-provided C3/DS genome data without bundling proprietary game files.

## Supported Input

- `.gen` genome files
- optional catalog/name metadata for chemical and genome labels
- local fixture folders for parity tests

Suggested environment variables for local tests:

- `CREATURES_C3DS_DATA`: installed C3/DS data folder
- `CREATURES_C3DS_FIXTURES`: curated local fixture folder

## Import Behavior

- Raw genome bytes are preserved.
- Valid standard genes are decoded through `GeneSchemaCatalog`.
- Unknown or nonstandard genes are preserved raw and reported.
- Imported genomes use `BiochemistryCompatibilityMode.C3DS` by default.
- Proprietary stock game data should stay outside the repository.

