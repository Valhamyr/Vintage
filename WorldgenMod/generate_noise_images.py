import json
import os
from noise import pnoise2
from PIL import Image

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
PATCH_FILE = os.path.join(
    SCRIPT_DIR,
    "FixedCliffs",
    "assets",
    "fixedcliffs",
    "worldgen",
    "landforms.json",
)
OUTPUT_DIR = os.path.join(SCRIPT_DIR, "noise_samples")
SIZE = 256
WARP_SCALE = 0.01
WARP_AMPLITUDE = 20.0

with open(PATCH_FILE) as f:
    patch_data = json.load(f)

landforms = patch_data.get("landforms", [])
os.makedirs(OUTPUT_DIR, exist_ok=True)

for lf in landforms:
    if not lf:
        continue
    code = lf.get("code")
    scale = lf.get("noiseScale", 0.001)
    octaves = lf.get("terrainOctaves", [])
    octave_thresholds = lf.get("terrainOctaveThresholds", [])
    ykeys = lf.get("terrainYKeyPositions", [])
    ythresh = lf.get("terrainYKeyThresholds", [])
    if not octaves:
        octaves = [1]
    if len(octave_thresholds) < len(octaves):
        octave_thresholds += [0] * (len(octaves) - len(octave_thresholds))
    img = Image.new("L", (SIZE, SIZE))
    pixels = []
    for y in range(SIZE):
        ynorm = y / (SIZE - 1)
        yfactor = 1.0
        if ykeys and ythresh:
            yfactor = ythresh[-1]
            for i in range(len(ykeys) - 1):
                if ynorm <= ykeys[i + 1]:
                    t1 = ythresh[i]
                    t2 = ythresh[i + 1]
                    p1 = ykeys[i]
                    p2 = ykeys[i + 1]
                    ratio = 0 if p2 == p1 else (ynorm - p1) / (p2 - p1)
                    yfactor = t1 + (t2 - t1) * ratio
                    break
        for x in range(SIZE):
            total = 0.0
            warp_x = pnoise2(x * WARP_SCALE, y * WARP_SCALE) * WARP_AMPLITUDE
            warp_z = pnoise2(x * WARP_SCALE + 1000, y * WARP_SCALE + 1000) * WARP_AMPLITUDE
            for i, amp in enumerate(octaves):
                freq = 2 ** i
                thr = octave_thresholds[i]
                raw = pnoise2((x + warp_x) * scale * freq * 1000,
                               (y + warp_z) * scale * freq * 1000)
                n = (raw + 1.0) * 0.5
                n = max(0.0, n - thr)
                n = 3 * n * n - 2 * n * n * n
                total += amp * n
            total = max(0.0, min(total * yfactor, 1.0))
            val = int(total * 255)
            pixels.append(val)
    img.putdata(pixels)
    img.save(os.path.join(OUTPUT_DIR, f"{code}.png"))
    print("Saved", code)
