# Code written by Gabriel Mailhot, 19/10/2026.
# Moves files of CalradiaRemembers.Logic's root namespace into a sub-namespace, CalradiaRemembers.Logic.<Sub>, and
# adds the `using` every user needs. Gabriel 19/10/2026: split the 420 flat files of Logic by theme, the way
# Logic.Duels and Logic.Grudges already are.
#
# THE RULE IT ENFORCES: a move must not add a pair of namespaces that use each other (RatchetTests pins their count,
# docs/maps/dependencies.md lists them). Two checks, both before anything is touched:
#   1. No file LEFT in the root of Logic may name a moved type. The new namespace uses the root (it sits inside it and
#      reads its foundation types); a root file naming it back would make the pair Logic <-> Logic.<Sub>.
#   2. For every other namespace that names a moved type: the moved files must not name one of ITS types, or the move
#      makes the pair <that namespace> <-> Logic.<Sub>.
# Names are matched as words on the code and the crefs of doc comments (a cref needs the using too); the prose of
# comments and string literals is ignored. Check 2 looks only at the namespaces Logic can see (its own and the SDK's);
# a name declared in two namespaces is ambiguous and skipped there.
#
# Usage, from the mod repo root:
#   python Tools/Move-LogicTypes.py <Sub> <file stem or Prefix*> [!stem] ...   dry run; !stem keeps one out: what would move, and why not
#   python Tools/Move-LogicTypes.py <Sub> <file stem or Prefix*> ... --apply   git mv, namespace, usings
# Then build, both suites, and Tools/Update-Maps.ps1.

import fnmatch
import os
import re
import subprocess
import sys

LOGIC = "CalradiaRemembers.Logic"
ROOTS = ["CalradiaRemembers", "CalradiaRemembers.Logic", "CalradiaRemembers.Tests",
         os.path.join("..", "LLM Roleplay Service", "NpcMemoryService.Core")]
DECL = re.compile(r"\b(?:class|struct|interface|enum|record|delegate\s+[\w<>\[\],. ]+?)\s+([A-Z]\w*)")
NAMESPACE = re.compile(r"^\s*namespace\s+([\w.]+)", re.M)
USING = re.compile(r"^[ \t]*using\s+(?:static\s+)?([\w.]+)\s*;[ \t]*\r?$", re.M)


def read(path):
    raw = open(path, "rb").read()
    bom = raw.startswith(b"\xef\xbb\xbf")
    return raw[3:].decode("utf-8") if bom else raw.decode("utf-8"), bom


def write(path, text, bom):
    open(path, "wb").write((b"\xef\xbb\xbf" if bom else b"") + text.encode("utf-8"))


def code_of(text):
    """The code without comments or string literals, plus the crefs of the doc comments (a cref is a reference)."""
    crefs = " ".join(re.findall(r'cref="([^"]*)"', text))
    text = re.sub(r"//[^\n]*", "", text)
    text = re.sub(r"/\*.*?\*/", " ", text, flags=re.S)
    # Interpolated strings stay: a {Type.Member} hole in $"..." is code.
    text = re.sub(r'(?<![$\w])@"(?:[^"]|"")*"', '""', text)
    text = re.sub(r'(?<![$@\\])"(?:\\.|[^"\\\n])*"', '""', text)
    return text + "\n" + crefs


def sources():
    for root in ROOTS:
        for d, dirs, files in os.walk(root):
            dirs[:] = [x for x in dirs if x not in ("obj", "bin", ".vs")]
            for f in files:
                if f.endswith(".cs"):
                    yield os.path.join(d, f).replace("\\", "/")


