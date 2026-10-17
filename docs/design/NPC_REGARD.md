# NPC regard: friends, enemies and a shared past, read from what the world remembers

DESIGN, 17/10/2026. Nothing here is built. Written for Gabriel to rule on the open questions at the end before any
increment starts.

Paths below are relative to the umbrella folder: `MOD` = `Calradia Remembers/CalradiaRemembers/`, `LOGIC` =
`Calradia Remembers/CalradiaRemembers.Logic/`, `SDK` = `LLM Roleplay Service/NpcMemoryService.Core/`, `GAME` =
`C:/Users/logra/BannerlordDecompiled/1.4.8/`, `PREFAB` = the game's
`Modules/SandBox/GUI/Prefabs/Encyclopedia/EncyclopediaSubPages/EncyclopediaHeroPage.xml`. A reference marked
"(trace)" was reported by a read-only tracing agent and is NOT VERIFIED by the author of this document (not
re-opened); every other reference was read directly.

## 1. Where this comes from

Gabriel, 17/10/2026: the vanilla encyclopedia hero page lists "Friends" and "Enemies" from vanilla hero-to-hero
relation. He saw an NPC listed as a Friend (vanilla relation 99) whose personal regard, on the mod's own ledger, was
-80. The mod judges relationships on its own ledger (`NpcProfile.ReputationWithPlayer`), so the page contradicts the
game the player is actually playing.

His rulings, not relitigated here:

1. The encyclopedia shows lists of CLANS (hostile, with affinity) in place of the vanilla NPC lists, then NPC Friends
   and Enemies by PERSONAL regard.
2. NPC-to-NPC regard is GENERATED AT CONSULTATION, never ticked for every pair. It is DERIVED from real traces, and
   every entry carries its REASON, so everything is verifiable.
3. When two NPCs have no relationship, there is a CHANCE of a generated PAST event between them, drawn once,
   persisted, dated before the campaign, injected into world history, counting toward friend/enemy. Many pairs get
   nothing. Only pairs the player actually looks at.
4. Bonus: conversation describes third parties from the same derived regard, so the encyclopedia and the
   conversation agree.

## 2. Three corrections to the premises (read these first)

### 2.1 Vanilla's friend line is +-50, not +-10

`Hero.IsFriend` is `GetHeroRelation(this, other) > DiplomacyModel.MaxNeutralRelationLimit` and `IsEnemy` is
`< MinNeutralRelationLimit` (`GAME/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/Hero.cs:2087-2095`). The
default model sets those limits to 50 and -50
(`GAME/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.GameComponents/DefaultDiplomacyModel.cs:72,74`). The page
fills its lists in `EncyclopediaHeroPageVM.AddHeroToRelatedVMList` (trace: `EncyclopediaHeroPageVM.cs:1066-1079`),
for every living adult non-notable hero. The +-10 the brief mentions is the mod's own `VanillaRelation.FriendTier` /
`EnemyTier` (`SDK/Standing/VanillaRelation.cs:30,49`), used by the mod's PROMPT, not by the page.

### 2.2 The case Gabriel saw needs no derivation at all

A regard of -80 is an NPC's regard for the PLAYER: that is the only personal ledger that exists. On the PLAYER's own
page, the right Friends and Enemies are simply the heroes whose `ReputationWithPlayer` is warm or cold. That ledger
already exists, is persisted, and needs no new rule. So the first increment (section 11) fixes exactly what he saw,
and proves the encyclopedia mechanism, before any NPC-to-NPC regard is designed into code.

### 2.3 NPC-to-NPC traces barely exist today

Almost every trace the mod keeps is between an NPC and the player. Verified per system:

| System | Can it hold an NPC-to-NPC edge? | What actually plants it |
|---|---|---|
| Grudges | Yes: `Grudge.TargetId` is general (`LOGIC/Grudges/Grudge.cs:23-24`) | Only three non-player planters (trace): `TryPlant(victim, captor, CaptorCruelty)` (`MOD/Chat/ChatViewModel.cs:3875`), a tale consequence (`MOD/.../AuthoredQuestBehavior.cs:1366`), a leaked letter whose author is an NPC (`MOD/.../CourierSecretBehavior.cs:459`). Every other plant targets `Hero.MainHero`. |
| Appreciations | Yes: `Appreciation.TargetId` (`LOGIC/Appreciations/Appreciation.cs:26`) | Every live plant found targets the player (trace). |
| Executed kin | No state of its own | Grudge against the player only (trace: `ExecutedKinGrudgeBehavior.cs:79,107`). |
| Stolen love, jealousy | Record the jilted spouse / rival, but | the resulting grudge targets the player (trace: `NpcMarriageEstrangementBehavior.cs:330`, `JealousyBehavior.cs:431`). |
| Duels, tournaments, nemesis | No | Player-centred by construction (trace: `TournamentOutcomeCapture.cs:62` skips matches without the player). |
| Schemes | Yes, while live: `Scheme.PlotterId` / `TargetId` (trace: `LOGIC/Scheme.cs:42`) | Terminal schemes are removed (trace: `SchemeStore.cs:495`); the outcome survives only as a `Rivalry` WorldEvent. |
| World events (battles, executions) | Yes: `WorldEvent.ActorIds` (trace: `MOD/WorldEvents/WorldEvent.cs:100`); a battle records both commanders (trace: `WorldEventFeeders.cs:667`) | Third-party events are DELETED 42 days after they happen: `WorldEventPruner.RumorWindowDays = 42` and `RemoveAll(!InvolvesPlayer && !IsPersistentDeed && today - Day > 42)` (`LOGIC/WorldEventPruner.cs:21,44`). |
| NPC divorces | Yes | `ForeignDivorceBehavior` detects couples no longer wed (`MOD/Marriage/ForeignDivorceBehavior.cs:25`, pure rule `LOGIC/ForeignDivorceDetection.cs`), and vanilla keeps `Hero.ExSpouses` (`Hero.cs:823`). |

