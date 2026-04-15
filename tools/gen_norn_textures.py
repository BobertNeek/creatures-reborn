"""
Generate procedural fur-pattern textures for the Geneforge norn model.
Warm amber/golden fur with cream underbelly — a natural fox-like palette.
Uses numpy for fast generation at 1024x1024.
"""

import os, math
import numpy as np
from PIL import Image, ImageFilter

# ── Palette ──────────────────────────────────────────────────────────────────
FUR_BASE  = np.array([190, 130, 50],  dtype=np.float32)
FUR_DARK  = np.array([140, 85,  30],  dtype=np.float32)
FUR_LIGHT = np.array([220, 170, 80],  dtype=np.float32)

SKIN_BASE  = np.array([245, 215, 185], dtype=np.float32)
SKIN_DARK  = np.array([225, 190, 160], dtype=np.float32)
SKIN_LIGHT = np.array([255, 235, 210], dtype=np.float32)

CLAW_COLOR = np.array([240, 230, 210], dtype=np.float32)

SIZE = 1024

ALPHA_DIR = r"E:\Creatures Reborn\resources for gpt\Geneforge4\Norn\Alpha Textures"
OUT_DIR   = r"E:\Creatures Reborn\CreaturesReborn\assets\textures\norn"


# ── Noise (vectorized Perlin-ish) ────────────────────────────────────────────
rng = np.random.RandomState(42)
_PERM = np.arange(256, dtype=np.int32)
rng.shuffle(_PERM)
_PERM = np.concatenate([_PERM, _PERM])


def _fade(t):
    return t * t * t * (t * (t * 6 - 15) + 10)


def _grad(h, x, y):
    """Vectorized gradient selection."""
    h = h & 3
    # h==0: x+y, h==1: -x+y, h==2: x-y, h==3: -x-y
    u = np.where((h == 0) | (h == 2), x, -x)
    v = np.where((h == 0) | (h == 1), y, -y)
    return u + v


def noise2d(x, y):
    """Vectorized 2D Perlin noise, x/y are 2D arrays. Returns array in ~[-1,1]."""
    xi = np.floor(x).astype(np.int32) & 255
    yi = np.floor(y).astype(np.int32) & 255
    xf = x - np.floor(x)
    yf = y - np.floor(y)
    u = _fade(xf)
    v = _fade(yf)
    aa = _PERM[_PERM[xi] + yi]
    ab = _PERM[_PERM[xi] + yi + 1]
    ba = _PERM[_PERM[xi + 1] + yi]
    bb = _PERM[_PERM[xi + 1] + yi + 1]
    x1 = (1 - u) * _grad(aa, xf, yf) + u * _grad(ba, xf - 1, yf)
    x2 = (1 - u) * _grad(ab, xf, yf - 1) + u * _grad(bb, xf - 1, yf - 1)
    return (1 - v) * x1 + v * x2


def fbm(x, y, octaves=5, lacunarity=2.0, gain=0.5):
    val = np.zeros_like(x)
    amp = 1.0
    freq = 1.0
    for _ in range(octaves):
        val += amp * noise2d(x * freq, y * freq)
        freq *= lacunarity
        amp *= gain
    return val


def make_uv_grid(size):
    u = np.linspace(0, 1, size, dtype=np.float32)
    v = np.linspace(0, 1, size, dtype=np.float32)
    return np.meshgrid(u, v)


def make_fur(size, fur_scale=6.0, stripe_scale=2.5, offset=0.0):
    """Generate fur-colored texture as (H, W, 3) uint8 array."""
    uu, vv = make_uv_grid(size)

    # Large-scale organic variation
    n1 = fbm(uu * fur_scale + offset, vv * fur_scale + offset, octaves=4)
    # Fine detail (fur strand texture)
    n2 = fbm(uu * 20 + offset + 100, vv * 20 + offset + 100, octaves=3) * 0.15
    # Subtle directional stripes (tabby-like)
    stripe = fbm(uu * stripe_scale + vv * 0.5 + offset + 50,
                 vv * stripe_scale + offset + 50, octaves=3) * 0.3

    combined = n1 * 0.5 + n2 + stripe
    t = np.clip(combined * 0.5 + 0.5, 0, 1)

    # Map t to fur colors: dark → base → light
    img = np.zeros((size, size, 3), dtype=np.float32)
    dark_mask = t < 0.4
    f_dark = t / 0.4
    f_light = (t - 0.4) / 0.6

    for c in range(3):
        img[:, :, c] = np.where(
            dark_mask,
            FUR_DARK[c] + (FUR_BASE[c] - FUR_DARK[c]) * f_dark,
            FUR_BASE[c] + (FUR_LIGHT[c] - FUR_BASE[c]) * f_light
        )

    return np.clip(img, 0, 255).astype(np.uint8)


