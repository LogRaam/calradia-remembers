===== MESSAGE 1 =====
# ⌨️ Tutorial: the console commands for players

**What is it for?**
Calradia Remembers has a set of console commands. Most are tools I use to test the mod, but some are for you: fix a memory the AI got wrong, see what you have learned about a character, read your saga, or send me exactly what I need when something goes wrong.

**Opening the console**
In a loaded campaign press `Alt + ~` (the key left of `1`), type a command, press Enter.
Every command starts with `cr.`. Most take an optional name at the end, a piece of a character's name (`Derthert`). Leave it out while a conversation is open and it targets the character you are talking to.

**The full list is in the game**
- `cr.help` lists every command.
- `cr.help <word>` filters it: `cr.help letter`, `cr.help memory`.
This list is read from the mod itself, so it is always current. This tutorial only picks out the useful ones.

Commands marked **(save)** change your campaign: save the game afterwards to keep the change.

===== MESSAGE 2 =====
## ✍️ Fix or shape a character's memory

When the AI remembers something wrong, or you want someone not to start as a stranger:

- `cr.memory Liena`: every memory she holds, each with a number. Read-only.
- `cr.memory_set 2 She remembered me warmly. Liena`: rewrite memory number 2. **(save)**
- `cr.forget 1 Liena`: delete memory number 1. **(save)**
- `cr.forget_all Liena`: a clean slate. **(save)**
- `cr.memory_add 30 We fought side by side at Pendraic. Derthert`: add a memory dated 30 days ago, to build a past together. **(save)**
- `cr.memory_repair_accents`: mends accents the console mangled before 2.6.11 (`Ã©` instead of `é`). Safe to run twice. **(save)**

**A rumour for the whole world instead?**
`cr.world_event A great fire gutted the docks at Sargot.` puts it in the world's news. It spreads by hearsay: characters near it, or of the realm it names, are the likeliest to have heard. **(save)**
To make ONE character know a precise fact, use `cr.memory_add`.

===== MESSAGE 3 =====
## 🔎 What you know, and your campaign at a glance

- `cr.matters Liena`: what matters most to her (her children, the honour of her house, a score to settle...), her fear, her superstition, the gifts she loves or shrugs at, and which of these you have learned.
- `cr.info Liena`: your standing with her.
- `cr.chronicle`: your saga in a chronicler's voice. `cr.chronicle_open` opens it on parchment.
- `cr.legends`: every legend of your campaign.
- `cr.help_pleas`: the calls for help waiting on your answer, and their deadlines.
- `cr.raid_restraint`: which friends or kin at war with you spare your villages.
- `cr.extortions`: every extortion you tried and every secret told.
- `cr.affairs`: every hidden affair between lords. ⚠️ **It spoils** what your companions have not found yet.

**Letters**
- `cr.letterbox`: the unopened letters. `cr.letterbox open 1` reads the first.
- `cr.awaited Liena`: marks her letters as awaited (the ribbon in the letter book): they come first. **(save)**

===== MESSAGE 4 =====
## 🛠️ The chat window and your models

- `cr.model`: which AI model each part of the mod is using right now.
- `cr.style_ui off`: hides the style button (the quill) in the chat header, until the game restarts. `on` brings it back.
- `cr.portrait_id Liena`: the file name to give a picture of her, to see it in the chat (see the portraits tutorial).
- `cr.reload_lore`: reloads your edited files (`world.txt`, `character_overrides.json`...) without restarting.

## 🐞 When something goes wrong

These change nothing; they let me see what you saw.
- `cr.prompt_dump Liena`: writes the exact text the AI would receive for her to a file beside the log, and tells you where. No AI is called, nothing is spent.
- `cr.save_sizes`: what each part of the mod weighed at your last save (save first). Useful when a save feels slow or big.
- The **Report** button in the chat header (on by default; MCM, Diagnostics, "Report button in the chat"): you describe the problem and it saves the whole conversation, what the AI was given and its replies to a file for you to send. Nothing is sent by itself.
- The log: `Documents\Mount and Blade II Bannerlord\Logs\CalradiaRemembers.log`

Send the file, the log lines and your Calradia Remembers version, and I can usually find the cause.

===== MESSAGE 5 =====
## 🧪 The rest: test tools

Everything else in `cr.help` is for testing the mod: commands that force a pregnancy, open a scene, raise an army or make a lord write to you. They work, but they are not how you play, and many change your campaign for good. Use them on a **copy** of your save.

**One worth knowing: the self-test**
`cr.test all` runs every self-test of the mod in your campaign and reports PASS, FAIL or MANUAL for each. It cleans up after itself and keeps out the few tests that cannot be undone. If you report a bug, the summary line (`451 PASS, 2 FAIL...`) and the FAIL lines from the log help a lot.
`cr.test list` lists them, `cr.test <name>` runs one.

The full written guide, with every command explained, is `CONSOLE_COMMANDS_GUIDE.md` in the mod's source.
