===== MESSAGE 1 =====
# 🗂️ Tutorial: make Calradia Remembers your own with the Custom folder

**What is it for?**
Most of what the characters are told comes from **plain text files** you can edit: how the world is described, who your character is, how lords, merchants or companions behave, the traditions of each culture, the tales each people tells around the fire, the writing styles...

These files live in `Modules/CalradiaRemembers/ModuleData/`. **If you edit them there, the next update of the mod overwrites your changes.**

The **Custom** folder fixes that:
`Modules/CalradiaRemembers/ModuleData/Custom/`
The mod **never ships files in it and never writes into it**. For every file, it looks in Custom **first**, and uses the shipped one only when Custom has none of the same name.

**The golden rule: copy, don't move.** Copy the file you want to change into Custom, **with the same name and in the same sub-folder**, and edit your copy. Keep in Custom only what you actually changed: everything else keeps the mod's defaults.

===== MESSAGE 2 =====
## What you can put in it

**The prompts** (copy them from `ModuleData/`):
- `world.txt`: the world as characters see it.
- `player_description.txt`: who your character is.
- `behavior_guidelines.txt`, plus one per kind of character: `_companion`, `_notable`, `_wanderer`, `_gangleader`. You can even add one per trade (`behavior_guidelines_merchant.txt`, `_artisan`, `_ruralnotable`, `_headman`, `_preacher`): start from a copy of `_notable`.
- `post_history_instructions.txt`: the last instructions, read after the conversation.
- `setting.json`, `cultural_traditions.json`, `jealousy_cultures.json` (keep the JSON valid; cultures are keyed by their **displayed name**, so an overhaul mod's culture, e.g. `"Nord"`, gets its own entry).
- `races.json`: for overhaul mods with non-human peoples (elves, dwarves, orcs...). Keyed by the game's race **name**, which `cr.appearance` prints (`cr.appearance who <name>` for someone else). An entry names the people (`"noun": "elf"` makes "an elf woman"), may add one line of its own, and may leave unsaid what the face reads wrongly for that race (`"omit": ["skin"]`). The shipped file has examples. A race with no entry reads as before.
- `women_in_ranks.json`: the share of women (0 to 100) among each culture's soldiers in battle, keyed by the game's culture **id** (`sturgia`, `nord`, `empire`...); a `realms` entry by kingdom id (`empire_s`) wins for that realm's armies. MCM *Women In The Ranks* turns it off.
- `character_overrides.json`: hand-written personalities for chosen characters (needs MCM *Use custom traits*; reload with `cr.reload_lore`).

**In sub-folders** (same name as in `ModuleData/`):
- `stations/merchant.txt`, `stations/lord.txt`...: who each social station is, and how they treat you at each rung of your standing, from a nobody to a sovereign.
- `folklore/battania.txt`...: the tales each people tells around the fire. A file named after another mod's culture id gives that people tales of its own.
- `Styles/MyVoice.txt`: a writing style of your own. It shows up in the style picker; with the same name as a shipped style, yours wins.

Every file explains itself in its first lines (the ones starting with `#`).

===== MESSAGE 3 =====
**Your creations** (see the other tutorials):
- `Quests/`: your written tales (Tale Editor).
- `scenarios/`: your scenario packs (Scenario Composer).
- `ChatPortraits/`: your chat portraits (from 2.8.8; see the portraits tutorial).
- `Prompts/`: your own prompt profiles, for those who want to rearrange what characters read. Pick one in MCM, **Prompt Profile**; `cr.prompt_check` shows what each profile changes. **Stock** always puts everything back.

## Template variables

In your styles and prompts you can write `{{tokens}}`, filled in for the character you are talking to, at each reply:
`{{char}}` the character's name · `{{char_gender}}` · `{{user}}` your character's name · `{{user_gender}}` · `{{user_clan}}` · `{{faction}}` your kingdom (or clan) · `{{location}}` where you are · `{{spouse}}` · `{{father}}` · `{{mother}}`
Case and spaces do not matter (`{{ User }}` works). A misspelled token is left as written, so you can spot it.
Example, in `Custom/Styles/Terse.txt`:
> Write {{char}}'s dialogue in short, clipped sentences, and have them address {{user}} by name when the scene turns tense.

===== MESSAGE 4 =====
## Apply your changes without restarting

Open the console (`Alt + ~`):
- `cr.reload_prompts` re-reads the prompts, `setting.json`, the culture files, the stations and the folklore. The next reply uses them.
- **Styles** are read each time they are used: just pick yours again.
- `cr.story_check` reloads the tales. Scenario packs are read when a save is loaded.
- `cr.lore_status` prints the Custom folder in use and the prompt files read from it.

## ⚠️ Two traps

**1. Your copy replaces the whole file, forever.** When the mod improves its own `behavior_guidelines.txt`, you will not get the improvement while your copy is in Custom. `cr.lore_status` warns you when the shipped file is **newer** than your copy: compare the two and carry over what you want, or delete your copy to go back to the mod's.

**2. The Compact prompt does not send `world.txt`.** If you turned on *Compact prompt for small models* in MCM, your edited `world.txt` never reaches one-to-one conversations. `cr.lore_status` says so too.

===== MESSAGE 5 =====
## Keep it outside the mod (recommended)

Re-extracting the mod over your install leaves Custom alone, but **deleting the mod folder** for a clean reinstall takes Custom with it. To be safe, keep your folder **outside** the mod:
1. Create a folder anywhere, e.g. `D:\Bannerlord\CR Custom\`, and move the content of your Custom folder into it (same sub-folders).
2. MCM → Calradia Remembers → Character → **Custom Folder Path**: paste the full path.
3. Load your save, or run `cr.reload_prompts`.
If the path does not exist, the mod quietly falls back to `ModuleData/Custom`: check with `cr.lore_status`, which prints the folder in use. After an update, check that the path is still set in MCM.

Bonus: you can keep **several sets** (a grim Calradia, a courtly one...) in different folders and switch between them by changing the path.

A `README.txt` in `ModuleData/Custom/` sums all this up. Show us your styles and your folklore! 🛡️
