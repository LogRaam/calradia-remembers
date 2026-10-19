# Code written by Gabriel Mailhot, 19/10/2026.
# Removes the `using CalradiaRemembers...;` lines a file no longer needs: the namespace no longer exists, or the file
# names none of its types. The dependency map reads usings without compiling (docs/maps/dependencies.md), so a dead
# import is a false edge, and a false edge can be a false pair of namespaces using each other: 47 dead imports of
# Diagnostics were removed by hand on 19/10/2026, and the root and Logic splits leave more behind.
#
# Conservative: a namespace that declares an extension method is always kept (its use leaves no type name in the file),
# and only plain `using CalradiaRemembers...;` lines are touched (no alias, no static). Names are matched on the code
# and the crefs of doc comments, as Tools/Move-LogicTypes.py does.
#
# Usage, from the mod repo root: python Tools/Remove-DeadUsings.py [--apply]   (a dry run lists what would go)

import importlib.util
import os
import re
import sys

spec = importlib.util.spec_from_file_location("mlt", os.path.join(os.path.dirname(__file__), "Move-LogicTypes.py"))
mlt = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mlt)

ROOTS = ["CalradiaRemembers", "CalradiaRemembers.Logic", "CalradiaRemembers.Tests"]
USING_OURS = re.compile(r"^[ \t]*using\s+(CalradiaRemembers(?:\.\w+)*)\s*;[ \t]*\r?\n", re.M)
EXTENSION = re.compile(r"\bstatic\b[^;{}=]*\(\s*this\s")


def main(argv):
    apply = "--apply" in argv
    files = {}
    for root in ROOTS:
        for d, dirs, names in os.walk(root):
            dirs[:] = [x for x in dirs if x not in ("obj", "bin", ".vs")]
            for f in names:
                if f.endswith(".cs"):
                    p = os.path.join(d, f).replace("\\", "/")
                    text, bom = mlt.read(p)
                    m = mlt.NAMESPACE.search(text)
                    files[p] = (m.group(1) if m else "", text, bom)

    types = {}
    extensions = set()
    for p, (ns, text, _) in files.items():
        code = mlt.code_of(text)
        types.setdefault(ns, set()).update(mlt.DECL.findall(code))
        if EXTENSION.search(code):
            extensions.add(ns)

    removed = 0
    for p, (ns, text, bom) in sorted(files.items()):
        code = mlt.code_of(text)
        new = text
        for m in USING_OURS.finditer(text):
            target = m.group(1)
            if target in extensions:
                continue
            names = types.get(target, set())
            if names and re.search(r"(?<![\w.])(" + "|".join(map(re.escape, sorted(names))) + r")\b", code):
                continue
            new = new.replace(m.group(0), "", 1)
            removed += 1
            print(f"{p}: using {target}")
        if apply and new != text:
            mlt.write(p, new, bom)

    print(f"{removed} dead using(s)" + ("" if apply else " (dry run; --apply to remove)"))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
