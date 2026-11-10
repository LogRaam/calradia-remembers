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
private, never in front of others (an embarrassing secret, something said in confidence). Since 2.7.19 a private
memory from your mod (any `SourceMod` other than CR's) also stays out of the chat's Memories panel, so the player does
not read a secret before it comes to light; a player who sets "Show secret memories" in the mod options sees it there,
marked secret. Give the memory a `SourceMod` (your mod's name): a private memory sent without one is recorded as
"(another mod)" and kept out of the panel too, but secrets stored before 2.7.19 with no source read as CR's own and
stay listed.

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

### Station definitions (`ModuleData/stations/<station>.txt`, since 2.8.0)

Eleven files say who a character of each social station is (`[who]`) and how they treat the player at each rung of the
player's standing (`[nobody]`, `[sellsword]`, `[risinghouse]`, `[lordoftherealm]`, `[sovereign]`): ruler, lord, noble,
companion, wanderer, merchant, artisan, ruralnotable, headman, preacher, gangleader. They take a `cr_patch` like every
narrative file, with one rule of their own: a line belongs to the section above it. A patch's lines are added after CR's,
so a patch that does not open with its own `[section]` header lands inside the last section, `[sovereign]`; open your patch
with the header of the section you mean. Repeating a header CR already has replaces that whole section, which is how you
rewrite one. What a station may do (a duel, a brawl) is decided by the game, never by these files. `cr.station <name>` shows
what a character is given.

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
- In game: `cr.scenarios` lists the loaded rules and whose pack each came from; `cr.test all` (`scenario_packs`)
  reports a rule whose trigger, guard or consequence CR does not know, or whose hero or kingdom does not resolve.
- **Answering a written tale** (since 2.7.10): every stage a tale reaches fires `ON_TALE:<tale id>:<stage id>`, and
  every ending `ON_TALE:<tale id>:end:<ending id>` (`end:reward`, `end:failure`). A rule naming the tale alone
  (`ON_TALE:<tale id>`) answers all of it. The tale's people are written as roles, in braces, wherever a rule names a
  hero: `{giver}`, `{husband}`, `{rival}`, `{player}`; where a realm is meant, a role stands for the realm that hero
  belongs to (`DECLARE_WAR:{husband},{player}`). `IF_RULES:{husband}` alone asks whether he rules his own kingdom.
  The rule's message names them too ("{husband}'s realm goes to war"). `PRESS_FOR_WAR:{husband},{player}`: a lord
  presses his king toward war; the king weighs his regard for him, the wars he already fights and the strength he
  would face, and declares or not (a king pressing himself declares).
- **A night in captivity, and a tale a rule opens** (since 2.7.21): `ON_CAPTIVE_NIGHT` fires once a night (at 23:00)
  while the player is a prisoner. `IF_CAPTOR_IS:hero` holds when the player is a prisoner of that hero's house (in a town
  or castle it holds, or in a party one of its members leads); `IF_PRESENT:hero` when that hero is where the player is
  held; `IF_CHANCE:0.25` on a roll below the chance (a chance that is no number never holds). `OPEN_TALE:tale,giver`
  opens a written tale that has `on_scenario` (HOWTO_Quests.md), with that giver, by its popup; the tale's own rules
  hold, and the rule does not play when they refuse it. Rhagaea's night, written as a rule:
  `ON_CAPTIVE_NIGHT` with `IF_CAPTOR_IS:lord_1_14`, `IF_PRESENT:lord_1_14`, `IF_CHANCE:0.25`, then
  `OPEN_TALE:rhagaea_night,lord_1_14`.

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
nothing and is not sent to scenario rules. Tales whose id begins `cr_selftest_` are cr.test all's own.

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

**A woman of the player's house (since 2.7.19).** When the player holds the say over the bride (the head of her house,
or her parent: his daughter, his sister), make the GROOM's side the decider: the groom himself, the head of his house,
or his parent. The door takes it as a match that asks the player: the groom's side is taught, in a conversation the
player opens, that he may ask for her hand; when the player gives it, CR asks the player in the chat "Give {bride}'s
hand to {groom}?" [Y/n], so she is never wed on the model's reading of a sentence. The law is the same (lawful, no war
or captivity between the houses, she holds him dear enough), and the family's consent is the player's own yes.
`Settled` reports it as any other. To have him bring it up unbidden, give him a `CrAims` aim as well.

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

