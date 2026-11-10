===== MESSAGE 1 =====
# 🏰 Tutorial: write stations and folklore

Two kinds of plain text files shape **who a character is** in Calradia, beyond their personality: their **station** (their place in society) and the **folklore** of their people (the tales told around the fire). Both live in `Modules/CalradiaRemembers/ModuleData/`, and both are yours to rewrite.

**The golden rule** (see the Custom folder tutorial): copy the file into `ModuleData/Custom/` **with the same sub-folder**, `Custom/stations/` or `Custom/folklore/`, and edit your copy. Updates never touch it. Your copy replaces the whole file.

# Stations: who they are, and how they meet you

Eleven files in `stations/`: `ruler`, `lord`, `noble`, `companion`, `wanderer`, `gangleader`, and one per trade of the town and village notables: `merchant`, `artisan`, `ruralnotable`, `headman`, `preacher`.

Each tells a character of that station **who they are**, and **how they treat you at each rung of your standing**. A merchant does not speak to a nameless sellsword the way he speaks to a king.

===== MESSAGE 2 =====
## A station file

Six sections, each opened by its name in brackets, written in the **second person** ("You are..."):
```
[who]
You are a merchant, a commoner of means: your wealth buys you a voice in the town, but not a title, and you know the difference.

[nobody]
To you this player is a nobody with a sword: you are guarded and brisk, careful of your purse, and you give nothing for free.

[sellsword]
To you this player is a sword for hire: you are polite, as one is with armed men...
```
- `[who]`: who they are.
- `[nobody]`: you have no house of note, no lands, no name yet.
- `[sellsword]`: a captain with a band, without lands or title.
- `[risinghouse]`: the head of a house of some weight, not yet a great lord.
- `[lordoftherealm]`: a great lord, with lands.
- `[sovereign]`: you rule a realm.

**Tips:** one or two sentences per section, plain and concrete: an attitude, not a speech. A section left empty says nothing. Lines starting with `#` are never read.
**What a station may DO** (a duel, a brawl) is decided by the game, never by this file: write how they **are**, not what they are allowed to do.
**Check it:** `cr.station Name` shows which station a character is given, where you stand, and the exact lines they receive.

===== MESSAGE 3 =====
# Folklore: the tales of each people

`folklore/` holds one file per people, named after the culture's id: `battania`, `empire`, `sturgia`, `vlandia`, `aserai`, `khuzait`, `nord`, `vakken`, `darshi`. Every character of that **culture** (their people, not their realm) knows some of these tales, **as tales, never as history**.

Each character carries **up to six**, and **loves one best**: that one alone they can **tell in full**, in their own voice. The others they can name or allude to. Not everyone carries the same ones.

## A tale
```
[The Wren and the Champion]
gist: the proudest of the Fian champions, who swore no living thing could best him, and the wren that did.
beat: A champion of the Fians, strong and fair, boasted at a feast that no living thing in all Battania could best him at anything.
beat: A wren on the rafters called down that it would race him to the top of the tallest hill at dawn, and the hall laughed.
beat: ...
beat: So when a child brags, a Battanian mother says: "Mind the wren in your hair."
```
- `[Title]` opens a tale.
- `gist:` what it is about, in **one line**. **Required**: a tale without it is not read.
- `beat:` one part of the story, in order. **Only a tale with beats can be told in full**, and only such a tale can be a character's favourite. A tale with a gist alone can only be named.
- A line with no key continues the line before it.

===== MESSAGE 4 =====
## Writing good tales
- **Five or six beats** is plenty: the facts are fixed, the teller supplies the voice.
- End on **what the people take from it**: a saying, a custom, a warning. That is what makes it feel lived in.
- Stay **inside Calradia**: its peoples, its gods, its places. No Earth names.
- Each tale costs a line in every prompt of the characters who carry it: a few good tales beat many thin ones.
- **Ask for one in game**: talk to a character of that people and ask them for a tale of their people, or the one they love best.

## For another mod's culture
A file named after **any other culture id** gives that people tales of its own: an overhaul mod's culture gets folklore just by adding `folklore/<its id>.txt`.

## Apply your changes
`cr.reload_prompts` re-reads the stations and the folklore without restarting. The next reply uses them.

**Modders:** ship your additions as a patch in your own module, `ModuleData/cr_patch/stations/<station>.txt` or `cr_patch/folklore/<id>.txt`. For stations, open your patch with the `[section]` you mean: a line belongs to the section above it, and repeating a section CR already has replaces it (see `EXTENSION_API.md`).

Share your tales and stations here! 🛡️
