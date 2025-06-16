# Repair Mod TODO

## Current State
- The only code exists in `RepairWorkbenchMod backup 7 last broken version before butchery structure.zip`. Inside the archive the core classes are empty stubs:
  - `BlockRepairBench` derives from `Block` but has no logic.
  - `BlockEntityRepairBench` derives from `BlockEntity` but is empty.
  - `ItemRepairHammer` derives from `Item` without functionality.
- Game logs reveal missing assets and recipe problems:
  - Client log warns about missing shape `game:shapes/block/cube.json` and missing texture `game:textures/block/wood/planks/copper.png` for the copper bench variant.
  - Server log shows `GridRecipe.ResolveIngredients` null reference errors during `AssetsLoaded`.

## Tasks
1. Reintroduce iron and steel bench variants with proper assets (textures, shapes, recipes).
2. Implement functional `BlockRepairBench`, `BlockEntityRepairBench` and `ItemRepairHammer` to handle repairing tools using nuggets.
3. Ensure all referenced shapes (`cube.json` etc.) and textures exist so the logs remain clean.
4. Review the structure of `butchering_1.9.0.zip` for working multiblock examples and replicate the approach.
5. Add nugget count visuals in the bowl and durability feedback on the tool.
6. Update crafting recipes and bump `modinfo.json` version once features are restored.
