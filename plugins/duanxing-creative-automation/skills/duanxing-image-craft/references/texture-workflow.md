# Texture workflow

## Intake

Collect the source file, recipe name/version, physical width and height, DPI, color mode, bit depth, tiling mode, line parameters, pixel/vector outputs, destination, and reviewer. Do not infer a missing production value.

## Supported first-phase recipes

- Texture uniformity while preserving the source's recognizable character.
- Basic restoration or clarity improvement.
- Texture extension, including the representative 100 x 200 mm to 200 x 200 mm case.
- Seamless flat tiling and half-drop tiling.
- Straight or S-shaped refraction lines with configurable width, spacing, density, and angle.
- Pixel export and editable Illustrator vector output.
- 1270, 2540, and 5080 DPI export when the agreed sample and machine resources permit it.

## Execution

1. Run the environment preflight.
2. Hash or otherwise identify the source, then create a working copy and task/version ID.
3. Show the normalized recipe parameters for confirmation.
4. Execute approved Photoshop pixel steps and Illustrator vector steps, recording each step result.
5. Stop on failure and retain the last valid checkpoint.
6. Validate deterministic output properties.
7. Present input/output comparison and the recipe/version record to the designated reviewer.
8. Export the production version only after explicit human approval.

## Acceptance record

Record environment versions, source identity, recipe version, parameters, elapsed time, generated files, automatic checks, reviewer, decision, and limitations. Classify feedback as a defect, recipe parameter issue, AI variance, sample issue, known process boundary, or new requirement.