### A letter from your mod, in CR's letter window (`CrLetters.Deliver`, since 2.7.17)

```csharp
bool sent = CrLetters.Deliver(lord, "My friend, the lists open in Pravend in five days...", "MyMod");
```

The body is yours, written in full; CR never rewrites it. It travels as CR's letters do: a courier rides out at once
and the letter opens in CR's window (parchment, the sender's hand and portrait) when it reaches the player, then stays
in the letter book. The player may reply, and the sender answers in CR's own voice; the sender remembers having
written it. False when refused: no campaign loaded, an empty body or one over 4000 characters, the player as sender,
or a sender who cannot write now (a child, a captive, dead or out of the world). There are no answer buttons yet: keep
your own Accept/Decline where you need one.

**Following your letter (since 2.7.19).** `DeliverWithId` does the same and returns the letter's id (null when
refused). Keep it, then ask about the letter or hear what the player did with it, without reading CR's internals:

```csharp
string id = CrLetters.DeliverWithId(lord, body, "MyMod");

CrLetters.Replied  += (letterId, sender, reply) => { if (letterId == id) { /* the player's own words */ } };
CrLetters.SetAside += (letterId, sender)        => { if (letterId == id) { /* read, not answered */ } };

string status = CrLetters.GetStatus(id);  // "on_the_road", "lost", "in_box", "delivered", "set_aside", "answered", "unknown"
string reply  = CrLetters.GetReply(id);   // the reply's text, or null
```

Both events are raised for every mod's letters: compare the id. `SetAside` is raised once, when the player
acknowledges the letter without replying, or throws it away unread from the letter box; a letter still `delivered` has
been read but not dealt with yet. Since 2.8.0 a letter that reached the player but waits unopened in the letter box is
`in_box` (it was `delivered` before the box). `lost` means its
courier was lost and CR sends it again. All of these name no Calradia Remembers type, so a mod that keeps CR optional
can use them (see the regard sources below). Letters delivered before 2.7.19 have no id.

### Lords with aims of their own (`CrAims`, since 2.7.19)

```csharp
AimResult r = CrAims.Register(new ExternalAim {
   Id = "lakonon", SourceMod = "MyMod", NpcId = lord.StringId,
   Want = "Lakonon. By right it is the Osticos', but the Vlandians hold it and the council will not vote a war for it.",
   PlayerPart = "his men can take what a claim alone cannot",   // optional, one line
   MissionId = null,                                            // optional: a MissionBoard deed the lord may ask outright
   ExpiresOnDay = null                                          // optional
});
CrAims.Raised += r => { /* r.AimId, r.SourceMod, r.NpcId, r.Channel, r.GameDay */ };
CrAims.Retract("MyMod", "lakonon");
```

You say WHAT a lord wants; CR decides WHEN it surfaces and remembers that it did. In a conversation the player opens,
the lord has leave to bring it up himself: at most twice, the second time only after 60 days while it still stands,
never toward an enemy or in captivity. He raises it as himself: an honest lord asks openly, a calculating one keeps his
purpose close, and neither states a falsehood. Without a `MissionId` he may sound the player out but promises nothing
the game cannot carry. One aim per lord across all mods (`LordAlreadyHasAnAim`), 40 per source, want 400 characters,
player's part 200; an aim is kept with the save. `Raised` means the lord was GIVEN THE FLOOR, not that he said it: he
may judge the moment wrong and stay silent.

Three channels, all on by default; `Channels` ("conversation", "letter", "audience") narrows them, and the twice-at-most
rule counts across all three:
- **conversation**: the lord may bring it up in a conversation the player opens.
- **audience** (since 2.8.0): it is a reason for him to seek the player out, the least urgent of all (a grudge, fresh
  news or the war duty come first), paced by the player's "lords approaching" setting: he asks for a word when the player
  walks into the place he stands, or rides out to meet them on the road. The invitation says only that it is "a matter of
  their own"; in the conversation that follows he may put it, even if "conversation" is not among the channels. `Raised`
  reports `audience` when that conversation opens.