The real NPC-to-NPC sources are therefore: vanilla facts (kin, clan, realm, war, `Hero.DeathMarkKillerHero` at
`Hero.cs:322`, `ExSpouses`), the three grudge planters, live schemes, world events while they last (42 days), and
the generated past this document adds. The design is honest about that: on most pages, at first, the lists will be
short, and the generated past is what fills them. Whether to start CAPTURING NPC-to-NPC schemes and captures
durably is a question for Gabriel (Q6), because it persists for unseen realism.

## 3. Goals and non-goals

Goals:

- The encyclopedia never calls someone a friend whom the mod's own ledger calls an enemy.
- On an NPC's page, Friends and Enemies come from a derived personal regard, each with a reason the player can
  check (a grudge, a killed brother, a shared campaign twenty years ago).
- A short list of clans, hostile and with affinity, replaces the vanilla hero grids as the first thing read.
- Conversation describes a third party with the same regard and the same reason.
- A pair with nothing between them may receive a past, once, true forever after.

Non-goals:

- No simulation. No daily tick, no stored score per pair. The regard is computed when the page opens (or a prompt is
  built) and thrown away.
- No change to vanilla relation, and no change to the game's AI, which keeps reading vanilla relation. The two
  ledgers diverge, as they already do for the player (`MOD/CLAUDE.md`, Architecture: "never synced from vanilla").
- No change to the ~40 policies that read `Hero.GetRelation` between NPCs for MECHANICS (`docs/maps/engine-facts.md:22`
  lists them: schemes, divorce, interception, secret propagation, and more). Moving those is a separate decision.
- No generated past for the player. The player's past is what the player played.
- No LLM call anywhere in this feature. Reasons and past events are deterministic text from templates: free, testable,
  identical on every opening.

## 4. What already exists and is reused

| Need | Existing piece |
|---|---|
| Personal regard scale and named rungs | `SDK/Standing/Regard.cs:30-84` (`FriendlyTier 10`, `GenuineTier 15`, `WarmTier 20`, `WaryTier -20`, `SouredTier -30`, `HostileTier -55`) |
| Words for a regard value | `RegardBands.Describe` (`SDK/Prompts/RegardBands.cs:20-57`) |
| Grudge decay by traits | `GrudgePolicy.EffectiveMagnitude(grudge, today, traits)` (`LOGIC/Grudges/GrudgePolicy.cs:193`) |
| Appreciation decay | `AppreciationPolicy.EffectiveMagnitude` (`LOGIC/Appreciations/AppreciationPolicy.cs:152`) |
| Grudge line naming a non-player target | `GrudgeNarrator.DisplayLine(grudge, targetIsPlayer, targetName, ...)` (called at `MOD/Encyclopedia/EncyclopediaHeroPageGrievancesMixin.cs:167`) |
| What the player may know of a grudge | `Grudge.KnownToPlayer` (`LOGIC/Grudges/Grudge.cs:46-47`), `GrudgeBehavior.KnownGrudgesFor` (used at `EncyclopediaHeroPageGrievancesMixin.cs:119`) |
| Stable per-seed roll | `FirstImpressionPolicy.StableRoll(seed)`, FNV-1a, 0..99, process-stable (`LOGIC/FirstImpressionPolicy.cs:120-136`). NOT `string.GetHashCode`, which two existing sites misuse (trace: `RomanticProfileBuilder.cs:43`, `CaptiveEncounterService.cs:465`). |
| Order-free pair key | `ForeignDivorceDetection.PairKey(a, b)` (`LOGIC/ForeignDivorceDetection.cs:56-57`). A private copy exists in `WorldEventFeeders.cs:125` (trace); reuse the public one. |
| Trait opposition | `CompanionRapport.Compatibility(RomanticTraits a, RomanticTraits b)`, symmetric, about -9..+9 (`LOGIC/CompanionRapport.cs:61`) |
| Presence | `Mapping.HeroPresence.IsInTheWorld` (`MOD/Mapping/HeroPresence.cs:20`) |
| Naming in prompts | `HeroLabel.Describe` (trace: `LOGIC/HeroLabel.cs:50`) |
| Clan relation as vanilla means it | `Clan.GetRelationWithClan` is leader-to-leader relation (`GAME/.../Clan.cs:939-945`) |
| Encyclopedia mixin that edits a vanilla list in place | `RelabelClanRelation` mutates `ViewModel.Stats` from `OnRefresh` (`MOD/Encyclopedia/EncyclopediaHeroPagePersonalRelationMixin.cs:101-134`) |
| A line saying what an NPC thinks of the player | Already shipped: "Personal regard: <band> (<n>)" (`EncyclopediaHeroPagePersonalRelationMixin.cs:89-96`) |
| Byte[] JSON save pattern | trace: `MOD/Tournaments/TournamentRivalryBehavior.cs:40,53-79`, `MOD/Aims/AimsBehavior.cs:78-118` |
| Per-NPC memory with a past date | `CrNpcMemory.AddMemory(CrMemoryEntry{..., DaysAgo})` (`MOD/Extension/CrNpcMemory.cs:79`; precedent `WorldEventFeeders.cs:992`) |