def make_skin(size, offset=200.0):
    """Generate skin-colored texture."""
    uu, vv = make_uv_grid(size)
    n = fbm(uu * 8 + offset, vv * 8 + offset, octaves=3)
    t = np.clip(n * 0.3 + 0.5, 0, 1)

    img = np.zeros((size, size, 3), dtype=np.float32)
    dark_mask = t < 0.5
    f_dark = t / 0.5
    f_light = (t - 0.5) / 0.5

    for c in range(3):
        img[:, :, c] = np.where(
            dark_mask,
            SKIN_DARK[c] + (SKIN_BASE[c] - SKIN_DARK[c]) * f_dark,
            SKIN_BASE[c] + (SKIN_LIGHT[c] - SKIN_BASE[c]) * f_light
        )

    return np.clip(img, 0, 255).astype(np.uint8)


def blend_alpha(fur_arr, skin_arr, alpha_path):
    """Blend fur/skin arrays using alpha mask (black=fur, white=skin)."""
    alpha = Image.open(alpha_path).convert("L").resize((SIZE, SIZE))
    a = np.array(alpha, dtype=np.float32)[:, :, np.newaxis] / 255.0
    result = fur_arr.astype(np.float32) * (1 - a) + skin_arr.astype(np.float32) * a
    return np.clip(result, 0, 255).astype(np.uint8)


def save(arr, name):
    img = Image.fromarray(arr)
    img = img.filter(ImageFilter.GaussianBlur(radius=0.8))
    path = os.path.join(OUT_DIR, f"{name}.png")
    img.save(path)
    print(f"  {name}.png -> {path}")


def main():
    os.makedirs(OUT_DIR, exist_ok=True)

    # ── Fur-only parts ───────────────────────────────────────────────────────
    fur_parts = {
        "Thigh_F":     10.0,
        "Shin_F":      20.0,
        "Humerus_F":   30.0,
        "Radius_F":    40.0,
        "Tail_Base_F": 50.0,
        "Tail_Tip_F":  60.0,
        "Ear_F":       70.0,
    }
    for name, offset in fur_parts.items():
        print(f"Generating {name}...")
        arr = make_fur(SIZE, offset=offset)
        save(arr, name)

    # ── Alpha-masked parts (fur + skin) ──────────────────────────────────────
    print("Generating Body_F (fur + skin)...")
    body_fur  = make_fur(SIZE, offset=0.0)
    body_skin = make_skin(SIZE, offset=150.0)
    alpha_body = os.path.join(ALPHA_DIR, "Body_A.png")
    if os.path.exists(alpha_body):
        arr = blend_alpha(body_fur, body_skin, alpha_body)
    else:
        arr = body_fur
    save(arr, "Body_F")

    print("Generating Head_F (fur + skin)...")
    head_fur  = make_fur(SIZE, offset=80.0)
    head_skin = make_skin(SIZE, offset=230.0)
    alpha_head = os.path.join(ALPHA_DIR, "Head_A.png")
    if os.path.exists(alpha_head):
        arr = blend_alpha(head_fur, head_skin, alpha_head)
    else:
        arr = head_fur
    save(arr, "Head_F")

    # ── Feet (fur + claw-colored tips) ───────────────────────────────────────
    print("Generating Feet_F (fur + claws)...")
    feet_fur = make_fur(SIZE, offset=90.0)
    claw_arr = np.full((SIZE, SIZE, 3), CLAW_COLOR, dtype=np.uint8)
    alpha_feet = os.path.join(ALPHA_DIR, "Feet_A.png")
    if os.path.exists(alpha_feet):
        arr = blend_alpha(feet_fur, claw_arr, alpha_feet)
    else:
        arr = feet_fur
    save(arr, "Feet_F")

    print("\nDone! 10 fur textures generated. Eye.png kept as-is.")


if __name__ == "__main__":
    main()