- **letter** (since 2.8.0): he may write to the player about it, through the paced mailbox like any other letter (its
  budget, his cooldown, the game's own rules on who may write). The letter is written from your Want, PlayerPart and his
  manner alone. `Raised` reports `letter` when the mailbox releases it.

### Who ended a mercenary contract (`CrMercenaries.ContractEndedByEmployer`, since 2.7.19)

```csharp
CrMercenaries.ContractEndedByEmployer(kingdom);   // the realm let the player's company go
```

The game reports a contract the employer ended and one the player walked out of the same way, and CR judges the end
by the realm's war: leaving a realm losing its war is held against the player by its lords. If your mod ends the
player's contract (a dismissal, a term run out, the war it was hired for over), say so the same day, before or after
the game reports the end: it is then a plain end, never an abandonment, and the player taking service with the
realm's enemy afterwards is no betrayal. Never call it when the player chose to leave. Names no Calradia Remembers
type. (tashmetu, for Mercenary Management.)

### Forbidding a conception (`CrConception.RegisterVeto`, since 2.8.0)

```csharp
CrConception.RegisterVeto("Traits of Calradia", (mother, father) =>
   IsSterile(mother) || IsSterile(father) ? "Sterile" : null);   // father is null when unknown
```

CR creates pregnancies of its own: an intimate act carried to completion, a tale's reward, a lover's child. Before
each, it asks every registered veto; the first that answers with a few words forbids it, and the log says who and why
(`[PREGNANCY] no conception: <source> forbids it ...`). Return null to let it be. A veto that throws is logged and never
forbids. Registering the same source again replaces its veto; `CrConception.UnregisterVeto(source)` withdraws it. Not
saved: register when a game starts or loads. The engine's own daily pregnancies are not CR's and are not asked.
(tashmetu, for Traits of Calradia's Sterile.)

### Queens with a chosen face (`ModuleData/cr_faces.json`, since 2.7.10)

`{ "queens": { "<realm id>": "<BodyProperties version=... key=... />" } }`: the line the face editor copies with
Ctrl+C, one per realm id (`vlandia`, `empire_s`...). CR dresses each realm's ruler's wife in it, at each session and
each day, and gives a former queen her own face back when the king remarries or the face is taken out. Only the face
changes: her name, age and build stay hers. Read in this order, the first one keeping a realm: the player's
`Custom/faces.json`, each mod's `ModuleData/cr_faces.json` in launcher order, CR's `ModuleData/faces.json`.
`cr.test all` (`realm_faces`) reports a line that does not load and a realm id the game does not know.

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

**If your mod keeps Calradia Remembers optional**, do not implement `IPairRegardSource`: the game reads every type of
your DLL when it loads it, and a class that implements an interface of CR's cannot load where CR is absent, so the
game refuses your whole mod ("could not be loaded correctly due to a dependency conflict"; tashmetu found it with
Regard and Standing). Since 2.7.19 register a function instead, from a method you call only once CR is there:

```csharp
CrPairRegard.RegisterPairRegardSource("MyMod.regard", (speaker, other, today) =>
   MyTraces(speaker, other)                              // your own code, naming none of CR's types
      .Select(x => (x.Weight, x.Line, x.PageLine, x.KnownToPlayer)));   // (weight, line, pageLine, knownToPlayer)

CrPairRegard.UnregisterPairRegardSource("MyMod.regard");
```

The same applies to every door: keep CR's types out of your own classes' signatures, fields and base types. Doors
documented "Names no Calradia Remembers type" are built for this.

`PairRegardTrace` is in `CalradiaRemembers.Logic.dll`. A friend takes +20 or more, an enemy -20 or less, ties and CR's
own traces included. What all sources say counts together for at most 40 either way (`PairRegardPolicy.OutsideCap`):
enough to tip a pair, never enough to drown what happened in CR (a kinsman executed weighs -70). On equal weight CR's
own reason is said first. `TracesFor` is called for each page opened and each character named in the chat, so keep
it cheap. A source that throws adds nothing and is logged once. The page weighs the vanilla friends and enemies and
those CR ties to the character; a source does not add new people to it.

### Which starting relations CR believes (`CrPairRegard.RegisterStartReason`, since 2.8.3)

The game's relation between two lords of different houses answers for their two clan leaders, and a new campaign
draws thousands of friendships and enmities between lords who never met. Since 2.8.3 CR reads every relation between
two NPCs on the pair's own slot (never the leaders'), and splits it in two:

- **What changed since CR first saw the pair change** is history, believed as it is. A change made through the game's
  `ChangeRelationAction` is heard; a value written with `SetPersonalRelation` is not, so the game's draw and any
  seeding your mod writes that way count as the START.
- **The start** is believed only when the two have a reason to know each other: kin, one house, one court, neighbouring
  lands, a war between their realms (for an enmity), or both ruling. With none, CR acts as if it were 0 and the
  character is only told of "a vague unease" (or goodwill) with no known cause.

If your mod seeds ties with reasons of its own, give CR the reason:

```csharp
CrPairRegard.RegisterStartReason("My Mod", (a, b) =>
   MyRivalPeoples(a, b) ? "an old rivalry between your peoples" : null);   // completes "known to each other through ..."

(int points, string basis, string reason)? bond = CrPairRegard.GetBond(heroId, otherId);   // what CR believes, or null
CrPairRegard.UnregisterStartReason("My Mod");
```

- The function is asked only for a start CR could not explain itself, so keep it cheap; one that throws is logged once
  and passed over. Register again each time a game starts or loads.
- `basis` is `"None"`, `"Earned"` (moved in play), `"Known"` (a reason) or `"Unexplained"` (start set aside).
- Nothing CR keeps for good reads a start before day `CrPairRegard.StartGraceDays` (2) of a new campaign: no shared past
  is drawn and no plot launched, so ties your mod seeds in the first days are what CR reads.
- A change your mod makes later, silently, would be read as part of the start. Announce it through the game's
  `HeroRelationChanged` event (`CampaignEventDispatcher.Instance.OnHeroRelationChanged`) after you write the value, with
  the exact change (CR reads the value before as the value now less the change) and no notification. After a pair's
  first announced change, everything since counts as earned.
- When CR changes a relation between two NPCs it writes their own slot, and goes through the game's action only for
  the heads of two houses or two of one house. A younger son's quarrel no longer moves his father's standing.
- A character who does not lead their house is told their house's standing in a sentence of its own ("Between
  houses: your house, under Derthert, is at odds with ..."), apart from their own friends and enemies.

### Tales a character carries beyond their people's (`CrFolklore.RegisterTaleSource`, since 2.8.7)

Each character carries up to six folk tales of their own people (`ModuleData/folklore/<culture id>.txt`), loves one best
and can tell that one in full. A faith, an order or a family that crosses cultures can give a character tales too:

```csharp
CrFolklore.RegisterTaleSource("My Mod", hero =>
   MyFaithOf(hero) is { } faith ? faith.TalesText : null);   // the text of a folklore file, or null
CrFolklore.UnregisterTaleSource("My Mod");
```

- Return the text of a folklore file, in the format of `ModuleData/folklore/battania.txt`: `[Title]`, `gist:` (required),
  `beat:` lines in order. A tale with beats may become the favourite the character tells in full; one without can only
  be named or alluded to.
- Your tales share the six places with the character's people's, alternating one and one, so neither crowds the other
  out. A tale whose title their people already tells is carried once.
- Return the same text for the same hero from one day to the next: the tales sit in the cached part of the prompt. It is
  called on every prompt built for that character, so keep it cheap (CR parses each distinct text once). One that throws
  is logged once and passed over. Registering the same name again replaces it; register each time a game starts or loads.

### A line on how a character looks (`CrAppearance.RegisterLineSource`, since 2.8.7)

CR describes a character's body from their face (and the player's appearance prose): to the character themselves ("YOUR
OWN APPEARANCE", in the second person) and to the character the player speaks with ("What you can SEE of them", in the
third person). Your mod may add a line to either:

