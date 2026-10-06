# How to write a quest for Calradia Remembers

A written tale is a JSON file: a quest in several stages, offered by a character, told to the
characters who know of it, and checked by the game at every step. You write the story, and the
model gives it voice. It rephrases your text in each character's words, but it cannot add a fact,
skip a stage or pay a reward. The game decides what counts.

No code and no build. Drop the file in a folder, and `cr.story_check` tells you what is wrong with it.

Written tales arrived in **2.6.12**. This guide covers what that version can play; section 9 lists
what is planned but not played yet.

---

## 1. Where the file goes

```
Modules/CalradiaRemembers/ModuleData/Quests/          the tales that ship with the mod
Modules/CalradiaRemembers/ModuleData/Custom/Quests/   yours: kept across updates, beside your other custom files
```

If you set a "Custom Folder Path" in the MCM, put them in its `Quests` folder instead. Files left in the older
`ModuleData/Quests/Custom/` are still read.

Every `*.json` file there is read when a campaign starts. One file holds one quest, or an array of
quests. If two files define the same `id`, the first one read wins and the other is reported.

The tales are experimental in 2.7.0 and OFF by default: turn on "Authored Quests (experimental)" in the
MCM to have them offered. Off stops new offers only: a tale
already under way plays to its end.

## 2. Checking your file

| Command | What it does |
|---|---|
| `cr.story_check` | Reloads every quest file, then lists what loaded and every problem found, in plain words. |
| `cr.story_check <name>` | The same, then says for each quest whether that character could give it now, and if not, why ("their regard for you is -5, and the quest needs at least 0"). |
| `cr.story_start <quest id> [name]` | Has that character give the quest now, without the dice. If they cannot, it says which condition failed. |
| `cr.story_status` | The tale under way: its stage, who and what it is bound to, the locals heard, the nights watched, the findings. |
| `cr.story_step` | Completes the current stage as if its objective were met. Use it to walk your stages. |
| `cr.story_abandon` | Ends the tale under way, without reward or failure. |

The problems come in two kinds:

- **Blocking.** The quest cannot be played safely, so it is not loaded. Examples: a stage that leads
  nowhere, stages that loop without ever reaching the reward, an objective the game cannot check, or
  a scene with nothing required to find.
- **Cosmetic.** The quest loads, and the report says what will go wrong. Examples: a `{placeholder}`
  that no role binds (the player would read it as written), or a consequence type the game does not
  carry out yet (it is skipped).

The log (`CalradiaRemembers.log`) carries every step under `[TALE]`.

## 3. The skeleton

```json
{
  "id": "mymod_the_missing_tithe",
  "title": "The Missing Tithe",
  "author": "you",
  "unique": true,
  "weight": 10,
  "requires": { ... },
  "binds":    { ... },
  "stages":   [ ... ],
  "reward":   { ... },
  "failure":  { ... }
}
```

| Field | Meaning |
|---|---|
| `id` | Unique across every file. Prefix it with your mod's name. |
| `title` | Shown in the journal as "Tale: <title>". |
| `unique` | `true` (the default): played once per campaign, won or lost. `false`: it may come back. |
| `repeat_cooldown_days` | For `unique: false`: how many days after it ended before it may be offered again. |
| `weight` | When a character could give several quests, they are chosen in proportion to their weights. A weight of 0 is never offered. Default 10. |

The rules around every offer:
- Only one tale runs at a time.
- A qualifying character raises a quest in about one conversation out of three.
- A quest the player declined is not raised again for a season.
- A character who already has a task for the player (a vanilla quest offered or under way) does not raise a tale too.

## 4. Who gives it: `requires` and `binds`

```json
"requires": {
  "giver":   { "role": "lord", "holds": "{village}", "min_regard": 0, "not_at_war_with_player": true },
  "village": { "type": "village", "near": "forest" },
  "player":  { "min_clan_tier": 0 }
},
"binds": {
  "giver":   "the lord who holds {village}",
  "village": "a village beside a forest"
}
```

