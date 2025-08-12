import json
import os
import argparse
import re
from opensimplex import OpenSimplex
from PIL import Image

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
# By default this script loads the landform definitions from the
# SelectedLandforms patch file so previews match the in-game data.
DEFAULT_LANDFORMS_FILE = os.path.join(
    SCRIPT_DIR,
    "SelectedLandforms",
    "assets",
    "selectedlandforms",
    "patches",
    "landforms.json",
)
DEFAULT_LANDFORM_CODE = ""
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
parser.add_argument(
    "--landforms-file",
    default=DEFAULT_LANDFORMS_FILE,
    help="Path to landforms JSON (default: %(default)s)",
)
parser.add_argument(
    "--code",
    default=DEFAULT_LANDFORM_CODE,
    help="Code of landform to render (default: all)",
)
parser.add_argument(
    "--definition",
    help="Inline landform JSON definition for quick testing (overrides file and code)",
)
parser.add_argument(
    "--zoom",
    type=float,
    default=1.0,
    help="Scale preview coordinates to reveal detail when noiseScale is small",
)
parser.set_defaults(heightmap=True)
args = parser.parse_args()
SIZE = args.size
HEIGHTMAP = args.heightmap
SEED = args.seed
LANDFORMS_FILE = args.landforms_file
ZOOM = args.zoom
TARGET_CODE = args.code
definition_json = args.definition
if definition_json:
    # Directly render the provided definition when given via the command line
    landforms = [json.loads(definition_json)]
else:
    with open(LANDFORMS_FILE) as f:
        patch_data = json.load(f)

    # The SelectedLandforms patch file may contain an array of operations instead
    # of a plain object. Extract the landform definitions regardless of format so
    # every variation gets rendered.
    landforms = []
    if isinstance(patch_data, list):
        for op in patch_data:
            path = op.get("path", "")
            if not path.startswith("/variants"):
                continue
            value = op.get("value")
            if isinstance(value, list):
                landforms.extend(value)
            elif isinstance(value, dict):
                landforms.append(value)
    else:
        landforms = patch_data.get("variants", [])

    allowed_codes = {
        "step_mountains_6tier_test_control",
        "step_mountains_4tiera",
        "step_mountains_6tier_test_small_2_big_789",
        "step_mountains_6tier_test_Large All",
        "step_mountains_6tier_test_large_123",
        "step_mountains_6tier_test_largemid_456",
        "step_mountains_6tier_test_smallall",
        "step_mountains_6tier_test_4all",
        "step_mountains_6tier_med2",
        "step_mountains_6tier_test_4_123",
        "step_mountains_6tier_test_4_456",
        "step_mountains_6tier_test_4_789",
        "step_mountains_6tier_test_4_23_2_5_2_7",
    }
    landforms = [lf for lf in landforms if lf.get("code") in allowed_codes]

    if TARGET_CODE:
        landforms = [lf for lf in landforms if lf.get("code") == TARGET_CODE]


os.makedirs(OUTPUT_DIR, exist_ok=True)

# Set up OpenSimplex noise generators similar to the mod's FastNoiseLite usage
main_noise = OpenSimplex(SEED)
warp_noise_x = OpenSimplex(SEED + 1)
warp_noise_z = OpenSimplex(SEED + 2)


def sample_height(params, x, z):
    """Return normalized height value for a coordinate or ``None`` when below
    the landform threshold.
    """
    scale = params.get("noiseScale", 0.001) * 2
    octaves = params.get("terrainOctaves", []) or [1]
    octave_thresholds = params.get("terrainOctaveThresholds", [])
    if len(octave_thresholds) < len(octaves):
        octave_thresholds += [0] * (len(octaves) - len(octave_thresholds))

    ykeys = params.get("terrainYKeyPositions", [])
    ythresh = params.get("terrainYKeyThresholds", [])
    base_height = params.get("baseHeight", 0.0)
    height_offset = params.get("heightOffset", 1.0)
    threshold = params.get("threshold", 0.0)

    plateau_count = params.get("plateauCount", 0)
    base_radius = params.get("baseRadius", 0.0)
    radius_step = params.get("radiusStep", 0.6)
    radius_noise_scale = params.get("radiusNoiseScale", 0.0)
    radius_noise_amp = params.get("radiusNoiseAmplitude", 0.0)

    step_factor = 1.0
    if plateau_count > 0 and base_radius > 0:
        cell_size = base_radius * 2.0
        base_cell_x = int(x // cell_size)
        base_cell_z = int(z // cell_size)

        step_factor = 0.0
        for cx in range(base_cell_x - 1, base_cell_x + 2):
            for cz in range(base_cell_z - 1, base_cell_z + 2):
                jitter_x = warp_noise_x.noise2(cx * 0.1, cz * 0.1) * cell_size * 0.2
                jitter_z = warp_noise_z.noise2(cx * 0.1 + 1000, cz * 0.1 + 1000) * cell_size * 0.2
                center_x = (cx + 0.5) * cell_size + jitter_x
                center_z = (cz + 0.5) * cell_size + jitter_z
                dx = x - center_x
                dz = z - center_z
                dist = (dx * dx + dz * dz) ** 0.5
                radius = base_radius
                if radius_noise_scale > 0:
                    n = warp_noise_x.noise2(cx * radius_noise_scale, cz * radius_noise_scale)
                    radius *= 1.0 + radius_noise_amp * n
                    if radius < base_radius:
                        radius = base_radius

                inner_radius = radius
                for _ in range(1, plateau_count):
                    inner_radius *= radius_step

                cur_radius = inner_radius
                for i in range(plateau_count - 1, -1, -1):
                    shape_noise = 1.0 + warp_noise_x.noise2((x + cx * 100 + i * 50) * 0.02,
                                                          (z + cz * 100 + i * 50) * 0.02) * 0.25
                    if dist <= cur_radius * shape_noise:
                        step_factor = max(step_factor, (i + 1) / plateau_count)
                        break
                    cur_radius /= radius_step

        if step_factor == 0.0:
            return None

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

    # Clamp to sane range so increased y-key variations do not overflow
    yfactor = max(0.0, min(yfactor, 1.0))

    total = max(0.0, min(total * yfactor * step_factor, 1.0))
    return base_height + height_offset * total


def render_landform(params, name):
    img = Image.new("L", (SIZE, SIZE))
    pixels = []

    if HEIGHTMAP:
        for z in range(SIZE):
            for x in range(SIZE):
                sx = (x - SIZE / 2) * ZOOM
                sz = (z - SIZE / 2) * ZOOM
                height = sample_height(params, sx, sz)
                val = 0
                if height is not None:
                    val = int(max(0.0, min(height, 1.0)) * 255)
                pixels.append(val)
    else:
        for x in range(SIZE):
            sx = (x - SIZE / 2) * ZOOM
            height = sample_height(params, sx, 0)
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


if __name__ == "__main__":
    for lf in landforms:
        code = lf.get("code", "landform")
        safe_code = re.sub(r"[^0-9A-Za-z_-]", "_", code)
        render_landform(lf, safe_code)
