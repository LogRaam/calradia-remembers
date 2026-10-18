# Calradia Remembers: NPC Memory & Conversation API

A public, stable API for other mods to observe conversations and read or inject an NPC's memory at
runtime. It lives in `CalradiaRemembers` (the layer that talks to TaleWorlds and owns the NPC profiles),
not in the LLM layer. All identity is by hero `StringId`, never a display name.

Check `CrNpcMemory.ApiVersion` before you call anything. It is bumped only on a breaking change; additive
members never bump it. Current version: **1**.

## What you get

### Conversation lifecycle (`CalradiaRemembers.CrConversation`)

```csharp
using CalradiaRemembers;

// Fired when a conversation OPENS, before the first exchange, so you can inject relevant memories in time
// for the first prompt.
CrConversation.Started += info =>
{
    string npcId  = info.NpcId;    // hero StringId
    string saveId = info.SaveId;   // Campaign.Current.UniqueGameId, stable per campaign (your isolation key)
    int    day    = info.GameDay;  // in-game day, for timeline context
    // ... recall this NPC's relevant memories from your own store and inject them (see AddMemory below) ...
};

// Fired when a conversation CLOSES, with the whole attributed transcript.
CrConversation.Ended += log =>
{
    foreach (CrTranscriptLine line in log.Lines)   // oldest first
    {
        // line.SpeakerId    -> hero StringId (null for narration / synthetic speakers)
        // line.SpeakerName  -> display name (log only; use SpeakerId for identity)
        // line.IsPlayer     -> the player's own line
        // line.IsNarration  -> scene narration rather than a spoken line
        // line.Text
    }
    // Third-party WITNESS lines are included and attributed by SpeakerId, so multi-party interjections
    // are no longer a guessing game.
};
```

Every subscriber is invoked under a guard: if your handler throws, it is logged and isolated, never
breaking the conversation or the game.

### NPC memory read/write (`CalradiaRemembers.CrNpcMemory`)

```csharp
using CalradiaRemembers;

// READ what CR already stores for an NPC (so you don't inject a duplicate). Empty if the id is unknown,
// the NPC has no profile yet, or no campaign is loaded.
IReadOnlyList<CrMemoryRecord> known = CrNpcMemory.GetMemories(npcId);
foreach (CrMemoryRecord m in known)
{
    // m.Day, m.Kind ("Agreement", "Conflict", "Other", ...), m.Summary
}

// INJECT a memory the NPC will treat as its own recollection in later conversations. The NPC's profile is
// created if it does not exist yet, so you can seed an NPC the player has never met.
MemoryResult result = CrNpcMemory.AddMemory(new CrMemoryEntry
{
    NpcId     = npcId,                                        // REQUIRED, hero StringId
    Summary   = "We agreed to meet at the ford at dawn.",     // REQUIRED, the remembered line
    Kind      = "Agreement",                                  // optional; "Hearsay" for news heard; unknown/engine kinds become "Other"
    DaysAgo   = 0,                                            // optional; 0 = today, dated back from now
    SourceMod = "MyMemoryAddon",                              // optional; kept with the memory since 2.7.9 (see RemoveMemory)
    Private   = false                                         // optional (2.6.10): true = said in confidence
});
// result: Added | RejectedInvalid | RejectedUnknownHero | RejectedNoStore
```

Injected memories become ordinary profile events, so they surface in later conversations exactly like
memories CR recorded itself, and are compacted over time by CR's own memory compressor. Mark one `Private`
(since 2.6.10) and it follows CR's audience rule for private memories: the character speaks of it only in
private, never in front of others (an embarrassing secret, something said in confidence).

### Taking a memory back (`CrNpcMemory.RemoveMemory`, since 2.7.9)

```csharp
int removed = CrNpcMemory.RemoveMemory(new CrMemoryRemoval
{
    NpcId     = npcId,                                  // REQUIRED
    Summary   = "We agreed to meet at the ford at dawn.",  // REQUIRED, the exact text you passed to AddMemory
    SourceMod = "MyMemoryAddon"                         // the SourceMod you added it with
});
```

Removes every memory of that character whose text is exactly what AddMemory stored and which YOUR mod added (asked
for by philip0105 for Vivid World: rumors forgotten or corrected later). A memory another mod added is never
removed. Memories injected before 2.7.9 carry no mod and are matched by their text alone, so you can clean up what
you added earlier. Returns how many were removed. A memory CR's compressor has already folded into a summary is
no longer there to remove.

