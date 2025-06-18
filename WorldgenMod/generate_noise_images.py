import json
import os
import argparse
from opensimplex import OpenSimplex
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
parser.add_argument(
    "--seed",
    type=int,
    default=0,
    help="Noise seed used for generation (default 0)",
)
parser.set_defaults(heightmap=True)
args = parser.parse_args()
SIZE = args.size
HEIGHTMAP = args.heightmap
SEED = args.seed

with open(PATCH_FILE) as f:
    patch_data = json.load(f)

landforms = patch_data.get("variants", [])
os.makedirs(OUTPUT_DIR, exist_ok=True)

# Set up OpenSimplex noise generators similar to the mod's FastNoiseLite usage
main_noise = OpenSimplex(SEED)
warp_noise_x = OpenSimplex(SEED + 1)
warp_noise_z = OpenSimplex(SEED + 2)


def sample_height(params, x, z):
    """Return normalized height value for a coordinate or ``None`` when below
    the landform threshold.
    """
    scale = params.get("noiseScale", 0.001)
    octaves = params.get("terrainOctaves", []) or [1]
    octave_thresholds = params.get("terrainOctaveThresholds", [])
    if len(octave_thresholds) < len(octaves):
        octave_thresholds += [0] * (len(octaves) - len(octave_thresholds))

    ykeys = params.get("terrainYKeyPositions", [])
    ythresh = params.get("terrainYKeyThresholds", [])
    base_height = params.get("baseHeight", 0.0)
    height_offset = params.get("heightOffset", 0.0)
    threshold = params.get("threshold", 0.0)

    warp_x = warp_noise_x.noise2(x * WARP_SCALE, z * WARP_SCALE) * WARP_AMPLITUDE
    warp_z = warp_noise_z.noise2(x * WARP_SCALE + 1000, z * WARP_SCALE + 1000) * WARP_AMPLITUDE

    total = 0.0
    for i, amp in enumerate(octaves):
        freq = 2 ** i
        thr = octave_thresholds[i]
        raw = main_noise.noise2((x + warp_x) * scale * freq, (z + warp_z) * scale * freq)
        n = (raw + 1.0) * 0.5
        n = max(0.0, n - thr)
        n = 3 * n * n - 2 * n * n * n
        total += amp * n

    total = max(0.0, min(total, 1.0))
    if total < threshold:
        return None

    yfactor = 1.0
    if ykeys and ythresh and len(ythresh) >= len(ykeys):
        yfactor = ythresh[-1]
        for i in range(len(ykeys) - 1):
            if total <= ykeys[i + 1]:
                t1 = ythresh[i]
                t2 = ythresh[i + 1]
                p1 = ykeys[i]
                p2 = ykeys[i + 1]
                ratio = 0 if p2 == p1 else (total - p1) / (p2 - p1)
                yfactor = t1 + (t2 - t1) * ratio
                break

    total = max(0.0, min(total * yfactor, 1.0))
    return base_height + height_offset * total


def render_landform(params, name):
    img = Image.new("L", (SIZE, SIZE))
    pixels = []

    if HEIGHTMAP:
        for z in range(SIZE):
            for x in range(SIZE):
                height = sample_height(params, x, z)
                val = 0
                if height is not None:
                    val = int(max(0.0, min(height, 1.0)) * 255)
                pixels.append(val)
    else:
        for x in range(SIZE):
            height = sample_height(params, x, 0)
            hpix = 0
            if height is not None:
                hpix = int(max(0.0, min(height, 1.0)) * (SIZE - 1))
            for y in range(SIZE):
                val = 255 if SIZE - 1 - y <= hpix else 0
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
