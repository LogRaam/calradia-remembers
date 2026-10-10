# -*- coding: utf-8 -*-
# Generates a written tale's pictures through OpenRouter, one consistent look for the whole tale (Gabriel, 10/10/2026:
# "Bannerlord is a world inspired by a blend of Antiquity and the Middle Ages"). Every prompt is prefixed with
# style_calradia.txt (the world, its peoples, what never appears, the look), then the tale's own style line, then the
# scene. The key is read from the OPENROUTER_API_KEY environment variable and never printed or written anywhere.
#
# Usage: python Tools/TaleImages/generate.py <spec.json> <out_folder> [image_file ...]
#   spec.json: {"model": "openai/gpt-5.4-image-2", "tale_style": "...", "images": [{"file": "x.jpg", "scene": "..."}]}
#   An image (or the whole spec) may name "provider": "xai" and a Grok Imagine "model" (Gabriel, 10/10/2026: the fallback
#   when the main model refuses a scene). xAI reads XAI_API_KEY; the same style block and scene go to both providers.
#   --warm <0..1> [--warm-box x0,y0,x1,y1] warms the mid tones of pale cloth after generation (warm.py), so soaked
#   linen reads as linen over skin; a colour grade only.
#   "aspect" on an image or the spec (or --aspect for the run) frames it: "4:3" by default, "2:3" a standing figure,
#   "16:9" a wide view; CR draws any shape whole (Gabriel, 10/10/2026: "que CR puisse supporter differents ratios").
#   --provider xai|openrouter and --model <id> override the spec for the whole run (Gabriel, 10/10/2026: the main model is
#   chosen per tale, OpenAI at about 0.18 USD an image or Grok imagine-image-2.0 at about 0.04).
#   With file names after the folder, only those images are made (to redo one); an existing image is never overwritten
#   unless it is named that way. Writes JPG (quality 90, longest side 1536 at most), like the Wolf of the Wood's.
import base64, io, json, os, sys, urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
API = "https://openrouter.ai/api/v1/chat/completions"
XAI_API = "https://api.x.ai/v1/images/generations"


def generate_xai(model, prompt, key, aspect=None):
    payload = {"model": model, "prompt": prompt, "n": 1, "response_format": "b64_json"}
    if aspect:
        payload["aspect_ratio"] = aspect
    body = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(XAI_API, data=body, headers={"Authorization": "Bearer " + key, "Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=300) as r:
            data = json.load(r)
    except urllib.error.HTTPError as e:
        raise RuntimeError("xAI refused (%d): %s" % (e.code, e.read().decode("utf-8", "replace")[:400]))
    items = data.get("data") or []
    if not items or not items[0].get("b64_json"):
        raise RuntimeError("no image in the answer: " + json.dumps(data)[:400])
    return base64.b64decode(items[0]["b64_json"]), data.get("usage", {})


def aspect_of(spec, image):
    return str(image.get("aspect", spec.get("aspect", "4:3"))).strip()


def framing(aspect):
    w, h = (float(v) for v in aspect.split(":"))
    shape = "tall portrait" if h > w else "square" if h == w else "wide landscape"
    return "FRAMING: a %s composition, %s." % (shape, aspect)


def prompt_for(spec, image):
    style = open(os.path.join(HERE, "style_calradia.txt"), encoding="utf-8").read().strip()
    parts = [style]
    if spec.get("tale_style"):
        parts.append("THIS TALE: " + spec["tale_style"].strip())
    parts.append("THE SCENE: " + image["scene"].strip())
    parts.append(framing(aspect_of(spec, image)))
    return "\n\n".join(parts)


def generate(model, prompt, key, aspect="4:3"):
    body = json.dumps({
        "model": model,
        "modalities": ["image", "text"],
        "image_config": {"aspect_ratio": aspect},
        "messages": [{"role": "user", "content": prompt}],
    }).encode("utf-8")
    req = urllib.request.Request(API, data=body, headers={"Authorization": "Bearer " + key, "Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=300) as r:
        data = json.load(r)
    msg = (data.get("choices") or [{}])[0].get("message", {})
    images = msg.get("images") or []
    if not images:
        raise RuntimeError("no image in the answer: " + json.dumps(data)[:400])
    url = images[0]["image_url"]["url"]
    usage = data.get("usage", {})
    return base64.b64decode(url.split(",", 1)[1]), usage


def save_jpg(raw, path):
    from PIL import Image
    img = Image.open(io.BytesIO(raw)).convert("RGB")
    longest = max(img.size)
    if longest > 1536:
        scale = 1536.0 / longest
        img = img.resize((int(img.width * scale), int(img.height * scale)), Image.LANCZOS)
    img.save(path, "JPEG", quality=90)
    return img.size


def main():
    args = sys.argv[1:]
    overrides = {}
    for flag in ("--provider", "--model", "--warm", "--warm-box", "--aspect"):
        if flag in args:
            i = args.index(flag)
            overrides[flag[2:]] = args[i + 1]
            del args[i:i + 2]
    if len(args) < 2:
        sys.exit("usage: generate.py <spec.json> <out_folder> [--provider xai|openrouter] [--model id] [image_file ...]")
    spec = json.load(open(args[0], encoding="utf-8"))
    warm_strength = float(overrides.pop("warm")) if "warm" in overrides else None
    warm_box = tuple(float(v) for v in overrides.pop("warm-box").split(",")) if "warm-box" in overrides else None
    spec.update(overrides)
    for image in spec["images"]:
        for k in overrides:
            image.pop(k, None)
    out = args[1]
    only = set(args[2:])
    os.makedirs(out, exist_ok=True)
    model = spec.get("model", "openai/gpt-5.4-image-2")

    for image in spec["images"]:
        path = os.path.join(out, image["file"])
        if only and image["file"] not in only:
            continue
        if os.path.exists(path) and image["file"] not in only:
            print("kept   ", image["file"])
            continue
        provider = image.get("provider", spec.get("provider", "openrouter"))
        chosen = image.get("model", model)
        key = os.environ.get("XAI_API_KEY" if provider == "xai" else "OPENROUTER_API_KEY")
        if not key:
            sys.exit(("XAI_API_KEY" if provider == "xai" else "OPENROUTER_API_KEY") + " is not set")
        aspect = aspect_of(spec, image)
        if provider == "xai":
            # Asked of xAI only when named: the 4:3 default ran without it and stays as it was.
            named = "aspect" in image or "aspect" in spec
            raw, usage = generate_xai(chosen, prompt_for(spec, image), key, aspect if named else None)
        else:
            raw, usage = generate(chosen, prompt_for(spec, image), key, aspect)
        size = save_jpg(raw, path)
        if warm_strength:
            from warm import warm
            from PIL import Image
            warm(Image.open(path), warm_strength, warm_box).save(path, "JPEG", quality=90)
        print("made   ", image["file"], "%dx%d" % size, "| usage:", json.dumps(usage))


if __name__ == "__main__":
    main()
