===== MESSAGE 1 =====
# 🎭 Tutorial: write your own characters (character_overrides.json)

**What is it for?**
Every character gets a personality from the game (five traits) and the mod builds who they are from it. With `character_overrides.json` you can **write a character yourself**: their temperament, how they speak, what they believe, who they love.
- Make King Derthert a cold accountant who remembers every debt.
- Give a companion a **conviction**, even a false one: she believes she is secretly a necromancer, and acts on it.
- Open a romance the game's roll had closed, by setting their **orientation**.
- Give them a **past** with someone: an old romance, a rivalry, a feud, remembered without bloating the backstory.

**Step 0: turn it on**
MCM → Calradia Remembers → Character → **Use custom traits**. **Nothing in the file applies while it is off**, and turning it off puts the game's own traits back.

**Step 1: get the file**
The mod ships a template full of explanations: `Modules/CalradiaRemembers/ModuleData/character_overrides.json`.
**Copy it into `ModuleData/Custom/`** and edit your copy, so an update never overwrites your work. Open it in any text editor (Notepad works, Notepad++ or VS Code are friendlier).

===== MESSAGE 2 =====
## Step 2: write a character

At the bottom of the file, between the `[` `]`, there is an example with `//` in front of each line (a `//` line is ignored). Remove the `//` and edit:
```json
[
  {
    "hero": "Derthert",
    "valor": 1,
    "honor": -1,
    "calculating": 2,
    "backstory": "A Vlandian king who trusts ledgers more than men, and remembers every debt.",
    "conviction": "He is convinced the player's clan is quietly plotting to unseat him, and reads every kindness as a move in that game."
  }
]
```
- `"hero"`: the character's name, as the game shows it (case does not matter).
- `"id"` (optional): their game id, when two characters share a name. `cr.portrait_id Name` shows it (the file name it prints, without `.png`).
- `"valor"`, `"honor"`, `"mercy"`, `"generosity"`, `"calculating"`: each from **-2 to +2**. Leave one out to keep the game's value. They replace the game's traits, so the campaign AI follows them too, not just the conversation.

**Several characters?** One `{ }` block each, **separated by a comma**:
`[ { "hero": "Derthert", ... }, { "hero": "Rhagaea", ... } ]`

===== MESSAGE 3 =====
## Backstory or conviction?

- **`backstory`**: **who they are and how they sound**. Voice, temperament, manner, quirks of speech. It colours how they **talk**, not what they **do**.
- **`conviction`**: **what they hold true, and so what they want**. This one changes their **decisions**. It may be false, and that is the fun: they act on it, and if it would ruin them to be known, they hide it.
A conviction **grants no powers** (he may hoard bones, he cannot raise the dead), brings in **no lore from outside** Calradia, and **cannot bend the world**: a belief makes them *want* something, only deeds decide whether they get it.

## The other fields
- `"orientation"`: `straight`, `bi`, `gay`, `lesbian`, `pan`, `ace`... replaces the game's roll, e.g. for a romance it blocked. (`cr.orientation` changes it for the session only; the file makes it permanent.)
- `"preferences"`: **how** they love: `MonogamousStrict`, `Polyamorous`, `Devoted`, `Possessive`, `SlowBurn`... One polyamorous character in a strictly monogamous culture, for instance.
- Adult content (Hardcore level only): `"kinks"`, and the gestures they `"loves"`, are `"lessFond"` of, or `"dislikes"`.
Every valid name is listed at the top of the template.

===== MESSAGE 4 =====
## Past bonds: `"past"`
Old romances, rivalries, friendships, feuds that happened before the game, without stuffing them in the backstory:
```json
"past": [
  { "with": "Rhagaea", "kind": "rivalry", "summary": "They courted the same allies for years before the war, and he still counts her every move." }
]
```
- `"with"`: the other character's name (or their `id` if two share it). `"kind"`: `romance`, `rivalry`, `friendship`, `feud` or `other`.
- The character remembers it when you **name** that person, or when that person is **in the scene**. A long list never weighs on every conversation.
- **Both** remember it, even if only one of them has an entry.
- It is a memory, not a number: no relation moves. For starting relations, use the History Editor (`history.md`).
- **A past with you**: `"with": "player"`, only in **this campaign's own file**, `ModuleData/Custom/campaigns/<id>/character_overrides.json` (`cr.lore_status` prints its exact path; a shared file cannot know who you will play). That one is remembered in every conversation with them.

===== MESSAGE 5 =====
## Step 3: load it and check

Save the file, then in the console (`Alt + ~`):
- `cr.reload_lore` reloads it and applies it, no restart (it is also read at every load).
- `cr.lore_status` tells you **which file was read**, how many characters loaded, the **error** if the file could not be read, and for each entry whether it **found the character**. Run it after every edit: it catches a typo in a name at once.
- `cr.backstory Derthert` shows what the game now holds for that character.

## The usual mistakes
- **"Use custom traits" is off**: nothing applies.
- **A missing comma** between two `{ }` blocks, or a missing quote: the whole file is refused. `cr.lore_status` shows the error and where.
- **The name does not match** (a title in front, a typo), or **two characters share it**: add their `"id"`. The same goes for a `"with"` in `"past"`: `cr.lore_status` warns when it finds no one.
- **You edited the file in `ModuleData/` instead of your copy in `Custom/`**: when a copy exists in Custom, it is the only one read.

Share the characters you wrote! 🛡️
