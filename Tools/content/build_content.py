#!/usr/bin/env python3
"""Compila el contenido de EstudiApp MTC.

Entrada (Tools/content/src):
  axes/*.md        ejes temáticos con sus unidades y lecciones (formato descrito en el README.md del proyecto)
  diagrams/*.json  diagramas interactivos
  books.json       bibliografía
  glossary.md      términos extra del glosario (además de los de cada lección)

Salida:
  EstudiAppMTC/Assets/StreamingAssets/content/curriculum.json
  EstudiAppMTC/Assets/_Project/Resources/UI/Fonts/NotoSansSC-Subset.otf  (sólo los ideogramas usados)

Las referencias "libro:páginas" se resuelven a la página del PDF con el índice de Tools/knowledge (kb.py build).
"""
import json
import re
import sqlite3
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent.parent
SRC = HERE / "src"
KB = ROOT / "Tools/knowledge/cache/kb.db"
OUT = ROOT / "EstudiAppMTC/Assets/StreamingAssets/content/curriculum.json"
FONTS = ROOT / "EstudiAppMTC/Assets/_Project/Resources/UI/Fonts"
NOTO = ROOT / "Tools/vendor/NotoSansSC-Regular.otf"

errors = []
warnings = []


def err(where, msg):
    errors.append(f"{where}: {msg}")


# ---- Páginas ---------------------------------------------------------------------------------------------------

