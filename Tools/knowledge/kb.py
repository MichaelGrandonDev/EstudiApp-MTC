#!/usr/bin/env python3
"""Base de conocimiento de EstudiApp MTC: texto página a página de los libros y apuntes, con búsqueda.

Fuentes (por orden de prioridad):
  1. Índice ya construido por Fluxus Meridian (~/Desktop/Fluxus Meridian APP/Tools/library/cache/library.db),
     que incluye el OCR de los libros escaneados (Li Ping, La práctica de Maciocia, Diagnóstico por la lengua…).
  2. Apuntes de la Escuela Neidan en ~/Desktop/Material-Mtc-Neidan (texto con pypdf).

Uso:
  python3 kb.py build
  python3 kb.py search jing prenatal --book liping,maciocia
  python3 kb.py search "ciclo de generación" --phrase
  python3 kb.py show suwen 12
  python3 kb.py books

El texto queda en cache/ (no se versiona ni se incluye en la app: los resúmenes de la app se redactan con
palabras propias y citan libro y página).
"""
import argparse
import json
import re
import sqlite3
import sys
from pathlib import Path

HOME = Path.home()
HERE = Path(__file__).resolve().parent
CACHE = HERE / "cache"
DB = CACHE / "kb.db"
FLUXUS_DB = HOME / "Desktop/Fluxus Meridian APP/Tools/library/cache/library.db"
NEIDAN = HOME / "Desktop/Material-Mtc-Neidan"

# id en la app -> id en el índice de Fluxus. Los cuatro primeros son las fuentes principales.
FLUXUS_BOOKS = {
    "liping": "gran_libro",
    "maciocia": "fundamentos",
    "suwen": "suwen",
    "focks": "focks",
    "practica": "practica",
    "lengua": "lengua",
    "pulso": "pulso_guia",
    "psique": "psique",
    "compendio": "compendio",
    "acupmoxi": "acup_moxi",
}


def connect():
    CACHE.mkdir(exist_ok=True)
    con = sqlite3.connect(DB)
    con.execute("""CREATE TABLE IF NOT EXISTS books(id TEXT PRIMARY KEY, title TEXT, author TEXT, source TEXT,
                   page_count INTEGER)""")
    con.execute("""CREATE VIRTUAL TABLE IF NOT EXISTS pages USING fts5(
                   text, book UNINDEXED, page UNINDEXED, printed UNINDEXED,
                   tokenize = 'unicode61 remove_diacritics 2')""")
    return con


def printed_from_text(text):
    """Escaneos a doble página (Li Ping): los números sueltos en una línea son las páginas impresas ("48-49")."""
    nums = [int(l.strip()) for l in text.split("\n") if re.fullmatch(r"\d{1,3}", l.strip())]
    nums = sorted(set(n for n in nums if 0 < n < 1000))
    if not nums:
        return None
    # Sólo pares consecutivos o un número suelto: evita confundir cifras del texto con folios.
    for a, b in zip(nums, nums[1:]):
        if b == a + 1:
            return f"{a}-{b}"
    return str(nums[-1]) if len(nums) == 1 else None


def build(_args):
    if DB.exists():
        DB.unlink()
    con = connect()
    if FLUXUS_DB.exists():
        src = sqlite3.connect(f"file:{FLUXUS_DB}?mode=ro", uri=True)
        for app_id, fluxus_id in FLUXUS_BOOKS.items():
            meta = src.execute("select title, author, file from books where id=?", (fluxus_id,)).fetchone()
            rows = src.execute("select text, page, printed from pages where book=? order by page", (fluxus_id,)).fetchall()
            if not rows:
                print(f"  {app_id:10s} sin páginas en el índice de Fluxus")
                continue
            title, author, file = meta if meta else (fluxus_id, "", "")
            if not any(pr for _, _, pr in rows):
                rows = [(t, p, printed_from_text(t)) for t, p, _ in rows]
            con.execute("insert into books values(?,?,?,?,?)", (app_id, title, author, file, len(rows)))
            con.executemany("insert into pages(text, book, page, printed) values(?,?,?,?)",
                            [(t, app_id, p, pr) for t, p, pr in rows])
            print(f"  {app_id:10s} {len(rows):5d} pág.  {title} ({author})")
    else:
        print(f"Aviso: no existe {FLUXUS_DB}; sólo se indexan los apuntes Neidan.")

    try:
        import pypdf
    except ImportError:
        sys.exit("Falta pypdf: python3 -m pip install pypdf")
    for pdf in sorted(NEIDAN.glob("*.pdf")):
        book = "neidan_" + re.sub(r"[^a-z0-9]+", "_", pdf.stem.lower()).strip("_")
        reader = pypdf.PdfReader(str(pdf))
        rows = [((page.extract_text() or "").strip(), book, i + 1, str(i + 1)) for i, page in enumerate(reader.pages)]
        con.execute("insert into books values(?,?,?,?,?)", (book, pdf.stem, "Escuela Neidan", str(pdf), len(rows)))
        con.executemany("insert into pages(text, book, page, printed) values(?,?,?,?)", rows)
        print(f"  {book:28s} {len(rows):4d} pág.")
    con.commit()
    print(f"Listo: {DB}")


def fts_query(terms, phrase):
    if phrase:
        return '"' + " ".join(terms).replace('"', "") + '"'
    return " AND ".join('"' + t.replace('"', "") + '"' for t in terms)


def search(args):
    con = connect()
    sql = """select book, page, printed, snippet(pages, 0, '[', ']', ' … ', 24) from pages
             where pages match ?"""
    params = [fts_query(args.terms, args.phrase)]
    if args.book:
        books = args.book.split(",")
        sql += f" and book in ({','.join('?' * len(books))})"
        params += books
    sql += " order by rank limit ?"
    params.append(args.limit)
    for book, page, printed, snip in con.execute(sql, params):
        print(f"{book:12s} pdf {page:4d}  impresa {str(printed or '-'):>5s}  {' '.join(snip.split())}")


def show(args):
    con = connect()
    row = con.execute("select text, printed from pages where book=? and page=?", (args.book, args.page)).fetchone()
    if not row:
        sys.exit("No existe esa página.")
    print(f"--- {args.book} pdf {args.page} (impresa {row[1]}) ---")
    print(row[0])


def books(_args):
    con = connect()
    for row in con.execute("select id, page_count, title, author from books order by id"):
        print(f"{row[0]:28s} {row[1]:5d}  {row[2]} — {row[3]}")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(required=True)
    sub.add_parser("build").set_defaults(func=build)
    s = sub.add_parser("search")
    s.add_argument("terms", nargs="+")
    s.add_argument("--book")
    s.add_argument("--phrase", action="store_true")
    s.add_argument("--limit", type=int, default=12)
    s.set_defaults(func=search)
    sh = sub.add_parser("show")
    sh.add_argument("book")
    sh.add_argument("page", type=int)
    sh.set_defaults(func=show)
    sub.add_parser("books").set_defaults(func=books)
    args = parser.parse_args()
    args.func(args)


if __name__ == "__main__":
    main()
