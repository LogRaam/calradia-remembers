# Makes a .crvid (CR's own video: a zip of JPEG frames and a manifest, read by CalradiaRemembers/Video/CrvidDecoder.cs)
# from any video ffmpeg reads (.mp4, .webm, .ivf...). Gabriel (11/10/2026): the game's own video player stops in
# conversations, so CR plays its own frames; authors keep their .mp4 and convert it with this.
#
#   python Tools/Video/make_crvid.py <input video> <output.crvid> [--fps 30] [--max 768x1152] [--quality 3] [--loop]
#
# The frame is fitted inside --max without distortion (even width and height), and never enlarged. 768x1152 is the largest measured in game
# (30 Hz, nothing skipped); the game refuses a frame with more pixels than that. --fps 30 divides 60 and 120 Hz screens
# evenly. --quality is ffmpeg's JPEG scale, 2 (best) to 31; 3 is close to the source. Frames are stored uncompressed in
# the zip: a JPEG does not shrink further, and stored entries read faster.
import argparse, json, os, shutil, subprocess, sys, tempfile, zipfile

FFMPEG_FALLBACK = os.path.expandvars(r"%LOCALAPPDATA%\Microsoft\WinGet\Packages\Gyan.FFmpeg.Essentials_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg-8.1.1-essentials_build\bin\ffmpeg.exe")


def ffmpeg():
    found = shutil.which("ffmpeg")
    if found: return found
    if os.path.exists(FFMPEG_FALLBACK): return FFMPEG_FALLBACK
    sys.exit("ffmpeg was not found: install it (winget install Gyan.FFmpeg.Essentials) or put it on PATH.")


def main():
    p = argparse.ArgumentParser(description="Convert a video to CR's .crvid")
    p.add_argument("input")
    p.add_argument("output")
    p.add_argument("--fps", type=float, default=30)
    p.add_argument("--max", default="768x1152", help="largest WIDTHxHEIGHT the frame is fitted into")
    p.add_argument("--quality", type=int, default=3)
    p.add_argument("--loop", action="store_true", help="loop by default in game")
    a = p.parse_args()
    mw, mh = (int(x) for x in a.max.lower().split("x"))
    if mw * mh > 768 * 1152: sys.exit("--max holds more pixels than the game plays (768x1152).")

    work = tempfile.mkdtemp(prefix="crvid_")
    try:
        # Fitted inside the box, never enlarged (Gabriel, 27/10/2026: a 336x880 .mp4 came out 440x1152, 1.7 times the
        # pixels and the size for no detail; the in-game frame scales the picture anyway).
        vf = f"fps={a.fps},scale=w='min({mw},iw)':h='min({mh},ih)':force_original_aspect_ratio=decrease:force_divisible_by=2"
        subprocess.run([ffmpeg(), "-hide_banner", "-loglevel", "error", "-y", "-i", a.input, "-vf", vf, "-q:v", str(a.quality),
                        os.path.join(work, "%06d.jpg")], check=True)
        frames = sorted(f for f in os.listdir(work) if f.endswith(".jpg"))
        if not frames: sys.exit("ffmpeg made no frame.")
        from PIL import Image
        with Image.open(os.path.join(work, frames[0])) as first: w, h = first.size
        manifest = {"version": 1, "fps": a.fps, "width": w, "height": h, "frames": len(frames), "loop": a.loop}
        with zipfile.ZipFile(a.output, "w", zipfile.ZIP_STORED) as z:
            z.writestr("manifest.json", json.dumps(manifest, indent=2))
            for f in frames: z.write(os.path.join(work, f), "frames/" + f)
        print(f"{a.output}: {len(frames)} frames, {w}x{h}, {a.fps} fps, {os.path.getsize(a.output) // 1024} KB")
    finally:
        shutil.rmtree(work, ignore_errors=True)


if __name__ == "__main__":
    main()