class Pages:
    """Página impresa -> página del PDF, por libro."""

    def __init__(self):
        self.con = sqlite3.connect(KB) if KB.exists() else None
        if self.con is None:
            warnings.append(f"Sin {KB}: las referencias no tendrán página de PDF (python3 Tools/knowledge/kb.py build).")
        self.cache = {}
        self.chapters = None

    def printed_map(self, book):
        if book in self.cache:
            return self.cache[book]
        mapping = []
        if self.con:
            for page, printed in self.con.execute("select page, printed from pages where book=? order by page", (book,)):
                if printed is None or printed == "":
                    continue
                parts = [int(x) for x in re.findall(r"\d+", str(printed))]
                if parts:
                    mapping.append((page, min(parts), max(parts)))
        # Descarta folios mal leídos por el OCR: cada página debe estar entre sus vecinas.
        clean = []
        for i, (page, lo, hi) in enumerate(mapping):
            prev_lo = mapping[i - 1][1] if i > 0 else -1
            next_lo = mapping[i + 1][1] if i + 1 < len(mapping) else 10 ** 6
            prev2 = mapping[i - 2][1] if i > 1 else -1
            next2 = mapping[i + 2][1] if i + 2 < len(mapping) else 10 ** 6
            if (prev_lo < lo < next_lo) or (prev2 < lo < next2 and (prev_lo < lo or lo < next_lo)):
                clean.append((page, lo, hi))
        self.cache[book] = clean
        return clean

    def suwen_chapter(self, number):
        if self.chapters is None:
            self.chapters = {}
            if self.con:
                for text, page in self.con.execute("select text, page from pages where book='suwen' and page >= 15 order by page"):
                    for line in text.split("\n"):
                        m = re.match(r"^Cap[ií]tulo\s+(\d+)\b", line.strip())
                        if m and int(m.group(1)) not in self.chapters:
                            self.chapters[int(m.group(1))] = page
        return self.chapters.get(number, 0)

    def resolve(self, book, pages):
        """pages: "19-21", "cap5", "cap5@34" (página de PDF explícita) o "@34"."""
        explicit = re.search(r"@(\d+)", pages)
        if explicit:
            return int(explicit.group(1))
        cap = re.match(r"cap\.?\s*(\d+)", pages)
        if cap:
            return self.suwen_chapter(int(cap.group(1))) if book == "suwen" else 0
        first = re.match(r"(\d+)", pages)
        if not first:
            return 0
        target = int(first.group(1))
        if book.startswith("neidan"):
            return target
        mapping = self.printed_map(book)
        if not mapping:
            return 0
        exact = [p for p, lo, hi in mapping if lo <= target <= hi]
        if exact:
            # Con escaneos a doble página puede haber errores de OCR: se toma la mediana de las coincidencias.
            return sorted(exact)[len(exact) // 2]
        # Interpolación con los vecinos más cercanos.
        before = [(p, lo) for p, lo, hi in mapping if hi < target]
        after = [(p, lo) for p, lo, hi in mapping if lo > target]
        if before and after:
            (p0, n0), (p1, n1) = before[-1], after[0]
            return round(p0 + (target - n0) * (p1 - p0) / max(1, n1 - n0))
        return 0


PAGES = None


def parse_ref(text, where):
    text = text.strip()
    note = None
    if "|" in text:
        text, note = [t.strip() for t in text.split("|", 1)]
    m = re.match(r"^([a-z0-9_\-]+)\s*:\s*(.+)$", text)
    if not m:
        err(where, f"referencia inválida «{text}» (formato libro:páginas)")
        return None
    book, pages = m.group(1), m.group(2).strip()
    shown = re.sub(r"@\d+", "", pages).strip()
    if shown.startswith("cap"):
        shown = "cap. " + re.sub(r"^cap\.?\s*", "", shown)
    ref = {"book": book, "pages": shown, "pdf": PAGES.resolve(book, pages)}
    if note:
        ref["note"] = note
    return ref


# ---- Texto -----------------------------------------------------------------------------------------------------

def rich(text):
    """**negrita** y *cursiva* a texto enriquecido de Unity."""
    text = re.sub(r"\*\*(.+?)\*\*", r"<b>\1</b>", text)
    text = re.sub(r"(?<![\w*])\*(?!\s)(.+?)(?<!\s)\*(?![\w*])", r"<i>\1</i>", text)
    return text


def split_cells(line):
    return [rich(c.strip()) for c in line.strip().strip("|").split("|")]


HEADER_KEYS = {"title", "hanzi", "pinyin", "subtitle", "color", "level", "premium", "requires", "description",
               "minutes", "diagrams", "summary"}


def read_header(lines, i):
    header = {}
    while i < len(lines):
        line = lines[i]
        m = re.match(r"^([a-z]+):\s*(.*)$", line)
        if not m or m.group(1) not in HEADER_KEYS:
            break
        header[m.group(1)] = m.group(2).strip()
        i += 1
    return header, i


def parse_axis_file(path):
    lines = path.read_text(encoding="utf-8").split("\n")
    axis = None
    unit = None
    i = 0
    while i < len(lines):
        line = lines[i].rstrip()
        if line.startswith("@axis "):
            header, i = read_header(lines, i + 1)
            axis = {
                "id": line[6:].strip(),
                "title": header.get("title"),
                "hanzi": header.get("hanzi"),
                "pinyin": header.get("pinyin"),
                "subtitle": header.get("subtitle"),
                "description": rich(header.get("description", "")),
                "color": header.get("color", "#2E7D6B"),
                "level": int(header.get("level", "1")),
                "premium": header.get("premium", "no").lower() in ("si", "sí", "yes", "true"),
                "requires": [r.strip() for r in header.get("requires", "").split(",") if r.strip()],
                "units": [],
            }
            continue
        if line.startswith("@unit "):
            uid, _, title = line[6:].partition("|")
            unit = {"id": uid.strip(), "title": title.strip(), "lessons": []}
            axis["units"].append(unit)
            i += 1
            continue
        if line.startswith("@lesson "):
            lid = line[8:].strip()
            header, i = read_header(lines, i + 1)
            body = []
            while i < len(lines) and not lines[i].startswith("@"):
                body.append(lines[i].rstrip())
                i += 1
            unit["lessons"].append(parse_lesson(lid, header, body, f"{path.name}:{lid}"))
            continue
        i += 1
    return axis


def parse_lesson(lid, header, body, where):
    lesson = {
        "id": lid,
        "title": header.get("title"),
        "hanzi": header.get("hanzi"),
        "pinyin": header.get("pinyin"),
        "minutes": int(header.get("minutes", "10")),
        "summary": rich(header.get("summary", "")),
        "diagramIds": [d.strip() for d in header.get("diagrams", "").split(",") if d.strip()],
        "blocks": [],
        "keyPoints": [],
        "terms": [],
        "references": [],
        "quiz": [],
    }
    if not lesson["title"]:
        err(where, "falta title")
    section = None
    pending_title = None
    paragraph = []
    table = []
    items = []
    question = None

    def flush():
        nonlocal paragraph, table, items, pending_title
        if paragraph:
            lesson["blocks"].append({"kind": "text", "title": pending_title, "text": rich(" ".join(paragraph))})
            pending_title = None
            paragraph = []
        if items:
            lesson["blocks"].append({"kind": "list", "title": pending_title, "items": [rich(x) for x in items]})
            pending_title = None
            items = []
        if table:
            rows = [split_cells(r) for r in table if not re.match(r"^\|?\s*:?-{2,}", r.strip())]
            lesson["blocks"].append({"kind": "table", "title": pending_title, "rows": rows})
            pending_title = None
            table = []

    for raw in body:
        line = raw.strip()
        if line.startswith("### "):
            flush()
            name = line[4:].strip().lower()
            section = {"puntos clave": "key", "términos": "terms", "terminos": "terms", "referencias": "refs",
                       "evaluación": "quiz", "evaluacion": "quiz"}.get(name)
            if section is None:
                err(where, f"sección desconocida «{line}»")
            continue
        if section == "key":
            if line.startswith("- "):
                lesson["keyPoints"].append(rich(line[2:]))
            continue
        if section == "terms":
            if line.startswith("- "):
                parts = [p.strip() for p in line[2:].split("|")]
                if len(parts) < 4:
                    err(where, f"término incompleto «{line}» (hanzi | pinyin | español | definición)")
                    continue
                lesson["terms"].append({"hanzi": parts[0], "pinyin": parts[1], "es": parts[2], "definition": rich(parts[3])})
            continue
        if section == "refs":
            if line.startswith("- "):
                ref = parse_ref(line[2:], where)
                if ref:
                    lesson["references"].append(ref)
            continue
        if section == "quiz":
            if line.startswith("? "):
                question = {"id": f"{lid}-q{len(lesson['quiz']) + 1}", "kind": "single", "prompt": rich(line[2:]),
                            "options": [], "answer": [], "explain": ""}
                lesson["quiz"].append(question)
            elif re.match(r"^- \[[ xX]\] ", line) and question is not None:
                if line[3] in "xX":
                    question["answer"].append(len(question["options"]))
                question["options"].append(rich(line[6:]))
            elif line.startswith("= ") and question is not None:
                question["explain"] = rich(line[2:])
            continue

        # Cuerpo de la lección
        if not line:
            flush()
            continue
        if line.startswith("## "):
            flush()
            pending_title = line[3:].strip()
            continue
        if line.startswith("> "):
            flush()
            text, _, source = line[2:].partition(" | ")
            block = {"kind": "quote", "title": pending_title, "text": rich(text.strip())}
            if source:
                block["source"] = parse_ref(source, where)
            lesson["blocks"].append(block)
            pending_title = None
            continue
        m = re.match(r"^!(nota|clinica|clínica)\s+(.*)$", line)
        if m:
            flush()
            kind = "note" if m.group(1) == "nota" else "clinical"
            lesson["blocks"].append({"kind": kind, "title": pending_title, "text": rich(m.group(2))})
            pending_title = None
            continue
        if line.startswith("::diagrama "):
            flush()
            lesson["blocks"].append({"kind": "diagram", "title": pending_title, "diagramId": line[11:].strip()})
            pending_title = None
            continue
        if line.startswith("|"):
            if paragraph or items:
                t = table
                table = []
                flush()
                table = t
            table.append(line)
            continue
        if line.startswith("- "):
            if paragraph or table:
                it = items
                items = []
                flush()
                items = it
            items.append(line[2:])
            continue
        if items or table:
            flush()
        paragraph.append(line)
    flush()

    for q in lesson["quiz"]:
        if len(q["options"]) < 2:
            err(where, f"{q['id']}: necesita al menos 2 opciones")
        if not q["answer"]:
            err(where, f"{q['id']}: sin respuesta correcta marcada")
        if len(q["answer"]) > 1:
            q["kind"] = "multi"
        elif [o.lower() for o in q["options"]] == ["verdadero", "falso"]:
            q["kind"] = "truefalse"
    if len(lesson["quiz"]) < 3:
        warnings.append(f"{where}: sólo {len(lesson['quiz'])} preguntas (se recomiendan 4 o más)")
    if not lesson["references"]:
        warnings.append(f"{where}: sin referencias")
    return lesson


# ---- Diagramas y glosario --------------------------------------------------------------------------------------

def load_diagrams():
    diagrams = []
    for path in sorted((SRC / "diagrams").glob("*.json")):
        data = json.loads(path.read_text(encoding="utf-8"))
        for d in data if isinstance(data, list) else [data]:
            where = f"{path.name}:{d.get('id')}"
            ids = {n["id"] for n in d.get("nodes", [])}
            layers = {l["id"] for l in d.get("layers", [])}
            for n in d.get("nodes", []):
                if "ref" in n and isinstance(n["ref"], str):
                    n["ref"] = parse_ref(n["ref"], where)
                if n.get("detail"):
                    n["detail"] = rich(n["detail"])
                if n.get("layer") and n["layer"] not in layers:
                    err(where, f"nodo {n['id']}: capa inexistente {n['layer']}")
            if d.get("layout"):
                for nid in d["layout"].get("order", []):
                    if nid not in ids:
                        err(where, f"layout: nodo inexistente {nid}")
            for e in d.get("edges", []):
                for end in (e.get("from"), e.get("to")):
                    if end not in ids:
                        err(where, f"flecha con nodo inexistente {end}")
                if e.get("layer") and e["layer"] not in layers:
                    err(where, f"flecha {e.get('from')}>{e.get('to')}: capa inexistente {e['layer']}")
                if e.get("detail"):
                    e["detail"] = rich(e["detail"])
            for s in d.get("steps", []):
                for f in s.get("focus", []):
                    if f not in ids:
                        err(where, f"paso «{s.get('title')}»: nodo inexistente {f}")
                s["text"] = rich(s.get("text", ""))
            for seg in d.get("segments", []):
                if seg.get("detail"):
                    seg["detail"] = rich(seg["detail"])
            for st in d.get("states", []):
                st["text"] = rich(st.get("text", ""))
                if isinstance(st.get("ref"), str):
                    st["ref"] = parse_ref(st["ref"], where)
            for z in d.get("zones", []):
                if z.get("detail"):
                    z["detail"] = rich(z["detail"])
            d["references"] = [parse_ref(r, where) if isinstance(r, str) else r for r in d.get("references", [])]
            if d.get("intro"):
                d["intro"] = rich(d["intro"])
            diagrams.append(d)
    return diagrams


def load_glossary(axes):
    terms = {}
    for axis in axes:
        for unit in axis["units"]:
            for lesson in unit["lessons"]:
                for t in lesson["terms"]:
                    terms.setdefault((t["pinyin"].lower(), t["es"].lower()), t)
    extra = SRC / "glossary.md"
    if extra.exists():
        for line in extra.read_text(encoding="utf-8").split("\n"):
            if line.startswith("- "):
                parts = [p.strip() for p in line[2:].split("|")]
                if len(parts) >= 4:
                    t = {"hanzi": parts[0], "pinyin": parts[1], "es": parts[2], "definition": rich(parts[3])}
                    terms.setdefault((t["pinyin"].lower(), t["es"].lower()), t)
    return sorted(terms.values(), key=lambda t: strip_accents(t["pinyin"]).lower())


def strip_accents(text):
    import unicodedata
    return "".join(c for c in unicodedata.normalize("NFD", text) if unicodedata.category(c) != "Mn")


# ---- Validación cruzada ----------------------------------------------------------------------------------------

def validate(curriculum):
    books = {b["id"] for b in curriculum["books"]}
    diagrams = {d["id"] for d in curriculum["diagrams"]}
    axes = {a["id"] for a in curriculum["axes"]}
    lesson_ids = set()
    for axis in curriculum["axes"]:
        for r in axis["requires"]:
            if r not in axes:
                err(axis["id"], f"requiere un eje inexistente {r}")
        for unit in axis["units"]:
            for lesson in unit["lessons"]:
                where = lesson["id"]
                if lesson["id"] in lesson_ids:
                    err(where, "id repetido")
                lesson_ids.add(lesson["id"])
                for d in lesson["diagramIds"] + [b["diagramId"] for b in lesson["blocks"] if b["kind"] == "diagram"]:
                    if d not in diagrams:
                        err(where, f"diagrama inexistente {d}")
                refs = lesson["references"] + [b["source"] for b in lesson["blocks"] if b.get("source")]
                for ref in refs:
                    if ref["book"] not in books:
                        err(where, f"libro inexistente en books.json: {ref['book']}")
                    elif not ref["pdf"]:
                        warnings.append(f"{where}: sin página de PDF para {ref['book']}:{ref['pages']}")
    for d in curriculum["diagrams"]:
        for ref in d["references"] + [n["ref"] for n in d.get("nodes", []) if n.get("ref")]:
            if ref and ref["book"] not in books:
                err(d["id"], f"libro inexistente {ref['book']}")


# ---- Fuente china ----------------------------------------------------------------------------------------------

def build_font(text):
    if not NOTO.exists():
        warnings.append(f"Sin {NOTO}: no se regenera la fuente china.")
        return
    try:
        from fontTools.ttLib import TTFont
    except ImportError:
        warnings.append("Sin fonttools (python3 -m pip install fonttools): no se regenera la fuente china.")
        return
    latin = set()
    for name in ("Outfit-Regular.ttf", "CormorantGaramond-SemiBold.ttf"):
        font = TTFont(FONTS / name)
        latin |= set(font.getBestCmap().keys()) if name.startswith("Outfit") else set()
    chars = sorted({c for c in text if ord(c) > 0x7F and ord(c) not in latin})
    extra = "阴阳五行气血精神津液脏腑经络穴中医针灸推拿气功"  # siempre disponibles
    chars = sorted(set(chars) | set(extra))
    unicodes = ",".join(f"U+{ord(c):04X}" for c in chars)
    out = FONTS / "NotoSansSC-Subset.otf"
    subprocess.run([sys.executable, "-m", "fontTools.subset", str(NOTO), f"--unicodes={unicodes}",
                    f"--output-file={out}", "--layout-features=*", "--no-hinting"], check=True)
    print(f"Fuente: {len(chars)} caracteres -> {out.name} ({out.stat().st_size // 1024} KB)")


# ---- Principal -------------------------------------------------------------------------------------------------

def main():
    global PAGES
    PAGES = Pages()
    axes = [parse_axis_file(p) for p in sorted((SRC / "axes").glob("*.md"))]
    axes = [a for a in axes if a]
    books = json.loads((SRC / "books.json").read_text(encoding="utf-8"))
    curriculum = {
        "axes": axes,
        "diagrams": load_diagrams(),
        "books": books,
        "glossary": load_glossary(axes),
    }
    validate(curriculum)
    for w in warnings:
        print("aviso:", w)
    if errors:
        for e in errors:
            print("ERROR:", e)
        sys.exit(f"{len(errors)} errores: no se escribió el contenido.")

    OUT.parent.mkdir(parents=True, exist_ok=True)
    text = json.dumps(curriculum, ensure_ascii=False, indent=1)
    OUT.write_text(text, encoding="utf-8")
    lessons = sum(len(u["lessons"]) for a in axes for u in a["units"])
    questions = sum(len(l["quiz"]) for a in axes for u in a["units"] for l in u["lessons"])
    print(f"Contenido: {len(axes)} ejes, {lessons} lecciones, {questions} preguntas, "
          f"{len(curriculum['diagrams'])} diagramas, {len(curriculum['glossary'])} términos -> {OUT.relative_to(ROOT)}")
    build_font(text)


if __name__ == "__main__":
    main()
