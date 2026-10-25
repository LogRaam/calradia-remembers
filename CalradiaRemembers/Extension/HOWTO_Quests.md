# How to write a quest for Calradia Remembers

A written tale is a JSON file: a quest in several stages, offered by a character, told to the
characters who know of it, and checked by the game at every step. You write the story, and the
model gives it voice. It rephrases your text in each character's words, but it cannot add a fact,
skip a stage or pay a reward. The game decides what counts.

A tale can branch. A stage may offer choices, like a choose-your-own-adventure book: the player
turns left or right, tells the lord or keeps silent, and the story takes another direction. Simple
flags remember what happened, and a tale may end one of several ways.

No code and no build. Write the file by hand, or with the **tale editor** (section 12), drop it in
a folder, and `cr.story_check` tells you what is wrong with it.

Written tales arrived in **2.6.12**; choices, endings and flags came with the tale editor. Section 11
lists what is read but not played yet.

---

## 1. Where the file goes

```
Modules/CalradiaRemembers/ModuleData/Quests/          the tales that ship with the mod
Modules/CalradiaRemembers/ModuleData/Custom/Quests/   yours: kept across updates, beside your other custom files
```

If you set a "Custom Folder Path" in the MCM, put them in its `Quests` folder instead. Files left in the older
`ModuleData/Quests/Custom/` are still read.

**Shipping tales with your own mod.** Put them in your module's own `ModuleData/cr_quests/` folder, one
sub-folder per tale if it has pictures. Your players copy nothing, an update of Calradia Remembers never
touches them, and disabling your mod in the launcher takes its tales away with it:

```
Modules/YourMod/ModuleData/cr_quests/the_bell_in_the_marsh/the_bell_in_the_marsh.json
Modules/YourMod/ModuleData/cr_quests/the_bell_in_the_marsh/sunken_chapel.jpg
```

Every enabled mod's `cr_quests` is read, in the launcher's order, after the folders above. In
`cr.story_check` a tale from another mod is named with its mod first (`YourMod: the_bell_in_the_marsh/...`).
Give your ids a prefix of your own (`yourmod_bell`): an id already defined by Calradia Remembers, by the
player's own files or by a mod read earlier wins, and yours is reported and left out.

Every `*.json` file there is read when a campaign starts. One file holds one quest, or an array of
quests. If two files define the same `id`, the first one read wins and the other is reported.

A tale with images is a folder of its own, its JSON beside its pictures (one level down is read too):

```
ModuleData/Custom/Quests/wolf_of_the_wood/tale.json
ModuleData/Custom/Quests/wolf_of_the_wood/tracks_at_dusk.jpg
```

The tales are experimental in 2.7.0 and OFF by default: turn on "Authored Quests (experimental)" in the
MCM to have them offered. Off stops new offers only: a tale
already under way plays to its end.

## 2. Checking your file

| Command | What it does |
|---|---|
| `cr.story_check` | Reloads every quest file, then lists what loaded and every problem found, in plain words. |
| `cr.story_check <name>` | The same, then says for each quest whether that character could give it now, and if not, why ("their regard for you is -5, and the quest needs at least 0"). |
| `cr.story_start <quest id> [name]` | Has that character give the quest now, without the dice. If they cannot, it says which condition failed. |
| `cr.story_status` | The tale under way: its stage, who and what it is bound to, the locals heard, the nights watched, the findings, the flags set, the path taken and the choices open. |
| `cr.story_step` | Completes the current stage as if its objective were met. On a stage with no objective, takes its first open choice. |
| `cr.story_choose <choice id>` | Takes that open choice of the current stage, however it would be taken. Use it to walk each path. |
| `cr.story_abandon` | Ends the tale under way, without reward or failure. |

The problems come in two kinds:

- **Blocking.** The quest cannot be played safely, so it is not loaded. Examples: a stage or a
  choice that leads nowhere, a path that can never reach an ending, an objective the game cannot
  check, a choice nobody could take, or a scene with nothing to find and no way out.
- **Cosmetic.** The quest loads, and the report says what will go wrong. Examples: a `{placeholder}`
  that no role binds (the player would read it as written), a consequence type the game does not
  carry out yet (it is skipped), a stage or an ending no path reaches, or a flag tested but never set.

