---
name: duanxing-image-craft
description: Operate the Duanxing Photoshop and Illustrator 2026 texture workflow through Codex. Use for deployment checks, texture uniformity, restoration, extension, seamless tiling, refraction lines, high-DPI export, version records, or UAT with Duanxing samples. Do not use for unrelated general image editing.
---

# Duanxing Image Craft

Use the plugin's `duanxing-adobe-automation` MCP server to run approved Duanxing image workflows. Keep Photoshop pixel work, Illustrator vector work, AI-assisted output, and human review traceable.

## Required preflight

Before the first Adobe operation in a session, call `CheckDuanxingEnvironment`.

The workflow may proceed only when all four customer prerequisites are satisfied:

- A usable paid GPT account/service is available and Codex login is verified.
- Codex is installed with this plugin and its MCP server enabled.
- Adobe Photoshop 2026 is installed, licensed, and starts successfully.
- Adobe Illustrator 2026 is installed, licensed, and starts successfully.

VPN connectivity and license/login state require human confirmation when the tool cannot verify them. Report missing prerequisites and stop Adobe mutations until they are resolved.

## Workflow selection

For deployment or diagnostics, read [references/deployment.md](references/deployment.md).

For texture processing, parameter recording, output, or UAT, read [references/texture-workflow.md](references/texture-workflow.md).

## Safety invariants

- Never overwrite the supplied original. Open or create a working copy first.
- Confirm the output directory, target physical size, DPI, file format, and required pixel/vector deliverables before execution.
- Use business-level MCP tools for normal work. Arbitrary Photoshop JavaScript remains disabled by default and may be enabled only for an authorized development session.
- Keep deterministic checks such as dimensions, DPI, file naming, format, and version identity outside generative judgment.
- Treat AI-generated or modified images as design-assistance output. Require the customer's designated design/process/quality reviewer before production release.
- Stop after a failed step; preserve the latest valid checkpoint and report a retry, rollback, or manual route.