Not found, to be written: a fief-neighbour helper (trace: none in famine, courier or Aims; distances are inlined at
six sites), an age-at-a-past-day helper (trace: `Hero.BirthDay` is read once, `PregnancyService.cs:348`), and shared
same-realm / at-war / is-kin helpers (trace: copy-pasted privately, e.g. `BannerlordGameStateBridge.cs:597`,
`AssemblyInvitationFactsBuilder.cs:58`).

## 5. The regard model

### 5.1 Shape

`PairRegard` is DIRECTED: what A thinks of B. A grudge is held by one side; a rescue weighs more on the rescued. The
pure policy, `PairRegardPolicy.Assess(PairRegardFacts facts)`, returns:

- `Points`, an int clamped to -100..100, on the same scale and with the same words as `Regard` / `RegardBands`;
- `Reasons`, the contributing traces, each with its signed weight after ageing, its reason line, and whether the
  player may know it;
- `Verdict`: Friend, Enemy, or Neither.

`PairRegardFacts` is a facts object (`{ get; init; }`, no positional constructor), one property per source below,
each documented with what it means and what goes wrong when it is false. The engine side (`MOD/Regard/`) fills it.

### 5.2 Sources and weights

Every weight is a named constant in the policy. All numbers below are proposals for Gabriel to tune.

| Source | Read from | Weight (A toward B) | Ageing | Reason line (example) |
|---|---|---|---|---|
| Spouse | `Hero.Spouse` | +15 | none | "his wife" |
| Parent / child | `Father`, `Mother`, `Children` | +10 | none | "his son" |
| Sibling | `Siblings` (`Hero.cs:852`) | +10 | none | "his brother" |
| Same clan, not kin (never added on top of a kin weight) | `Hero.Clan` | +5 | none | "of his house" |
| Former spouse | `ExSpouses`, `ForeignDivorceBehavior` | -25 | half every 6 years | "their marriage ended" |
| B killed A's kin | kin's `DeathMarkKillerHero == B` (`Hero.cs:319-322`) | -70 if executed, -45 in battle | none for an execution; half every 10 years otherwise | "executed his brother Garios" |
| A's grudge on B | grudge ledger, `HolderId == A`, `TargetId == B`, unresolved | minus `GrudgePolicy.EffectiveMagnitude` | the grudge policy's own decay | `GrudgeNarrator.DisplayLine` |
| A's appreciation of B | appreciation ledger, same filter | plus `AppreciationPolicy.EffectiveMagnitude` | its own decay | its narrator line |
| B schemed against A, discovered | `Scheme`, state Discovered / Failed / Succeeded | -30 | while the record or its WorldEvent lasts | "plotted to slander him" |
| Recent battle | WorldEvent `Battle` with both as `ActorIds` | -10 to the loser | lasts 42 days (pruner) | "beat him at Pravend" |
| Generated shared past | the new ledger, section 7 | per kind, 20 to 50 | per kind, section 7.5 | the event's own line |
| Realms at war | `MapFaction.IsAtWarWith` | -5 | none | not a reason on its own |

Two rules keep the structural facts honest:

- **Kin, clan and war alone never list anyone.** Their weights sit below the friend and enemy lines, so a brother is
  in the Family grid, not in Friends, unless something real happened between them. They tip a pair that has a reason.
- **Vanilla hero relation is NOT a source** (see Q5). It has no reason attached, so a friend made of it could not
  say why, which breaks ruling 2.

### 5.3 Combining and the reason

- Sum the signed weights, clamp to -100..100.
- Friend when `Points >= Regard.WarmTier` (20); Enemy when `Points <= Regard.WaryTier` (-20). Both are named rungs,
  so the regard ratchet (`docs/maps/README.md`, "a regard threshold newly written as a bare number") stays green.
  The largest kin weight (+15 for a spouse) stays below the Friend line, so kin alone never lists (Q3 asks whether a
  spouse should).
- The shown reason is the largest contributor whose sign matches the verdict. When an opposing contributor is at
  least half as strong, the line says so: "Enemy: executed his brother (though they share a house)". Conversation gets
  the top two reasons, the encyclopedia one.
- **The knowledge gate decides what may be SHOWN, not what is TRUE.** A contributor carries `KnownToPlayer`:
  vanilla facts and generated pasts are public; a grudge uses its own `KnownToPlayer`; a live undiscovered scheme is
  never public. If the dominant reason is not known to the player, the encyclopedia falls back to the next known one;
  if no known reason remains, the pair is not listed on the page at all (it still colours the holder's
  conversation). Listing "Enemy" without a reason the player may read would be either a leak or an unverifiable
  claim.

### 5.4 Clans

Clan regard on a page for hero H, toward clan C: the derived regard of C's leader toward H's clan leader (or toward H
when H leads). This mirrors vanilla's own meaning of a clan relation, which is leader to leader
(`Clan.cs:939-945`). War is shown as a separate tag ("at war") rather than folded into the number. Whether "hostile"
should mean regard or war is Q1.

## 6. Which pairs are computed, and what it costs

When the page of hero H opens, the candidate set is:

1. H's kin: spouse, former spouses, parents, children, siblings.
2. H's clan members; H's clan leader and realm ruler.
3. The leaders of the other clans of H's realm.
4. The owners and governors of fiefs NEAR H's clan fiefs (new pure `FiefNeighbourPolicy`: two fiefs are neighbours
   within a named distance, expressed in travel days through the existing conversion
   `LetterGenerationService.DistancePerTravelDay`, which ROADMAP names as the one to keep; not re-read here).