The tale editor shows the very same problems, in the same words, as you write.

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
  "endings":  { ... },
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
  "rival":   { "role": "clan_leader", "same_realm_as": "{giver}" },
  "player":  { "min_clan_tier": 0 }
},
"binds": {
  "giver":   "the lord who holds {village}",
  "village": "a village beside a forest",
  "rival":   "the head of another house of the giver's realm"
}
```

| Field | Meaning |
|---|---|
| `giver.role` | `lord`, `notable`, `gang_leader`, `spouse` (the player's own husband or wife), `companion` (one of the player's companions, never kin; since 2.7.17), or `any`. A tale of someone riding with the player who "left their father's farm" wants `companion` with `with_player`, so a spouse or a child in the party is not asked. |
| `giver.holds` | `"{village}"`: the village is picked among the giver's clan villages. Without it, the giver's home settlement is used, when it is a village. |
| `giver.min_regard` | The giver's regard for the player on Calradia Remembers' own ledger (the one built in conversation, not the game's relation). |
| `giver.not_at_war_with_player` | Default `true`. |
| `giver.culture` | The giver's culture: one id (`"battania"`) or a list (`["battania", "sturgia"]`). The culture's name works too, in any case. Omit it for any culture. |
| `giver.realm` | The giver's realm: one id (`"vlandia"`) or a list, or the realm's name, in any case. The three empires share one culture, so only this tells them apart: `empire` (north), `empire_w` (west), `empire_s` (south). A giver with no realm never matches. Omit it for any realm. Since 2.7.17. |
| `giver.gender` | `female` or `male`: the giver must be a woman, or a man. Omit it for either. A tale for "the player's wife" is `"role": "spouse", "gender": "female"`, whatever the player's own sex. |
| `giver.with_player` | `true`: the giver must be travelling in the player's own party. |
| `giver.not_pregnant` | `true`: never while the giver is with child. |
| `giver.married` | `true`: the giver is married, to someone other than the player. Needed by `husband`. |
| `giver.not_clan_leader` | `true`: never a giver who leads her house (she could not leave it). |
| `giver.not_of_players_realm` | `true`: never a giver of the player's own realm. |
| `giver.trade` | A notable's trade: one (`"merchant"`) or a list (`["merchant", "artisan"]`) of `merchant`, `artisan`, `ruralnotable`, `headman`, `preacher`. Only a notable plies one, so the role must be `notable` (or `any`). Omit it for any. Since 2.7.19. |
| `husband` | The giver's own husband (or wife), bound as `{husband}`: `"husband": { "role": "spouse_of_giver" }`. He must be alive and in the world. `"rules_a_realm": true` asks that he rule a realm. A choice may be `said` to `{husband}`, as to `{giver}` or `{rival}`. |
| `village.type` | `village` (the default) or `town` (since 2.7.19): the place the tale is bound to, still written `{village}` in its lines. With `"holds": "{village}"` it is one of the giver's house's towns; without, the giver's home, when it is a town (a town's merchant, artisan or preacher). A town under siege is never chosen. |
| `village.near` | `"forest"`, or omit it. |
| `village.culture` | The village's culture, written the same way. A tale with `"holds": "{village}"` then looks among the giver's villages for one of that culture. |
| `rival` | A second lord, bound as `{rival}`: the head of another house of the giver's own realm, someone the player can find. Never the giver's house or the player's. A giver with no realm has no rival, so the tale is not offered by them. |
| `player.min_clan_tier` | 0 to 6. |
| `player.gender` | `female` or `male`: the tale is offered only to a woman, or a man. Omit it for either. Since 2.7.19. |
| `content` | The content level the tale needs, as the mod options name it: `none` (the default, every player), `mature`, `explicit` or `hardcore`. The tale is offered only when the player's setting reaches it, and a tale under way stops, without reward or penalty, if the player lowers the setting below it. Any other word refuses the tale. |

### A tale that begins on the map: `on_map`

```json
"requires": { "giver": { "role": "spouse", "gender": "female", "with_player": true, "not_pregnant": true } },
"on_map": {
  "still_hours": 3, "by_day": true, "in_givers_lands": true, "chance_per_day": 0.15,
  "invitation": "While your people make camp, {giver} has slipped away toward the sound of falling water.",
  "accept": "Go to her", "decline": "Leave her to her solitude"
}
```

Such a tale is never offered in a conversation. Each hour the game counts how long the player's party has stood in
the same place, out in the open (never in a settlement, a battle or at sea). Once a day, when a tale could begin
with someone travelling in the party (the giver must, so `requires.giver.with_player` is required), the dice are
cast once for all such tales: the tale begins on `chance_per_day`. A popup shows `invitation`; yes accepts the
tale, its first stage being the invitation itself, and opens the conversation with the giver, the player having
come to find them. No costs nothing, and the tale may come back after a season. A unique tale is played once.

| Field | Meaning |
|---|---|
| `still_hours` | Hours the party must have stood still. Default 3. |
| `by_day` | Default `true`: only by day. |
| `in_givers_lands` | Default `true`: only when the nearest settlement is of the giver's own culture. |
| `chance_per_day` | Above 0, at most 1. Default 0.15. |
| `invitation`, `accept`, `decline` | The popup's text and its two answers. `{giver}` is filled. |

### A tale a scenario opens: `on_scenario` (since 2.7.21)

```json
"on_scenario": {
  "invitation": "The bolt slides back. A lady stands in the torchlight: {giver} asks to see you.",
  "accept": "Follow her", "decline": "Stay in your cell"
}
```

Such a tale is never offered in a conversation nor begun on the map: a scenario rule opens it with `OPEN_TALE:<tale
id>,<giver>` (EXTENSION_API.md, scenarios), naming who gives it. The tale's own `requires` still hold for that giver
(her regard, her sex, the content level), as do `unique` and `repeat_cooldown_days`, and nothing opens while another
tale is under way. A popup shows `invitation`; yes begins the tale, its first stage being the invitation itself, and
opens the conversation with the giver; no costs nothing for a season. A tale has `on_map` or `on_scenario`, not both.

When the quest is offered, each role is bound to a real hero or place, and every `{role}` in your
text is filled with its name. The roles the game binds are `{giver}`, `{village}` (with
`requires.village`) and `{rival}` (with `requires.rival`). List every role you use under `binds`,
with a short description for the reader of the file.

`{player}` is the player's name, filled in every tale (since 2.7.9) with no `binds` entry. Use it
where the words are the character's own, above all in memories: a memory saying "The player found
them" reads out of character in the memories panel, and the character may repeat it. Text the
model reads as instructions (canvases) may keep saying "the player", as CR's own prompts do.

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
| `objective` | What the game checks (section 6). A stage with choices may have none. |
| `canvas` | How the moment feels. Spoken by the **giver**, when this stage is theirs to say. One line, or a list of lines (section 9). |
| `facts` | What the giver must convey, without changing it. |
| `knowers` | Lines for other characters, framed as what they believe at this stage. The key is a role (`"{giver}"`) or `"locals_of:{place}"` (the people of that place: its notables, and the villagers met in its streets). Only notables count toward `talk_to_locals`. |
| `choices` | Ways out of the stage (section 7). |
| `on_complete.next` | Where the story goes when the objective is met: the next stage's `id`, `"reward"` to end the tale in success, or `"end:<id>"` for a named ending (section 8). |

What a character is told is gated by the stage. A character knows only their own lines, and only
while the stage lasts. Nobody is ever told what comes next, not even the choices still ahead, so a
twist stays hidden until the player uncovers it. Give the locals lines on every stage where they
should still talk about the matter. Otherwise they know nothing of it and answer from what they
really know of their land.

The character whose conversation completes a stage keeps speaking from that stage until the
conversation ends: the lord who hears your report, or the villager who answered the last question.
The same holds for the conversation that ends the tale: the lord speaks from it while you are
still talking to him.

Characters never lie. If a character must not tell, write what they believe, and leave the rest
unsaid.

The first stage is the request itself: it is completed when the player accepts the tale in the
conversation where it is offered, so it cannot offer choices. Put them on the next stage.

### A stage's image

`"image": "tracks_at_dusk.jpg"` on a stage names a picture of the tale's own folder (`.png` or `.jpg`, a bare
file name). When that stage's scene opens, or a conversation that IS that stage begins (the character an objective
names, the one a choice is said to, the one offering the tale for its first stage, or a villager of the place a
questioning turns on), the picture opens by
itself in the panel left of the chat, drawn whole and never stretched. Any shape works: a wide 4:3 or 16:9 view,
a tall 2:3 figure, a square; the panel fits the picture at its own proportions. A scene's findings, hazards and people met may
carry an `"image"` too: it opens when the narration reveals, springs or brings them in (a person met shows over a
danger, a danger over a finding). A name with a folder in it, another format, or a file that is not
there is reported by `cr.story_check` and the scene opens without it. In the Tale Editor, pick the file beside the
stage's Image field to fill its name and see it.

In a scene with nobody in it (an exploration), the picture stands large beside the chat; in a conversation it is small,
in the top-left corner, so the character stays in view. A click on it enlarges it, another shrinks it.

**A picture by realm** (since 2.7.10): an image name may carry `{giver_realm}`, the game id of the giver's realm
(`vlandia`, `empire`, `aserai`...): `"image": "queen_{giver_realm}.jpg"` shows `queen_vlandia.jpg` when the tale is
played in Vlandia. Put one file per realm in the tale's folder; a realm with no file shows no picture.
`{giver_realm}` in a line of text names the realm.

**A new picture each time** (since 2.7.21): `"image_pool": ["night_1.crvid", "night_2.crvid", "night_3.crvid"]` on a
stage gives several pictures or videos. Each time the tale is played, one the player has not seen yet is chosen, and it
holds for the whole tale; once every one was seen, they start over. It stands in for `"image"`. For a tale that comes
back (`"unique": false` with a `repeat_cooldown_days`).

**A picture that moves** (since 2.7.9): the image may be a video, a `.crvid` (CR's own format: the frames as JPEG in a
zip, with a small manifest). Its first frame IS its picture. It plays once where the picture would show, then rests on its
first frame; PLAY, STOP and LOOP (on/off) sit at its bottom-right corner. On the Classic skin the first frame shows still.
Make one from any video with `python Tools/Video/make_crvid.py clip.mp4 clip.crvid` (it needs ffmpeg): the frame is
fitted within 768x1152 (the largest the game plays), at 30 frames a second by default. Keep clips short: a few seconds
at full size is tens of megabytes. For a slow zoom over a still picture, do not use ffmpeg's `zoompan` filter as is: it
rounds the frame to whole pixels and the picture trembles; zoom with sub-pixel precision instead.

## 6. Objectives

| `type` | Done when | Fields |
|---|---|---|
| `talk_to` | The player opens a conversation with that hero. With `"when": "closed"` (since 2.7.21), the stage completes when that conversation ENDS, so everything said in it counts first (the regard the giver gained or lost, for `regard_rose`). | `hero`: a role, `"{giver}"` or `"{rival}"`; `when`: `opened` (the default) or `closed` |
| `return` | The player speaks with the giver again. | none |
| `talk_to_locals` | The player has spoken with `count` different notables of the place. | `place`, `count` |
| `be_at` | The player's party comes within `radius` of the place. | `place`, `radius` (default 5) |
| `keep_watch` | The player waits nights near the place. A scene opens on a night between the first and last of `nights`: from the first night it is a roll of `chance` per night, and the last night is certain. The night opens the stage that follows, which should be an `explore`. | `place`, `radius`, `nights: [first, last]`, `chance` (0 to 1) |
| `explore` | A scene in the chat, with a narrator and no character (section 10). Done when the player closes it. | `scene` |
| `defeat_party` | A band spawned near the place is destroyed by the player or the player's army. If someone else destroys it, it regroups. | `spawn` |
| `wait` | That many days pass with no other way out taken. On a choice, it is how "doing nothing" is chosen. | `days` |
| `war_ends` | The war between that person's realm and the player's is over: it was seen during this stage, and now the two are at peace. A stage begun before its war is declared waits for the war first. | `hero`: a role |
| `dies` | That person is no longer in the world (dead, or taken out of it by the game). A captive is still in the world. | `hero`: a role |
| `weds_player` | That person is married to the player. | `hero`: a role |
| `bring` | The player hands that person the goods, with the bag in a conversation with them. | `item`, `count` (default 1, at most 200), `to`: a role (`{giver}` when left out) |

The last three are ways out the world takes, not the player: read once a day, on a stage or a choice. They let a
stage with no time limit end however the world turns, which matters since only one tale runs at a time: a war stage
nothing could leave would block every other tale. Since 2.7.17.

**Goods asked: `bring`** (since 2.7.19). `"objective": { "type": "bring", "item": "grain", "count": 30 }` asks the
player for 30 grain for the giver. The player hands them over with the bag (the chat's give button) in a conversation
with `to`: the bag opens on the party's goods that answer, he picks them, and on his confirmation exactly `count` leave
the party, never more; then the stage is done, or the choice taken. Short of the count, nothing leaves and the tale
waits. Merely talking to the giver with the grain in the baggage takes nothing. When the conversation opens, the chat
reminds the player that the goods go over with the bag; say it in the stage's journal line too. A hand-over written in
the chat ("here are your 30 sacks") counts as well: the player is asked "Hand 30 grain to ..., as they asked?" and, on
his yes, exactly `count` leave as with the bag; short of the count, nothing leaves and he is told why.

`item` names the goods as the game does:
- a kind: `horses` (riding and war horses, not pack animals), `livestock` (cows, sheep, hogs...), `grain` (also
  `food`, `wheat`);
- an item category: `wine`, `iron`, `tools`, `salt`, `fish`, `beer`, `oil`, `linen`... Any item of the category
  counts. The game's trade goods carry one item each, named as their category;
- one item, by its id (`sumpter_horse`), when no kind or category says it.

The checker cannot know the game's items: a misspelt name is found when the game loads the tale. It is then listed by
`cr.story_check`, and the tale is never offered.

A night counts once, read an hour past midnight, with the player's party inside the radius. Waiting inside the
village counts; say so in the stage's journal line, since players will wander off into the wood otherwise. The tale's
village is marked on the map, as a vanilla quest marks its destination.

A scene opens whenever the story reaches its stage: right away, or when the conversation that led
there ends.

`spawn`:

```json
"spawn": { "id": "slavers", "template": "looters", "size": [12, 20],
           "name": "Slavers of the {village} wood", "near": "{village}", "flee_after_days": 20 }
