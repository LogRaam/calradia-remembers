TRANSLATING CALRADIA REMEMBERS

What can be translated today: the Mod Configuration Menu page (every setting's name, its explanation, the group
names and the buttons). The English file in EN/ is the template; it lists every text with its id.

To make a translation (example: Simplified Chinese, the game's "CNs" folder):

1. Copy the EN folder and name the copy after the language, for example CNs.
2. In the copy's language_data.xml, set id and name to the game's own name for that language, and supported_iso to
   the game's codes for it. Copy both from the game's Modules/Native/ModuleData/Languages/<language>/language_data.xml,
   so the game switches to your file when the player picks that language. Change xml_path to the new folder
   ("CNs/std_CalradiaRemembers_mcm.xml").
3. In std_CalradiaRemembers_mcm.xml, set the tag language to that same name and translate each text="...". Keep
   every id exactly as it is. A text left out simply shows in English.
4. Keep each explanation short: the menu shows about 250 characters before it cuts the text off.

You can ship a translation as its own small mod (its own ModuleData/Languages folder, the same ids): the game reads
the language files of every loaded module.

Not translatable yet: the choices inside drop-down lists, the encyclopedia texts the mod adds, and the notices it
shows during play. What characters say follows the game's language (MCM "Reply Language").