5. Anyone named with H in a trace: grudge or appreciation holder/target, scheme plotter/target, `ActorIds` of a live
   world event, the killer of H's kin.

Filtered by `HeroPresence.IsInTheWorld`, not a child, not the player (the player is handled by section 8.1), not a
notable (vanilla excludes notables too, trace: `AddHeroToRelatedVMList`; see Q7). Capped at 40 candidates, kin and
traces first.

Cost: one pass over the grudge, appreciation and scheme lists filtered by H (they are flat lists of hundreds, trace:
`GrudgeLedger.cs:19`), one pass over the ~150 town and castle fiefs for neighbours, then 40 small sums. Vanilla's own
`Refresh` already walks every living and dead hero on each opening (trace: `EncyclopediaHeroPageVM.cs:963-972`), so
this adds less than the page already spends. Nothing is cached across openings: the answer is always the world's
current state.

Directed regard is computed BOTH ways for each candidate: H's regard for X decides H's lists; X's regard for H is
what a second page would show and costs the same sum again. Only the first is shown on H's page.

## 7. The generated shared past

### 7.1 When a draw happens

On a page opening, a candidate X qualifies for a draw when the pair (H, X) has no contributor of magnitude 10 or more,
is not kin, and has never been drawn. At most 6 new draws per opening (the closest candidates first), so paging
through the encyclopedia grows the ledger slowly and never stalls a page. A conversation prompt NEVER draws (Q8): it
reads what the encyclopedia already drew, so a conversation is deterministic and costs nothing new.

### 7.2 The roll

- Seed: `StableRoll(PairKey(a, b) + "|past|v1")`. The `v1` lets a future catalog change use a new stream without
  touching drawn pairs.
- First roll: does anything happen? Base 30 percent, plus 10 when the pair fits at least two kinds strongly
  (neighbours AND opposed traits, for instance). About two pairs in three get nothing, which is the "good share"
  the ruling asks for.
- Second roll (a separate seed suffix): which kind, weighted by fit among the kinds whose conditions hold.
- Third and fourth rolls: how many years before the campaign, and the place.
- The result, INCLUDING "nothing", is written to the ledger. The seed makes it reproducible, but the facts it was
  drawn from (neighbours, realm) change over a campaign; persisting the outcome is what makes "never re-rolled"
  true.

### 7.3 Catalog

Each kind has a fit condition read from facts the game holds NOW, a direction per side, and a template. Templates
name the region by the nearest town to the place, never an owner or a conquest, because ownership before the campaign
is unknown (7.6).

| Kind | Fits when | A toward B | B toward A | Line (A's side) |
|---|---|---|---|---|
| Boundary quarrel | their clans hold neighbouring fiefs | -30 | -30 | "quarrelled with B over grazing land near Pravend, N years ago" |
| Unforgiven insult | `Compatibility <= -4`, same realm or neighbours | -35 | -20 | "was insulted by B at a feast in Sargot and never forgot it" |
| Old friendship | `Compatibility >= 4`, same culture, ages within 10 years | +35 | +35 | "has been B's friend since they were young men in Ortysia" |
| Rode together | same realm now and same culture | +25 | +25 | "rode beside B against raiders near Varcheg" |
| Rescue | either Valor >= 1; A younger | +45 (rescued) | +15 | "owes B his life since a skirmish near Epicrotea" |
| Unpaid debt | B's Generosity <= 0 | -25 (lender) | -10 | "lent B a sum that was never repaid" |
| Tourney rivalry | both have Valor >= 0 and are lords | -20 | -20 | "lost to B at the lists of Pen Cannoc and wants it back" |
| Refused suit | opposite sex, neither has ever married (`Spouse == null`, `ExSpouses` empty), since a past marriage date is not knowable | -20 (suitor) | 0 | "asked for B's hand and was refused" |

Excluded on purpose: any death (both are alive), any marriage or child (would contradict vanilla family), any capture
of a fief, any kingdom change, anything involving a third named person.

### 7.4 Dating

- The campaign start day is `Campaign.Current.Models.CampaignTimeModel.CampaignStartTime.ToDays`
  (`GAME/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.GameComponents/DefaultCampaignTimeModel.cs:7`, year
  1084 plus one season). `CampaignTime.ToDays` counts from the epoch (`GAME/.../CampaignTime.cs:140`), so a day
  before the start is a smaller POSITIVE number, the same unit the mod already uses (`(int) CampaignTime.Now.ToDays`).
- Years before the start: 1 to 20, drawn, but never earlier than the year the younger of the two came of age. Both
  must be adults on that day: age at day D is `(D - BirthDay.ToDays) / DaysPerYear` (new pure helper
  `AgeAtDay`; `WorldCalendar.DaysPerYear` via `LOGIC/ChronicleCalendar.cs:21`), compared with the game's
  `AgeModel.HeroComesOfAge`. If no year fits (one of them came of age after the start), the draw is "nothing".
- Dated from the START, not from today: a draw made in the fifth year of play is still placed before the campaign,
  where the player saw nothing and nothing can contradict it.

### 7.5 Ageing

A generated past weighs less the older it is: full weight up to 2 years, then halved every 8 years for quarrels and
insults, every 15 for friendships and rescues. Pure function `PastEventAgeing.Weight(kind, yearsAgo)`. The ledger
stores the event; the weight is always recomputed.

### 7.6 How it avoids contradicting known history

- Before the start: the player witnessed nothing then.
- No event claims ownership, conquest, a death, a marriage, a child, or a realm allegiance in the past. Clans change
  realms during play and vanilla keeps no history of who held what at the start, so any such claim could be false.
- "Rode together" requires the same culture rather than the same realm in the past, and its line never names a king.
- Both are alive and in the world now; both were adults then.
- A pair with an existing trace never draws, so a generated friendship cannot sit beside a real execution.
- Once written, it is true. NPCs never lie (Gabriel, 20/09/2026): either of the two may refuse to talk about it, but
  never deny it.

### 7.7 Persistence: why not WorldEventStore as it stands

Ruling 3 says "injected into world history as a real world event". The world event store cannot hold it today:

- `WorldEventStore.Record` replaces a day of 0 or less with today (`MOD/WorldEvents/WorldEventStore.cs:173`); a
  positive past day survives that, but
- the pruner deletes any third-party event older than 42 days (`LOGIC/WorldEventPruner.cs:44`), so a past dated
  years ago is deleted on the very `Record` call that adds it (`WorldEventStore.cs:176`);
- the only escape, `IsPersistentDeed`, shares a cap of 50 with other mods' kept deeds (`WorldEventPruner.cs:35,47`);
  hundreds of pasts would evict their entries or be evicted;
- awareness fades to zero after 40 days (`LOGIC/RumorAwareness.cs:27`), so no NPC would ever "hear" it through the
  news channel anyway;
- the chronicle shows only the player's events (trace: `WorldEventStore.cs:101-102`).

Options:

- (A) `IsPersistentDeed = true`: rejected for the cap reason above.
- (B) A new flag on `WorldEvent` (for instance `IsAncientHistory`) that the pruner and awareness both skip:
  additive, but touches two shared rules and a modder-facing record, and still leaves the past invisible to news.
- (C, recommended) **The past lives in its own ledger, which IS the world's history of pairs.** The new store,
  `SharedPastStore`, is the single source of truth: the encyclopedia reads it, the regard policy reads it, and both
  parties' prompts read it. It has the shape of a world event (kind, two actor ids, place id, day, magnitude per
  side, summary) so it can be MIRRORED into WorldEventStore later if option B is ever built. Optionally, each party
  also gets a per-NPC memory through `CrNpcMemory.AddMemory` with `DaysAgo` (the `RememberHouse` precedent,
  `WorldEventFeeders.cs:992`), which costs a profile write for heroes the player may never meet (Q4).

