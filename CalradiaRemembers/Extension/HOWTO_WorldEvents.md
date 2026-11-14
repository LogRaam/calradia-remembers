# How to feed world events into Calradia Remembers

Your mod changes the world (a siege, an execution, a rebellion, a horse race). One call tells
Calradia Remembers about it, and from then on characters who would plausibly have heard of it can
bring it up in conversation, as hearsay, hedged by how surely it reached them.

The door is one-way: you push facts in and never read CR's internals. Everything you hand it is
validated at the door.

`PlaceId`, the "named heroes hear it" rule in conversation and the
`cr.heard` command arrived in **2.5.22**; `ActorHeroIds` in **2.5.23**.

---

## 1. The call

```csharp
FeedResult r = CalradiaRemembers.Feed.Record(new ExternalWorldEvent {
    Id         = "mybridge_siege_town_V1_day412", // your STABLE id: CR dedups on it
    SourceMod  = "MyBridge",                       // required
    Summary    = "The siege of Sargot was broken after forty days.",
    Magnitude  = 70,                               // 0..100, how far the news carries
    FactionIds = new List<string> { "vlandia" },   // KINGDOM ids (see 3)
    HeroIds    = new List<string> { "lord_4_1" },  // the heroes it CONCERNS (the town's lord)
    ActorHeroIds = new List<string> { "lord_1_1" },// whoever DID it (the besieger), see 3
    PlaceId    = "town_V1",                        // the settlement where it happened
    Lifetime   = ExternalEventLifetime.Rumor,
});
```

Also: `Feed.Update(e)` replaces the entry with the same `Id` (or records it fresh), and
`Feed.Retract(id)` removes it so characters stop speaking of it. Since 2.8.10, `Feed.List(sourceMod)` returns copies of
what your mod recorded that CR still holds (a faded rumor is gone; `HeroIds` carries every hero named; `Category` is not
kept), so a mod can rebuild its own view after a load without keeping a second list.

`FeedResult` tells you what happened: `Recorded`, `RejectedDuplicate` (that `Id` is already stored),
`RejectedThrottled` (more than 20 records from your `SourceMod` today), `RejectedInvalid` (no
`SourceMod`, or an empty summary), `RejectedNoStore` (no campaign running).

Ids that do not resolve are dropped quietly (heroes, factions, place). The event is still recorded,
but it loses that tag. The log says so for a place.

---

## 2. Who hears it

Nothing is copied into any character's memory. Every time a character's prompt is built, CR scores
each stored event for that character:

```
score = (Magnitude + 25 if one of FactionIds is their realm + up to 25 for being near PlaceId)
        x recency                          (falls linearly from 1 to 0 over 40 days)
        + 25 if they are named in HeroIds  (added after recency)
```

At **50 or more** they have heard it. Roughly 62+ is held as second-hand news, 90+ as common
knowledge, and the character's wording hedges accordingly.

How long a character keeps it, by situation:

| Magnitude | Situation | Heard until the event is |
|---|---|---|
| 100 | anyone | 20 days old |
| 100 | of the tagged realm | 23 days old |
| 70 | of the tagged realm | 18 days old |
| 70 | anyone | 11 days old |
| 60 | of the tagged realm | 16 days old |
| 50 | at the place | 13 days old |
| 50 | anyone | only the day itself |
| 30 | named in HeroIds | 6 days old |
| under 50, untagged | a stranger | never |

Then the character has to actually bring it up. The prompt's news block holds the **6 best-scored**
events that character has heard, competing with CR's own news. Any other event they have heard
comes up when the player's question names one of its heroes or realms, or shares a distinctive word
with its summary (a place name, "siege").

---

## 3. Tagging, the things that go wrong

- **Tag the kingdom, not the clan.** A lord's realm is his kingdom whenever his clan has one, so a
  clan id in `FactionIds` only matches clans that belong to no kingdom.
- **`HeroIds` is who the fact is ABOUT.** A named hero hears it more readily. The main effect, though,
  is that the event surfaces when the player mentions that hero. Naming the player there is fine (the
  owner of a besieged town, say): it stays news.
- **`ActorHeroIds` is who DID it** (the besieger, the executioner). List them whoever they are; actors
  count as concerned too. When the player is among them, CR files the event as the player's own deed:
  it enters their chronicle, and characters speak of it as something the player did. Your bridge never
  has to check for the player itself. Before 2.5.23 the player's id in `HeroIds` was enough to do
  this, which turned a siege of the player's own town into "a deed of yours". Set it by reflection if
  your bridge must also run on older versions (on those, leave the player out of `HeroIds` unless they
  acted).
- **Give it a place.** Without `PlaceId`, distance plays no part, and only magnitude, realm and naming
  carry the news.

---

## 4. Rumor or Deed

This only decides how long the entry is **stored**, not how long it is talked about.

- `Rumor` (default): pruned once 42 days old, or earlier if the store holds 200 rumours (oldest first).
  After that, the same `Id` can be recorded again.
- `Deed`: kept (up to 50 deeds, oldest first), so dedup on its `Id` holds for good.

