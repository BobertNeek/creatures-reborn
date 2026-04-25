"""
Generate UV-safe texture maps for the current 3D Norn GLB.

The v1 direction follows art/concepts/norn-style-v1.png: warm amber fur,
cream skin, teal genetic markings, dark hair tuft, and glossy teal eyes.
Sexual dimorphism and mutation-driven variation are applied at runtime by
CreatureAppearance/NornAppearanceApplier, so these maps remain a neutral
base texture set for the shared model.
"""

from __future__ import annotations

import math
import os
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter


SIZE = 1024
REPO_ROOT = Path(__file__).resolve().parents[1]
OUT_DIR = REPO_ROOT / "assets" / "textures" / "norn"
DEFAULT_ALPHA_DIR = (
    REPO_ROOT.parents[0]
    / "resources for gpt"
    / "Geneforge4"
    / "Norn"
    / "Alpha Textures"
)
ALPHA_DIR = Path(os.environ.get("NORN_ALPHA_DIR", DEFAULT_ALPHA_DIR))


# ── Palette from art/concepts/norn-style-v1.png ─────────────────────────────
FUR_DARK = np.array([108, 65, 28], dtype=np.float32)
FUR_BASE = np.array([190, 116, 42], dtype=np.float32)
FUR_LIGHT = np.array([232, 174, 92], dtype=np.float32)

SKIN_DARK = np.array([206, 144, 112], dtype=np.float32)
SKIN_BASE = np.array([238, 182, 144], dtype=np.float32)
SKIN_LIGHT = np.array([255, 219, 182], dtype=np.float32)

CREAM_DARK = np.array([183, 135, 82], dtype=np.float32)
CREAM_BASE = np.array([235, 201, 142], dtype=np.float32)
CREAM_LIGHT = np.array([255, 230, 180], dtype=np.float32)

MARKING = np.array([42, 91, 82], dtype=np.float32)
MARKING_DARK = np.array([21, 53, 48], dtype=np.float32)
HAIR_DARK = np.array([50, 30, 17], dtype=np.float32)
HAIR_LIGHT = np.array([112, 76, 43], dtype=np.float32)
CLAW_COLOR = np.array([232, 214, 178], dtype=np.float32)


# ── Deterministic noise ─────────────────────────────────────────────────────
rng = np.random.RandomState(0xB10C0A)
_PERM = np.arange(256, dtype=np.int32)
rng.shuffle(_PERM)
_PERM = np.concatenate([_PERM, _PERM])


def _fade(t: np.ndarray) -> np.ndarray:
    return t * t * t * (t * (t * 6 - 15) + 10)


def _grad(h: np.ndarray, x: np.ndarray, y: np.ndarray) -> np.ndarray:
    h = h & 3
    u = np.where((h == 0) | (h == 2), x, -x)
    v = np.where((h == 0) | (h == 1), y, -y)
    return u + v


def noise2d(x: np.ndarray, y: np.ndarray) -> np.ndarray:
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


def fbm(x: np.ndarray, y: np.ndarray, octaves: int = 5, lacunarity: float = 2.0, gain: float = 0.5) -> np.ndarray:
    val = np.zeros_like(x)
    amp = 1.0
    freq = 1.0
    for _ in range(octaves):
        val += amp * noise2d(x * freq, y * freq)
        freq *= lacunarity
        amp *= gain
    return val


def uv_grid(size: int = SIZE) -> tuple[np.ndarray, np.ndarray]:
    u = np.linspace(0, 1, size, dtype=np.float32)
    v = np.linspace(0, 1, size, dtype=np.float32)
    return np.meshgrid(u, v)


def lerp(a: np.ndarray, b: np.ndarray, t: np.ndarray | float) -> np.ndarray:
    return a + (b - a) * t


def fur_field(offset: float, scale: float = 5.5) -> tuple[np.ndarray, np.ndarray, np.ndarray]:
    uu, vv = uv_grid()
    large = fbm(uu * scale + offset, vv * scale + offset, octaves=4)
    fine = fbm(uu * 26 + offset * 0.13, vv * 38 + offset * 0.17, octaves=3) * 0.18
    directional = np.sin((uu * 35.0 + vv * 12.0 + offset) * math.pi) * 0.035
    markings = fbm(uu * 8.0 + offset * 0.3, vv * 7.0 - offset * 0.2, octaves=4)
    return large + fine + directional, markings, vv


def make_fur(offset: float, marking_strength: float = 0.22, cream_belly: bool = False) -> np.ndarray:
    field, markings, vv = fur_field(offset)
    t = np.clip(field * 0.5 + 0.5, 0, 1)

    dark_mask = t < 0.45
    dark_t = np.clip(t / 0.45, 0, 1)[:, :, None]
    light_t = np.clip((t - 0.45) / 0.55, 0, 1)[:, :, None]
    fur = np.where(
        dark_mask[:, :, None],
        lerp(FUR_DARK, FUR_BASE, dark_t),
        lerp(FUR_BASE, FUR_LIGHT, light_t),
    )

    if cream_belly:
        belly = np.clip((vv - 0.40) * 3.0, 0, 1)[:, :, None]
        cream_noise = np.clip(fbm(*uv_grid(), octaves=3) * 0.20 + 0.68, 0, 1)[:, :, None]
        cream = lerp(CREAM_DARK, CREAM_LIGHT, cream_noise)
        fur = lerp(fur, cream, belly * 0.58)

    spot = np.clip((markings - 0.02) * 4.5, 0, 1)[:, :, None]
    mottled = lerp(MARKING_DARK, MARKING, np.clip(t[:, :, None], 0, 1))
    fur = lerp(fur, mottled, spot * marking_strength)
    return np.clip(fur, 0, 255).astype(np.uint8)