Entry: `PairKey`, `AId`, `BId`, `Kind` (enum, values appended only), `Day`, `PlaceId`, `WeightAtoB`, `WeightBtoA`,
`Summary` (the neutral line), `Version`. A drawn-nothing pair is stored as a `PairKey` in a separate set, a few bytes
each.

### 7.7a Ruling (Gabriel, 17/10/2026): the world gets a long-term memory

> "Pour le World Events, l'expiration de 42 jours devrait etre pour les evenements recents. Mais apres 42 jours, ca
> devrait basculer vers une memoire long terme. La memoire long terme servirait entre autre a definir les relations
> entre NPC.. et pourrait parfois etre utilise pour des conversations entre joueur et NPC sur le passe."

This supersedes options A to C above and answers question 4. The world event store gets two tiers:

- **Recent (up to 42 days), unchanged.** News: it spreads by distance and fades (`RumorAwareness`), the pruner's
  window still applies to it.
- **Long-term (after 42 days).** When the pruner would delete a third-party event, it moves it to a second list
  instead, under a new additive save key. Proposed bar: only what a region would still tell years later (a battle of
  several hundred men or more, a siege, a fief changing hands, the death or execution of a lord, a marriage, a war or
  a peace, a discovered scheme). A skirmish of a few dozen men fades as today.
- **Who knows a long-term event.** Not by distance any more (that is news): its actors, their kin and clan; the
  people of the place it happened in; for the greatest (magnitude 45 and up, `BattleRecordText.Magnitude`), everyone
  of the realms involved.
- **Bound.** A cap (proposed 3000 entries, a few hundred kilobytes of JSON), evicting the lowest magnitude first,
  then the oldest.
- **The generated shared past is written straight into it**, dated before the campaign start: this is the literal
  "injected into world history" of ruling 3, with no separate ledger. A drawn-nothing pair stays a small set of keys.
- **Uses.** The regard policy reads the long-term events two NPCs share (winner and loser of a battle, a marriage, an
  execution). In conversation, when the player names a person, a place or a realm, the one to three most relevant
  long-term events the NPC would know go below the `CURRENT ENCOUNTER:` marker, so a lord can speak of the battle
  his father lost at Pravend ten years ago. Only on mention, to keep the prompt budget.
- **Old saves.** Nothing to migrate: events still inside the 42-day window at load age into the long-term tier;
  older ones were already gone.
- **Modder API.** The `WorldEvent` record does not change; a read door for the long-term tier can come later.

Still open from this: the significance bar above, and question 6 (should captures, executions and discovered schemes
between NPCs be recorded as world events, so they reach the long-term tier).

### 7.8 How it is shown and spoken of

