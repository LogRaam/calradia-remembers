# -*- coding: utf-8 -*-
# The Nexus artifact is a .rar, not our zip (Gabriel used to compress the module folder by hand, and a hand rar once
# carried the wrong version and ChatPortraits). Builds it FROM the verified zip and proves it holds the same thing:
# Usage: python Tools/Release/makerar.py <version>   (after makezip.py and verifyzip.py)
#   1. unzips CalradiaRemembers-v<version>.zip into a temp folder,
#   2. rars that folder into CalradiaRemembers-v<version>.rar at the repo root (Rar.exe a -r -m5),
#   3. extracts the rar into a second temp folder and compares the two trees file by file,
#   4. compares every dll in the rar with the DEPLOYED one, byte for byte,
#   5. prints the rar's SHA-256.
# Exit code 0 when everything matches, 1 otherwise. An existing rar is never overwritten without --force: the rar of a
# version already uploaded is the record of what players downloaded (a rebuild of the same zip gives other bytes).
import filecmp, hashlib, os, shutil, subprocess, sys, tempfile, zipfile

if len(sys.argv) < 2:
    sys.exit("usage: makerar.py <version>")
VERSION = sys.argv[1].lstrip("v")
BASE = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
ZIP = os.path.join(BASE, "CalradiaRemembers-v%s.zip" % VERSION)
RAR = os.path.join(BASE, "CalradiaRemembers-v%s.rar" % VERSION)
RAR_EXE = r"C:\Program Files\WinRAR\Rar.exe"
UNRAR_EXE = r"C:\Program Files\WinRAR\UnRAR.exe"
DEPLOYED_BIN = r"C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\CalradiaRemembers\bin\Win64_Shipping_Client"

if os.path.exists(RAR) and "--force" not in sys.argv:
    sys.exit("%s already exists: if that version is published, open the next number instead; --force to rebuild it" % RAR)
if not os.path.exists(ZIP):
    sys.exit("no archive at %s (run makezip.py %s first)" % (ZIP, VERSION))

work = tempfile.mkdtemp(prefix="cr_rar_%s_" % VERSION)
packed, unpacked = os.path.join(work, "packed"), os.path.join(work, "unpacked")
os.makedirs(packed)
os.makedirs(unpacked)
ok = True
try:
    zipfile.ZipFile(ZIP).extractall(packed)
    if os.path.exists(RAR):
        os.remove(RAR)  # only reached with --force
    subprocess.run([RAR_EXE, "a", "-r", "-m5", "-idq", RAR, "CalradiaRemembers"], cwd=packed, check=True)
    subprocess.run([UNRAR_EXE, "x", "-idq", RAR], cwd=unpacked, check=True)

    def compare(a, b, rel=""):
        global ok
        d = filecmp.dircmp(a, b)
        for name in d.left_only + d.right_only:
            ok = False
            print("  *** only on one side:", os.path.join(rel, name))
        _, mismatch, errors = filecmp.cmpfiles(a, b, d.common_files, shallow=False)
        for name in mismatch + errors:
            ok = False
            print("  *** differs:", os.path.join(rel, name))
        for sub in d.common_dirs:
            compare(os.path.join(a, sub), os.path.join(b, sub), os.path.join(rel, sub))

    print("=== the rar holds exactly the zip ===")
    compare(os.path.join(packed, "CalradiaRemembers"), os.path.join(unpacked, "CalradiaRemembers"))
    print("  same" if ok else "  *** NOT THE SAME ***")

    print("=== the rar's dlls are the deployed ones ===")
    rar_bin = os.path.join(unpacked, "CalradiaRemembers", "bin", "Win64_Shipping_Client")
    for name in sorted(n for n in os.listdir(DEPLOYED_BIN) if n.lower().endswith(".dll")):
        mine = os.path.join(rar_bin, name)
        same = os.path.exists(mine) and filecmp.cmp(os.path.join(DEPLOYED_BIN, name), mine, shallow=False)
        ok &= same
        if not same:
            print("  *** differs from the deployed one:", name)
    print("  identical" if ok else "  *** MISMATCH ***")

    sha = hashlib.sha256(open(RAR, "rb").read()).hexdigest()
    print()
    print("rar:    %s (%.1f MB)" % (RAR, os.path.getsize(RAR) / 1024.0 / 1024.0))
    print("SHA-256: %s" % sha)
    print("VERDICT:", "rar ready to upload" if ok else "*** DO NOT SHIP ***")
finally:
    shutil.rmtree(work, ignore_errors=True)

sys.exit(0 if ok else 1)
