# Authored quests: a repertoire of multi-stage quests in JSON

DRAFT, 03/10/2026, revised the same day with Gabriel's rulings (no moon, exploration scenes, consequences, any
giver, one at a time). Increment 1 built 03/10/2026; branches, endings, flags and the tale editor built 04/10/2026
(see "Branches: the design as built").

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
   - No moon phase is invented: the game keeps none, and the sky would contradict us.

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
    "Three women of {village} have vanished since the harvest, each taken at night.",
    "Shepherds heard howling and saw something large moving at the edge of the forest.",
    "Nobody has seen the beast clearly."
  ],
  "knowers": {
    "locals_of:{village}": [
      "You believe a werewolf haunts the forest; your cousin swears she saw its eyes.",
      "Since the harvest, three women have gone missing, each taken at night."
    ],
    "{giver}": [ "You have heard the villagers' tale and do not know what to believe." ]
  },
  "on_complete": { "next": "keep_watch" }
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
| `choices` (optional) | Several ways out of a stage: see "Branches: the design as built". |

### The closed list of objectives (Inc 1: what the werewolf needs, plus `deliver_item`)

| Type | Verified by | Engine fact |
|---|---|---|
| `talk_to` `{hero}` | A CR conversation with that hero, opened after the stage began. | The conversation open event. |
| `talk_to_locals` `{place, count}` | That many distinct people of the place spoken to since the stage began. | Same. |
| `be_at` `{place, radius}` | The player's party within `radius` of the place. | `MobileParty.MainParty` position. |
| `keep_watch` `{place, radius, nights, chance}` | Nights spent near the place, at night, by the campaign clock; each night a roll may open the stage's scene. See below. | Position, `CampaignTime.IsNightTime`, a seeded roll. |
| `explore` `{scene}` | An exploration scene (below) that ran and closed, with its required findings recorded by the game. | The scene's own record. |
| `defeat_party` `{spawn}` | A party CR spawned for this quest beaten in battle by the player. | The battle end event, the party's id. Spawned with `TransientPartyRegistry` so it survives a save. |
| `deliver_item` `{item}` | The item handed over through the offer bag, matched as supply tasks are. | `SupplyItemMatcher`. |
| `return` | Back to the giver: `talk_to {giver}` with the reward settled. | As `talk_to`. |

Later types, one by one, each with its own check: `win_tournament`, `capture` (reuse the capture task),
`escort` (step 3 of the roadmap), `free_prisoner`, `wait_days`.

### Keeping watch (Gabriel, 03/10/2026: no moon)

- **No moon.** Bannerlord keeps no moon phases, and a phase we invented could disagree with the sky the player sees.
- **Nights spent waiting.** Instead, the player spends nights near the place, waiting.
  - `nights: [2, 5]` with `chance: 0.35`: from the 2nd night on, each night the scene may open; on the 5th it opens
    for certain, so a patient player is never left waiting forever.
  - The roll is seeded from the quest and the day, so a reload does not reroll it.
  - A night counts only if the player's party spends it within `radius`; leaving resets nothing, it simply does not
    count.
- **The journal says what to do,** never when it will happen: "Keep watch by the wood of {village} at night."

### Exploration scenes: the chat without a character (Gabriel, 03/10/2026)

A local exploration, played in the chat window with no one to talk to at first.

- **It opens on a description.** The model writes it from the scene's `opening` canvas and facts, in the second
  person: "Mud, and in it the prints of boots, many of them, leading deeper into the wood." Then it asks the player
  what they do.
- **The player says what they do, and the model narrates what follows,** as a narrator, never as a character: "You
  follow the tracks, keeping to the bracken..." It may describe freely (the wind, the cold, the player's own
  caution), but the things the player can FIND are the scene's `findings`, written in the file.
- **Findings are the game's, the moment is the model's.**
  - Each finding has an id. When the narration reveals one, the interpreter reports `scene_finding` with that id.
  - The game accepts only ids the scene declares, and records each once.
  - A finding marked `required` is what the stage needs.
  - If the player closes the scene before a required finding came up, the closing narration states it anyway, from
    the file, so a clue can be missed only in its manner, never in substance. "Whatever you did, you would have
    found this."
- **Meeting someone.** A scene may declare `encounters`: a bound hero, or a heroless person written in the file (an
  escaped girl, a lookout). When the narration brings them in, the interpreter reports `scene_encounter` with that id,
  and the game adds them to the conversation as a speaker, with their own knowledge from the file. From there it is an
  ordinary multi-party conversation, the narrator still present for what the player does.
- **A plain voice** (Gabriel, 03/10/2026). The narrator of a scene speaks plainly, whatever narrative style the player
  chose for conversations, so a finding reads as a finding. Characters met in the scene keep their own voice.