### Live knowledge every reply (`CrPrompt.RegisterVariable` + `heard_now.txt`, since 2.7.9)

For knowledge that changes all the time (rumors heard, forgotten, denied), do not store it in CR at all: register a
`{{variable}}` with `CrPrompt.RegisterVariable`, and ship `ModuleData/cr_patch/heard_now.txt` containing it. Your
provider is called on every prompt build (every reply), on the game thread, with `PromptVarFacts` (`NpcId`, `Lean`
when the prompt must be short). `heard_now.txt` is written in the uncached tail of the prompt, just before CR's
output rules: changing text there costs no cache, and it does not push those rules up the way a long block in
`post_history_instructions.txt` (which comes after them) would. Keep it to facts and who told them, around 800 to
1,000 characters (about 300 when `Lean`). Never reference a changing variable from `world.txt` or the behaviour
guidelines: they sit in the cached part of the prompt.

Call `AddMemory` (and `GetMemories`) on the **game thread**: from a campaign event, a tick, or a menu or
button handler, never from a background task. An injected memory never moves regard by itself; it is
something the character remembers and can speak from. Do not inject what CR already records on its own (a
rescue from captivity, a battle, a tournament, a letter): the character would remember it twice.

### Scenario packs (`ModuleData/cr_scenarios`, since 2.7.9)

A scenario is a rule that makes the world react: when a trigger fires (`ON_DEATH`, `ON_MARRIAGE`, `ON_WAR_DECLARED`,
`ON_PEACE`) and its guards pass, its consequences change the world and its message is told in the realm. Build a pack
with `Docs/ScenarioComposer.html` and ship it in your own module's `ModuleData/cr_scenarios/*.json`; no code and no
reference to CR. Packs are read in this order: CR's own (`ModuleData/scenarios`), the player's
(`Custom/scenarios`, kept across CR updates), then each enabled mod's, in launcher order.

- **Give every rule an id of its own, prefixed with your module id** (`bellum_siege_song`). A one-shot rule is
  sealed in the save by its id alone, so the first pack read keeps an id and a later rule reusing it is dropped and
  logged (`[SCENARIO] rule '...' from YourMod skipped`).
- **Name heroes and kingdoms by their game id** (`lord_1_14`, `empire_s`), not by name: the game translates names,
  so a name only resolves in one language. An exact id always wins over a name.
- `IF_HERO_IS` asks who the event is about: for a death, the one who died (never the killer); for a marriage, either
  spouse.
