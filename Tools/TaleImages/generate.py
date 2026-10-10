# -*- coding: utf-8 -*-
# Generates a written tale's pictures through OpenRouter, one consistent look for the whole tale (Gabriel, 10/10/2026:
# "Bannerlord is a world inspired by a blend of Antiquity and the Middle Ages"). Every prompt is prefixed with
# style_calradia.txt (the world, its peoples, what never appears, the look), then the tale's own style line, then the
# scene. The key is read from the OPENROUTER_API_KEY environment variable and never printed or written anywhere.
#
# Usage: python Tools/TaleImages/generate.py <spec.json> <out_folder> [image_file ...]
#   spec.json: {"model": "openai/gpt-5.4-image-2", "tale_style": "...", "images": [{"file": "x.jpg", "scene": "..."}]}
#   With file names after the folder, only those images are made (to redo one); an existing image is never overwritten
#   unless it is named that way. Writes JPG (quality 90, longest side 1536 at most), like the Wolf of the Wood's.
import base64, io, json, os, sys, urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
API = "https://openrouter.ai/api/v1/chat/completions"


def prompt_for(spec, image):
    style = open(os.path.join(HERE, "style_calradia.txt"), encoding="utf-8").read().strip()
    parts = [style]
    if spec.get("tale_style"):
        parts.append("THIS TALE: " + spec["tale_style"].strip())
    parts.append("THE SCENE: " + image["scene"].strip())
    return "\n\n".join(parts)


def generate(model, prompt, key):
    body = json.dumps({
        "model": model,
        "modalities": ["image", "text"],
        "image_config": {"aspect_ratio": "4:3"},
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
    if len(sys.argv) < 3:
        sys.exit("usage: generate.py <spec.json> <out_folder> [image_file ...]")
    key = os.environ.get("OPENROUTER_API_KEY")
    if not key:
        sys.exit("OPENROUTER_API_KEY is not set")
    spec = json.load(open(sys.argv[1], encoding="utf-8"))
    out = sys.argv[2]
    only = set(sys.argv[3:])
    os.makedirs(out, exist_ok=True)
    model = spec.get("model", "openai/gpt-5.4-image-2")

    for image in spec["images"]:
        path = os.path.join(out, image["file"])
        if only and image["file"] not in only:
            continue
        if os.path.exists(path) and image["file"] not in only:
            print("kept   ", image["file"])
            continue
        raw, usage = generate(model, prompt_for(spec, image), key)
        size = save_jpg(raw, path)
        print("made   ", image["file"], "%dx%d" % size, "| usage:", json.dumps(usage))


if __name__ == "__main__":
    main()
