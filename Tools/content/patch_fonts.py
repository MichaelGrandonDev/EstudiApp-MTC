#!/usr/bin/env python3
"""Añade a Outfit las vocales del pinyin con carón (ǐ ǒ ǔ ǚ y mayúsculas) que la fuente no trae.

Sin ellas Unity las toma de la fuente china de respaldo y el pinyin queda con huecos. Se componen con la base y el
carón combinante de la propia fuente, centrado igual que en ǎ/ě. Outfit es OFL sin nombre reservado.
Uso: python3 Tools/content/patch_fonts.py
"""
from pathlib import Path

from fontTools.ttLib import TTFont
from fontTools.ttLib.tables._g_l_y_f import Glyph, GlyphComponent

FONTS = Path(__file__).resolve().parents[2] / "EstudiAppMTC/Assets/_Project/Resources/UI/Fonts"
TARGETS = {
    0x01D0: ("dotlessi", "uni030C"),  # ǐ
    0x01D2: ("o", "uni030C"),  # ǒ
    0x01D4: ("u", "uni030C"),  # ǔ
    0x01CF: ("I", "uni030C.case"),  # Ǐ
    0x01D1: ("O", "uni030C.case"),  # Ǒ
    0x01D3: ("U", "uni030C.case"),  # Ǔ
}


def bounds(font, name):
    glyph = font["glyf"][name]
    glyph.recalcBounds(font["glyf"])
    return glyph.xMin, glyph.xMax, glyph.yMin, glyph.yMax


def patch(path):
    font = TTFont(path)
    order = font.getGlyphOrder()
    glyf, hmtx = font["glyf"], font["hmtx"]
    cmaps = [t for t in font["cmap"].tables if t.isUnicode()]
    best = font.getBestCmap()
    added = []
    for code, (base, mark) in TARGETS.items():
        if code in best or base not in order:
            continue
        if mark not in order:
            mark = "uni030C"
        if mark not in order:
            continue
        bx0, bx1, _, _ = bounds(font, base)
        mx0, mx1, _, _ = bounds(font, mark)
        dx = round((bx0 + bx1) / 2 - (mx0 + mx1) / 2)
        dy = 0
        if base in ("I", "O", "U") and mark == "uni030C":
            _, _, _, top = bounds(font, base)
            _, _, _, x_top = bounds(font, "o")
            dy = top - x_top
        name = "uni%04X" % code
        composite = Glyph()
        composite.numberOfContours = -1
        composite.components = []
        for glyph_name, x, y in ((base, 0, 0), (mark, dx, dy)):
            component = GlyphComponent()
            component.glyphName, component.x, component.y, component.flags = glyph_name, x, y, 0
            composite.components.append(component)
        composite.components[0].flags = 0x0200  # USE_MY_METRICS
        glyf.glyphs[name] = composite
        order.append(name)
        hmtx.metrics[name] = hmtx.metrics[base]
        for table in cmaps:
            table.cmap[code] = name
        added.append(chr(code))
    if not added:
        print(f"{path.name}: nada que añadir")
        return
    font.setGlyphOrder(order)
    glyf.glyphOrder = order
    font["maxp"].numGlyphs = len(order)
    if "post" in font and font["post"].formatType == 2.0:
        font["post"].extraNames = []
        font["post"].mapping = {}
    font.save(path)
    print(f"{path.name}: añadidas {''.join(added)}")


if __name__ == "__main__":
    for file in sorted(FONTS.glob("Outfit-*.ttf")):
        patch(file)
