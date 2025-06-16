
# 🛠 Repair Workbench Mod — Project Background

## 📌 Overview

The **Repair Workbench Mod** is a custom mod for _Vintage Story_ that introduces a new mechanic for repairing tools, weapons, and armor using a physical 3-block-wide bench, with no GUI. The player interacts directly with the workbench using standard in-game tools and materials.

## 🎯 Mod Features (Goal)

This mod aims to:

- Add **three tiers of repair benches** (Copper, Iron, Steel), each with distinct models and repair effectiveness.
- Enable repairs via **hammer strikes**, consuming **nuggets or bits** of compatible materials placed in a side bowl.
- Support:
  - **Base-game and modded items**
  - **Armor**
  - **Tools and weapons**
- Provide a **GUI-free** interaction: players use their hotbar and direct item placement.

## 🔧 Implementation Highlights (So Far)

- ✅ Basic multiblock structure: 3 blocks wide (left, middle, right)
- ✅ Middle block shows the **tool being repaired**
- ✅ Right block features a **bowl** that visually displays the inserted nugget
- ✅ Manual hammer use (in-hand) initiates repairs
- ✅ Durability tooltip planned (and partially implemented)
- ✅ Repair tiers:
  - Copper: 5% durability per nugget
  - Iron: 10%
  - Steel: 15%
- ✅ Creative and handbook visibility support
- ✅ Uses **base game hammers** (no new tool types added)
- ✅ Textures styled to match Vintage Story: wooden tops, metal trim

## 🐞 Troubleshooting & Fixes

- Repeated log-based debugging to solve:
  - Block shape errors
  - Texture mapping mismatches
  - Duplicate block/recipe conflicts
  - Crashes on `AssetsLoaded` due to malformed recipe structures
- Transitioned to a **clean single-variant base** (Copper only) to isolate failures
- Verified shape/texture/recipe behavior using working mods (like a butchering mod)
- Ensured mod loads correctly in both SP and server mode

## 🗺 Roadmap (Planned)

- [ ] Reintroduce Iron and Steel variants with unique textures and balancing
- [ ] Visual indicator for nugget type and quantity in bowl
- [ ] Visual feedback for tool damage level
- [ ] Full support for all tool/armor types (including modded gear)
- [ ] Repair animation or spark effect when hammer is used
- [ ] Balance and progression testing for in-game economy
- [ ] Optional config for durability %, materials, and tier unlocks

## 🧠 Tech Notes

- Mod uses:
  - `blocktypes/repairbench.json` for variant definitions
  - `shapes/block/repairbench_{tier}.json` for 3D structure
  - Base-game textures via `game:block/wood/planks/copper` etc.
- Crafting recipes defined as `"grid"` types with object-style `ingredients` mapping
- All components designed to match the **aesthetic and mechanical tone** of Vintage Story
