===== MESSAGE 1 =====
# 🌍 Tutorial: translate Calradia Remembers

**First, the good news: the characters already speak your language.**
What characters say, their letters, their memories and the chronicle are written by the AI, so they need no translation:
- with the game in your language, they answer in it;
- with an English game, they answer in the language **you write in**;
- MCM → Content → **Reply Language**: name a language (`Spanish`, `Türkçe`...) to force every reply into it, whatever you write.
(Texts already written in an older save stay as they were.)

**What a translation file covers today: the mod's MCM page.** Every setting's name, its explanation, the group names, the buttons and (since 2.8.10) the choices inside drop-down lists.
**Not translatable yet:** the encyclopedia texts the mod adds, the mod's own windows (chat, letters, council), and the notices shown during play.

Everything is in `Modules/CalradiaRemembers/ModuleData/Languages/`:
- `EN/`: the English template, with every text and its **id**;
- `README.txt`: the translator's guide, which this post follows.

===== MESSAGE 2 =====
## Step by step (example: Turkish)

1. **Copy** the `EN` folder and name the copy after the language, e.g. `TR`.
2. Open the copy's `language_data.xml`. Replace its whole `<LanguageData ...>` opening line with the one from the game's own file: `Modules/Native/ModuleData/Languages/<your language>/language_data.xml`. Copy it **whole**: `id`, `name`, `subtitle_extension`, `supported_iso` **and** `text_processor` (without it, your language loses its grammar rules in the whole game). The `id` must be **exactly** the game's, accents included (`Türkçe`, `Français`).
Then change the `xml_path` to your folder: `TR/std_CalradiaRemembers_mcm.xml`.
3. Open `std_CalradiaRemembers_mcm.xml`. Set `<tag language="..."/>` to that same name, and translate each `text="..."`. **Never change an `id`.** A text you leave out shows in English.
4. **Save both files as UTF-8.** Notepad++ or VS Code show the encoding at the bottom.
5. **Restart the game**: language files are read at startup only.

===== MESSAGE 3 =====
## ⚠️ The traps (each one silently loads nothing)
- **A bare `"` or `&` inside a text breaks the whole file.** Write `&quot;` and `&amp;` instead.
- **Nothing between the first line (`<?xml ... ?>`) and `<base>`**, not even a comment: the game reads only what comes right after the first line.
- **The wrong `id`** in `language_data.xml` (a missing accent, a translated name): the game never matches your file to its language.
- **Explanations longer than about 250 characters** are cut off in the menu. Keep them short.

## Share it as a small mod
A translation can be **its own mod**: a module with its own `ModuleData/Languages/<language>/` holding your two files, the same ids. The game reads the language files of every loaded module, so players install it next to Calradia Remembers and an update of CR never overwrites it.

When CR adds settings, the English template gains new ids: compare it with your file to see what is new. Missing ones show in English until you add them.

Translating? Say which language here, so two people don't do the same one! 🛡️
