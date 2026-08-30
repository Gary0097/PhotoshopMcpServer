# Deployment workflow

## Pre-arrival gate

Confirm with the customer that GPT has been purchased, Codex is installed and logged in, VPN is connected, and Photoshop 2026 plus Illustrator 2026 are installed and activated. Record the actual Adobe installation paths; the project defaults are `K:\TOOL\Adobe Photoshop 2026` and `K:\TOOL\Adobe Illustrator 2026`.

## First deployment

1. Call `CheckDuanxingEnvironment` and preserve its result in the deployment record.
2. Resolve every failed machine check. Ask the customer to confirm GPT entitlement, Codex login, VPN, and Adobe license state.
3. Call `LaunchPhotoshop`, `GetPhotoshopVersion`, `LaunchIllustrator`, and `GetIllustratorVersion`.
4. Use a non-sensitive sample to test opening a working copy, one reversible Photoshop operation, a simple Illustrator document/path operation, save/export, and rollback.
5. Confirm that the original is unchanged and record outputs, versions, elapsed time, and unresolved issues.

The first deployment is complete only after both Adobe applications are reachable and a real sample passes the basic save/export chain. Target elapsed time is approximately one hour. The later ten-minute target applies only to a compatible environment with prerequisites already installed and activated.
