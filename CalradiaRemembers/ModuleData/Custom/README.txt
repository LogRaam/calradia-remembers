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

TO KEEP A CUSTOM STYLE

Put your style file in the Styles subfolder here instead of the main Styles folder, for example:
  Custom\Styles\MyVoice.txt

It will show up in the style picker like any other style, and if it shares a name with a shipped style,
your version is the one that gets used.

HOW THE LOOKUP WORKS

For any given file name, the mod loads the one in Custom if it exists, otherwise it loads the standard
one shipped in ModuleData. Nothing needs to be moved back and forth: keep only what you actually want to
override in this folder, and everything else keeps using the mod's shipped defaults.

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
