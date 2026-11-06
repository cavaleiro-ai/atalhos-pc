#!/usr/bin/env python3
"""Desenha a logo do Atalhos PC e gera o .ico do Windows e os PNGs do Android (requer Pillow)."""
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent
BG_TOP = (28, 44, 92)
BG_BOTTOM = (16, 22, 48)
TILE = (214, 226, 255)
ACCENT = (76, 141, 255)
PLAY = (255, 255, 255)
SS = 4  # desenha maior e reduz, para bordas suaves


def gradient(size):
    img = Image.new("RGB", (1, size))
    for y in range(size):
        t = y / max(1, size - 1)
        img.putpixel((0, y), tuple(int(BG_TOP[i] + (BG_BOTTOM[i] - BG_TOP[i]) * t) for i in range(3)))
    return img.resize((size, size))


def logo(size, rounded=True):
    s = size * SS
    img = gradient(s).convert("RGBA")
    if rounded:
        mask = Image.new("L", (s, s), 0)
        ImageDraw.Draw(mask).rounded_rectangle((0, 0, s - 1, s - 1), radius=int(s * 0.22), fill=255)
        img.putalpha(mask)
    d = ImageDraw.Draw(img)
    # quatro azulejos: o deck de atalhos, com o de baixo a direita em destaque e o simbolo de abrir
    margin, gap = s * 0.2, s * 0.06
    tile = (s - 2 * margin - gap) / 2
    radius = int(s * 0.055)
    for row in range(2):
        for col in range(2):
            x, y = margin + col * (tile + gap), margin + row * (tile + gap)
            d.rounded_rectangle((x, y, x + tile, y + tile), radius=radius, fill=ACCENT if (row, col) == (1, 1) else TILE)
    cx, cy = margin + tile + gap + tile / 2, margin + tile + gap + tile / 2
    r = tile * 0.27
    d.polygon([(cx - r * 0.75, cy - r), (cx - r * 0.75, cy + r), (cx + r, cy)], fill=PLAY)
    return img.resize((size, size), Image.LANCZOS)


def main():
    logo(512).save(ROOT / "assets" / "logo.png")

    win = ROOT / "windows" / "app.ico"
    logo(256).save(win, sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])

    res = ROOT / "android" / "src" / "res"
    for name, size in {"mdpi": 48, "hdpi": 72, "xhdpi": 96, "xxhdpi": 144, "xxxhdpi": 192}.items():
        out = res / f"mipmap-{name}"
        out.mkdir(parents=True, exist_ok=True)
        logo(size).save(out / "ic_launcher.png")


if __name__ == "__main__":
    main()