Characters stop bringing up **both** on the same 40-day recency curve. Use `Deed` for a fact you must
never re-record (and may later `Update` or `Retract`), and `Rumor` for everything else.

---

## 5. Worked examples

Each one is a situation, the call that describes it, and what CR then does with it. The code uses the
game's own objects, the way a bridge has them at hand; the names in comments are vanilla ids.

### A siege of a town the player owns

Lucon (`lord_1_1`) lays siege to Sargot (`town_V1`), which the player holds.

```csharp
Feed.Record(new ExternalWorldEvent {
    Id           = "mybridge_siege_" + town.StringId,      // stable, no day: the siege is a standing fact
    SourceMod    = "MyBridge",
    Summary      = town.Name + ", held by " + town.Owner.Name + ", is under siege by " + besieger.Name + ".",
    Magnitude    = 85,
    HeroIds      = new List<string> { town.Owner.StringId },   // the player: CONCERNED, did nothing
    ActorHeroIds = new List<string> { besieger.StringId },     // Lucon: he is the one besieging
    FactionIds   = new List<string> { town.MapFaction.StringId, besieger.MapFaction.StringId },
    PlaceId      = town.StringId,
    Lifetime     = ExternalEventLifetime.Deed,
});
// ...and when the siege ends, however it ends:
Feed.Retract("mybridge_siege_" + town.StringId);
```

What CR does: it is **news**, not the player's deed, because the player is not among the actors. Anyone
in Calradia has heard it for 16 days, people of either realm for 21, and people of either realm near
Sargot for 25. Lucon, named and of a tagged realm, has it for 30 days wherever he is. Once retracted, nobody speaks of it
as still going on.

Before 2.5.23, the player's id in `HeroIds` made this "a deed of yours". On older versions, leave the
player out.

### An execution by the player's decree

The player has a captive lord put to death in the dungeon of Usanc Castle (`castle_V1`).

```csharp
Feed.Record(new ExternalWorldEvent {
    Id           = "mybridge_execution_" + victim.StringId,
    SourceMod    = "MyBridge",
    Summary      = "By decree, " + executioner.Name + " has executed " + victim.Name + ".",
    Magnitude    = 88,
    HeroIds      = new List<string> { victim.StringId },       // the one it was done to
    ActorHeroIds = new List<string> { executioner.StringId },  // here the player, but list them whoever they are
    FactionIds   = new List<string> { victim.MapFaction.StringId },
    PlaceId      = victim.CurrentSettlement?.StringId,         // the dungeon he died in (still set when
                                                               // HeroKilledEvent fires); null if carried in a party
    Lifetime     = ExternalEventLifetime.Deed,
});
```

What CR does: the player is among the actors, so it is **the player's own deed**. It enters their
chronicle, and characters who have heard of it speak of it as something the player did, in the part of
their prompt about the player's reputation. The bridge never checks for the player; the same line
reports an execution by any lord, and then it is ordinary news.

### A village's harvest failing

Calioc (`village_V1_1`, near Sargot) produces half its usual output. Nobody did it.

```csharp
Feed.Record(new ExternalWorldEvent {
    Id         = "mybridge_harvest_" + village.StringId + "_" + day,   // a moment: the day belongs in the id
    SourceMod  = "MyBridge",
    Summary    = village.Name + " has fallen off and brings in barely half its usual harvest.",
    Magnitude  = 35,
    HeroIds    = new List<string> { village.Owner.StringId },
    FactionIds = new List<string> { village.MapFaction.StringId },
    PlaceId    = village.StringId,
    Lifetime   = ExternalEventLifetime.Rumor,                          // no actors: news, and it may age out
});
```

What CR does: a small matter, and it stays local. A stranger far away never hears of it. A stranger near
Calioc, or someone of its realm anywhere, has it for 6 days. Someone of its realm near Calioc has it for
16 days. The village's owner, being named, hears of it more readily wherever he is. This is where
`PlaceId` matters most: at high magnitudes everyone hears the news anyway, and the place only adds a few
days.

---

## 6. Testing your bridge

- **Was it recorded?** `CalradiaRemembers.log` gets one line per record:
  `[FEED] MyBridge recorded a world event (id ..., mag 70, 1 hero ref(s), 1 faction ref(s), place town_V1): ...`
- **Has this character heard it, and why?** In the console (Alt+~):

  ```
  cr.heard <name> <event id>    one event: the verdict, every term, days left, or what would reach them
  cr.heard <name>               every event fed by another mod
  cr.heard <name> all           CR's own events too
  ```

  With no name, it asks about the character you are talking to. The verdict is the same arithmetic
  the prompt uses. "Faded" means your bridge worked and the test came too late. "Could not hear it
  even on the day it happened" means the tags are wrong, and the line says which ones to add.
- **A known-good reference:** `cr.feed_test` feeds a magnitude-60 fact tagged with your realm and the
  nearest settlement, then prints how far it will carry and the id to check.

To get it said out loud, ask the character about recent news, or name someone the event is about.
They use it as hearsay when it fits the talk; they never recite it.
