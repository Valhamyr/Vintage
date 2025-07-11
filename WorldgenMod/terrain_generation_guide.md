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

- `sinkholeplateaus` (stepped depressions)
- `dryseapillars` (pillar regions with open paths)
- `widepillarcliffs` (cliff clusters with wide ledges)

---

## ⚖️ Config Adjustments

### For `widepillarcliffs`:

**1. terrainYKeyThresholds and Positions** Replace with more plateau-friendly transitions:

```json
"terrainYKeyPositions":    [0.0, 0.35, 0.45, 0.46, 0.6, 0.7],
"terrainYKeyThresholds":   [1.0, 1.0, 0.5, 0.5, 0.2, 0.0]
```

This creates vertical cliff walls with broad shelves between them.

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

## 🗂 Loading Landform Parameters

The game reads the file `assets/fixedcliffs/worldgen/landforms.json` during
server startup. Each object in the `variants` array defines a landform used in
terrain generation. Tweaking values such as `terrainOctaves`,
`terrainYKeyPositions` and `terrainYKeyThresholds` directly changes how sharp
the cliffs appear and how terrace levels stack.

---

## 🌟 Landform Balancing

Adjust weights in `landformConfig.json` to prioritize dramatic formations:

```json
{
  "code": "sinkholeplateaus", "weight": 2000
},
{
  "code": "dryseapillars", "weight": 1500
},
{
  "code": "widepillarcliffs", "weight": 1200
}
```

---

## 📏 Landform Tuning Guidelines

* **noiseScale** – lower values stretch patterns horizontally, creating wider canyons or plateaus.
* **terrainOctaves** – early high values keep large, flat platforms while later octaves add detail.
* **terrainYKeyPositions/Thresholds** – pair together to form vertical steps or gentle slopes.

Example parameters:

```json
"sinkholeplateaus": {
  "noiseScale": 0.0003,
  "terrainOctaves": [0, 0, 0, 0, 0, 1, 1, 1, 0],
  "terrainYKeyPositions": [0.40, 0.50, 0.62, 0.65, 0.67],
  "terrainYKeyThresholds": [1, 0.75, 0.6, 0.55, 0]
},
"terraceplateaus": {
  "heightOffset": 0.65,
  "terrainYKeyPositions": [0.40, 0.55, 0.70, 0.75],
  "terrainYKeyThresholds": [1, 0.5, 0.2, 0]
}
```

These tweaks widen canyon floors and raise terrace heights for easier building.

---

## ✅ Final Notes

- Avoid abrupt vertical jumps without plateaus (makes terrain unplayable)
- Keep some low-noise or low-threshold areas for navigability
- All noise layers should remain seed-stable
- Output worldgen should be visually interesting AND survivable

Codex: apply these rules when refactoring landform definitions or noise code.

---

**End of Guide**

