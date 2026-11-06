#!/usr/bin/env python3
"""Baixa os icones das categorias da biblioteca (Noto Emoji, Apache 2.0) e grava um PNG de 256 px por categoria.

Requer Pillow. Uso: baixar_icones_biblioteca.py PASTA_DESTINO
"""
import io
import sys
import time
import urllib.request
from pathlib import Path

from PIL import Image

# pasta da categoria, codigo do emoji; sem logos de marcas
CATEGORIES = {
    "Anime & Manga": "1f338",
    "Games": "1f3ae",
    "Memes": "1f923",
    "Movies": "1f3ac",
    "Music": "1f3b5",
    "Politics": "1f3db",
    "Pranks": "1f0cf",
    "Reactions": "1f62e",
    "Sound Effects": "1f50a",
    "Sports": "26bd",
    "Television": "1f4fa",
    "Tiktok Trends": "1f4f1",
    "Viral": "1f525",
    "Whatsapp Audios": "1f4ac",
}


def main():
    dest = Path(sys.argv[1])
    dest.mkdir(parents=True, exist_ok=True)
    for folder, emoji in CATEGORIES.items():
        url = f"https://raw.githubusercontent.com/googlefonts/noto-emoji/v2.042/png/512/emoji_u{emoji}.png"
        data = urllib.request.urlopen(urllib.request.Request(url, headers={"User-Agent": "AtalhosPC/1.0"}), timeout=60).read()
        with Image.open(io.BytesIO(data)) as im:
            im.convert("RGBA").resize((256, 256), Image.LANCZOS).save(dest / (folder + ".png"))
        print("ok", folder, flush=True)
        time.sleep(0.5)


if __name__ == "__main__":
    main()