```csharp
CrAppearance.RegisterLineSource("My Mod", (heroId, seenByOther) =>
   IsScarred(heroId) ? (seenByOther ? "A scar runs across her left cheek." : "A scar runs across your left cheek.") : null);
CrAppearance.UnregisterLineSource("My Mod");
```

- `seenByOther` true is the third person (the player as a character sees them); false is the character's own body,
  second person ("you"). Write the line to match.
- CR's own text comes first and is never changed. At most two lines from all mods are kept, in the order of the mods'
  names, and a line over 200 characters is left out whole (never cut).
- Return the same line until the looks change (the character's own appearance sits in the cached part of the prompt),
  and keep it cheap. One that throws is logged once and passed over.

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

**The bond itself (since 2.7.20).** A bond made in CR's chat (a courtship, a secret lover, a consort) lives in CR; read
it, and hear when it changes, to carry it into your own mod (LapitaPolata: "Affairs of Calradia does not know about it"):

```csharp
CrRomanceStatus s = CrRomance.GetStatus(npcId);   // null for an unknown hero or no campaign
// s.Bond: "none", "curious", "courting", "lovers", "secret_lover", "committed", "consort", "spouse",
//         "former_spouse", "estranged" or "broken"; s.SinceDay (null when no day was recorded); s.OpenTerms

CrRomance.StatusChanged += change =>
{
    if (change.DuringSelfTest) return;            // cr.test all moves a bond and puts it back
    // change.NpcId, change.Before.Bond, change.After.Bond, change.After.SinceDay, change.GameDay, change.SaveId
};
```

The bond is read as the encyclopedia's Heart section tells it: the game's marriage first (an ex-spouse is
"former_spouse" even when CR's romance still reads committed), then CR's own romance. "secret_lover" is any love kept
hidden: the player's own secret lover, or a married character's affair with the player. New words may be added: treat
an unknown one as "none". Bonds are read when a conversation closes and every in-game hour, so the event may come up
to an hour after the change; a loaded save's bonds are never announced, read them with `GetStatus` at session start.
CR never writes into your mod: what a bond means there is your bridge's call.

**Hidden affairs between lords (since 2.8.3).** Since 2.8.2 a married lord may take a hidden lover in CR (a handful in
the realm, never the player, nor the player's spouse or household). If your mod has affairs of its own, keep one lord
from carrying one in each mod: CR asks your veto before any affair, tells you when one begins or ends, and lets you
read them. CR knows nothing of your mod (tashmetu, for Romance & Seduction):

```csharp
CrRomance.RegisterAffairVeto("My Mod", (married, lover) =>
   InMyAffair(married) || InMyAffair(lover) ? "already in an affair" : null);   // null lets it be

CrRomance.HiddenAffairChanged += change =>
{
    if (change.DuringSelfTest) return;            // cr.test all begins one and ends it
    // change.SubjectId (the married lord), change.LoverId, change.Began (false when it ended), change.GameDay
};

CrHiddenAffair a = CrRomance.GetHiddenAffair(heroId);   // either side; null when none
IReadOnlyList<CrHiddenAffair> all = CrRomance.GetHiddenAffairs();
```

The veto works like the conception veto: the first that answers with words forbids the affair, and the log says who
and why (`[AFFAIR] no affair: <source> forbids it ...`); one that throws is logged and never forbids; the same source
registered again replaces its veto; `UnregisterAffairVeto(source)` withdraws it. Not saved: register when a game
starts or loads. An affair ends on a death, a marriage, the player telling it, or after about three years. A loaded
save's affairs are never announced: read them with `GetHiddenAffairs` at session start. Before 2.8.3 the only way in
was CR's internal `HiddenAffairBehavior`: use these instead, which will not move.

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
`cr.test all` runs: the tests move regard on real characters and put it back without a notice, so skip it too.

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