| Field | Meaning |
|---|---|
| `giver.role` | `lord`, `notable`, `gang_leader`, or `any`. |
| `giver.holds` | `"{village}"`: the village is picked among the giver's clan villages. Without it, the giver's home settlement is used, when it is a village. |
| `giver.min_regard` | The giver's regard for the player on Calradia Remembers' own ledger (the one built in conversation, not the game's relation). |
| `giver.not_at_war_with_player` | Default `true`. |
| `village.near` | `"forest"`, or omit it. |
| `player.min_clan_tier` | 0 to 6. |

When the quest is offered, each role is bound to a real hero or place, and every `{role}` in your
text is filled with its name. In 2.6.12 the roles that bind are `{giver}` and `{village}`. List
every role you use under `binds`, with a short description for the reader of the file.

## 5. Stages

```json
{
  "id": "question_the_village",
  "journal": "Ask the people of {village} what they have seen.",
  "objective": { "type": "talk_to_locals", "place": "{village}", "count": 2 },
  "canvas": "The villagers are frightened and half ashamed of their fear.",
  "facts": [ "Three women have vanished since the harvest." ],
  "knowers": {
    "locals_of:{village}": [ "You believe a werewolf haunts the wood." ]
  },
  "on_complete": { "next": "keep_watch" }
}
```

| Field | Meaning |
|---|---|
| `journal` | The line logged in the quest journal when the stage begins. |
| `objective` | What the game checks (section 6). |
| `canvas` | How the moment feels. Spoken by the **giver**, when this stage is theirs to say. |
| `facts` | What the giver must convey, without changing it. |
| `knowers` | Lines for other characters, framed as what they believe at this stage. The key is a role (`"{giver}"`) or `"locals_of:{place}"` (the people of that place: its notables, and the villagers met in its streets). Only notables count toward `talk_to_locals`. |
| `on_complete.next` | The next stage's `id`, or `"reward"` to end the tale in success. |

What a character is told is gated by the stage. A character knows only their own lines, and only
while the stage lasts. Nobody is ever told what comes next, so a twist stays hidden until the
player uncovers it. Give the locals lines on every stage where they should still talk about the
matter. Otherwise they know nothing of it and answer from what they really know of their land.

The character whose conversation completes a stage keeps speaking from that stage until the
conversation ends: the lord who hears your report, or the villager who answered the last question.

Characters never lie. If a character must not tell, write what they believe, and leave the rest
unsaid.

## 6. Objectives

| `type` | Done when | Fields |
|---|---|---|
| `talk_to` | The player opens a conversation with that hero. | `hero`: a role, `"{giver}"` |
| `return` | The player speaks with the giver again. | none |
| `talk_to_locals` | The player has spoken with `count` different notables of the place. | `place`, `count` |
| `be_at` | The player's party comes within `radius` of the place. | `place`, `radius` (default 5) |
| `keep_watch` | The player waits nights near the place. A scene opens on a night between the first and last of `nights`: from the first night it is a roll of `chance` per night, and the last night is certain. The night opens the stage that follows, which should be an `explore`. | `place`, `radius`, `nights: [first, last]`, `chance` (0 to 1) |
| `explore` | A scene in the chat, with a narrator and no character (section 7). Done when the player closes it. | `scene` |
| `defeat_party` | A band spawned near the place is destroyed by the player or the player's army. If someone else destroys it, it regroups. | `spawn` |

A night counts once, read an hour past midnight, with the player's party inside the radius. Waiting inside the
village counts; say so in the stage's journal line, since players will wander off into the wood otherwise. The tale's
village is marked on the map, as a vanilla quest marks its destination.

`spawn`:

```json
"spawn": { "id": "slavers", "template": "looters", "size": [12, 20],
           "name": "Slavers of the {village} wood", "near": "{village}", "flee_after_days": 20 }
```

The band patrols around its place. It is spawned again after a load, and it is taken off the map
however the tale ends. `flee_after_days` fails the tale if the band is still alive that many days
after the stage began. A band warned by an `alert` hazard is a third larger.

## 7. Exploration scenes

```json
"objective": { "type": "explore", "scene": {
  "opening":    { "canvas": "Another cold night by the wood...", "facts": [ "..." ] },
  "findings":   [ { "id": "chains", "required": true,  "text": "A dropped iron collar and a chain." },
                  { "id": "ruts",   "required": false, "text": "Cart ruts toward the coast road." } ],
  "hazards":    [ { "id": "snare", "trigger": "pushing through the undergrowth without care",
                    "effect": { "type": "injure", "hit_points": [15, 30] } } ],
  "encounters": [ { "id": "girl", "name": "a girl from {village}", "persona": "barefoot, shivering...",
                    "knows": [ "Men took her, not a beast." ] } ],
  "closing": "Whatever the player did, they leave the wood knowing it was men."
} }
```

The narrator speaks plainly, in the second person, and ends each reply by asking what the player
does. It reveals a finding, springs a hazard or brings in an encounter only when the player's own
actions lead there. It may not invent any of the three.

| Part | Meaning |
|---|---|
| `findings` | What there is to discover. At least one must be `required`. A required finding the player never reached is told when the scene closes: a clue can be missed only in how it is found, never altogether. |
| `hazards` | What the player's choices can spring. `trigger` says what the player must do. Effects: `injure` (`hit_points: [min, max]`, never below 1 HP), `alert` (the stage's band will be larger), `lose_time` (the scene ends, the night is lost). Each hazard springs once. |
| `encounters` | People who may be met in the scene, voiced by the narrator, who say only what they `know`. |
| `closing` | Shown when the scene closes, after any missed findings. |

## 8. Reward, failure and consequences

```json
"reward": {
  "regard": 12, "gold": 500,
  "memory": "The player unmasked the 'werewolf' of {village}.",
  "consequences": [
    { "type": "memory", "who": "locals_of:{village}", "text": "A stranger ended the wolf of the wood." },
    { "type": "world_event", "summary": "The 'werewolf' of {village} was a band of slavers.", "magnitude": 35, "place": "{village}" }
  ]
},
"failure": { "days": 40, "regard": -3, "memory": "I asked for help and nothing came of it." }
```

| Field | Meaning |
|---|---|
| `regard` | Added to the giver's regard for the player (negative on a failure takes it away). |
| `gold` | Paid to the player by the game (positive amounts only). |
| `memory` | What the giver remembers, in the giver's own voice. |
| `consequences` | `memory`: `who` (a role or `locals_of:{place}`) remembers `text`. `world_event`: news that spreads through the realm (`summary`, `magnitude` 0 to 100, `place`). |
| `failure.days` | The tale fails that many days after it was accepted. |

## 9. Read but not played yet

These fields are accepted, so files written for later versions still load, but 2.6.12 does
nothing with them:
- `may_materialize`, `materialize_when` and `escort_to` on an encounter (a met person who becomes a
  real companion to escort);
- `behavior` on a spawn (every band patrols around its place);
- `party` on an `alert` effect;
- `hours` on a `lose_time` effect.

Planned for later versions: branches (several ways out of a stage), the giver seeking out the player,
ambushes that turn a scene into a battle, escorts, and more consequence types.

## 10. A full example

`Modules/CalradiaRemembers/ModuleData/Quests/wolf_of_the_wood.json` is a complete tale: a request, a
village to question, nights of watch, a scene in the wood, a report, a band to destroy, and the
return. Copy it into `ModuleData/Custom/Quests/`, change its `id`, and make it yours.
