===== MESSAGE 1 =====
# 🖼️ Tutorial: your own portraits in the chat

**What is it for?**
In the chat window, each line shows the speaker's 3D portrait from the game. You can replace it with **any picture you like**: a painting, a drawing, an AI-made portrait, for any character **and for your own**.

The same picture is used wherever Calradia Remembers shows that character: the **chat**, the **war council**, the **letters**, and the **Balance of Power** screen.

**Where do the pictures go?**
`Modules/CalradiaRemembers/ModuleData/ChatPortraits/`
The folder is created the first time you load a game, with a short `README.txt` inside.

**How to name them**
The file name says who it is. `.png`, `.jpg` or `.jpeg`:
- `player.png`: **your own** character, whatever their name.
- `lord_1_14.png`: a character by their **game id**. The safest: it never changes, even if the name does or the game is in another language.
- `rhagaea.png`: a character by their **name**, in lowercase, spaces as underscores (`derthert.png`, `caladog.png`). Simpler, but two characters sharing a name share the picture.

===== MESSAGE 2 =====
## Step by step

1. Start a conversation with the character.
2. Open the console (`Alt + ~`) and type `cr.portrait_id` (or `cr.portrait_id Rhagaea` from anywhere). It prints the file name to use, for example:
`lord_1_14.png  (or the friendlier  rhagaea.png)`
and whether a picture is already found for them.
3. Save your picture in `ChatPortraits` under that name.
4. Their next line in the chat shows it. No restart needed.

**Tips**
- **Square** pictures look best, **512 x 512 or larger**.
- A face and shoulders read better than a full figure at that size.
- No picture for a character? Their line keeps the game's 3D portrait. Nothing breaks.

## ⚠️ Good to know
- **Replacing a picture** with a new one **of the same name** shows the old one until you **restart the game** (pictures are loaded once). To see a change right away, give the new file another name and delete the old one.
- Re-extracting the mod over your install leaves `ChatPortraits` alone, but **deleting the mod folder** for a clean reinstall deletes your pictures too. **Keep a copy** elsewhere.
- **From 2.8.8**, you can put them in `ChatPortraits` inside your **Custom folder** instead (`ModuleData/Custom/ChatPortraits`, or the one set in MCM): it is looked in first, and with a Custom folder outside the mod, a clean reinstall no longer touches them (see the Custom folder tutorial).

Show us your gallery! 🛡️
