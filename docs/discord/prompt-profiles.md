===== MESSAGE 1 =====
# 🧩 Tutorial (advanced): prompt profiles, the Prompt Composer

**What is it for?**
Before each reply, the mod writes the character a long set of instructions: the **prompt**. It is made of about **120 sections**, in a fixed order: who they are, what they know, their memories of you, the format of the reply, and so on.

A **prompt profile** lets you take that apart: **change the order** of the sections, **turn some off**, or **reword** them, for every character or for one kind of character only. It is the mod's "overclocking": for power users and overhaul modders. If you only want a different **voice**, a style is much simpler (see the styles tutorial).

**Stock is always one click away.** MCM → Calradia Remembers → Content → **Prompt Profile**: **Stock** is the mod's own prompt. Pick it and everything is back, no file deleted.

**A few sections are locked**, because the game would break or because they protect you:
- **sealed** (cannot be moved, reworded or turned off): the reply format the game reads, and the two consent rules;
- **required** (cannot be turned off, can be reworded): who is who, the wall against inventing facts, "claims are not proof", the format reminder, and how a captive's escape is decided.
Everything else is yours.

===== MESSAGE 2 =====
## A profile is a folder

In your Custom folder, `ModuleData/Custom/Prompts/<profile name>/`:
```
Prompts/
  my_profile/
    profile.json
    layouts/default.json
    layouts/companion.json
    sections/dialogue_style.txt
```
The folder name is the profile's id, and the profile appears in the **Prompt Profile** list. Everything you do not provide stays as in stock: a profile can be **one reworded line**, or a complete overhaul.

**profile.json**
```json
{ "name": "My profile", "based_on": "2.8.8", "parent": "" }
```
`parent` (optional): another profile's id. Yours starts from it instead of stock, so a small tweak of someone's overhaul need not copy all of it.

**Sections are named by a key** (`identity`, `dialogue_style`, `world_description`...). Every key is listed, in order, with its zone, its lock and what it says, in `Modules/CalradiaRemembers/Docs/PROMPT_SECTIONS.md` (from 2.8.8). To see a section's current text, `cr.prompt_dump Name` writes the exact prompt a character would receive to a file next to the log, without calling the AI.

**A mod that gives characters their own nature** (traits, a temperament) can leave out CR's: `archetype` is the line CR reads from the game's traits ("The Hollow Lord...", under `CHARACTER:`), `personality` the personality line and the order to stay true to it (from 2.8.17). List them under `"off"`; the identity around them stays. With `archetype` off, the romance block also loses what the archetype wrote there: its `Archetype:` line and its two descriptive paragraphs (from 2.8.18); the orientation, the patterns and the bond with you stay.

===== MESSAGE 3 =====
## Layouts: the order, by kind of character

`layouts/<kind>.json`, where the kind is one of: `spouse`, `companion`, `captive_scene`, `gang_leader`, `notable`, `wanderer`, `lord`, `default`. The most specific that fits the character is used, otherwise `default`, otherwise stock.
```json
{
  "prefix": ["format_instructions", "identity", "dialogue_style"],
  "tail": ["role_anchor", "format_reminder"],
  "off": ["prose_craft"]
}
```
- `prefix`: the part sent once and then **cached** by the provider (cheaper on every following reply). Put what never changes during a conversation here.
- `tail`: the part re-sent fresh every reply, after the current situation.
- `off`: sections turned off.
A section you do not list **keeps its stock place**, so a layout made for an older version never loses what came since.

⚠️ **Moving a per-reply section up into the prefix** is allowed, but it breaks the cache: every reply then costs the full prompt. It is your money; the mod warns you, it never stops you.

===== MESSAGE 4 =====
## Rewording a section

`sections/<key>.txt`. By default your words **replace** the stock text. Inside the file:
- `!append` on a line of its own: your words are **added** after the stock text instead.
- `!remove <text>`: drops every stock line containing that text.
- `!# ...`: a note the model never sees.

## Check it
- `cr.prompt_check` lists the profiles found, the one in use, what each section reworded does, the layout each kind of character gets, and every **problem**: an unknown key, a locked section kept in place, a per-reply section put above the cache. Run it after every change.
- `cr.prompt_dump Name`: the prompt as it now comes out, to compare before and after.
- Picking another profile in MCM applies it at once; after editing files, pick it again or reload your save.
- A profile changes the **character** prompt: conversations, letters and the round table. Envoys, the council, memory summaries and strategic reports have prompts of their own: `Docs/PROMPT_PIPELINES.md` in the mod folder lists every one, and which of your text files (`world.txt`, `post_history_instructions.txt`...) reaches each.

## For modders
Ship profiles in your module's `ModuleData/cr_prompts/<profile id>/`. They appear in every player's list. A player who wants to change one line copies yours into Custom, or makes a small profile with yours as its `parent`.

Built a profile worth sharing? Post it, with what it changes and why! 🛡️
