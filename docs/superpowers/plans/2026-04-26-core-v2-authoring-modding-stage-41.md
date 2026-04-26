# Core v2 Stage 41: Authoring And Modding Surface

## Goal

Add a validated C# authoring surface for the definition types that are now stable enough to expose internally: chemical metadata, CA presets, agent affordances, and lab run configs.

## Constraints

- Prefer C# definition records and catalogs; do not add JSON schemas yet.
- Invalid definitions return actionable validation issues instead of throwing.
- Keep validation pure and Godot-free.
- Built-in definitions must validate cleanly.
- CA presets may apply values to pure `Room` instances for tests and future tools.

## Research Mapping

- C3/DS authoring worked because agents, CA fields, scripts, and genomes were inspectable and moddable.
- The modern surface should expose definitions only after earlier typed metadata and debug views exist.
- Validation is required before allowing external mod data to influence chemistry, CA fields, agents, or lab runs.

## Implementation Steps

- Add red tests for built-in validation, invalid chemical metadata, invalid CA presets, invalid affordances, invalid lab configs, and CA preset application.
- Add `AuthoringDefinitionBundle`, `AuthoringValidator`, `AuthoringValidationIssue`, and CA preset definition records.
- Reuse existing catalogs: `ChemicalCatalog`, `CaChannelCatalog`, `AgentAffordanceCatalog`, and `LabRunConfig`.
- Run focused tests, full simulation tests, build, marker scan, diff checks, commit, and push.
