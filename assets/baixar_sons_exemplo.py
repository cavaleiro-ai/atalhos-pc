#!/usr/bin/env python3
"""Baixa os sons de exemplo (licencas livres, conferidas pela API do Wikimedia Commons) e os icones Noto Emoji.

Corta cada som em no maximo 6 s, nivela o volume, grava em MP3 ao lado do icone e registra os creditos em LICENCAS.md.
Requer ffmpeg e Pillow. Uso: baixar_sons_exemplo.py PASTA_TEMPORARIA PASTA_DESTINO
"""
import html
import json
import re
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

from PIL import Image

sys.stdout.reconfigure(encoding="utf-8")
UA = "AtalhosPC-exemplos/1.0 (uso pessoal)"
MAX_SECONDS = 6
# nome no app, arquivo no Commons, codigo do icone Noto Emoji
SOUNDS = [
    ("Cachorro latindo", "Barking of a dog.ogg", "1f436"),
    ("Gato miando", "Meow.ogg", "1f431"),
    ("Galo cantando", "Rooster crowing.ogg", "1f413"),
    ("Pato", "Mudchute duck 1.ogg", "1f986"),
    ("Sapo", "Single Frog Croak.oga", "1f438"),
    ("Aplausos", "277021 sandermotions applause-2.wav", "1f44f"),
    ("Viva", "Clapping hurray (cropped).oga", "1f389"),
    ("Buzina", "Car Horn.wav", "1f697"),
    ("Trovão", "Thunder 01.ogg", "26c8"),
    ("Vidro quebrando", "Glass breaking (Gravity Sound).wav", "1f4a5"),
    ("Sino", "Theaterglocke Bezirksmuseum Brigittenau.oga", "1f514"),
]
ACCEPTED = re.compile(r"^(CC0|Public domain|CC BY(-SA)? [0-9.]+)", re.I)


def get(url):
    req = urllib.request.Request(url, headers={"User-Agent": UA})
    for attempt in range(5):
        try:
            return urllib.request.urlopen(req, timeout=60).read()
        except urllib.error.HTTPError as e:
            if e.code != 429:
                raise
            wait = int(e.headers.get("Retry-After") or 60) + 5
            print(f"  429: aguardando {wait}s", flush=True)
            time.sleep(wait)
    raise RuntimeError("muitas recusas: " + url)


def metadata():
    query = urllib.parse.urlencode({
        "action": "query", "format": "json", "prop": "imageinfo", "iiprop": "url|extmetadata",
        "titles": "|".join("File:" + s[1] for s in SOUNDS),
    })
    data = json.loads(get("https://commons.wikimedia.org/w/api.php?" + query))
    found = {}
    for page in data["query"]["pages"].values():
        if "imageinfo" not in page:
            print("NAO ACHEI:", page.get("title"), flush=True)
            continue
        ii = page["imageinfo"][0]
        meta = ii["extmetadata"]
        artist = html.unescape(re.sub(r"<[^>]+>", "", meta.get("Artist", {}).get("value", "desconhecido"))).strip()
        found[page["title"][5:]] = dict(url=ii["url"], page=ii["descriptionurl"], license=meta.get("LicenseShortName", {}).get("value", ""), artist=artist)
    return found


def main():
    temp, dest = Path(sys.argv[1]), Path(sys.argv[2])
    temp.mkdir(parents=True, exist_ok=True)
    dest.mkdir(parents=True, exist_ok=True)
    info = metadata()
    lines = [
        "# Licenças dos sons de exemplo", "",
        "Áudio: Wikimedia Commons. Cada arquivo foi cortado em até 6 segundos, teve o volume nivelado e foi convertido para MP3; "
        "essas alterações valem para os sons em CC BY e CC BY-SA, que continuam sob a mesma licença.", "",
        "Ícones: [Noto Emoji](https://github.com/googlefonts/noto-emoji), Google, licença Apache 2.0.", "",
        "| Som | Original | Autor | Licença |", "|---|---|---|---|",
    ]
    for name, title, emoji in SOUNDS:
        m = info.get(title.replace("_", " "))
        if m is None or not ACCEPTED.match(m["license"]):
            print(f"PULADO {name}: licenca {m and m['license']!r}", flush=True)
            continue
        raw = temp / ("raw_" + re.sub(r"[^\w.]", "_", title))
        if not raw.exists():
            raw.write_bytes(get(m["url"]))
            time.sleep(6)
        out = dest / (name + ".mp3")
        subprocess.run(["ffmpeg", "-nostdin", "-y", "-loglevel", "error", "-i", str(raw), "-t", str(MAX_SECONDS),
                        "-af", f"loudnorm=I=-16:TP=-1.5:LRA=11,afade=t=out:st={MAX_SECONDS - 0.3}:d=0.3",
                        "-map_metadata", "-1", "-vn", "-ac", "2", "-ar", "44100", "-b:a", "160k", str(out)], check=True)

        png = temp / f"emoji_{emoji}.png"
        if not png.exists():
            png.write_bytes(get(f"https://raw.githubusercontent.com/googlefonts/noto-emoji/v2.042/png/512/emoji_u{emoji}.png"))
            time.sleep(1)
        with Image.open(png) as im:
            im.convert("RGBA").resize((256, 256), Image.LANCZOS).save(dest / (name + ".png"))

        duration = subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", str(out)],
                                  capture_output=True, text=True, check=True).stdout.strip()
        print(f"ok {name}: {float(duration):.1f}s, {out.stat().st_size // 1024} KB, {m['license']}", flush=True)
        lines.append(f"| {name} | [{title}]({m['page']}) | {m['artist'][:70].replace('|', '/')} | {m['license']} |")
    (dest / "LICENCAS.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    print("=== FIM", flush=True)


if __name__ == "__main__":
    main()
