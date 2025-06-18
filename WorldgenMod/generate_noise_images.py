import json
import os
import argparse
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
WARP_SCALE = 0.01
WARP_AMPLITUDE = 20.0

parser = argparse.ArgumentParser(
    description="Render preview noise maps from landform definitions"
)
parser.add_argument(
    "--size",
    type=int,
    default=256,
    help="Width and height of the generated PNG images",
)
parser.add_argument(
    "--cross-section",
    dest="heightmap",
    action="store_false",
    help="Generate vertical cross-sections instead of a top-down heightmap",
)
parser.set_defaults(heightmap=True)
args = parser.parse_args()
SIZE = args.size
HEIGHTMAP = args.heightmap

with open(PATCH_FILE) as f:
    patch_data = json.load(f)

landforms = patch_data.get("landforms", [])
os.makedirs(OUTPUT_DIR, exist_ok=True)


def render_landform(params, name):
    scale = params.get("noiseScale", 0.001)
    octaves = params.get("terrainOctaves", [])
    octave_thresholds = params.get("terrainOctaveThresholds", [])
    ykeys = params.get("terrainYKeyPositions", [])
    ythresh = params.get("terrainYKeyThresholds", [])
    base_height = params.get("baseHeight", 0.0)
    height_offset = params.get("heightOffset", 0.0)
    threshold = params.get("threshold", 0.0)

    if not octaves:
        octaves = [1]
    if len(octave_thresholds) < len(octaves):
        octave_thresholds += [0] * (len(octaves) - len(octave_thresholds))

    img = Image.new("L", (SIZE, SIZE))
    pixels = []

    if HEIGHTMAP:
        for z in range(SIZE):
            for x in range(SIZE):
                total = 0.0
                warp_x = pnoise2(x * WARP_SCALE, z * WARP_SCALE) * WARP_AMPLITUDE
                warp_z = (
                    pnoise2(x * WARP_SCALE + 1000, z * WARP_SCALE + 1000)
                    * WARP_AMPLITUDE
                )
                for i, amp in enumerate(octaves):
                    freq = 2**i
                    thr = octave_thresholds[i]
                    raw = pnoise2(
                        (x + warp_x) * scale * freq * 1000,
                        (z + warp_z) * scale * freq * 1000,
                    )
                    n = (raw + 1.0) * 0.5
                    n = max(0.0, n - thr)
                    n = 3 * n * n - 2 * n * n * n
                    total += amp * n
                total = max(0.0, min(total, 1.0))
                if total < threshold:
                    val = 0
                else:
                    height = base_height + height_offset * total
                    val = int(max(0.0, min(height, 1.0)) * 255)
                pixels.append(val)
    else:
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
                warp_z = (
                    pnoise2(x * WARP_SCALE + 1000, y * WARP_SCALE + 1000)
                    * WARP_AMPLITUDE
                )
                for i, amp in enumerate(octaves):
                    freq = 2**i
                    thr = octave_thresholds[i]
                    raw = pnoise2(
                        (x + warp_x) * scale * freq * 1000,
                        (y + warp_z) * scale * freq * 1000,
                    )
                    n = (raw + 1.0) * 0.5
                    n = max(0.0, n - thr)
                    n = 3 * n * n - 2 * n * n * n
                    total += amp * n
                total = max(0.0, min(total * yfactor, 1.0))
                if total < threshold:
                    val = 0
                else:
                    val = int(total * 255)
                pixels.append(val)
    img.putdata(pixels)
    img.save(
        os.path.join(
            OUTPUT_DIR, f"{name}_{'heightmap' if HEIGHTMAP else 'preview'}.png"
        )
    )
    print("Saved", name, "heightmap" if HEIGHTMAP else "preview")


for lf in landforms:
    if not lf:
        continue
    base_code = lf.get("code", "landform")
    render_landform(lf, base_code)
    for mut in lf.get("mutations", []):
        params = lf.copy()
        params.update(mut)
        params.pop("chance", None)
        mcode = mut.get("code", "mut")
        render_landform(params, f"{base_code}_{mcode}")
