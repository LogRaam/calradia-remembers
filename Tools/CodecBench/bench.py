# Code written by Gabriel Mailhot, 10/11/2026.
# Compares codecs on the real JSON of CR's compressed stores, as a save writes it (cr.dump_stores, then save).
#
# Gabriel (10/11/2026): "est-ce qu'on aurait un gain si on utilisait une compression LZW, ZIP, PPM ou autre de nos
# donnees avant de les placer dans une sauvegarde?" CR saves GZip (.NET GZipStream, CompressionLevel.Optimal, which is
# zlib level 6 on .NET Framework) then Base64 pieces (SaveBlobCodec). This measures the alternatives on the same input:
# size, ratio against the shipped GZip, and time to compress and to read back, on this machine.
#
# Usage: python Tools/CodecBench/bench.py [folder]   (default: the Logs/cr_stores folder cr.dump_stores writes)
# Needs: pip install zstandard pyppmd brotli (each codec is skipped, and said so, when its module is missing).

import lzma
import bz2
import os
import sys
import time
import zlib

try:
    import zstandard
except ImportError:
    zstandard = None
try:
    import pyppmd
except ImportError:
    pyppmd = None
try:
    import brotli
except ImportError:
    brotli = None


def gzip_like(level):
    def c(b):
        co = zlib.compressobj(level, zlib.DEFLATED, 31)  # 31 = gzip wrapper, as GZipStream writes
        return co.compress(b) + co.flush()
    return c, lambda b: zlib.decompress(b, 47)


def codecs():
    out = [("gzip-6 (shipped)",) + gzip_like(6), ("gzip-9",) + gzip_like(9),
           ("bzip2-9", lambda b: bz2.compress(b, 9), bz2.decompress),
           ("xz-6", lambda b: lzma.compress(b, preset=6), lzma.decompress),
           ("xz-9e", lambda b: lzma.compress(b, preset=9 | lzma.PRESET_EXTREME), lzma.decompress)]
    if zstandard:
        for lvl, longw in ((3, False), (19, False), (22, True)):
            params = zstandard.ZstdCompressionParameters.from_level(lvl, enable_ldm=longw, window_log=27 if longw else 0)
            cc = zstandard.ZstdCompressor(compression_params=params)
            dd = zstandard.ZstdDecompressor(max_window_size=1 << 27)
            out.append((f"zstd-{lvl}{' long' if longw else ''}", cc.compress, dd.decompress))
    else:
        print("zstd: module 'zstandard' missing, skipped")
    if brotli:
        out.append(("brotli-11 w24", lambda b: brotli.compress(b, quality=11, lgwin=24), brotli.decompress))
    else:
        print("brotli: module missing, skipped")
    if pyppmd:
        for order, mem in ((6, 64), (8, 256), (16, 256)):
            out.append((f"ppmd-H o{order} {mem}MB",
                        (lambda o, m: lambda b: pyppmd.compress(b, max_order=o, mem_size=m << 20))(order, mem),
                        (lambda o, m: lambda b: pyppmd.decompress(b, max_order=o, mem_size=m << 20))(order, mem)))
        # Variant I (PPMd8), the one ZIP and the usual .NET libraries implement; the reader is told the length.
        for order, mem in ((6, 64), (8, 256), (16, 256)):
            def enc(b, o=order, m=mem):
                e = pyppmd.Ppmd8Encoder(o, m << 20, pyppmd.PPMD8_RESTORE_METHOD_RESTART)
                return len(b).to_bytes(4, "little") + e.encode(b) + e.flush()

            def dec(b, o=order, m=mem):
                d = pyppmd.Ppmd8Decoder(o, m << 20, pyppmd.PPMD8_RESTORE_METHOD_RESTART)
                return d.decode(b[4:], int.from_bytes(b[:4], "little"))

            out.append((f"ppmd-I o{order} {mem}MB", enc, dec))
    else:
        print("ppmd: module 'pyppmd' missing, skipped")
    return out


def timed(f, b):
    t = time.perf_counter()
    r = f(b)
    return r, (time.perf_counter() - t) * 1000


def documents():
    # The real Documents folder, as the game resolves it (it may be redirected, e.g. to OneDrive on another drive).
    try:
        import ctypes
        buf = ctypes.create_unicode_buffer(260)
        ctypes.windll.shell32.SHGetFolderPathW(None, 5, None, 0, buf)  # CSIDL_PERSONAL
        if buf.value:
            return buf.value
    except Exception:
        pass
    return os.path.join(os.path.expanduser("~"), "Documents")


def main():
    folder = sys.argv[1] if len(sys.argv) > 1 else os.path.join(documents(), "Mount and Blade II Bannerlord", "Logs", "cr_stores")
    files = sorted(f for f in os.listdir(folder) if f.endswith(".json")) if os.path.isdir(folder) else []
    if not files:
        print(f"No store dumps in {folder}: run cr.dump_stores in game, then save.")
        return 1

    table = codecs()
    totals = {name: [0, 0.0, 0.0] for name, _, _ in table}
    raw_total = 0
    for fname in files:
        data = open(os.path.join(folder, fname), "rb").read()
        if not data:
            continue
        raw_total += len(data)
        print(f"\n{fname}: {len(data):,} bytes raw")
        base = None
        for name, comp, decomp in table:
            packed, ct = timed(comp, data)
            back, dt = timed(decomp, packed)
            assert back == data, f"{name} did not round-trip {fname}"
            base = base or len(packed)
            totals[name][0] += len(packed)
            totals[name][1] += ct
            totals[name][2] += dt
            print(f"  {name:20} {len(packed):>12,}  {len(data) / len(packed):6.1f}x  {100.0 * len(packed) / base:6.1f}% of gzip-6  "
                  f"compress {ct:8.0f} ms  read {dt:7.0f} ms")

    print(f"\nALL STORES: {raw_total:,} bytes raw")
    base = totals[table[0][0]][0]
    for name, (size, ct, dt) in totals.items():
        print(f"  {name:20} {size:>12,}  {raw_total / max(size, 1):6.1f}x  {100.0 * size / max(base, 1):6.1f}% of gzip-6  "
              f"compress {ct:8.0f} ms  read {dt:7.0f} ms  in the save ~{size * 4 // 3:,} (Base64)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