def main(argv):
    apply = "--apply" in argv
    args = [a for a in argv if a != "--apply"]
    if len(args) < 2 or not re.fullmatch(r"[A-Z]\w*", args[0]):
        print(__doc__ or "usage: Move-LogicTypes.py <Sub> <stem or Prefix*> ... [--apply]")
        return 2
    sub, patterns = args[0], args[1:]
    target_ns = f"{LOGIC}.{sub}"

    files = {}
    for path in sources():
        text, bom = read(path)
        m = NAMESPACE.search(text)
        files[path] = {"text": text, "bom": bom, "ns": m.group(1) if m else "", "code": code_of(text)}

    root_files = [p for p in files if p.startswith(LOGIC + "/") and p.count("/") == 1]
    stems = {os.path.basename(p)[:-3]: p for p in root_files}
    keep = [pat[1:] for pat in patterns if pat.startswith("!")]
    moving = sorted({stems[s] for pat in patterns if not pat.startswith("!") for s in stems
                     if fnmatch.fnmatchcase(s, pat) and not any(fnmatch.fnmatchcase(s, k) for k in keep)})
    if not moving:
        print("Nothing matches in the root of Logic:", " ".join(patterns))
        return 1
    for p in moving:
        if files[p]["ns"] != LOGIC:
            print(f"REFUSED: {p} declares namespace {files[p]['ns']}, not {LOGIC}.")
            return 1

    names = sorted({n for p in moving for n in DECL.findall(files[p]["code"])})
    word = re.compile(r"(?<![\w.])(" + "|".join(map(re.escape, names)) + r")\b")
    # Fully qualified, or partly (`Logic.X` written inside a CalradiaRemembers namespace): both become the full new name.
    fq = re.compile(r"(?<![\w.])(?:CalradiaRemembers\.)?Logic\.(" + "|".join(map(re.escape, names)) + r")\b")

    # Who declares what, outside the moving files: for check 2.
    declared_in = {}
    for p, f in files.items():
        if p in moving or "/CalradiaRemembers.Tests/" in "/" + p:
            continue
        for n in DECL.findall(f["code"]):
            declared_in.setdefault(n, set()).add(f["ns"])
    owner = {n: next(iter(s)) for n, s in declared_in.items() if len(s) == 1}

    problems = []
    users = {}
    for p, f in files.items():
        # The SDK cannot see Logic: a name it shares with a moved type is its own type.
        if p in moving or p.startswith("../"):
            continue
        hits = sorted(set(word.findall(f["code"])) | set(fq.findall(f["text"])))
        if not hits:
            continue
        users[p] = hits
        if p in root_files:
            problems.append(f"{p} stays in the root of Logic and names {', '.join(hits)}")

    moved_code = "\n".join(files[p]["code"] for p in moving)
    # A member the moved files declare (a private helper method called Grudge, say) is not that namespace's type.
    members = set(re.findall(r"\b(?:public|private|internal|protected|static)\b[^;={}()]*?\s(\w+)\s*[(<]", moved_code))
    used_by_moved = {}
    for n, ns in owner.items():
        # Logic only sees itself and the SDK: a mod type named in a moved file is a coincidence, not a reference.
        if ns in (LOGIC, target_ns, "") or n in names or n in members or not (ns.startswith(LOGIC + ".") or ns.startswith("NpcMemoryService")):
            continue
        if re.search(r"(?<![\w])" + re.escape(n) + r"\b", moved_code):
            used_by_moved.setdefault(ns, []).append(n)
    for p in sorted(users):
        ns = files[p]["ns"]
        if "/CalradiaRemembers.Tests/" not in "/" + p and ns in used_by_moved:
            problems.append(f"{p} ({ns}) names {', '.join(users[p])}, and the moved files name its "
                            f"{', '.join(sorted(used_by_moved[ns])[:5])}")

    print(f"Move to {target_ns}: {len(moving)} file(s), {len(names)} type(s)")
    for p in moving:
        print("  ", os.path.basename(p))
    print(f"Files that will import {target_ns}: {len([p for p in users if files[p]['ns'] != target_ns])}")
    if problems:
        print("REFUSED, the move would add a pair of namespaces that use each other:")
        for line in problems:
            print("  -", line)
        return 1
    if not apply:
        print("Dry run. Add --apply to move.")
        return 0

    os.makedirs(os.path.join(LOGIC, sub), exist_ok=True)
    for p in moving:
        dest = f"{LOGIC}/{sub}/{os.path.basename(p)}"
        subprocess.run(["git", "mv", p, dest], check=True)
        text, bom = read(dest)
        text = re.sub(r"^(\s*namespace\s+)" + re.escape(LOGIC) + r"(?=\s*[;{\r\n])", r"\g<1>" + target_ns, text, count=1, flags=re.M)
        write(dest, text, bom)

    for p in users:
        f = files[p]
        text = fq.sub(target_ns + r".\1", f["text"])
        if f["ns"] != target_ns and not f["ns"].startswith(target_ns + ".") and word.search(code_of(text)):
            text = add_using(text, target_ns)
        if text != f["text"]:
            write(p, text, f["bom"])
            print("  updated", p)
    return 0


def add_using(text, ns):
    """Adds `using ns;` among the file's top usings, in order among the CalradiaRemembers ones."""
    if re.search(r"^[ \t]*using\s+" + re.escape(ns) + r"\s*;", text, re.M):
        return text
    nl = "\r\n" if "\r\n" in text else "\n"
    head_end = NAMESPACE.search(text)
    limit = head_end.start() if head_end else len(text)
    usings = [m for m in USING.finditer(text, 0, limit)]
    line = f"using {ns};"
    if usings:
        ours = [m for m in usings if m.group(1).startswith("CalradiaRemembers") and not m.group(0).lstrip().startswith("using static")]
        after = None
        for m in ours:
            if m.group(1) < ns:
                after = m
        if after is None and ours:
            return text[:ours[0].start()] + line + nl + text[ours[0].start():]
        anchor = after or usings[-1]
        end = text.index("\n", anchor.start()) + 1
        return text[:end] + line + nl + text[end:]
    region = re.search(r"^#region[^\n]*\n", text, re.M)
    if region and region.start() < limit:
        return text[:region.end()] + line + nl + text[region.end():]
    return line + nl + nl + text


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
