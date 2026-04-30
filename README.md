# EntryPointSelector-MatchmakerUI

Matchmaker UI companion for `hazelify.EntryPointSelector` on SPT.

This add-on places the insertion-point workflow directly on Tarkov's raid location screen. It supports:

- custom insertion selection on the matchmaker screen
- last-extract deployment messaging with live map/extract hints
- automatic runtime learning of extract positions as maps are loaded
- localization-backed extract display names from the installed SPT database

## Status

This plugin has been tested in-game and is fully functional with:

- SPT `4.0.13`
- `hazelify.EntryPointSelector` `1.3.0`

## Important Note About Forge

This project will **not** be submitted to SPT Forge.

The current [SPT Forge Content Guidelines](https://forge.sp-tarkov.com/content-guidelines) include a `No AI-Generated Mods` rule. This project was substantially produced with AI assistance, so publishing it to Forge would conflict with that policy. The code is instead provided here on GitHub for transparent inspection, local use, and manual maintenance by its owner.

That decision is about policy compliance, not project quality. The plugin is tested, readable, and intended to remain understandable and maintainable.

## Dependencies

- `hazelify.EntryPointSelector`
- SPT `4.0.13`
- optional: `com.fika.core`

## Installation

1. Install `hazelify.EntryPointSelector`.
2. Copy `archon.EntryPointSelector.MatchmakerUI.dll` into:
   `BepInEx/plugins/archon.EntryPointSelector.MatchmakerUI/`
3. Launch the game once so the plugin can create and maintain its runtime data files.

## Runtime Data

The plugin writes its learned extract-position catalog to:

`BepInEx/plugins/archon.EntryPointSelector.MatchmakerUI/RuntimeExtractCatalog.json`

This file is built from the extracts the game actually loads at runtime, so it can adapt to modded maps and extract behavior without relying on a fixed shipped seed.

## Design Notes

- The original `hazelify.EntryPointSelector` plugin remains the authority for insertion logic and saved spawn data.
- This add-on only extends the raid selection UI and reads existing saved position data from the original plugin.
- Extract names are resolved from the installed SPT locale database first, then fall back to the original plugin's internal lists if needed.

## Community Standards

This repository aims to stay aligned with the spirit of the current [SPT Community Standards](https://forge.sp-tarkov.com/community-standards): clear ownership, transparent distribution, and no misleading representation of what the mod is or how it was produced.
