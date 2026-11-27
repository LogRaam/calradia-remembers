CALRADIA REMEMBERS, CUSTOM FOLDER

This folder is for YOUR edits. The mod never ships files here and never writes into it, so when you
update the mod (re-extract the archive over your install), whatever you put in Custom is left alone.

WHY THIS EXISTS

Every editable prompt file (world.txt, behavior_guidelines.txt, and the rest) lives in ModuleData, right
next to the mod's own files. If you edit one of those directly and then update the mod, the update
overwrites your changes with the shipped default. Custom fixes that: the mod checks here FIRST, and only
falls back to the shipped file if nothing of the same name exists in Custom.

TO KEEP AN EDITED PROMPT

Copy the file you want to customize into this folder, keeping the same name, then edit your copy here.
For example:
  Custom\world.txt
  Custom\player_description.txt
  Custom\behavior_guidelines.txt
  Custom\post_history_instructions.txt
  Custom\behavior_guidelines_gangleader.txt (and the other station variants)
  Custom\behavior_guidelines_merchant.txt (one trade of town or village notable; also _artisan,
    _ruralnotable, _headman, _preacher. None ships: a notable whose trade has no file of its own uses
    behavior_guidelines_notable.txt. A trade file replaces that shared file for its trade, so copy
    behavior_guidelines_notable.txt under the new name and edit it, rather than starting empty)
  Custom\cultural_traditions.json (JSON: keep the structure valid, matching "Name": "description"
    pairs with commas between them; keyed by the culture's DISPLAY NAME, case-sensitive, so an overhaul
    mod's own culture, for example "Nord", goes in as its own entry)
  Custom\jealousy_cultures.json (same JSON rules and same DISPLAY NAME keying as cultural_traditions.json)
  Custom\women_in_ranks.json (the share of women, 0 to 100, among each culture's soldiers in battle; keyed by the
    game's culture ID, not its name: sturgia, battania, khuzait, nord, empire, vlandia, aserai; a "realms" entry by
    kingdom id, such as empire_s, wins over the culture for that realm's armies. MCM "Women In The Ranks". Since 2.8.10)
  Custom\setting.json
  Custom\races.json (how each of the game's races is described, for overhaul mods with non-human peoples: keyed by
    the game's race NAME, which "cr.appearance" and "cr.appearance who <name>" print. An entry names the people
    ("elf" makes "an elf woman"), may add one line of its own and may leave unsaid facts the face reads wrongly for
    that race (skin, hair, eyes, beard, build, height, age). The examples in the shipped file explain each field.
    Since 2.8.22)
  Custom\character_overrides.json (your hand-written characters; needs MCM "Use custom traits"; cr.reload_lore)
  Custom\campaigns\<campaign id>\character_overrides.json (characters for ONE campaign only, laid over the shared
    file: heroes born or spawned in a campaign get ids like CharacterObject_2526 that mean someone else in the next
    one. cr.lore_status shows this campaign's folder. Since 2.8.10)
  Custom\stations\merchant.txt (who each social station is and how they meet you; same names as ModuleData\stations)
  Custom\folklore\battania.txt (the tales a people tells; a file named after another mod's culture id adds that people)

TO KEEP WHAT YOU MAKE

These folders are read from here too, beside the mod's own:
  Custom\Quests\          your written tales (Tale Editor), one folder per tale with its pictures
  Custom\scenarios\       your scenario packs (Scenario Composer)
  Custom\Prompts\         your prompt profiles (MCM "Prompt Profile"; cr.prompt_check; keys in Docs\PROMPT_SECTIONS.md)
  Custom\ChatPortraits\   your chat portraits (player.png, an id such as lord_1_14.png, or a name); looked in before
                          ModuleData\ChatPortraits, which is still read

TO KEEP A CUSTOM STYLE

Put your style file in the Styles subfolder here instead of the main Styles folder, for example:
  Custom\Styles\MyVoice.txt

It will show up in the style picker like any other style, and if it shares a name with a shipped style,
your version is the one that gets used.

HOW THE LOOKUP WORKS

For any given file name, the mod loads the one in Custom if it exists, otherwise it loads the standard
one shipped in ModuleData. Nothing needs to be moved back and forth: keep only what you actually want to
override in this folder, and everything else keeps using the mod's shipped defaults.

Your copy replaces the whole file, so improvements the mod later makes to that file stop reaching you.
cr.lore_status prints the Custom folder in use, the prompt files read from it, and flags a shipped file
NEWER than your copy: compare the two, or delete your copy to go back to the mod's.

APPLYING A CHANGE WITHOUT RESTARTING

  cr.reload_prompts   the prompts, setting.json, the culture files, stations and folklore
  cr.reload_lore      character_overrides.json
  cr.story_check      the tales
  Styles are read each time they are used; scenario packs when a save is loaded.

KEEPING IT SAFE FROM A CLEAN REINSTALL

Re-extracting the mod over your install leaves this folder alone, but deleting the mod folder deletes it
too. Move its content to a folder outside the mod (same sub-folders) and enter that folder's full path in
MCM -> Calradia Remembers -> Character -> "Custom Folder Path". A path that does not exist falls back to
this folder without a word: cr.lore_status shows which one is in use.

TEMPLATE VARIABLES

Your styles and overrides can use SillyTavern-style {{token}} variables. They are resolved fresh for the
NPC you are actually talking to and the player you actually are, at the moment each reply is written, so
the same style file works correctly for every character instead of freezing onto whoever you first used it
with.

Supported tokens:
  {{char}}        the NPC's name
  {{char_gender}} the NPC's gender, "woman" or "man"
  {{user}}        your character's name
  {{user_gender}} your character's gender, "woman" or "man"
  {{user_clan}}   your clan's name
  {{faction}}     your faction: your kingdom, or your own clan if you have none (also {{user_faction}})
  {{location}}    the settlement or scene you are in (empty when roaming the world map)
  {{spouse}}      your spouse's name (empty if you have none)
  {{father}}      your father's name (empty if unknown)
  {{mother}}      your mother's name (empty if unknown)

Case does not matter ({{char}}, {{Char}}, and {{CHAR}} all work), and spaces inside the braces are fine
({{ user }} works too). An unrecognized token, such as a typo, is left exactly as written rather than
silently removed, so you can spot the mistake.

Example, in a Custom\Styles\ file:
  Write {{char}}'s dialogue in short, clipped sentences, and have them address {{user}} by name when the
  scene turns tense.