- **What the narration alone can never do:** complete a deed, invent a finding or a hazard, or kill the player.
  Everything with a cost goes through the scene's `hazards` (below), which the game carries out.
- **Built on what exists.**
  - The chat already runs scenes the player does not open on a person: the captive scenes have a director and a
    narration line.
  - Witnesses already join mid-conversation (`AddWitness`, the Invite button).
  - The new parts are a narrator speaker with no hero, and the two interpreter reports.

```json
"objective": {
  "type": "explore",
  "scene": {
    "opening": {
      "canvas": "Night by the wood, after waiting. The player notices something on the path; quiet, cold, watchful.",
      "facts": [ "Fresh boot prints in the mud lead from the village edge into the wood." ]
    },
    "findings": [
      { "id": "chains", "required": true, "text": "A clearing with a dropped iron collar and a length of chain." },
      { "id": "pelt", "required": true, "text": "A wolf pelt rigged on a wooden frame: the beast the shepherds saw." },
      { "id": "ruts", "required": false, "text": "Cart ruts leading out of the wood toward the road." }
    ],
    "encounters": [
      { "id": "escaped_girl", "name": "a girl from {village}", "persona": "barefoot, terrified, escaped from the slavers' camp two nights ago",
        "knows": [ "Men took her, not a beast. They keep the women in a camp further in and wait for a buyer from the coast." ] }
    ],
    "closing": "Whatever the player did, they leave the wood knowing it was men, not a beast, and that the women are being sold."
  }
}
```

### Hazards: a scene can go wrong (Gabriel, 03/10/2026)

A scene may hurt the player or turn into a fight. Each hazard is written in the file, and the game carries out its
effect. The model decides only when the player's own choices spring it, never whether it exists.

```json
"hazards": [
  { "id": "snare", "trigger": "pushing through the undergrowth off the track without care",
    "effect": { "type": "injure", "hit_points": [15, 30] } },
  { "id": "lookout", "trigger": "being seen or heard by the slavers' lookout near the camp",
    "effect": { "type": "ambush", "party": "slavers", "share": 0.5 } }
]
```

- **The report.** When the narration springs one, the interpreter reports `scene_hazard` with its id. The game accepts
  only ids the scene declares, each once.
- **Never out of nowhere.** The `trigger` is written into the narrator's prompt, so the danger follows from what the
  player chose to do. A careful player who keeps to the track never meets the snare.
- **Effects, a closed list:**

| Effect | What the game does | Notes |
|---|---|---|
| `injure` `{hit_points}` | Takes hit points from the player, never below 1. | `Hero.HitPoints`; a wounded player rests as after a battle. |
| `ambush` `{party, share}` | Closes the scene and starts a real battle against the quest's spawned party, or part of it. | `PlayerEncounter` with the spawned party; needs a spike. |
| `alert` `{party}` | The band is warned: it moves away or doubles its watch, making the hunt harder. | Changes the spawned party's behaviour. |
| `lose_time` `{hours}` | The night is lost; the scene ends at dawn with nothing more found. | Required findings still stated at closing. |
| `lose_item` / `gain_item` | A dropped purse, a found dagger. | Through the inventory; bounded value. |
| `wound_companion` `{hit_points}` | A companion who came along is hurt instead. | Only if one is in the scene. |

More can be added later, each with its own check. None of them kills the player or a companion.

### Encounters who become real (Gabriel, 03/10/2026)

An encounter is a person of the scene, written in the file. When the story needs them in the world, they become a real,
temporary character.

- **Example.** The player offers to escort the escaped girl back to the village. The interpreter reports
  `scene_materialize` with her id, and the game creates her as a hero:
  - a commoner of {village}'s culture, with a name drawn by the game;
  - she joins the player's party for the journey;
  - she keeps what she knows from the file as her memories, so she can be spoken to like anyone else.
- **New objective: `escort` `{who, to}`.** Reaching the place with her in the party completes the stage. She then
  stays there as a villager, with what happened as her memory. When the quest ends, she is retired: she leaves the
  party and is no longer offered anywhere.
- **Only when the story needs it.** A scene encounter who is never materialised stays in the scene and in the quest's
  memories.
- **A spike first.** Creating a hero mid-campaign (a template, a culture, a settlement), keeping her through a save,
  and retiring her cleanly. `escort` is also what step 3 of the roadmap (the prince) needs.

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

## Consequences and rewards (Gabriel, 03/10/2026: mostly regard, and consequences)

- **The reward is mostly regard,** on the mod's own ledger, with a memory line for the giver. Gold stays modest.
- **A quest should leave a mark on the world.** `consequences` is a closed list, each one carried out by the game:

| Consequence | What the game does | Status |
|---|---|---|
| `regard` `{who, amount}` | Moves a character's regard for the player. | exists |
| `memory` `{who, text}` | A memory line for a character, or for every notable of a place. | exists |
| `world_event` `{summary, magnitude, place}` | News that spreads (or `secret`, known to those named). | exists |
| `grudge` `{holder, against}` | A grudge between two characters (the Smoke quest's rival house). | needs a ruling: NPC-to-NPC grudges |
| `settlement` `{place, prosperity, loyalty, security}` | Small shifts to a place's state. | engine setters, to check |
| `war_averted` `{realm_a, realm_b, days}` | Holds off a declaration of war between two realms for a while. | needs a spike: where vanilla decides war |
| `peace_pressure` `{realm_a, realm_b}` | Raises the will for peace between two realms at war. | needs the same spike |

- **Failure has consequences too** (Gabriel: yes): a failed unique quest leaves a memory with the giver AND with the
  place ("nobody came"), and is gone for that campaign.

## Who offers a quest, and how many

- **Any character can offer one** (Gabriel, 03/10/2026: "peu importe si il est Lord ou non"): a lord, a notable, a
  gang leader, an artisan. `requires.giver.role` names which, and the quest's own facts say why that person.
- **The giver brings it** (step 1 of the roadmap: an audience, a road encounter, a letter that asks the player to come),
  or the player raises it with someone who has it to give.
- **One authored quest at a time** (Gabriel). Ordinary tasks run alongside it as today.
- **The journal shows it apart from a task** (Gabriel): its own look in the quest log, so the player sees at a glance
  that this is a story, not an errand. To check in the engine: what `QuestBase` lets a quest change (a special quest
  flag, a title colour, an icon).

## How the model is told (prompt side)

- **When.** A character with a role in an active authored quest gets one block, below the cache marker, only while
  that is so.
- **The giver, at each stage:** the stage's canvas, its facts, what the giver knows, and the rule: "say these in your
  own words; keep every fact; add none; do not speak of what you have not been told".
- **A knower:** only their knower lines, framed as what they believe, never as the plot.
- **The narrator of a scene:** the opening, the findings not yet revealed (as what exists to be found, not as a script),
  the encounters, and the limits above.
- **Never shown to anyone:** the stage list, later stages, the twist.
- **The interpreter learns only the two scene reports,** and only inside a scene. No verb completes a stage.

## The four drafts

In `docs/design/quests/`:

| File | Unique | Shape | The turn |
|---|---|---|---|
| `wolf_of_the_wood.json` | yes | a village, nights of watch, an exploration scene, a hunt | The werewolf is a slaving band (Gabriel's). |
| `smoke_over_the_hills.json` | yes | burned farms blamed on an enemy realm | The raiders wear the colours of a rival house of the giver's own realm. Ends on a choice in Inc 2 (branches); in Inc 1, report it and the giver acts. |
| `the_lost_banner.json` | repeatable, long cooldown | deserters carry off a house's banner | The deserters are not cowards: their captain sold them out. Bring the banner back, and the giver remembers the deserters' tale. |
| `the_bitter_well.json` | yes | a town falls sick | Not poison: grain sold rotten by a merchant. The gang leader knows who; the giver must hear it from the player. |

## Increments (revised again 03/10/2026, after the rulings on hazards and real encounters)

1. **The engine and the werewolf.**
   - The JSON loader, its report of authoring problems, and the stage machine.
   - Saving and loading an active quest.
   - The journal entry with its own look.
   - The objectives `talk_to`, `talk_to_locals`, `keep_watch`, `explore`, `defeat_party` and `return`.
   - The exploration scene: the plain narrator, findings, one encounter, and the hazards that need no spike
     (`injure`, `lose_time`, `alert`).
   - Rewards: regard and memories.
   - The werewolf playable end to end, with unit tests on every rule and a cr.testall that walks the stages.
2. **Offering and consequences.**
   - The giver comes to the player (step 1 of the roadmap).
   - Branches: built 04/10/2026, with endings, flags and the tale editor (below).
   - The spikes for `war_averted` and the NPC grudge.
   - `ambush` (a scene that turns into a real battle).
   - Encounters who become real, and the `escort` objective.
3. **For modders and players.** The Custom folder, cr_patch, HOWTO_Quests.md in Docs, and cr.quest_check.
   Done 04/10/2026: the Custom folder (read since Inc 1), `HOWTO_Quests.md` (ships in Docs), `cr.story_check [name]`
   (the check is `story_check`, not `quest_check`, which names the older tasks), and `weight` and
   `repeat_cooldown_days` honoured. Left: cr_patch for quest files.
4. **More quests.** A first lot of unique quests, drafted by Claude and corrected by Gabriel.

## Branches: the design as built (04/10/2026)

Gabriel, 04/10/2026: "With Twine you can give a quest several paths, like a choose-your-own-adventure book: the
player could decide whether to turn right or left, and depending on what happens the story takes another direction.
CR's quests must support that." Built on branch `quest-branches` from the goal in `docs/design/quest-editor/goal.md`;
every decision taken alone is in `docs/design/quest-editor/DECISIONS.md`. It answers the four questions the proposal
left open: some choices are said aloud (choices by words); the journal shows the ways out the author names; doing
nothing is a choice when the file says so (`wait`); the NPC-to-NPC grudge is still left for a ruling.

**Choices.** A stage may carry `choices` beside, or instead of, its objective. Each has an `id`, a `label`, an
optional `journal` line, a `next`, `if` (flags that must hold for it to be open) and `sets` (flags set when taken),
and may carry its own `canvas`, `facts` and `knowers`, said by the character in whose conversation it is taken. A
choice is taken one of three ways:
- **by an act**: an `objective` from the closed list (`talk_to`, `return`, `talk_to_locals`, `be_at`,
  `defeat_party`, and the new `wait`: days passing with no other way out taken). The first met wins and the others
  close. A choice cannot keep watch or open a scene; it leads to a stage that does.
- **in a scene**: in an `explore` stage, a choice with no objective. The narrator is told the open ones with their
  marker `[[choice:id]]` and writes it when the player's words take that path; only a declared, open marker counts,
  only the first. The scene then ends and the story follows it on closing. Closed with no choice, it follows the
  stage's `on_complete.next`, else its first `default` choice (which may not hang on a flag); one is required. Under
  each narration the player has not answered, the open ways are listed by number with "Something else..."
  (Gabriel: "like an LLM does in the CLI"); a click or a typed number sends the way's words, and only the narrator's
  marker moves the story.
- **by words**: `"said": {"to": "{rival}", "meaning": "..."}`, taken when the action interpreter reports, in a
  conversation with that character, that the player said what it means. Only the choices open in that conversation
  are taught, and only a declared, open id is accepted.

**Endings.** `next` may be `"end:<id>"`, an entry of the new `endings` map, each shaped like the reward (regard, gold,
memory, a last `journal` line, consequences). `"reward"` stays the single success ending, so every file written before
loads and plays unchanged. `failure` stays the time-out.

**Flags.** Named booleans, set by a choice, a finding or a met encounter, tested by `if` (`"name"`, or `"!name"` for
not set; a list holds when each does) on choices, knower lines, canvas and fact lines (a line may be
`{"text", "if"}`), and consequences. No arithmetic, no text.

**Secrecy.** No character and no narrator is told a choice still ahead, a closed choice, a later stage, or a line
whose flag does not hold. The character whose conversation ends the tale keeps speaking from it until it closes.

**The loader** refuses what could strand the player (a `next` that leads nowhere, a reachable stage that can never
reach an ending, a choice nobody can take, a scene with choices and no way out, the request offering choices) and
reports what is only wasted (a stage or ending no path reaches, a flag tested but never set, a default mark that does
nothing). Loops are allowed when they have an exit. One broken way out is reported once, never echoed.

**The {rival}** a tale may need: the head of another house of the giver's realm whom the player can find
(`requires.rival`, `AuthoredRivalPolicy`).

**Saves.** The run gains additive fields only (flags, the path taken, the ending, per-choice progress); a run saved
before reads with none and plays on.

**The tale editor** (`Docs/TaleEditor.html`): one offline page that opens, edits, checks, maps and saves tales, and
imports Twine stories. Its checks are a JavaScript copy of the loader held to it by a shared corpus of fixtures run
through both (C# directly, the editor's own code under Node); its fields come from a JSON Schema generated from the
C# model (`docs/design/quest-editor/quest.schema.json`), kept current by the Maps suite.

**Worked examples** (in `docs/design/quests/`, not shipped): `smoke_over_the_hills.json` (expose, confront, keep
silent; a confrontation then leads to the lord after all, a price for silence said aloud, or nothing) and
`the_bell_in_the_marsh.json` (a turn left or right on a causeway, a loop back to the watch, two scenes in a row).

## Still open

Nothing blocking Inc 1. Rulings of 03/10/2026, for the record:

1. The narrator speaks plainly, whatever the player's style.
2. A scene can hurt the player or turn into an ambush, through declared hazards, and richer consequences may follow.
3. An encounter can become a real, temporary character when the story needs it (escort the girl home).