- Encyclopedia: as the reason of a Friend or Enemy entry ("Enemy: quarrelled over grazing land near Pravend, 9 years
  ago"; the years are counted from TODAY when shown, since nobody in the world knows of "the campaign"), and in a small "Old history" text block on both heroes' pages listing every shared past of that hero.
- Conversation: in the speaking NPC's relationships line and in the third-party description (section 9), in the
  NPC's own words ("you remember: you rode beside Garios against raiders near Varcheg, eleven years ago").

## 8. Encyclopedia changes

### 8.1 The player's own page

- Friends: heroes with a stored profile whose `ReputationWithPlayer >= Regard.WarmTier`, in the world, sorted by
  regard. Enemies: `<= Regard.WaryTier`. The store is `NpcMemoryCampaignBehavior.Instance.Store`
  (`EncyclopediaHeroPagePersonalRelationMixin.cs:91-92`, a `BannerlordNpcMemoryStore`,
  `MOD/Behaviors/NpcMemoryCampaignBehavior.cs:89`), which already enumerates with `ListIds()` and `Get(id)`
  (`MOD/Storage/BannerlordNpcMemoryStore.cs:43,53`).
- The DIRECTION differs from NPC pages, on purpose: on an NPC's page the lists say what H thinks of others; on the
  player's page they say what others think of the player, because the player has no ledger of his own regard. The
  section titles say so ("Who holds you dear", "Who holds you in contempt", wording to settle).
- Reason: the strongest KNOWN appreciation or grudge on that hero toward the player, else the band word and number
  ("friendly regard, +34").
- Clans: by their leader's `ReputationWithPlayer`.
- This alone removes the case Gabriel saw.

### 8.2 An NPC's page

- Clans: "Houses at odds" and "Houses of affinity", by section 5.4, with an "at war" tag, at most 6 each.
- Friends and Enemies: H's derived regard toward each candidate, with its reason.
- What H thinks of the player: already shipped as the "Personal regard:" row. Proposal: append the dominant KNOWN
  reason when there is one ("bitter grudge (-80): you executed his brother"). See Q2.
- Old history: the shared pasts of H, if any.

### 8.3 How the vanilla lists are replaced

The page binds `MBBindingList<HeroVM> Allies` and `Enemies` (`EncyclopediaHeroPageVM.cs:603,586`), overflow
lists `AdditionalAllies` / `AdditionalEnemies` (`:757,740`) with `AnyAdditional*`, `Additional*String`, `Additional*Hint`
(trace: `:740-859`), and header strings `AlliesText` / `EnemiesText` set to `str_friends` / `str_enemies` (trace:
`:910-911`). The prefab shows them in `AlliesDivider` / `AlliesGridParent` / `AlliesGrid` and `EnemiesDivider` /
`EnemiesGridParent` (`PREFAB:166,169,171,198,201`). There is no clan list on the vanilla page (trace).

Route (b), recommended: **refill the vanilla lists from a mixin's `OnRefresh`.** Clear `Allies`, `Enemies` and both
overflow lists, add `new HeroVM(hero)` for our entries (13 shown, the rest in overflow, as vanilla's own capacity of
13, `:33`), set the `AnyAdditional*` and `Additional*String` properties ourselves (vanilla's
`OnAdditionalListsUpdated` is private, `EncyclopediaHeroPageVM.cs:1128-1136`), and retitle the headers.
This keeps vanilla portraits, hover tooltips and click-through, and has an exact precedent: `RelabelClanRelation`
already edits a vanilla list from `OnRefresh` because a Harmony patch on `Refresh` crashed on 1.4.8
(`EncyclopediaHeroPagePersonalRelationMixin.cs:101-109`). The reasons cannot ride on the portrait (`HeroVM`'s hint is
the vanilla hero tooltip, `HeroVM.cs:208-212`), so a text block "Why:" with one line per listed hero is appended
after `EnemiesGridParent` by an `Append` prefab patch. The clan lists are a new appended block before
`AlliesDivider`.

Route (a), fallback: a `PrefabExtensionSetAttributePatch` setting `IsVisible="false"` on the four vanilla widgets,
and our own lists drawn as text with encyclopedia links. UIExtenderEx 2.13.2 ships the patch type (trace: string
present in the DLL), but the mod has never used anything except `PrefabExtensionInsertPatch` with `Append` (trace),
so it is unproven here.

Two things route (b) rests on that are NOT verified: that UIExtenderEx runs the mixin's `OnRefresh` AFTER vanilla's
`Refresh` has filled the lists (only the comment at `:105-106` says so), and that nothing else refills them in the
same frame. The first `cr.testall` check (section 10) pins both.

## 9. Conversation reuse

Today the third party is described from VANILLA relation:

- `ThirdPartyDescriptor.Categorize` (trace: `LOGIC/ThirdPartyDescriptor.cs:90-121`), fed by
  `CharacterRelationManager.GetHeroRelation(npc, other)` in `ContextualNameResolver` (trace:
  `MOD/Mapping/ContextualNameResolver.cs:226`, and `:296` for whereabouts knowledge). It lands in
  `EncounterContext.ContextualNames` and is rendered inside `AppendEncounterContext`, BELOW the `CURRENT ENCOUNTER:`
  marker (trace: `SDK/Prompts/PromptBuilder.cs:1409-1413`; `docs/maps/prompt-sections.md:104`).
- `RelationshipsBuilder` lists "Friends:" (vanilla `>= 10`) and "Enemies:" (`<= -10`), six each, bare names
  (`MOD/Mapping/RelationshipsBuilder.cs:30-32,75-91,197-206`). Rendered in the RELATIONSHIPS block, in the cached
  PREFIX, dropped by the Lean policy (`docs/maps/prompt-sections.md:31`).

Change:

- `ContextualNameResolver` asks the same `PairRegardPolicy` for the speaker's regard toward the named person and
  passes the dominant reason: "Garios of the dey Meroc (Vlandian): an enemy (you quarrelled over grazing land near
  Pravend, nine years ago)". Here the knowledge gate is the SPEAKER's, not the player's: a speaker knows
  his own grudges, secret or not, and may keep them to himself. Below the marker, so the cache is untouched; it
  adds about 60 characters per named person in a turn that names one.
