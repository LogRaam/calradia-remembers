# Authored quests: a repertoire of multi-stage quests in JSON

DRAFT, 03/10/2026. Not built. For Gabriel to read, correct, and rule on the open questions at the end.

## Where this comes from

- Lordfadooboo and fkasad (Nexus, 02/10/2026) asked for characters who come to the player WITH a task
  (ROADMAP, `bae3842`).
- Gabriel, 03/10/2026, gave a werewolf example. A village sees a werewolf near its forest, and women go missing on
  full-moon nights. A lord asks the player to look into it. The player questions the villagers, goes to the forest on
  a full-moon night, and finds clues: the "werewolf" is a band of brigands selling villagers to slavers. The lord then
  sends the player to hunt the band down.
- Gabriel's rulings:
  - Quests can have several stages.
  - Some quests are unique to a campaign and some repeat, but unique is better, because a repeated quest bores.
  - Quests live in a JSON repertoire that players and modders can extend.
  - The text is a canvas that the model rephrases, not fixed wording.

## What today's tasks lack

- **They are one gesture.** Today's tasks (`QuestType`: deliver grain, capture a lord, plead a case, scout a town...)
  are each a single deed that the game verifies. They have no progression and no turn of events.
- **The giver is generic.** Each task is generated from a lord's grounded menu (`ViableQuestScanner`). It shows in the
  journal through `CalradiaRemembersQuest` and `QuestJournalBridge`, but nothing about it is written as a story.
- **An authored quest is different.** It is a written story in stages. Each stage ends on a deed that the game checks,
  and the people involved know different things at each stage.

## The rules that do not move

1. **The game advances the quest, never the model.** Every stage ends on a condition the engine verifies, from a
   closed list (below). A character saying "well done, the bandits are dead" moves nothing. This is the same rule as
   every task since 2.4 ("verified by the game, never by the character's word").
2. **Canvas, not script (Gabriel, 03/10/2026).**
   - Each stage gives the model a `canvas`: what the beat is, and the mood.
   - Each stage also gives a list of `facts`: what must be true in whatever is said.
   - The model says it in the character's own voice and style. It may not change a fact, add a fact, or reveal a
     later stage's facts.
3. **Gating before declaration (Gabriel, 09/09/2026).**
   - A character is told only what they know at this stage.
   - Before the forest, the villagers believe in the werewolf. They are not lying: they believe it.
   - Only after the clue does the lord learn the truth, and only from the player.
4. **NPCs never lie (Gabriel, 20/09/2026).**
   - A character may be wrong, vague, frightened or silent.
   - A character is never instructed to deceive. Where the story needs a deceiver (the slavers), it is a party on the
     map, not a speaking character told to lie.
5. **What the player can verify is never faked (Gabriel).**
   - A clue is narrated where the player can look: the forest, at night.
   - A spawned band is a real party that can be beaten.
   - A date the player can check is a date the game keeps (see the moon question below).

## The file

- **Location.** `ModuleData/Quests/*.json` for the ones that ship. `ModuleData/Quests/Custom/*.json` for players
  (survives updates, like the other Custom folders). `cr_patch` for other modules.
- **One file, one or more quests.** Ids are unique across all files; a duplicate id is reported, never loaded twice.

```json
{
  "id": "cr_wolf_of_the_wood",
  "title": "The Wolf of the Wood",
  "unique": true,
  "repeat_cooldown_days": 0,
  "weight": 10,

  "requires": {
    "giver": { "role": "lord", "owns": "{village}", "min_regard": 0, "not_at_war_with_player": true },
    "village": { "type": "village", "near": "forest", "max_prosperity": 600 },
    "player": { "min_clan_tier": 1 },
    "season": ["autumn", "winter"]
  },

  "binds": {
    "giver": "the lord who holds {village}",
    "village": "a village near a forest",
    "elder": "a notable of {village}"
  },

  "stages": [ ... ],

  "reward": { "gold": 1500, "regard": 10, "renown": 5, "memory": "..." },
  "failure": { "days": 40, "memory": "...", "regard": -3 }
}
```

### `requires` and `binds`

- **`requires` decides whether the quest may be offered now.** It is checked against the live game when a giver is
  chosen. Every key maps to a fact CR already reads (clan tier, ownership, regard on the mod's ledger, war, season,
  settlement type).
- **`binds` names the roles the story needs.** The first giver and place that satisfy `requires` are bound for the
  life of the quest. Every text can then use `{giver}`, `{village}`, `{elder}` and so on.
- **A unique quest binds once per campaign.** A repeatable one binds afresh each time it is offered, so the same
  story happens to other people in another place.

### `stages`

Each stage:

```json
{
  "id": "question_the_village",
  "journal": "Ask the people of {village} what they have seen.",
  "objective": { "type": "talk_to_locals", "place": "{village}", "count": 2 },
  "canvas": "The villagers are frightened and half-ashamed of their fear. They speak of the beast in lowered voices.",
  "facts": [
    "Three women of {village} have vanished since the harvest, each on a full-moon night.",
    "Shepherds heard howling and saw something large moving at the edge of the forest.",
    "Nobody has seen the beast clearly."
  ],
  "knowers": {
    "locals_of:{village}": [
      "You believe a werewolf haunts the forest; your cousin swears she saw its eyes.",
      "Since the harvest, three women have gone missing, each on a full-moon night."
    ],
    "{giver}": [ "You have heard the villagers' tale and do not know what to believe." ]
  },
  "on_complete": { "narration": null, "next": "the_wood_by_moonlight" }
}
```