def make_skin(offset: float) -> np.ndarray:
    uu, vv = uv_grid()
    pores = fbm(uu * 12 + offset, vv * 10 + offset, octaves=4)
    blush = np.clip((1 - np.abs(uu - 0.5) * 2) * 0.15 + 0.55 + pores * 0.22, 0, 1)
    skin = lerp(SKIN_DARK, SKIN_LIGHT, blush[:, :, None])
    return np.clip(skin, 0, 255).astype(np.uint8)


def make_hair(offset: float) -> np.ndarray:
    uu, vv = uv_grid()
    strand = np.sin((uu * 55 + vv * 16 + offset) * math.pi) * 0.15
    noise = fbm(uu * 18 + offset, vv * 30 + offset, octaves=4) * 0.35
    t = np.clip(0.45 + strand + noise, 0, 1)
    hair = lerp(HAIR_DARK, HAIR_LIGHT, t[:, :, None])
    return np.clip(hair, 0, 255).astype(np.uint8)


def make_eye() -> np.ndarray:
    uu, vv = uv_grid()
    x = uu - 0.5
    y = vv - 0.5
    r = np.sqrt(x * x + y * y)
    angle = np.arctan2(y, x)
    iris = np.clip(1 - r * 2.5, 0, 1)
    ring = np.clip((0.42 - np.abs(r - 0.24)) * 7.0, 0, 1)
    radial = (np.sin(angle * 18 + r * 45) * 0.5 + 0.5) * 0.22
    teal_dark = np.array([16, 86, 84], dtype=np.float32)
    teal_light = np.array([92, 214, 196], dtype=np.float32)
    img = lerp(teal_dark, teal_light, np.clip(iris + radial, 0, 1)[:, :, None])
    pupil = r < 0.10
    img[pupil] = np.array([6, 12, 14], dtype=np.float32)
    img = lerp(img, np.array([10, 36, 38], dtype=np.float32), ring[:, :, None] * 0.45)
    highlight = ((uu - 0.36) ** 2 + (vv - 0.34) ** 2) < 0.004
    img[highlight] = np.array([240, 255, 245], dtype=np.float32)
    glint = ((uu - 0.43) ** 2 + (vv - 0.29) ** 2) < 0.0007
    img[glint] = np.array([255, 255, 255], dtype=np.float32)
    return np.clip(img, 0, 255).astype(np.uint8)


def blend_alpha(base: np.ndarray, overlay: np.ndarray, alpha_name: str) -> np.ndarray:
    alpha_path = ALPHA_DIR / alpha_name
    if not alpha_path.exists():
        return base

    alpha = Image.open(alpha_path).convert("L").resize((SIZE, SIZE), Image.Resampling.LANCZOS)
    a = np.array(alpha, dtype=np.float32)[:, :, None] / 255.0
    result = base.astype(np.float32) * (1 - a) + overlay.astype(np.float32) * a
    return np.clip(result, 0, 255).astype(np.uint8)


def save(arr: np.ndarray, name: str, blur: float = 0.45) -> None:
    img = Image.fromarray(arr)
    if blur > 0:
        img = img.filter(ImageFilter.GaussianBlur(radius=blur))
    path = OUT_DIR / f"{name}.png"
    img.save(path)
    print(f"  {name}.png -> {path}")


def main() -> None:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    print(f"Writing Norn textures to {OUT_DIR}")
    print(f"Alpha masks: {ALPHA_DIR}")

    save(make_fur(10.0, marking_strength=0.28), "Thigh_F")
    save(make_fur(20.0, marking_strength=0.24), "Shin_F")
    save(make_fur(30.0, marking_strength=0.20), "Humerus_F")
    radius = blend_alpha(make_fur(40.0, marking_strength=0.18), make_skin(180.0), "Radius_A.png")
    save(radius, "Radius_F")
    save(make_fur(50.0, marking_strength=0.34), "Tail_Base_F")
    save(make_hair(60.0), "Tail_Tip_F")

    ear = blend_alpha(make_fur(70.0, marking_strength=0.16), make_skin(200.0), "Ear_A.png")
    save(ear, "Ear_F")

    body = blend_alpha(make_fur(80.0, marking_strength=0.26, cream_belly=True), make_skin(150.0), "Body_A.png")
    save(body, "Body_F")

    head = blend_alpha(make_fur(90.0, marking_strength=0.30, cream_belly=True), make_skin(230.0), "Head_A.png")
    save(head, "Head_F")

    feet = blend_alpha(make_fur(100.0, marking_strength=0.18), np.full((SIZE, SIZE, 3), CLAW_COLOR, dtype=np.uint8), "Feet_A.png")
    save(feet, "Feet_F")

    save(make_hair(110.0), "Hair")
    save(make_eye(), "Eye", blur=0.15)
    print("Done.")


if __name__ == "__main__":
    main()