- Every rule that answers an event plays, one after the other in reading order, each checked at its own turn (it sees
  what the rules before it changed). Rules sharing a `group` are alternatives: one of them is drawn, by `weight`. Use a
  group for outcomes that exclude each other (CR's four outcomes of marrying Rhagaea are the group `rhagaea_marriage`).
- A rule plays only when every one of its consequences can really apply now: a war between realms already at war, a
  peace between realms at peace, a relation with someone no longer in the world. Otherwise it does not play for this
  event and its message is not told; it is not sealed, so it is asked again only if the same trigger fires again (a
  death happens once: a rule on one hero's death that cannot apply then never plays). Guards check the rest: `IF_AT_WAR:A,B`, `IF_AT_PEACE:A,B`, `IF_ALIVE:hero`, `IF_KINGDOM_EXISTS:kingdom`,
  `IF_RULES:hero,kingdom`, besides `IF_HERO_IS`, `IF_PLAYER_INVOLVED`, `IF_PLAYER_RULES`, `IF_PLAYER_AT_WAR` and the
  player's sex.
- In game: `cr.scenarios` lists the loaded rules and whose pack each came from; `cr.testall` (`scenario_packs`)
  reports a rule whose trigger, guard or consequence CR does not know, or whose hero or kingdom does not resolve.
- **Answering a written tale** (since 2.7.10): every stage a tale reaches fires `ON_TALE:<tale id>:<stage id>`, and
  every ending `ON_TALE:<tale id>:end:<ending id>` (`end:reward`, `end:failure`). A rule naming the tale alone
  (`ON_TALE:<tale id>`) answers all of it. The tale's people are written as roles, in braces, wherever a rule names a
  hero: `{giver}`, `{husband}`, `{rival}`, `{player}`; where a realm is meant, a role stands for the realm that hero
  belongs to (`DECLARE_WAR:{husband},{player}`). `IF_RULES:{husband}` alone asks whether he rules his own kingdom.
  The rule's message names them too ("{husband}'s realm goes to war"). `PRESS_FOR_WAR:{husband},{player}`: a lord
  presses his king toward war; the king weighs his regard for him, the wars he already fights and the strength he
  would face, and declares or not (a king pressing himself declares).

### Tale progress (`CalradiaRemembers.CrTales`, since 2.7.12)

Read-only events for a tale's progress, the same moments scenario rules answer with `ON_TALE`, for answers that belong
in your own code:

```csharp
CrTales.StageReached += p => { /* p.Where is the stage id */ };
CrTales.Ended += p =>
{
   // p.Where: "end:reward", "end:<id>", "end:failure" or "end:abandoned"
   bool sheLeft = p.Consequences.Any(c => c.Type == "elope" && c.Status == CrTaleConsequenceStatus.Done);
};
CrTales.ConsequenceSettled += p => { /* a return that waited: Done, or NotDone after 60 days */ };
```

`CrTaleProgress` carries `TaleId`, `Where`, `GameDay`, `Roles` (role name to hero StringId or place id, the player as
`player`), `Flags` (set so far on the path taken) and `Consequences`: what this stage's `on_enter` or this ending
carried out, each with `Type`, `WhoId`, `OtherId`, `Status` (`Done`, `NotDone` with a `Reason`, or `Waiting`) and only
the ones whose `if` held. Answer what is `Done`: an `elope` is `NotDone` when the marriage could not end (she is with
child, a captive, or leads her house). A `return_spouse` is `Waiting` while her husband is on campaign or either is a
captive; `ConsequenceSettled` follows (`Where` "settled") when it happens or is given up. An abandoned tale carries out
nothing and is not sent to scenario rules. Tales whose id begins `cr_selftest_` are cr.testall's own.

### Matches the player brokers (`CalradiaRemembers.CrMatches`, since 2.7.12)

Hand the player a match to broker: the one who decides is taught `arrange_match` for it in CR's chat, and CR's own law
settles it.

```csharp
MatchRegistrationResult r = CrMatches.Register(new CrMatchRequest {SuitorId = suitorId, BrideId = brideId, DeciderId = headId, SourceMod = "MyMod"});
CrMatches.Settled += o => { /* o.Wed, or o.Reason: her heart, her family, a war between the houses... */ };
CrMatches.Withdraw(suitorId, brideId);
```

`DeciderId` is the bride herself, or the head of her house or a parent. The door refuses at once, keeping nothing,
what the law would refuse outright: an id that names no one (`RejectedUnknown`), one person twice
(`RejectedSamePerson`), the player on either side (`RejectedPlayer`: the player's own marriage keeps CR's own rules), a couple the game will not wed (`RejectedNotLawful`), a decider with no say over her hand
(`RejectedNoAuthority`). What can change later is weighed when the decider consents in the chat, as for a match asked
by letter: no captivity and no war between the houses, she must hold the suitor a friend, and her family's regard for
the player or a gain in standing. `Settled` fires each time the decider is asked: a refused match stays registered and
may be asked again; a wed one is dropped, as is a match whose suitor or bride died or was wed meanwhile, or whose
decider lost the say over her (a new head of house: register again with the new one). A match asked
of the player by letter is taught first. Kept in memory only and forgotten when the game is left: register again when a
game is loaded.

### Companion patrols (`CalradiaRemembers.CrPatrols`, since 2.7.17)

Read the patrols the player's companions lead, and hear their battles and their end:

```csharp
foreach (CrPatrolInfo p in CrPatrols.Current) { /* p.CompanionId, p.SettlementId, p.BattlesWon, p.RidingHome... */ }
CrPatrols.Fought += b => { /* b.Won, b.FoeName, b.FoeMen; b.Patrol already counts this battle */ };
CrPatrols.Ended += e => { /* e.How: Back, Scattered, Captured or Killed; e.MenBack when Back */ };
```

Every `CrPatrolInfo` is a copy taken when you asked or when the event fired: changing it changes nothing. `Ended` fires
after the patrol left `Current`. A patrol leaves only through one of the four ends, so each one you saw in `Current`
ends with exactly one `Ended`.

### Queens with a chosen face (`ModuleData/cr_faces.json`, since 2.7.10)

`{ "queens": { "<realm id>": "<BodyProperties version=... key=... />" } }`: the line the face editor copies with
Ctrl+C, one per realm id (`vlandia`, `empire_s`...). CR dresses each realm's ruler's wife in it, at each session and
each day, and gives a former queen her own face back when the king remarries or the face is taken out. Only the face
changes: her name, age and build stay hers. Read in this order, the first one keeping a realm: the player's
`Custom/faces.json`, each mod's `ModuleData/cr_faces.json` in launcher order, CR's `ModuleData/faces.json`.
`cr.testall` (`realm_faces`) reports a line that does not load and a realm id the game does not know.

A realm can also carry her chat portrait: `"<realm id>": { "face": "<BodyProperties ... />", "portrait":
"QueenPortraits/queen_<realm>.jpg" }`. The picture (.png or .jpg, any shape: the chat keeps its proportions) sits under
the faces file's own folder, never outside it. It follows the face: the woman wearing it shows it in chat, letters and
the council, and a former queen goes back to her usual portrait. A picture the player dropped in `ChatPortraits` for
that hero still wins.

### Reading regard (`CrNpcMemory.GetRegard`, since 2.7.4)

```csharp
int? regard = CrNpcMemory.GetRegard(npcId);   // -100..100 on CR's own ledger, or null when CR holds no profile yet
```

The character's own personal regard for the player, built in conversation: never the game's relation. Read-only;
`RegardChanged` below reports every move of it.

### What lies between two characters (`CrPairRegard`, since 2.7.17)

When the player names someone in the chat, the speaker is told whether that person is a friend or an enemy and why;
the encyclopedia's friends, enemies and houses read the same weighing. Add what your mod knows to it:

```csharp
public sealed class MySource : IPairRegardSource
{
   public IEnumerable<PairRegardTrace> TracesFor(Hero speaker, Hero other, int today)
   {
      // as the SPEAKER sees the other; nothing when nothing lies between them
      yield return new PairRegardTrace {
         Weight = 25,                                         // signed, on the -100..100 regard scale
         Line = "you fought side by side for years",          // told to the speaker in the chat
         PageLine = "fought side by side for years",          // shown on the speaker's page
         KnownToPlayer = true                                 // false: the speaker knows it, the page never shows it
      };
   }
}

CrPairRegard.RegisterPairRegardSource(new MySource());
```

`PairRegardTrace` is in `CalradiaRemembers.Logic.dll`. A friend takes +20 or more, an enemy -20 or less, ties and CR's
own traces included. What all sources say counts together for at most 40 either way (`PairRegardPolicy.OutsideCap`):
enough to tip a pair, never enough to drown what happened in CR (a kinsman executed weighs -70). On equal weight CR's
own reason is said first. `TracesFor` is called for each page opened and each character named in the chat, so keep
it cheap. A source that throws adds nothing and is logged once. The page weighs the vanilla friends and enemies and
those CR ties to the character; a source does not add new people to it.

### Cultures (`CrCultures.MarriageCustom`, since 2.7.5)

```csharp
MarriageCustomReading custom = CrCultures.MarriageCustom(hero.Culture.Name.ToString(), hero.Clan?.Tier ?? 0);
// custom.Strictness: 0 low, 1 medium, 2 high; custom.SharesPartners: a house of that tier shares partners
```

How strictly a culture holds marriage, by CR's own jealousy cultures (jealousy_cultures.json, or a Custom copy). The
stable door to what `CalradiaRemembers.Jealousy.JealousyCultureLoader` answers: read this, never the internal type.

### Romance (`CrRomance`, since 2.7.5)

```csharp
bool? welcome = CrRomance.WouldAcceptIntimacy(npcId);   // CR's chat would allow intimacy with the player now
SharedIntimacyResult r = CrRomance.RecordSharedIntimacy(npcId, "We spent the night at the inn in Pravend.", "MyMod");
```

`WouldAcceptIntimacy` reads the consent bar CR's own chat holds (regard, the character's nature, a marriage to
another); false when adult content is off, null for an unknown hero. `RecordSharedIntimacy` records a night that
happened in your mod as if it had happened in CR's chat: the memory, and the romance arc moved exactly as the chat
moves it. It is refused, writing nothing (`NotWelcome`), whenever the chat would not have allowed it, so ask
`WouldAcceptIntimacy` before offering the night. Regard is not moved. A plain `Intimacy` memory through `AddMemory`
stays a memory only: it never moves the romance arc.

CR also writes, on its own, the memory a quest giver keeps of the game's own quests (begun, done, failed, let lapse):
do not inject those.

### Regard changes (`CrNpcMemory.RegardChanged`, since 2.6.7)

```csharp
CrNpcMemory.RegardChanged += change =>
{
    // change.NpcId, change.Before, change.After (-100..100), change.Cause, change.GameDay, change.SaveId
};
```

Fired each time a character's **personal regard** for the player actually changes, whatever changed it: a
conversation, a letter, a quest, a promise kept or broken, a grievance put to rest, jealousy, a rescue, a
departure from their realm. `Cause` is one of `conversation`, `letter`, `quest`, `commitment`, `compensation`,
`grudge`, `jealousy`, `divorce`, `rescue`, `stance`, `violence`, `departure`, `clan_standing`, `self_test`, `unspecified`;
more may be added, so treat an unknown one as a change like any other. `clan_standing` (since 2.6.8) is the
one-time warm start a character's clan standing gives at a first meeting: it comes FROM the game's relation, so
a mod mirroring regard into that relation should skip it. `self_test` (since 2.6.9) is every change made while
`cr.testall` runs: the tests move regard on real characters and put it back without a notice, so skip it too.

**Which deeds write the game's relation as well.** Of CR's own regard changes, only a divorce also writes the
game's relation for the same event; council outcomes, ransoms and schemes write the game's relation without moving
regard, so a mirror cannot count them twice. Raised on the game thread, after the change, each listener
under its own guard.

**Two numbers, two names.** CR keeps its own regard (what one character personally thinks of the player,
built in conversation) apart from the game's relation. In the prompt the game's value is called **"your clan's
standing"** and CR's is **"your own personal regard"**; write prompt rules with those words, or the model may
read the wrong number. `RelationToPlayer` in `VerbFacts` and `PromptVarFacts` is always CR's personal regard,
never the game's relation.

**CR never writes the game's relation from regard, on purpose.** The game's relation is effectively clan-wide
(the engine resolves a lord's relation through his clan leader), and regard is paced (a +1 cap and a cooldown
in conversation) precisely so a player cannot talk their way to the top. A mod that mirrors `RegardChanged` into
the game's relation takes that balance on itself; do it from this event, never as a side effect of a prompt
variable provider.

## Notes and limits (v1)

- **Kinds you cannot spoof.** `FirstMeeting`, `Captivity`, and `Farewell` are engine-owned (they drive
  have-we-met logic, compression, and input locking). A caller asking for one gets `Other` instead.
- **`Hearsay` for news a character only heard** (since 2.7.2). A memory of kind `Hearsay` is shown apart from what
  the character lived with the player, under a "what has reached your ears" header. It is never counted against the
  6 lived memories the Compact prompt keeps (Compact shows the 2 latest heard ones beside them), and it is never
  shielded from compression by being recent. Any other kind is history with the player: use `Hearsay` for a rumour.
- **`OwnLife` for the character's own life** (since 2.7.5): their attachments, grudges, feuds and affairs with
  others, things the player had no part in. Shown under its own header, apart from the history with the player, never
  counted against the lived memories Compact keeps, folded first by compression, and never framed as a predecessor's
  history when the player's heir takes over. A memory of kind `Other` is history WITH the player: do not file a
  noble's own affair as `Other`.
- **No quota on AddMemory.** Unlike `Feed.Record` (20 events per mod per day), nothing limits how many memories a
  mod injects; news of the world belongs in `Feed.Record`, and a memory is for what touches that character.
- **Summary length** is bounded (300 chars) to keep the save blob safe; longer lines are trimmed.
- **`Started` fires for every conversation type** (ordinary, captive, companion, commoner). v1 does not
  tag which; ask if you need that distinction.
- **No update/remove yet.** v1 is read + inject. Targeted edit/remove and a `Discovery`-based recall path
  are candidates for a later version.

## Calling without a compile-time reference

If you would rather not reference CR's assembly, reach these types by reflection: read the static
`CrNpcMemory.ApiVersion` first, then invoke `CrNpcMemory.GetMemories` / `CrNpcMemory.AddMemory` and
subscribe to the `CrConversation.Started` / `CrConversation.Ended` events on the `CalradiaRemembers`
types.

## Quick in-game check

`cr.api_probe <npc name>` prints the API version and save id, injects a test memory on that NPC, and
reads it back, so you can confirm the door works before wiring your addon.
