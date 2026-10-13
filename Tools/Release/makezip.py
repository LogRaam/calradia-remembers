# -*- coding: utf-8 -*-
# Builds the release archive from the DEPLOYED module folder, which is the 2.5.8 lesson: zip what the game
# actually loaded, not what the repo says should have been built.
# Usage: python Tools/Release/makezip.py <version>   ->  CalradiaRemembers-v<version>.zip at the repo root.
# The game must be closed and the Release build deployed first (the deployed folder IS what gets zipped).
import os, sys, zipfile

if len(sys.argv) < 2:
    sys.exit("usage: makezip.py <version>")
VERSION = sys.argv[1].lstrip("v")
BASE = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))

SRC = r"C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\CalradiaRemembers"
OUT = os.path.join(BASE, "CalradiaRemembers-v%s.zip" % VERSION)
ROOT = "CalradiaRemembers"

# ChatPortraits is excluded deliberately: the mod recreates that folder itself (2.5.3 onward).
SKIP_DIRS = {"ChatPortraits"}
# The player's own prompt profiles and the record of the MCM pick (the Prompt Composer, 2.7.10) live here when no Custom
# path is set: the developer's own must never ship. Skipped by PATH, since CR's stock texts will ship under
# ModuleData/Prompts.
SKIP_PATHS = {os.path.join("ModuleData", "Custom", "Prompts"),
              # The realm queens are held back (Gabriel, 13/10/2026: "ne livre pas les reines"); their data lives in
              # Extras/RealmQueens. Skipped here too, so a copy left in an older deploy can never ship.
              os.path.join("ModuleData", "QueenPortraits"),
              os.path.join("ModuleData", "Quests", "the_kings_hall")}
SKIP_FILES = {os.path.join("ModuleData", "faces.json")}

if os.path.exists(OUT):
    os.remove(OUT)

count = 0
with zipfile.ZipFile(OUT, "w", zipfile.ZIP_DEFLATED) as z:
    for dirpath, dirnames, filenames in os.walk(SRC):
        rel_dir = os.path.relpath(dirpath, SRC)
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS and os.path.normpath(os.path.join(rel_dir, d)) not in SKIP_PATHS]
        arc_dir = ROOT if rel_dir == "." else ROOT + "/" + rel_dir.replace("\\", "/")
        if rel_dir != ".":
            z.writestr(arc_dir + "/", "")
            count += 1
        for f in sorted(filenames):
            if os.path.normpath(os.path.join(rel_dir, f)) in SKIP_FILES:
                continue
            z.write(os.path.join(dirpath, f), arc_dir + "/" + f)
            count += 1

size = os.path.getsize(OUT)
print("wrote %s" % OUT)
print("entries: %d, size: %.2f MB" % (count, size / 1024.0 / 1024.0))
