===== MESSAGE 1 =====
# 🎬 Tutorial: make a .crvid video with the CRVID Player

**What is a .crvid?**
Calradia Remembers has its own little video format, `.crvid`, made for the **pictures of written tales**. Where a tale shows a picture beside the chat, it can show a **moving picture** instead: a slow pan over a moonlit wood, torchlight in a hall, a figure turning away...

The game's own video player cannot play inside a conversation, so CR has its own: a `.crvid` is simply the video's frames as JPEG pictures, in a zip, with a small description file. No sound.

In game, the video:
- plays **once** where the picture would be, then rests on its **first frame** (which is also its picture);
- has **PLAY**, **STOP** and **LOOP** buttons in its bottom-right corner;
- grows or shrinks with a click, like any tale picture;
- on the **Classic** chat skin, shows its first frame, still.

**The tool:** `CrvidPlayer.html`, attached to this post (from 2.8.8 it also ships with the mod: `Modules/CalradiaRemembers/Docs/CrvidPlayer.html`). Double-click it: it opens in your browser, works offline and sends nothing anywhere. It **converts** a video to `.crvid`, **plays** `.crvid` files, and **shrinks** them. Use Chrome or Edge.

===== MESSAGE 2 =====
## Step 1: prepare your clip

Any `.mp4`, `.webm` or `.mov` your browser can play. A few tips:
- **Short.** A few seconds. Every frame is a full picture: **6 seconds at full size weigh several megabytes**, often tens.
- **Any shape**: wide, tall or square. The tool fits it within **768 x 1152** pixels (the largest the game plays) and never enlarges a smaller one.
- **No sound**: it is dropped.
- **Make the first frame count**: it is the picture shown before and after the video plays, and the only one on the Classic skin.
- **A slow zoom over a still picture?** Do not make it with ffmpeg's `zoompan` filter as is: it moves by whole pixels and the picture **trembles**. Use a video editor that zooms smoothly.

## Step 2: convert it

Drop the video on the player (or **Open videos**). It reads every frame and saves each one as a JPEG, at 30 frames a second. The bar at the bottom shows its progress, frame by frame. When it is done, the browser **downloads** `yourclip.crvid` and the video appears in the list, ready to watch.

===== MESSAGE 3 =====
## The SSIM setting: quality against size

Next to the buttons, **SSIM** (default **0.960**) decides how much quality each frame may lose to save space. SSIM measures how alike two pictures look, from 0 to 1; 1 is identical. For every frame, the tool picks the **smallest** JPEG that still looks at least that much like the original.
- **Higher** (0.970, 0.980): closer to the source, heavier file.
- **Lower** (0.950, 0.940): lighter file, more blur and blocks in fine detail.
0.960 is a good balance. Set it **before** dropping the video.

When a conversion ends, the bar says the size before and after, and the mean and lowest SSIM reached.

**Already have a .crvid that is too heavy?** Open it, set a lower SSIM, click **Recompress**. It saves a copy (e.g. `clip.ssim950.crvid`) and keeps the original. It saves less than converting from the source video, since the frames were already JPEGs: start from the source when you have it.

## Watch before you use it

Click a video in the list. **Space** plays and pauses, **Left / Right** step one frame, **Up / Down** change video, **L** loops, and you can slow it down (x0.25, x0.5). **Open a folder** lists every `.crvid` in it. Look for trembling, flicker or blocky skies.

===== MESSAGE 4 =====
## Step 3: put it in a tale

A `.crvid` goes wherever a tale takes a picture. Put it in the **tale's own folder**, next to its JSON:
```
ModuleData/Custom/Quests/my_tale/my_tale.json
ModuleData/Custom/Quests/my_tale/moonlit_wood.crvid
```
In the **Tale Editor**, use the file button beside a stage's **Image** field and pick the `.crvid`. Or write it by hand: `"image": "moonlit_wood.crvid"`. It works the same on a scene's findings, hazards and people met, and in an `image_pool` (a new picture or video each time the tale is played).

Then `cr.story_check` in the console tells you if a file is missing.

## Test it in game without a tale

Open a conversation with anyone, then in the console (`Alt + ~`):
`cr.video show C:\Videos\moonlit_wood.crvid`
It shows the video exactly as a tale would. Use the **full path**, and keep it **without spaces** (the console splits on them). `cr.video replay` plays it again, `cr.video stop` stops it.

Made something beautiful? Post it here! 🛡️
