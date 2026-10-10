# -*- coding: utf-8 -*-
# Warms the pale, neutral tones of a picture: the soaked linen or cotton of a dress, where the skin under it should
# read faintly through (Gabriel, 10/10/2026: his hand retouch of a bathing scene made the wet cotton believable, and
# the model's own rendering stayed grey). A colour grade only: it changes no shape and adds nothing to the picture.
#
# Usage: python Tools/TaleImages/warm.py <in.jpg> <out.jpg> [--strength 0.5] [--box x0,y0,x1,y1]
#   --strength  0 to 1 (default 0.5): how far pale tones move toward a warm skin tone.
#   --box       the area to warm, as fractions of the width and height (default: the whole picture). A box keeps the
#               mist and the waterfall, which are pale too, out of it. The edge of the box is feathered.
import argparse

import numpy as np
from PIL import Image, ImageFilter

WARM = np.array([1.14, 0.95, 0.86])   # the tint the cloth's mid tones move toward: a light skin warmth, not orange


def warm(img, strength=0.5, box=None):
    rgb = np.asarray(img.convert("RGB"), dtype=np.float32) / 255.0
    mx, mn = rgb.max(axis=2), rgb.min(axis=2)
    lum = rgb.mean(axis=2)
    sat = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-6), 0)
    # Fairly neutral MID tones of the cloth: where wet cotton clings, the skin under it reads in the half-shadows, while
    # the white highlights of the folds stay cloth (as Gabriel's hand retouch did). Dark shadows, saturated greens,
    # blacks and the brightest whites are left alone.
    mid = np.clip(1 - np.abs(lum - 0.52) / 0.30, 0, 1)
    pale = mid * np.clip(1 - sat / 0.35, 0, 1)

    if box:
        h, w = lum.shape
        x0, y0, x1, y1 = box
        mask = Image.new("L", (w, h), 0)
        inner = Image.new("L", (int((x1 - x0) * w), int((y1 - y0) * h)), 255)
        mask.paste(inner, (int(x0 * w), int(y0 * h)))
        mask = mask.filter(ImageFilter.GaussianBlur(radius=max(w, h) * 0.03))
        pale = pale * (np.asarray(mask, dtype=np.float32) / 255.0)

    weight = (pale * strength)[..., None]
    out = rgb * (1 - weight) + np.clip(rgb * WARM, 0, 1) * weight
    return Image.fromarray((np.clip(out, 0, 1) * 255).astype(np.uint8))


def main():
    p = argparse.ArgumentParser()
    p.add_argument("src")
    p.add_argument("dst")
    p.add_argument("--strength", type=float, default=0.5)
    p.add_argument("--box", type=lambda s: tuple(float(v) for v in s.split(",")))
    a = p.parse_args()
    warm(Image.open(a.src), a.strength, a.box).save(a.dst, "JPEG", quality=90)
    print("warmed", a.dst)


if __name__ == "__main__":
    main()
