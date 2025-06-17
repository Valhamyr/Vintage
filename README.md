# Vintage Story Modding Environment

This repository is a starting place for creating mods for the sandbox game [Vintage Story](https://www.vintagestory.at/). Below is a short overview of the modding workflow, file structure and useful resources based on the official wiki.

## Working in this repository

When starting a new mod from scratch, first create a dedicated folder inside this repository and keep all of the mod's files there. Avoid placing mod files directly in the repository root so that different mods do not overwrite one another.


## Example mods
- **Fixed Cliffs Landforms** (`WorldgenMod/FixedCliffs`)
- **Landform Teleport** (`TeleportLandformMod`)
The fixed-cliffs example now includes richer parameters like `terrainOctaves`
and `terrainYKeyPositions` that control the resulting landform shapes.
## Supported Vintage Story version

According to the [Vintage Story wiki main page](https://wiki.vintagestory.at/Main_Page), the latest stable release is **version 1.20.12** (June 8, 2025) `Purely stable performance`.

## Mod types

Vintage Story recognises three basic categories of mods:

- **Theme Pack** – visual changes only; cannot modify gameplay assets.
- **Content Mod** – adds or changes blocks, items and other assets purely through JSON files.
- **Code Mod** – like a content mod but may also include C# code for additional behaviour.

Start with a content mod if you are new, then progress to code mods for more advanced features.

## Basic mod structure

A typical mod folder contains the following layout:

```
myMod/
├─ assets/
│  └─ <modid>/
│      └─ … (textures, blocktypes, itemtypes, etc.)
├─ modinfo.json            # required descriptor file
├─ modicon.png             # optional icon shown in the mod manager
├─ <compiled .dll and .pdb files> (for code mods)
└─ src/                    # optional source code
    └─ … .cs files
```

Each domain inside `assets/` separates your content from the vanilla game assets. Use the same folder names as the in-game asset categories (e.g. `blocktypes`, `itemtypes`, `shapes`, `textures`).

### modinfo.json example

A minimal `modinfo.json` might look like:

```json
{
  "type": "code",
  "modid": "mycoolmod",
  "name": "My Cool Mod",
  "authors": ["ExampleAuthor"],
  "description": "Mod that is so cool it freezes you.",
  "version": "1.2.3",
  "dependencies": {
    "game": "1.20.12"
  }
}
```

Important fields include `type` (one of `theme`, `content`, or `code`), `modid` which must be lowercase alphanumerics only, `version` using semantic versioning, and optional dependency entries.

## Asset system overview

Most game content is loaded from asset JSON files. The official *Asset System* documentation lists many categories, including:

- `blocktypes` – block definitions
- `itemtypes` – items
- `entities` – creature data
- `worldproperties` – lists of properties
- `recipes`, `grid`, `smithing` – crafting information
- `worldgen`, `terrain`, `tree` – world generation
- `shapes`, `textures`, `sounds`, `music`, `shaders`, and more

Assets are located under `%appdata%/Vintagestory/assets/` on Windows (equivalent paths exist for other OSes). Study these files to learn the expected JSON format and patch them in your own domain.

Domains allow you to override default assets or supply new ones. To override a vanilla file, replicate its relative path under your mod domain or under the `game` domain in your mod archive.

## Further information

The Vintage Story wiki contains extensive guides and references:

- [Modding Basics Portal](https://wiki.vintagestory.at/Modding:Modding_Basics_Portal)
- [Getting Started](https://wiki.vintagestory.at/Modding:Getting_Started)
- [Mod Packaging](https://wiki.vintagestory.at/Modding:Mod_Packaging)
- [Modinfo](https://wiki.vintagestory.at/Modding:Modinfo)

Explore the wiki for tutorials on specific asset types, mod compatibility, world generation configuration and more.

Happy modding!
