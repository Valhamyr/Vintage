**Guide: Improving Perlin Noise Terrain for Vintage Story Mods (for Codex)**

This document outlines the goals and technical steps required to improve terrain generation using Perlin noise for a Vintage Story worldgen mod. It should be used as a guide for a code generation assistant (like Codex) to implement or refactor terrain logic according to these principles.

---

## ✨ Goal Overview

- Prioritize **harsh vertical elevation changes** (cliffs, plateaus, towers)
- Ensure there are **flat regions suitable for player building**
- Avoid smooth, rolling hills or soft terrain transitions

---

## 🧰 Key Landform Targets

Focus landforms to adjust:

- `sheercliffs` (main vertical cliff biome)
- `towercliffs` (spires/towers)
- `flatlands` (flat fill-in areas between dramatic terrain)

---

## ⚖️ Config Adjustments

### For `sheercliffs`:

**1. terrainYKeyThresholds and Positions** Replace with more plateau-friendly transitions:

```json
"terrainYKeyPositions":    [0.0, 0.35, 0.45, 0.46, 0.6, 0.7],
"terrainYKeyThresholds":   [1.0, 1.0, 0.5, 0.5, 0.2, 0.0]
```

This creates: vertical cliff walls, flat shelves, and transition bands.

**2. terrainOctaves** Sharpen terrain details:

```json
"terrainOctaves": [0.1, 0.2, 0.4, 0.6, 1.0, 0.6, 0.4, 0.3, 0.2]
```

---

## 🔄 Noise Remapping Logic

In the noise sampling function (typically part of `GenChunkColumn` or equivalent), remap noise as follows:

```csharp
float rawNoise = noise.GetNoise(x, z);  // Range: ~-1 to 1
float n = (rawNoise + 1f) * 0.5f;       // Normalize to 0-1

// Apply S-curve to emphasize cliffs and flat zones
n = 3 * n * n - 2 * n * n * n;
```

This smooths middle terrain and sharpens changes near extremes.

---

## 🌍 Domain Warping (Optional)

Add variation by offsetting input coordinates with secondary noise layers:

```csharp
float warpX = warpNoiseX.GetNoise(x * 0.01f, z * 0.01f) * 20f;
float warpZ = warpNoiseZ.GetNoise(x * 0.01f + 1000, z * 0.01f + 1000) * 20f;
float finalNoise = mainNoise.GetNoise(x + warpX, z + warpZ);
```

This helps terrain feel less grid-aligned or uniform.

---

## 🌟 Landform Balancing

Adjust weights in `landformConfig.json` to prioritize dramatic formations:

```json
{
  "code": "sheercliffs", "weight": 400
},
{
  "code": "towercliffs", "weight": 300
},
{
  "code": "flatlands", "weight": 200
}
```

---

## ✅ Final Notes

- Avoid abrupt vertical jumps without plateaus (makes terrain unplayable)
- Keep some low-noise or low-threshold areas for navigability
- All noise layers should remain seed-stable
- Output worldgen should be visually interesting AND survivable

Codex: apply these rules when refactoring landform definitions or noise code.

---

**End of Guide**