| Field | Meaning |
|---|---|
| `journal` | The journal line while this stage is open (fixed text, it is UI). |
| `objective` | One condition the engine verifies, from the closed list below. |
| `canvas` | The beat for the model: what this moment is and how it feels. Rephrased freely. |
| `facts` | What must stay true in anything said. Fixed in substance, free in wording. |
| `knowers` | Who knows what at this stage, injected into THEIR prompt only. Keys are roles or selectors. |
| `on_complete.narration` | Optional. Shown when the objective is met; may itself be a canvas and facts (see the wood). |
| `on_complete.next` | The next stage id, or `"reward"`. |
| `branches` (optional) | Several ways out of a stage, each with its own objective and its own `next`. Not in Inc 1. |

### The closed list of objectives (Inc 1: the five the werewolf needs, plus `deliver_item`)

| Type | Verified by | Engine fact |
|---|---|---|
| `talk_to` `{hero}` | A CR conversation with that hero, opened after the stage began. | The conversation open event. |
| `talk_to_locals` `{place, count}` | That many distinct notables of the place spoken to since the stage began. | Same. |
| `be_at` `{place, radius, night, moon}` | The player's party within `radius` of the place; night by the campaign clock; moon as below. | `MobileParty.MainParty` position, `CampaignTime.IsNightTime`. |
| `defeat_party` `{spawn}` | A party CR spawned for this quest beaten in battle by the player. | The battle end event, the party's id. Spawned with `TransientPartyRegistry` so it survives a save. |
| `deliver_item` `{item}` | The item handed over through the offer bag, matched as supply tasks are. | `SupplyItemMatcher`. |
| `return` | Back to the giver: `talk_to {giver}` with the reward settled. | As `talk_to`. |

Later types, one by one, each with its own check: `win_tournament`, `capture` (reuse the capture task),
`escort` (step 3 of the roadmap), `free_prisoner`, `wait_days`.

### Spawned parties

```json
"spawn": {
  "id": "slavers",
  "template": "looters",
  "size": [12, 20],
  "name": "Slavers of the {village} wood",
  "near": "{village}",
  "behavior": "hide_near",
  "flee_after_days": 20
}
```

- **The party is real.** It exists only while the quest needs it, is named, and is on the map.
- **`behavior`.** `hide_near` keeps it near the place, `roam` lets it move.
- **`flee_after_days` is the failure clock** for that stage: past it, the band leaves the region and the quest fails
  with its `failure` memory.

## How the model is told (prompt side)

- **When.** A character with a role in an active authored quest gets one block, below the cache marker, only while
  that is so.
- **The giver, at each stage:** the stage's canvas, its facts, what the giver knows, and the rule: "say these in your
  own words; keep every fact; add none; do not speak of what you have not been told".
- **A knower:** only their knower lines, framed as what they believe, never as the plot.
- **Never shown to anyone:** the stage list, later stages, the twist.
- **The interpreter is not told the quest.** No verb completes a stage, so nothing in a reply can complete one.

## The four drafts

In `docs/design/quests/`:

| File | Unique | Shape | The turn |
|---|---|---|---|
| `wolf_of_the_wood.json` | yes | village, night, clue, hunt | The werewolf is a slaving band (Gabriel's). |
| `smoke_over_the_hills.json` | yes | burned farms blamed on an enemy realm | The raiders wear the colours of a rival house of the giver's own realm. Ends on a choice in Inc 2 (branches); in Inc 1, report it and the giver acts. |
| `the_lost_banner.json` | repeatable, long cooldown | deserters carry off a house's banner | The deserters are not cowards: their captain sold them out. Bring the banner back, and the giver remembers the deserters' tale. |
| `the_bitter_well.json` | yes | a town falls sick | Not poison: grain sold rotten by a merchant. The gang leader knows who; the giver must hear it from the player. |

## Open questions for Gabriel

1. **The moon.**
   - Bannerlord has no moon phases in its rules.
   - We could keep a 28-day cycle on our own calendar, show the next full moon in the journal, and let
     `be_at { moon: "full" }` check that.
   - But if the game's sky draws a moon whose shape disagrees, the world lies about something the player can see.
     Someone needs to look at the night sky in game once.
   - If the sky shows no phases, our calendar is safe. If it does, we read the engine's or drop the moon.
2. **Rewards.** Gold and regard as today's tasks, plus a memory line for the giver. Renown? An item (a named weapon
   from the band's leader)?
3. **Failure.** A unique quest that fails is gone for that campaign. Should it leave a memory with the village too
   ("nobody came")?
4. **Who offers it.** Through step 1 of the roadmap (the giver comes to the player, by letter or in person), or also
   found by asking around: a notable of the village speaks of the werewolf, and the lord gives the quest when the player
   raises it?
5. **How many at once.** One authored quest active at a time, or several?
