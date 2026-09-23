# How to feed world events into Calradia Remembers

Your mod changes the world (a siege, an execution, a rebellion, a horse race). One call tells
Calradia Remembers about it, and from then on characters who would plausibly have heard of it can
bring it up in conversation, as hearsay, hedged by how surely it reached them.

The door is one-way: you push facts in and never read CR's internals. Everything you hand it is
validated at the door.

`PlaceId`, the "named heroes hear it" rule in conversation and the
`cr.heard` command arrived in **2.5.22**.

---

## 1. The call

```csharp
FeedResult r = CalradiaRemembers.Feed.Record(new ExternalWorldEvent {
    Id         = "mybridge_siege_town_V1_day412", // your STABLE id: CR dedups on it
    SourceMod  = "MyBridge",                       // required
    Summary    = "The siege of Sargot was broken after forty days.",
    Magnitude  = 70,                               // 0..100, how far the news carries
    FactionIds = new List<string> { "vlandia" },   // KINGDOM ids (see 3)
    HeroIds    = new List<string> { "lord_4_1" },  // the heroes it is about
    PlaceId    = "town_V1",                        // the settlement where it happened
    Lifetime   = ExternalEventLifetime.Rumor,
});
```

Also: `Feed.Update(e)` replaces the entry with the same `Id` (or records it fresh), and
`Feed.Retract(id)` removes it so characters stop speaking of it.

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

## 3. Tagging, the three things that go wrong

- **Tag the kingdom, not the clan.** A lord's realm is his kingdom whenever his clan has one, so a
  clan id in `FactionIds` only matches clans that belong to no kingdom.
- **`HeroIds` is who the fact is ABOUT.** A named hero hears it more readily. The main effect, though,
  is that the event surfaces when the player mentions that hero. Only put the PLAYER's id there for
  the player's own deed: it turns the event into the player's chronicle, told back to the player as
  something they did, not as news.
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

## 5. Testing your bridge

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