- `RelationshipsBuilder` lists the derived friends and enemies, with `HeroLabel` names and a short reason. It is in
  the prefix, but the derived regard does not move within a conversation, so the prefix stays stable turn to turn.
  Lean drops it already, so `LeanPromptPolicyTests` (budget 7400 at
  `LLM Roleplay Service/NpcMemoryServiceTests/LeanPromptPolicyTests.cs:120`; the 6950 in `MOD/CLAUDE.md` is stale)
  is not affected. The Compact budget tests must still be run.
- The companion camp note (`ResolveCompanionCampNote`, trace `EncounterContextBuilder.cs:1464-1485`) stays on vanilla
  relation for now: companions earn vanilla relation through real campaign events, and that is a mechanic, not a
  page. Listed as Q9.
- Every PromptBuilder edit runs the full SDK suite (house rule).

## 10. Testing

Pure rules, test-first, Gherkin names, a STAKE comment on each test:

- `PairRegardPolicy`
  - `GIVEN_a_lord_whose_brother_another_lord_executed_WHEN_his_page_is_read_THEN_the_executioner_is_his_enemy_and_the_page_says_why`
    (stake: Gabriel's ruling that every entry carries its reason; the case vanilla gets wrong).
  - `GIVEN_two_brothers_with_nothing_between_them_WHEN_regard_is_read_THEN_neither_is_listed_as_a_friend`
    (stake: kin belongs in Family; a page full of brothers in Friends tells the player nothing).
  - `GIVEN_an_enemy_whose_only_reason_is_a_secret_grudge_WHEN_the_page_is_built_THEN_he_is_not_listed`
    (stake: a listed enemy with a hidden reason is either a leak or an unverifiable claim).
  - `GIVEN_vanilla_relation_99_and_a_bitter_grudge_WHEN_regard_is_read_THEN_he_is_an_enemy` (stake: the exact
    report of 17/10/2026, transposed to two NPCs).
- `SharedPastPolicy`
  - `GIVEN_the_same_pair_WHEN_drawn_twice_THEN_the_same_past_comes_out` (stake: "drawn once, never re-rolled").
  - `GIVEN_a_lord_who_came_of_age_after_the_campaign_began_WHEN_a_past_is_drawn_THEN_there_is_none` (stake: both
    must have been adults then).
  - `GIVEN_any_drawn_past_WHEN_dated_THEN_it_falls_before_the_campaign_began` (stake: the player saw everything since).
  - `GIVEN_a_thousand_unrelated_pairs_WHEN_drawn_THEN_most_have_no_shared_past` (stake: "a good share get nothing";
    the bound is the published proportion, plus or minus a tolerance).
  - `GIVEN_two_lords_with_opposed_honour_WHEN_a_past_is_drawn_THEN_it_is_never_an_old_friendship`.
- `AgeAtDay`, `FiefNeighbourPolicy`, `PastEventAgeing`, `EncyclopediaRegardLists` (sorting, capacity 13 and
  overflow, the clan lists).

Engine self-test, `cr.testall` case `npc_regard` (it arranges its own context, per Gabriel's 13/10 ruling):

1. Pick two living adult lords A and B of different clans with no trace; plant a `Tale` grudge A on B (the existing
   non-player planter); build A's page lists through the real mixin path; expect B in Enemies with the grudge line.
2. Construct `EncyclopediaHeroPageVM` for A and check that `Allies` / `Enemies` hold OUR entries after the
   constructor and after a second `Refresh()` call. This is the check that route (b) rests on.
3. Pick a fresh pair, draw; assert the ledger entry or the drawn-nothing mark exists, the day is before the campaign
   start, both were adults; draw again and assert nothing changed.
4. Build a prompt dump for A naming B and assert the reason line appears below the marker.
5. Cleanup: remove the planted grudge and the ledger entries written by the test.

Maps to regenerate and read: `save-format.md` (one new key), `inventory.md`, `constants.md` (the weights),
`engine-facts.md` (new facts read), `decisions-regard.md` (the friend/enemy cut-offs if expressed as a regard-only
rule), `prompt-sections.md` if a section moves.

## 11. Increments, in build order

1. **The player's own page from personal regard.** Route (b) on the player page only: Friends and Enemies from
   `ReputationWithPlayer`, reason from known grudges and appreciations. `cr.testall` check 2. Fixes the reported case
   and proves the mechanism before anything is designed on top of it.
2. **Derived regard on NPC pages, from what exists.** `PairRegardPolicy` with vanilla facts, NPC-to-NPC grudges and
   appreciations, schemes, recent world events. Candidate set, Why block. No generation yet: pages will be sparse,
   and that sparseness is the measure of how much the past is needed.
3. **Clan lists** on both pages, once Q1 is answered.
4. **Shared past.** `SharedPastPolicy`, `SharedPastStore` with its save key, the draw on page opening, the Old
   history block. `cr.testall` check 3.
5. **Conversation.** `ContextualNameResolver` and `RelationshipsBuilder` read the derived regard. Full SDK suite,
   Compact budget.
6. **Optional, only on Gabriel's word (Q6):** capture NPC-to-NPC schemes, captures and executions durably at the
   moment they happen.

## 12. Save format

- One new key, additive: `CalradiaRemembers_SharedPast_v1bytes`, JSON in a byte array as the other ledgers (the
  native string limit is why the mod uses bytes). Contents: the entries and the drawn-nothing set.
- No migration. An old save has no key: the ledger is empty and the next page opening draws. Nothing is backfilled,
  because nothing should exist for pairs nobody looked at.
- Derived regard is never saved.
- Size bound: a soft cap of 4000 entries (a few hundred kilobytes); past it, new draws stop rather than evict, so a
  past once shown never disappears.
- A kind enum gains values only at the end, as `WorldEventType` does (trace: `WorldEvent.cs:13-88`).

## 13. Risks

- **Route (b) ordering.** If UIExtenderEx ever runs `OnRefresh` before vanilla fills the lists, vanilla wins
  silently. Check 2 catches it; route (a) is the fallback.
- **Two ledgers, two answers, one page.** The page will still show vanilla's "Clan Relation" number beside our
  lists (relabelled at `EncyclopediaHeroPagePersonalRelationMixin.cs:111-134`). A generated quarrel between two lords
  whose vanilla relation is 80 will look odd to a player who reads both. Q5.
- **Sparse pages before increment 4.** Expected; it is also the honest state of the mod's NPC-to-NPC memory.
- **Template monotony.** Eight kinds over many pairs will repeat. Variants per kind (three lines each) are cheap; an
  LLM rewrite is not proposed.
- **Persisting for pages the player only skimmed.** Six draws per opening, and the drawn-nothing marks, are the
  price of "never re-rolled". Bounded by the cap.
- **Names in reasons.** A reason naming a killed brother must route through `HeroLabel` in the prompt and survive a
  hero whose name collides with another.

## 14. Open questions for Gabriel

**Answered 17/10/2026 (Gabriel: "1: les deux, 2: oui, 3: oui, 4: oui"):**
- Hostile clan: BOTH. A clan is listed hostile by the leader's regard, and a clan of a realm at war is marked as such.
- Vanilla relation BIASES the draw of a generated past (a pair at vanilla +80 cannot draw a quarrel, and the reverse).
- Captures, executions and discovered schemes between NPCs ARE recorded as world events, so they reach the long-term tier.
- The significance bar of 7.7a is accepted as proposed.

**Answered 17/10/2026, on the long-term memory as built (`27937a7`), Gabriel: "1: secret event devrait etre tagged
secret et ces secrets ne devraient etres connus que de ceux qui le connaissent. 2: oui, 3: oui, 4: oui":**
- A secret stays a secret in the long-term tier: `cr.world_event_secret`/`_hide` reach an aged event, and a secret's
  prompt line carries `RumorDiscretion.SecretTag` (discretion, never denial). Who knows it is still "those it names";
  no ledger of confidants was built (`4582aa1`).
- Risings (and the return of a risen house), divorces and lords fallen in battle are on the bar (`HistoryKind`
  Rebellion, Divorce, FellInBattle; a fall in battle is recorded at magnitude 50, so the realm knows it).
- In an old save, a field battle whose size was never recorded fades. Accepted.
- A lesser lord's capture by a lord goes straight into the tier at magnitude 25, so it is evicted first. Accepted.


1. **"Hostile clan": the leader's regard, or being at war?** Recommended: the leader's regard (vanilla itself means
   leader-to-leader by "clan relation", `Clan.cs:939`), with "at war" shown as a tag beside it. Two clans of the same
   realm can hate each other; two enemy realms' clans may respect each other.
2. **The line about the player on an NPC's page** already exists ("Personal regard: bitter grudge (-80)"). Should it
   also give the dominant known reason, or stay a band and a number?
3. **Spouse in Friends?** With the proposed weights no kin lists without a real reason, a spouse included (+15,
   below the Friend line at 20). Should a spouse list as a Friend by default, or stay in the Family grid only?
4. **ANSWERED 17/10/2026, see 7.7a: a long-term world memory.** (Was: "Injected into world history": option C AMENDS your ruling 3**, it is not just an implementation choice. Your
   words were "a real world event"; option B is that literal reading. Option C keeps the past in its own ledger, read
   by the encyclopedia and both parties' prompts, because the world event store prunes anything older than 42 days
   and its awareness fades in 40. What C loses: a THIRD party cannot answer "what happened between A and B?" from the
   news channel, since `ResolveQueryRelevantFacts` matches world events by `ActorIds` (trace:
   `EncounterContextBuilder.cs:4309,4346-4348`). B keeps that, at the cost of touching the pruner, the awareness
   score and a modder-facing record. Which do you want? And should both parties also receive a per-NPC memory entry?
5. **Vanilla relation.** It is excluded from the derived regard (no reason attached). Should it instead BIAS the
   draw (a pair at vanilla 80 cannot draw a quarrel), so the page never shows a generated enemy beside a vanilla
   "Clan Relation" of 80?
6. **Capture NPC-to-NPC traces when they happen?** Discovered schemes, a lord taking another prisoner, an execution
   of a lord by a lord. Today they vanish after 42 days. Capturing them is cheap but persists for unseen realism;
   reading vanilla's `DeathMarkKillerHero` already covers the deaths for free.
7. **Notables.** Vanilla lists none. Should a town's notables appear in a lord's lists when a trace binds them?
8. **Does a conversation count as "looking"?** Proposed: no, only the encyclopedia draws, so prompts stay
   deterministic. Should talking to H also draw for H's close circle?
9. **Companions among themselves** keep vanilla relation (`CompanionRapport`, a mechanic). Leave as is?
10. **Probability and catalog.** About one pair in three gets a past; eight kinds; 1 to 20 years before the start.
    Adjust any of these, and add or remove kinds (the refused suit is the only one touching courtship).
