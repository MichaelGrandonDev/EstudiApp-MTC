#!/usr/bin/env python3
"""Genera el icono de EstudiApp MTC: un Taiji en papel y tinta con anillo de jade sobre fondo noche.

Salida (Assets/_Project/Branding):
  AppIcon.png      1024 px a sangre, para iOS y Android (el sistema recorta la forma)
  AppIcon-mac.png  1024 px con la forma redondeada y el margen que usan los iconos de macOS
  AppIcon-android-bg.png / AppIcon-android-fg.png  capas del icono adaptativo de Android
Uso: python3 Tools/branding/make_icon.py
"""
import math
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

OUT = Path(__file__).resolve().parents[2] / "EstudiAppMTC/Assets/_Project/Branding"
S = 2048  # se dibuja al doble y se reduce para suavizar bordes

NIGHT_TOP = (40, 52, 64)
NIGHT_BOTTOM = (24, 30, 38)
PAPER = (246, 241, 231)
INK = (36, 42, 51)
JADE = (46, 125, 107)
GOLD = (201, 154, 46)


def gradient(size):
    img = Image.new("RGB", (size, size))
    draw = ImageDraw.Draw(img)
    for y in range(size):
        t = y / (size - 1)
        color = tuple(round(a + (b - a) * t) for a, b in zip(NIGHT_TOP, NIGHT_BOTTOM))
        draw.line([(0, y), (size, y)], fill=color)
    return img


def taiji(draw, cx, cy, r):
    """Yang (papel) arriba y Yin (tinta) abajo, cada uno con la semilla del otro."""
    box = lambda x, y, rr: [x - rr, y - rr, x + rr, y + rr]
    draw.ellipse(box(cx, cy, r), fill=INK)
    draw.pieslice(box(cx, cy, r), 180, 360, fill=PAPER)
    h = r / 2
    draw.ellipse(box(cx + h, cy, h), fill=PAPER)
    draw.ellipse(box(cx - h, cy, h), fill=INK)
    dot = r * 0.14
    draw.ellipse(box(cx + h, cy, dot), fill=INK)
    draw.ellipse(box(cx - h, cy, dot), fill=PAPER)
    draw.ellipse(box(cx, cy, r), outline=PAPER, width=round(r * 0.035))


def background(size, scale=1.0):
    """Fondo noche con el halo de jade; `scale` encoge el halo junto con el emblema."""
    img = gradient(size).convert("RGBA")
    glow = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    c, g = size / 2, size * 0.34 * scale
    ImageDraw.Draw(glow).ellipse([c - g, c - g, c + g, c + g], fill=JADE + (110,))
    img.alpha_composite(glow.filter(ImageFilter.GaussianBlur(size * 0.06 * scale)))
    return img


def emblem(img, scale=1.0):
    """Anillos, Taiji y puntos dorados centrados; `scale` es su tamaño relativo al lienzo."""
    draw = ImageDraw.Draw(img)
    c = img.width / 2
    k = img.width * scale
    ring = k * 0.335
    draw.ellipse([c - ring, c - ring, c + ring, c + ring], outline=JADE, width=round(k * 0.028))
    ring_gold = ring + k * 0.03
    draw.ellipse([c - ring_gold, c - ring_gold, c + ring_gold, c + ring_gold], outline=GOLD, width=round(k * 0.006))
    taiji(draw, c, c, k * 0.29)
    for i in range(8):
        a = math.radians(-90 + i * 45)
        rr = ring_gold + k * 0.045
        x, y = c + rr * math.cos(a), c + rr * math.sin(a)
        d = k * 0.011
        draw.ellipse([x - d, y - d, x + d, y + d], fill=GOLD)
    return img


def artwork(size):
    return emblem(background(size))


def rounded_mask(size, inset, radius):
    mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(mask).rounded_rectangle([inset, inset, size - inset, size - inset], radius=radius, fill=255)
    return mask


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    art = artwork(S)
    art.resize((1024, 1024), Image.LANCZOS).convert("RGB").save(OUT / "AppIcon.png")

    # macOS: forma de 824/1024 con radio ~185 y sombra suave.
    inset = round(S * 100 / 1024)
    radius = round(S * 185 / 1024)
    inner = art.resize((S - 2 * inset, S - 2 * inset), Image.LANCZOS)
    canvas = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    shadow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).rounded_rectangle([inset, inset + S * 0.012, S - inset, S - inset + S * 0.012], radius=radius, fill=(0, 0, 0, 90))
    canvas.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(S * 0.012)))
    tile = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    tile.paste(inner, (inset, inset))
    canvas.paste(tile, (0, 0), rounded_mask(S, inset, radius))
    canvas.resize((1024, 1024), Image.LANCZOS).save(OUT / "AppIcon-mac.png")

    # Android adaptativo: el lanzador sólo garantiza el círculo central de 66/108 de cada capa,
    # así que el emblema (0,84 del lienzo con los puntos dorados) se reduce para caber entero.
    scale = 0.7
    background(S, scale).resize((1024, 1024), Image.LANCZOS).convert("RGB").save(OUT / "AppIcon-android-bg.png")
    fg = emblem(Image.new("RGBA", (S, S), (0, 0, 0, 0)), scale)
    fg.resize((1024, 1024), Image.LANCZOS).save(OUT / "AppIcon-android-fg.png")
    print("Iconos ->", OUT)


if __name__ == "__main__":
    main()
