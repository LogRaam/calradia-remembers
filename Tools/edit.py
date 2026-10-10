# -*- coding: utf-8 -*-
# Exact-text replacement across files, keeping each file's byte-order mark and line endings (many files here are
# UTF-8 with a BOM and CRLF, which a naive rewrite silently changes). Two phases: every replacement in every file is
# checked first, and nothing is written unless all of them match, so a failed edit never leaves half the files done.
#
# As a module:   from edit import ed, edit_all
#                ed("path/File.cs", [(old, new), ...])                 one file
#                edit_all({"a.cs": [(old, new)], "b.md": [(old, new)]}) several, all or nothing
# From a shell:  python Tools/edit.py spec.json
#                spec.json: {"files": {"path": [{"old": "...", "new": "...", "all": false}, ...]}}
#                (a JSON spec keeps backslashes and quotes out of shell and Python string escaping)
# Each "old" must occur exactly once in its file, unless "all": true (then at least once, every occurrence replaced).
# Text is matched with LF line endings whatever the file uses, so write old/new with plain "\n".
import json, os, sys

BOM = b"\xef\xbb\xbf"


def _load(path):
    raw = open(path, "rb").read()
    bom = raw.startswith(BOM)
    text = raw[len(BOM):].decode("utf-8") if bom else raw.decode("utf-8")
    crlf = "\r\n" in text
    return text.replace("\r\n", "\n"), bom, crlf


def _apply(path, text, pairs):
    for pair in pairs:
        old, new, every = (pair[0], pair[1], False) if isinstance(pair, (tuple, list)) else (pair["old"], pair["new"], pair.get("all", False))
        n = text.count(old)
        if n == 0 or (n > 1 and not every):
            raise ValueError("%s: expected %s of %r, found %d" % (path, "at least one" if every else "exactly one", old[:80], n))
        text = text.replace(old, new) if every else text.replace(old, new, 1)
    return text


def edit_all(spec):
    """spec: {path: [(old, new) or {"old","new","all"}]}. Checks everything, then writes everything."""
    staged = []
    for path, pairs in spec.items():
        text, bom, crlf = _load(path)
        staged.append((path, _apply(path, text, pairs), bom, crlf))
    for path, text, bom, crlf in staged:
        if crlf:
            text = text.replace("\n", "\r\n")
        open(path, "wb").write((BOM if bom else b"") + text.encode("utf-8"))
        print("edited", path)


def ed(path, pairs):
    edit_all({path: pairs})


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit("usage: edit.py spec.json")
    spec = json.load(open(sys.argv[1], encoding="utf-8"))["files"]
    try:
        edit_all(spec)
    except ValueError as e:
        sys.exit("nothing written: %s" % e)
