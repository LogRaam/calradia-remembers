# Authored history: a campaign's past, written by the player on a timeline

## 1. Where this comes from

Gabriel, 17/10/2026, on making the world's long-term memory a file the player can edit:

> "En fait, le fichier est interessant car je pourrais mettre en scene des evenements du passe qui ne se sont pas
> produits dans le temps de jeu."

Then, on the three open questions:

> "it can also be able to set a starting vanilla. A file per save ID. File editable with an external tool.. something
> like a web page with a timeline."

So:

- the file holds events from BEFORE the campaign began, which the game never played;
- an event can set a starting vanilla relation between two characters;
- there is one file per campaign (`Campaign.UniqueGameId`, the id every save of one campaign shares);
- it is edited with a standalone web page that shows a timeline, shipped like the Tale Editor and the Scenario
  Composer (`CalradiaRemembers/Extension/*.html`, copied to the module's docs).

It builds on the world's long-term memory (`27937a7`, `LongTermHistoryPolicy`, `WorldEventStore` second tier) and
feeds the NPC regard work (`docs/design/NPC_REGARD.md`): two lords who share a battle or a betrayal in the file are
friends or enemies because of it.

## 2. Ownership: the file owns its events, the save owns the rest

The objection to an editable memory was drift: a loose file falls out of step with older saves. It does not apply to
authored events, because they are not copies of anything the save produced:

- **Game-recorded history stays in the save**, untouched by the file.
- **Authored events belong to the file.** Each has a stable id. At every load (and on `cr.history_reload`), the
  long-term tier's authored entries are replaced by the file's: an added event appears, an edited one changes, a
  deleted one disappears. Loading an older save of the same campaign reads the same file, so nothing drifts.
- **Authored events are never evicted** by the cap of 3000 (`LongTermHistoryPolicy.Evict` skips them); the cap
  bounds what the game records, not what an author wrote.
- **No file, no change.** A campaign without a file behaves exactly as today.

## 3. Files

Folder: `Documents/Mount and Blade II Bannerlord/CalradiaRemembers/History/`.

- `<UniqueGameId>.history.json`: the authored history. Written by the editor, read by the mod. Never written by the
  mod once it exists (the author's file is the author's).
- `<UniqueGameId>.world.json`: a snapshot the mod writes for the editor: the calendar (campaign start year and
  season, today), every hero (id, name, clan, culture, gender, birth year, alive or dead, spouse), clans, realms,
  settlements, and the game-recorded long-term events (read-only, so the author sees the whole timeline and does not
  write a past that contradicts it). Written on `cr.history_file` and refreshed at each save.
- `<UniqueGameId>.history.problems.txt`: what the last load set aside, and why.

`cr.history_file` writes the snapshot, creates an empty history file if none exists, and prints both paths.

## 4. Format (version 1)

```json
{
  "format": 1,
  "campaign": "<UniqueGameId>",
  "title": "The Pravend years",
  "events": [
    {
      "id": "pravend-betrayal",
      "year": 1078,
      "season": "spring",
      "kind": "betrayal",
      "summary": "Lord X opened the gates of Pravend to the Vlandians, and Lord Y lost his brother there.",
      "actors": ["lord_1_1"],
      "harmed": ["lord_2_3"],
      "place": "town_EN1",
      "realms": ["empire_w"],
      "importance": 60,
      "secret": false,
      "knownBy": [],
      "relations": [ { "a": "lord_1_1", "b": "lord_2_3", "change": -40 } ]
    }
  ]
}
```

- **Ids, not names.** The editor picks heroes, places and realms from the snapshot and shows their names; the file
  stores ids, so namesakes cannot be confused (`HeroLabel`'s lesson). A hand-written name is accepted when exactly
  one hero bears it, and reported otherwise.
- **Date: in-game calendar year and season**, the way the timeline shows it. It must fall before the campaign start;
  an event dated inside the campaign is set aside (the game owns that time).
- **Kinds:** the long-term kinds (battle, siege, fief taken, execution, fell in battle, marriage, divorce, war, peace,
  rebellion, scheme, capture) plus authored ones the game never records: betrayal, feud, kindness, debt, rescue,
  duel, other. Every authored kind is kept (an author's event is history by definition).
- **Importance (0 to 100):** the magnitude. From 45 up, the realms named know it (`RealmWideMagnitude`).
- **Secret and knownBy:** a secret is known to its actors, the harmed, and those listed in `knownBy`; nobody else.
  This is the confidant list asked for on 17/10 ("connus que de ceux qui le connaissent"), which the game-recorded
  secret does not have yet; the store gains an additive `KnownToIds` field and `RumorDiscretion` reads it.
- **Relations:** each entry either moves the vanilla relation between two heroes by `change`, or sets the value the
  pair starts at with `set` (-100 to 100; one or the other). Gabriel left the choice open on 17/10 ("continu"), so both
  are offered: a `change` adds up when several events touch one pair, a `set` makes two lords the game generated at
  +80 start as enemies. Applied once, see section 6.
- **The player's character** may be an actor, harmed, or one side of a relation. Such an event is the player's own
  past; a relation naming the player also moves the mod's personal regard (`NpcProfile.ReputationWithPlayer`), since
  an NPC's feeling toward the player is judged on that ledger, not vanilla's (chosen 17/10, Gabriel did not object).

## 5. Validation (pure, `CalradiaRemembers.Logic`)

A load never fails as a whole: each event is accepted, accepted with warnings, or set aside, and the problems file
says which and why (the `AuthoringProblems()` spirit of scenario packs).

Set aside:
- an actor, harmed hero, place or realm the campaign does not know;
- a date not before the campaign start;
- a hero dated before his birth (an actor in 1070 born in 1075);
- a kind that kills (execution, fell in battle) whose victim (the harmed) is alive today. The player can open the
  encyclopedia and see him alive: never fake what the player can verify.
- a duplicate id (the second one).

Accepted with a warning:
- a marriage between two heroes not married to each other today (they may have divorced or been widowed);
- an unknown season (read as spring).

(A fief taken whose taker's house does not hold it today is simply history moving on: no warning.)

Dropped from an accepted event:
- a relation entry naming the same hero twice or an unknown hero; a change outside -100..100 is clamped.

## 6. Starting vanilla relations

Gabriel: "it can also be able to set a starting vanilla." An event's `relations` move the vanilla relation
(`ChangeRelationAction.ApplyRelationChangeBetweenHeroes`, no notification) between two heroes.

To survive edits and reloads without stacking, the save keeps a ledger (additive key): for each event id and pair,
the change already applied. At each sync the mod applies the difference between what the file asks and what the
ledger holds:

- a new `change` applies it; an edited one applies only the difference (from -40 to -60 applies -20);
- a new `set` applies the value minus today's relation; an edited one moves the pair by the edit only (from -60 to
  -30 applies +30), so what the campaign did to the pair since is kept;
- switching an entry between `change` and `set` undoes the old one first;
- a removed entry or event reverts what it had applied.

Built in `HistoryRelationLedger.Plan` (pure); each step says whether the player is one side.

The game may move the relation afterwards (that is the point of a starting relation); the ledger tracks only what the
file did, never the relation's value.

## 7. Who knows an authored event

The same rule as the long-term tier (`LongTermHistoryPolicy.WouldKnow`): actors and harmed, their kin and clan, the
people of the place, and for importance 45 and up, the realms named. A secret narrows it to actors, harmed and
`knownBy`.

## 8. What it feeds

- **Conversation:** the planned injection of long-term events on mention (`NPC_REGARD.md` section 9) reads authored
  events like any other: when the player names a person, a place or a realm, the one to three most relevant events
  the NPC would know go below the `CURRENT ENCOUNTER:` marker. Not built yet; it is the next increment of that work.
- **NPC regard:** `PairRegardPolicy` reads the events a pair shares, authored ones included, and the vanilla
  relation the file set.
- **cr.history** lists authored events with an `authored` tag.

## 9. The editor page (`HistoryEditor.html`)

A single standalone HTML file, no network, shipped beside `TaleEditor.html`:

- **Open:** the world snapshot and the history file (file pickers or drag and drop). In Chrome and Edge, the File
  System Access API saves the history file in place; elsewhere, Save downloads it.
- **Timeline:** horizontal, by year and season, from the oldest event to the campaign start (a marked line). Authored
  events are cards coloured by kind; game-recorded long-term events show as grey, read-only marks. Zoom by decade or
  year; filter by hero, realm, place or kind.
- **Edit:** a side form with searchable pickers fed by the snapshot (heroes show name, clan and life years), a
  relation editor (pick two heroes, a slider -100..100), secret and knownBy.
- **Checks in the page:** the same rules as section 5, shown live on each card, so the author fixes them before the
  game sees them. The mod's validation stays the authority.

## 10. Increments

1. **Logic (pure, tested):** the format model and parser, the calendar conversion (year and season to a day, with
   `CampaignTime.DaysInYear` and `DaysInSeason` passed in), validation (section 5), the relation ledger diff
   (section 6), `KnownToIds` in `RumorDiscretion`, authored entries exempt from eviction.
2. **Mod:** the folder and files, the snapshot writer, the sync at session launch and on `cr.history_reload`, the
   ledger in the save, the problems file, `cr.history_file`, the `authored` tag in `cr.history`; a testall that writes
   a small history file for a real lord, reloads, and finds the event, its knowledge and its relation change, then
   edits and removes it and checks the ledger reverts.
3. **The editor page,** with its own fixture snapshot so it can be tried without the game.
4. **Conversation injection on mention** (shared with `NPC_REGARD.md`).

## 11. Save format

Additive only: authored events live in the existing long-term tier (`CalradiaRemembers_WorldHistory_v1bytes`) with
an additive `AuthoredId` field; the relation ledger gets its own key. An older save without them reads empty and
behaves as today.
