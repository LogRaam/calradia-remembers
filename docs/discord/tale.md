===== MESSAGE 1 =====
# 📖 Tutorial: write your own quest with the Tale Editor

**What is a tale?**
A tale is a **quest you write**, in several stages, that a character offers you in conversation. You write the story; the AI gives it a voice. It retells your lines in each character's own words, but it **cannot add a fact, skip a stage or pay a reward**: the game checks every step.

A tale can be:
- **simple**: a lord asks you to go to his village and come back;
- **branching**, like a choose-your-own-adventure book: tell the lord or keep silent, and the story goes another way, to one of several endings;
- **alive in the world**: the villagers know their part of the story, nobody knows what comes next, and an ending can leave memories, news in the realm, a grudge, gold, an item...

The mod ships with one as a model: the werewolf of the wood.

⚠️ **Tales are off by default** (still experimental). To have them offered: MCM → Calradia Remembers → Content → **Authored Quests (experimental)**.

**Where is the tool?**
`Modules/CalradiaRemembers/Docs/TaleEditor.html`
Double-click it: it opens in your browser, works offline and sends nothing anywhere. The **Guide** button at the top opens the full authoring guide (`HOWTO_Quests.md`), with every field explained.

===== MESSAGE 2 =====
## Step 1: start a tale

**New** gives you two choices:
- **A blank tale**: one stage and a reward, ready to grow.
- **From the werewolf**: the tale that ships with the mod. **The best way to learn**: open it, click around, see how it is built.
You can also **Open...** an existing tale, or **Import Twine...** (see the last message).

**The screen:**
- **Left**: the parts of the tale: its **Stages**, and its **Endings** (`reward` = the success ending, `failure` = when time runs out, plus any named endings you add).
- **Middle**, three tabs: **Edit** (the forms), **Story map** (the whole story as boxes and arrows), **JSON** (the file itself).
- **Right**: **Checks**, the game's own checks, live, in the same words the game uses. A **blocking** problem means the game will refuse the tale; the others are warnings. Click one to jump to the field.

## Step 2: the header and who gives it

- **Header**: an **id** (unique; prefix it with your name, e.g. `gab_miller_silence`), the **title** shown in the journal, whether it plays once per campaign, and a weight.
- **Who gives it**: a lord, a notable, a gang leader, your spouse, a companion... with conditions: their regard for you, their culture or realm, not at war with you, and so on.
- **Roles**: the people and places the tale is bound to when it is offered, written in braces in your text: `{giver}`, `{village}` (one of the giver's villages, or a town), `{rival}` (the head of another house of the same realm), `{husband}`... The game fills in the real names.

===== MESSAGE 3 =====
## Step 3: stages

Each stage is one moment of the story. Its fields:
- **Journal**: the line written in the quest journal when the stage begins.
- **Objective**: what the game checks to move on. Talk to someone (`talk_to`), go back to the giver (`return`), question the notables of a place (`talk_to_locals`), go there (`be_at`), keep watch at night (`keep_watch`), explore a scene told by a narrator (`explore`), defeat a band (`defeat_party`), let days pass (`wait`), bring goods (`bring`), or wait for the world: a war ends, someone dies, someone marries you.
- **Canvas**: how the moment feels, spoken by the giver.
- **Facts**: what the giver must tell you, word for word.
- **Knowers**: what *other* characters believe at this stage: `{rival}` or `locals_of:{village}` (the people of the place). They know only their own lines, and only during that stage.
- **Next**: where the story goes when the objective is done: another stage, `reward`, or a named ending.

**The first stage is the request itself**: it ends when you accept the tale. Put no choices there; they go on the next stage.

**Choices**: on a stage, add ways out, each taken by an act (go somewhere, talk to someone, wait) and leading somewhere else. **Flags** remember what happened (*"the miller spoke"*), and a choice, a line or a consequence can depend on them.

Check the **Story map** often: a path leading nowhere shows **in red**, a part no path reaches is **faded**. Renaming a stage carries every arrow along.

===== MESSAGE 4 =====
## Step 4: endings

- **reward**: the success ending. **Named endings**: as many as you want (*exposed*, *silence*...).
- **failure**: what happens if time runs out (**Days**). With no days, the tale never fails on its own.

Each ending can give or take **regard** and **gold**, leave the giver a **memory** in their own voice, write a last **journal** line, and carry **consequences**: a memory for someone else, news spread through the realm, a grudge, an item given, and more. Each can depend on a flag.

## Step 5: save and test

Click **Save** (`Ctrl + S`) and put the `.json` file in:
`Modules/CalradiaRemembers/ModuleData/Custom/Quests/`
(or the `Quests` folder of the Custom folder you set in MCM). Updates never touch it. A tale with pictures gets a folder of its own: its JSON next to its images.

In the game, open the console (`Alt + ~`):
- `cr.story_check` reloads every tale and lists every problem, in plain words.
- `cr.story_check Name`: could this character give each tale now? If not, why (*"their regard for you is -5, and the quest needs at least 0"*).
- `cr.story_start <tale id> [Name]`: that character gives it now, without waiting for the dice.
- `cr.story_status`: where the tale under way stands.
- `cr.story_step` completes the current stage; `cr.story_choose <choice id>` takes a choice. Walk each path!
- `cr.story_abandon` ends the tale, without reward or penalty.

===== MESSAGE 5 =====
## How tales are offered in play
- Only **one tale at a time**.
- A character who can give one raises it in about **one conversation out of three**.
- Declined, it is not raised again for a **season**.
- A character who already has a task for you does not raise a tale as well.

## Writing in Twine
Already know [Twine](https://twinery.org)? Draw your story there, publish it to a file (`.html`) or export it as Twee, and use **Import Twine...**. Each passage is a moment; its **tag** says what the player must do to get there (`talk_to:giver`, `be_at:village`, `wait:3`...). Links are the choices, a passage with no links is an ending, and variables become flags. Harlowe and SugarCube are both read. Whatever the import cannot map is listed, never dropped silently. Details in the Guide, section 13.

## For modders
Ship your tales in your own module: `ModuleData/cr_quests/`, one folder per tale if it has pictures. No code, and no copy for your players to make.

Write a tale? Share it here, we want to play it! 🛡️
