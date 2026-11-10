===== MESSAGE 1 =====
# 📜 Tutorial: make the world react with the Scenario Composer

**What is a scenario?**
A scenario is a small rule you write that makes Calradia *react* to something that happens in your game. No code, no install: you build it in a page that opens in your browser, and Calradia Remembers reads it.

Every rule has four parts:
- **Trigger**: the event that wakes the rule up. A hero dies, two heroes wed, a war is declared, a peace is made, a written tale reaches a stage, a night passes while you are a prisoner.
- **Guards** (optional): conditions that must all be true. *"The one who died is Rhagaea"*, *"the player is at war with the Southern Empire"*, *"the player rules a kingdom"*...
- **Consequences**: what changes in the world. Declare a war, make a peace, change your relation with a hero, change a clan's influence, plant a memory in a character's head (they will bring it up in conversation), push a lord to press his king for war, open a written tale.
- **Message**: a line told in the realm as news. Characters can hear of it like any other event.

Example of what it gives you: *"When Rhagaea dies, the Northern and Western Empires turn on a leaderless South."* That one actually ships with the mod.

**Where is the tool?**
It ships with the mod: `Modules/CalradiaRemembers/Docs/ScenarioComposer.html`
Double-click it and it opens in your browser. It works offline.

===== MESSAGE 2 =====
## Step 1: build a rule

Click **+ Add rule**. A card appears:
- **id**: a unique name for the rule. Make it yours, e.g. `my_rhagaea_court`. ⚠️ Do not reuse an id that already exists (`rhagaea_death_eagles_turn` is already the mod's): only the first rule with a given id is kept.
- **trigger**: pick from the list.
- **fire once**: ticked means the rule plays once per save, then it is sealed. Unticked means it plays every time the event happens.
- **group / weight**: leave them empty for now (see Step 4).

Under **Guards**, click **+ guard**. Under **Consequences**, click **+ consequence**. Each dropdown explains itself in small text under it. Type the message at the bottom.

On the right you see the finished JSON live, and a warning if something is missing.

**Name things by their game id, not their name.** The game translates names, so `Rhagaea` only works in English. Use `lord_1_14`. Kingdoms: `empire` (Northern), `empire_w` (Western), `empire_s` (Southern), `vlandia`, `battania`, `sturgia`, `khuzait`, `aserai`. The player is `PLAYER`.
To find a hero's id in game: open the console (`Alt + ~`) and type `cr.portrait_id Rhagaea`. It prints `lord_1_14.png (or the friendlier rhagaea.png)`: the first name, without `.png`, is the id.

===== MESSAGE 3 =====
## Step 2: a full example

*"If I am at war with the Southern Empire when Rhagaea dies, my voice carries more at court."*

- id: `my_rhagaea_court`
- trigger: `ON_DEATH`
- guard: `IF_HERO_IS` → `lord_1_14`
- guard: `IF_PLAYER_AT_WAR` → `empire_s`
- consequence: `CHANGE_INFLUENCE` → `PLAYER` → `+50`
- message: *With the empress fallen, those who fought her are heard louder at court.*
- fire once: ✅
(From 2.8.8, the **Load Rhagaea example** button loads exactly this rule.)

The JSON on the right will look like this:
```json
{
  "id": "my_rhagaea_court",
  "trigger": "ON_DEATH",
  "guards": ["IF_HERO_IS:lord_1_14", "IF_PLAYER_AT_WAR:empire_s"],
  "consequences": ["CHANGE_INFLUENCE:PLAYER:+50"],
  "message": "With the empress fallen, those who fought her are heard louder at court.",
  "fireOnce": true
}
```
Every rule that answers the same event plays, one after the other. So this one plays **alongside** the mod's own Rhagaea rule, not instead of it.

===== MESSAGE 4 =====
## Step 3: install it

1. Name your pack at the top right (e.g. `my_scenarios.json`) and click **Download pack**.
2. Put the file in your **Custom** folder:
`Modules/CalradiaRemembers/ModuleData/Custom/scenarios/`
(create the `scenarios` folder if it is not there; if you set your own Custom folder in MCM, use `scenarios` inside that one). Mod updates never touch the Custom folder, so your packs survive updates.
3. Load your save.

To edit a pack later: **Import JSON** in the tool, change it, download again.

## Step 4: alternatives (group + weight)

Give several rules the **same group** and only **one** of them plays, drawn by **weight**. Example, two rules on `ON_MARRIAGE` with the guard `IF_PLAYER_INVOLVED`, both in group `my_wedding`:
- weight 3: the realm cheers the match → `CHANGE_INFLUENCE:PLAYER:+20`
- weight 1: the nobles grumble → `CHANGE_INFLUENCE:PLAYER:-10`
So it goes well three times out of four.

===== MESSAGE 5 =====
## Step 5: test it in game

Open the console (`Alt + ~`):
- `cr.scenarios` lists every loaded rule and where it came from. Check that yours is there.
- `cr.scenario_fire ON_DEATH Rhagaea` fires the trigger now, as if Rhagaea had died (she does not actually die). It tells you which rules fired, or why none did.

⚠️ **The test is real.** Its consequences really happen (wars declared, influence given), a "fire once" rule is sealed in that save, and the mod's own rules on the same event fire too. **Test on a throwaway save and do not save afterwards.**

A rule did not play? Usually one of these:
- a guard was false (Rhagaea is not at war with you, she is already dead...);
- a consequence could not apply. A rule plays only if **all** of its consequences can really happen: no war between realms already at war, no relation with someone gone. Then the whole rule waits, and nothing is told;
- it is "fire once" and already played in this save;
- its id is used by another pack.
`cr.test all` also reports a rule with an unknown keyword, or a hero or kingdom id that does not exist.

## For modders
Ship your packs in your own module: `ModuleData/cr_scenarios/*.json`. No code, no reference to CR needed. Prefix your ids with your module id. Full reference: `EXTENSION_API.md`, section *Scenario packs*.

Questions or a rule you are proud of? Post it here! 🛡️