```

The band patrols around its place. It is spawned again after a load, and it is taken off the map
when the story leaves its stage, however it leaves. `flee_after_days` fails the tale if the band is
still alive that many days after the stage began. A band warned by an `alert` hazard is a third
larger. `"template": "local"` makes it of the region's bandits (those of the nearest hideout), with a share of seasoned
men that follows how seasoned the player's own troops are. With `"scale_with_player": true` the band grows with the player's healthy men (about six in ten of
them, never below its own `size`, at most 120), so the last fight stays a fight for a lord with an army.

**If the player loses to the band.** By default a lost fight changes nothing: the band is still there, and the player
may try again until `flee_after_days` or the tale's `failure.days` run out. `"on_defeat"` makes the defeat a moment
of the tale:

```json
"spawn": { "id": "slavers", "near": "{village}", "size": [12, 20],
           "on_defeat": {
             "journal": "The slavers threw you back. They are still in the wood near {village}.",
             "memory": "The player went against the slavers of {village} and was thrown back.",
             "consequences": [ { "type": "memory", "who": "locals_of:{village}",
                                 "text": "The lord's champion came back beaten. The slavers are still out there." } ] } }
```

Without `"next"`, the tale goes on: the journal line, the giver's `memory` and `regard`, and the `consequences`
(memory, world_event, grudge, as in an ending) are carried out the first time the player loses on this stage, never
twice; the band stays to be beaten. With `"next": "end:<id>"`, a defeat ends the tale on that named ending (it must be
in `"endings"`; a stage is refused, since the band leaves with its stage). The Wolf of the Wood goes on after a defeat.

## 7. Choices: a story with several paths

A stage may carry `choices` instead of (or as well as) a single objective. Each choice is one way
out, and the first one taken wins: the others close.

```json
{
  "id": "what_to_do",
  "journal": "You know who burned {village}. What you do with it is yours.",
  "choices": [
    { "id": "expose", "label": "Lay the surcoat before {giver}", "journal": "Tell {giver}.",
      "objective": { "type": "talk_to", "hero": "{giver}" }, "next": "end:exposed" },
    { "id": "confront", "label": "Take it to {rival} himself", "journal": "Or confront {rival}.",
      "objective": { "type": "talk_to", "hero": "{rival}" }, "sets": "confronted", "next": "the_rival_knows" },
    { "id": "silence", "label": "Keep it to yourself", "journal": "Or keep it to yourself.",
      "objective": { "type": "wait", "days": 10 }, "next": "end:silence" }
  ]
}
```

| Field | Meaning |
|---|---|
| `id` | Unique in its stage. A choice made in a scene uses only letters, digits and `_`. |
| `label` | What the player can do, in plain words. In a scene, it is what the player is shown and what the narrator is told. |
| `journal` | A line added to the journal under the stage's own, while the choice is open. |
| `next` | Where it leads: a stage, `"reward"`, or `"end:<id>"`. |
| `if` | Flags that must hold for the choice to be open (section 9). A closed choice is never shown, never told and never taken. |
| `sets` | Flags set when the choice is taken. |
| `default` | In a scene: taken when the scene closes with no choice made, if the stage has no `on_complete.next`. |
| `canvas`, `facts`, `knowers` | What is said in the conversation that takes the choice, by the character in it: the lord who hears the truth, the rival shown the proof. Without them, that character speaks from the stage. |

**Give a choice stage its own `knowers`.** While a stage with choices is under way, its characters know only
that stage's `knowers` (and `canvas`/`facts` for the one being spoken to), not the earlier stages'. A giver
whose stage forgets to say "You asked the player to seek the elders' counsel" answers "The elders? Why do you
ask?" Restate, in each choice stage, what each character there must still know. (Thank you, tashmetu.)

A choice is taken in one of three ways:

- **By an act**: it carries an `objective`: `talk_to`, `return`, `talk_to_locals`, `be_at`,
  `defeat_party` or `wait`, or one the world takes (`war_ends`, `dies`, `weds_player`). The first one met wins. (A choice cannot keep watch or open a scene:
  lead it to a stage that does.) A choice's band does not flee; it stays until the stage ends.
- **In a scene**: in an `explore` stage, a choice with no objective is a turn the player takes in
  the scene (section 10).
- **By words**: a choice with `"said": { "to": "{rival}", "meaning": "the player agrees to keep
  silent for a price" }` is taken when, in a conversation with that character, the player says or
  agrees to what it means. Only the choices open in that very conversation are listened for. `to`
  is `{giver}` or `{rival}`.

Whatever way a choice is taken, the game decides: the model never takes one by saying so.

Loops are allowed when there is a way out of them: the player may turn back to an earlier stage as
often as they like, as long as some path still leads to an ending.

**Stages as states of one conversation.** Stages whose choices are all said to the same character can be
moments of a single conversation that the player moves between and back by what is said: each stage a state
with its own `canvas` and `image`, each linking to the others. The character speaks the `canvas` of the stage
they are in; when words move the tale to another such stage, they speak from the new one, and its picture
replaces the one beside the chat (a stage with no image keeps the one shown). A choice that carries its own
`canvas`, `facts` or `knowers` is spoken from instead, as for any choice.

## 8. Endings, reward, failure and consequences

```json
"endings": {
  "exposed": {
    "regard": 8, "gold": 600,
    "memory": "The player found that {rival}'s riders burned {village}, and brought me the truth.",
    "journal": "You laid the truth before {giver}.",
    "consequences": [
      { "type": "world_event", "summary": "Riders of {rival}'s house burned the farms of {village}.", "magnitude": 45, "place": "{village}" },
      { "type": "memory", "who": "{rival}", "text": "The player came to me first, then went to {giver} all the same.", "if": "confronted" }
    ]
  },
  "silence": { "regard": -3, "memory": "I asked the player for proof; none came." }
},
"reward": { "regard": 12, "gold": 500, "memory": "The player unmasked the 'werewolf' of {village}." },
"failure": { "days": 40, "regard": -3, "memory": "I asked for help and nothing came of it." }
```

A path leads to a named ending with `"next": "end:<id>"`. `"reward"` is the single success ending
that every tale written before endings existed leads to; it is still the one `"next": "reward"`
reaches. `failure` is what the tale leaves when its time runs out before any ending is reached.

| Field | Meaning |
|---|---|
| `regard` | Added to the giver's regard for the player (negative takes it away). |
| `gold` | Paid to the player by the game. Negative (since 2.7.19): the player pays it to the giver (a fine, a bribe, a child's keep). A choice whose `next` leads straight to such an ending is offered only while the player has that much, so the tale never says he paid what he could not; an ending reached otherwise (a failure, a defeat) takes what he has, up to the amount. Do not make such a choice a scene's `default`. |
| `memory` | What the giver remembers, in the giver's own voice. |
| `journal` | The last line written in the journal when the tale ends this way. |
| `consequences` | `memory`: `who` (a role or `locals_of:{place}`) remembers `text`. A `locals_of` memory leaves out anyone the same ending already gives a memory to by name (the giver, when the ending has a `memory`, or a role with a `memory` of its own), so a giver who lives in that place does not remember the moment twice. `world_event`: news that spreads through the realm (`summary`, `magnitude` 0 to 100, `place`). `grudge`: `who` (a role) holds `text`, the cause in their words, `against` another role, `magnitude` 0 to 100 deep (30 when left out); it fades with time like every grudge. `conception`: a chance of a child between the player and `who` (a role, `{giver}` when left out), `chance` 0 to 1 (the game's own 0.15 when left out); the woman of the two carries it, through the same conception as every intimate scene (of age, not already with child, the Life and Death Cycle on), and two women or two men conceive nothing. `elope`: `who` (the giver when left out) leaves her husband `spouse` (`{husband}` when left out) with the player: her marriage ends through the game's own divorce, she rides in the player's party as a guest and may later marry the player, and her husband holds a grudge; nothing happens if the marriage cannot end (she is with child, a captive, or leads her house). `item` (since 2.7.19): the player is given `count` (1 when left out, at most 200) of `item`, one item by its id
(`"wine"`, `"sumpter_horse"`), into the party's baggage; a kind or a category is refused, since "a horse" would be
whichever horse the game loaded first. `return_spouse`: she goes back to him, wed again, and leaves the player's party; nothing happens if either has wed someone else, or if she now heads a house of her own. If he is away on campaign (in an army or a battle) or either is a captive, she goes to his house's home and the wedding waits, tried again each day for up to 60 days. `regard` (since 2.7.21): the regard of `who` (a role, `{giver}` when left out) for the player moves by `amount` (-100 to 100, never 0), on CR's own ledger; unlike an ending's own `regard`, it may carry `if`, so a reward only some paths earn. Any of them may carry `if`: it is carried out only when its flags hold. |
| `failure.days` | The tale fails that many days after it was accepted. Without it, the tale never fails on its own. |

Every ending closes the journal entry as completed; the time-out closes it as failed.

**Consequences on a stage (`on_enter`).** A stage may carry the same consequences in `"on_enter": [ ... ]`: they are
carried out when the tale enters that stage, each under its own `if` (the flags set by the choice that led there
already hold). So a tale can carry a lady away midway and bring her back at an ending, or not:

```json
{ "id": "the_war", "journal": "{husband}'s realm takes up arms.",
  "on_enter": [ { "type": "elope", "if": "with_her" } ],
  "choices": [ { "id": "give_back", "label": "Give her back", "said": { "to": "{husband}", "meaning": "the player gives {giver} back" }, "next": "end:returned" } ] }
```

with `"returned": { "consequences": [ { "type": "return_spouse" } ] }` among the endings. The first stage is the
request, left when the player accepts: its `on_enter` is never carried out, and the checker says so.

## 9. Flags

Flags are a tale's memory of what happened: simple names, set and tested, with no numbers and no
text.

- **Set** by a choice taken (`"sets"`), a finding revealed or a person met in a scene (`"sets"` on
  them). A required finding told when a scene closes sets its flags too.
- **Tested** by `"if"`: `"saw_light"` holds when the flag is set, `"!saw_light"` when it is not. A
  list (`["saw_light", "!refused"]`) holds when every name in it does.
- **Set by the game as the tale ends** (since 2.7.21): `regard_rose` when the giver holds the player higher than when
  the tale began, `regard_fell` when lower, neither when unmoved. An ending's consequences may test them (a warm memory
  under `regard_rose`, a cold one under `regard_fell`, a neutral one under `["!regard_rose", "!regard_fell"]`). No choice
  sets them, and they are not read before the ending.

`if` works on choices (open or not), on knower lines, canvas and fact lines, and on consequences. A
line may be a plain string, or an object with a condition:

```json
"knowers": {
  "{giver}": [
    "The player found the boy at the old mill.",
    { "text": "The boy says smugglers keep a lantern there.", "if": "saw_light" },
    { "text": "Nobody has told you of any light.", "if": "!saw_light" }
  ]
},
"canvas": [ "The lord's relief is plain.", { "text": "He asks about the light in the wood.", "if": "saw_light" } ]
```

A flag tested but set nowhere is reported: it never holds, so whatever depends on it is dead.

## 10. Exploration scenes

```json
"objective": { "type": "explore", "scene": {
  "title":      "The marsh by {village}",
  "opening":    { "canvas": "A still night, thick with fog...", "facts": [ "..." ] },
  "findings":   [ { "id": "boot", "required": false, "text": "A man's boot stuck in the mud.", "sets": "found_boot" } ],
  "hazards":    [ { "id": "sinkhole", "trigger": "stepping off the causeway",
                    "effect": { "type": "injure", "hit_points": [10, 25] } } ],
  "encounters": [ { "id": "tam", "name": "Tam of {village}", "persona": "a villager hiding in the reeds",
                    "knows": [ "Men with lanterns, not ghosts, ring the bell." ], "sets": "found_tam" } ],
  "closing": "The bell falls silent behind you."
} },
"choices": [
  { "id": "left",  "label": "Follow the causeway left, toward the bell", "next": "the_chapel" },
  { "id": "right", "label": "Take the right fork, where the footprints go", "if": "found_boot", "next": "the_reeds" },
  { "id": "back",  "label": "Go back to the village", "default": true, "next": "keep_watch" }
]
```

The narrator speaks plainly, in the second person, and ends each reply by asking what the player
does. It reveals a finding, springs a hazard or brings in an encounter only when the player's own
actions lead there. It may not invent any of the three, nor a way out.

| Part | Meaning |
|---|---|
| `title` | The name over the chat window during the scene. Empty: "The wood by <village>". |
| `findings` | What there is to discover. A scene without choices needs at least one `required`. A required finding the player never reached is told when the scene closes: a clue can be missed only in how it is found, never altogether. |
| `hazards` | What the player's choices can spring. `trigger` says what the player must do. Effects: `injure` (`hit_points: [min, max]`, never below 1 HP), `alert` (the stage's band will be larger), `lose_time` (the scene ends, the night is lost). Each hazard springs once. |
| `encounters` | People who may be met in the scene, voiced by the narrator, who say only what they `know`. |
| `closing` | Shown when the scene closes, after any missed findings. |
| `invite_prisoners` | Off by default: the player's prisoners may be invited into the scene only when it is on. Companions and lords riding with the player always may. |

The narrator is also told, from the game, the ground under the player, the season, the hour and the
player's people, so the same wood reads differently to an Aserai at night and a Battanian at noon.

**Choices in a scene.** The narrator is told the ways out open now, and takes one only when the
player's own words choose it. Under each narration the player has not yet answered, the open ways
are listed like a menu:

```
1. Follow the causeway left, toward the bell
2. Go back to the village
3. Something else...
```

A click on a way sends its words as the player's line; typing the number alone ("1") does the same.
The narrator tells it, and only the narrator's marker moves the story: the click alone never does.
"Something else..." puts the cursor in the input to write freely. A closed way is not listed: "1" is
always the first way listed. Once a way is taken, the scene ends and the story follows it.

A scene closed with no way taken follows its stage's `on_complete.next` if it has one, else its
choice marked `"default": true` (which may not depend on a flag). One of the two is required.

## 11. Read but not played yet

These fields are accepted, so files written for later versions still load, but the game does
nothing with them yet:
- `may_materialize`, `materialize_when` and `escort_to` on an encounter (a met person who becomes a
  real companion to escort);
- `behavior` on a spawn (every band patrols around its place);
- `party` on an `alert` effect;
- `hours` on a `lose_time` effect.

Planned for later versions: the giver seeking out the player, ambushes that turn a scene into a
battle, escorts, quarrels between two lords left by a tale, and more consequence types.

## 12. The tale editor

`Modules/CalradiaRemembers/Docs/TaleEditor.html` writes tales without touching JSON. Open it in any
browser: it works offline, installs nothing, and sends nothing anywhere; it saves only the file you
ask it to.

- **Start** from a blank tale, from the werewolf, from a quest file you open (one tale or a list),
  or from a Twine story (section 13). You can also drop a file on the page.
- **Edit** every part with forms: the header, who gives it, the roles, each stage (its objective,
  what is said, its choices), the scenes, the endings, the reward and the failure. Each field
  carries its one-line help from this guide; fields the game reads but does not play yet are marked.
  Renaming a stage or an ending carries every path that led to it along.
- **See** the story as a map: stages and endings as boxes, choices as labelled arrows (an act, a
  turn in a scene, words), a path that leads nowhere in red, a stage that can never reach an ending
  outlined in red, a part no path reaches faded. Click a box to open it.
- **Check** as you write: the editor runs the game's own checks, word for word, blocking problems
  apart from the rest, each pointing at its field.
- **Save** the file and put it in `ModuleData/Custom/Quests/`. What it saves loads in the game with
  no problem it did not show you.

## 13. Writing tales in Twine

[Twine](https://twinery.org) draws a story as passages joined by links. The editor turns a Twine 2
story into a tale: publish it to a file (`.html`) or export it as Twee (`.twee`), then use "Import
Twine...". Harlowe and SugarCube are both read.

The convention:
- **Each passage is a moment, and its tag says what the player must do to get there**: `talk_to:giver`,
  `talk_to:rival`, `return`, `talk_to_locals:village:2`, `be_at:village`, `keep_watch:village`
  (or `keep_watch:village:1-3:0.5` for the nights and the chance), `defeat_party:village`, `wait:3`.
  The start passage needs no tag: it is the request.
- **Links are the choices.** `[[Tell the lord->The lord hears]]`: the passage the link leads to says,
  by its tag, how the choice is taken. A passage with a single link becomes a plain stage.
- **A passage tagged `explore` is a scene.** Its links to passages tagged `finding` (and `required`),
  `hazard` (`injure`, `alert` or `lose_time`, with `hp:10-25`) or `encounter` are things in the
  scene; its other links are the ways out the player picks in words. Tag one of their passages
  `default`.
- **A passage with no links is an ending.** Tag it `regard:5` or `gold:200`.
- **Variables become flags.** `(set: $x to true)` or `<<set $x to true>>` sets the flag `x` on
  arriving at the passage. `(if: $x)[...]` or `<<if $x>>...<</if>>` puts what is inside under the
  flag: a link becomes a choice open only then, a line a line said only then. `not $x`, `!$x`,
  `(unless: $x)` and `(else:)` / `<<else>>` give `!x`.
- **Lines that fill fields**: `Journal: ...`, `Fact: ...`, `Memory: ...` (an ending), `Closing: ...`
  (a scene), `Trigger: ...` (a hazard), `{giver} knows: ...` or `locals_of:{village} knows: ...`.
  The rest of a passage's text is its canvas.

Everything the import cannot map (another macro, an unknown tag, a passage nothing links to, the
story's script and stylesheet) is listed in the import notes, never dropped silently; the tale
then opens in the editor with its problems shown.

A tiny story, as Twee:

```
:: StoryTitle
The Miller's Silence

:: A quiet word
The lord asks you to find out why the miller of {village} will not grind his grain.
Journal: Find out why the miller of {village} has shut his mill.
[[Go to the mill->At the mill]]

:: At the mill [be_at:village]
The mill is shut. The miller will say only that a lord's men came by night.
(set: $miller_spoke to true)
[[Tell the lord->The lord hears]]
[[Let it lie->Silence]]

:: The lord hears [talk_to:giver regard:6]
The lord hears it and goes quiet.
(if: $miller_spoke)[He asks what else the miller said.]
Memory: The player told me why the mill of {village} stopped.

:: Silence [wait:7 regard:-3]
Journal: You let the mill stay shut.
```

It becomes a request, a stage reached by being at the village (setting `miller_spoke`), and a
choice between telling the lord (talk to the giver) and letting a week pass, each with its ending.

## 14. Full examples

`Modules/CalradiaRemembers/ModuleData/Quests/wolf_of_the_wood/wolf_of_the_wood.json` is a complete tale: a request, a
village to question, nights of watch, a scene in the wood, a report, a band to destroy, and the
return. Copy it into `ModuleData/Custom/Quests/`, change its `id`, and make it yours, or open it in the editor
("From the werewolf").
