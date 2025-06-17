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
    "patches",
    "landforms.json",
)
OUTPUT_DIR = os.path.join(SCRIPT_DIR, "noise_samples")
SIZE = 256

with open(PATCH_FILE) as f:
    patch_data = json.load(f)

landforms = [entry.get("value") for entry in patch_data if isinstance(entry, dict)]
os.makedirs(OUTPUT_DIR, exist_ok=True)

for lf in landforms:
    if not lf:
        continue
    code = lf.get("code")
    scale = lf.get("noiseScale", 0.001)
    img = Image.new("L", (SIZE, SIZE))
    pixels = []
    for y in range(SIZE):
        for x in range(SIZE):
            n = pnoise2(x * scale * 1000, y * scale * 1000, octaves=4)
            val = int((n + 0.5) * 255)
            pixels.append(val)
    img.putdata(pixels)
    img.save(os.path.join(OUTPUT_DIR, f"{code}.png"))
    print("Saved", code)
