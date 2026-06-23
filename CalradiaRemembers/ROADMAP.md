# Calradia Remembers — Roadmap

The long-term direction of the mod, agreed 2026-06-12. Each layer is shippable and
useful on its own, and each one feeds the next — the order is the architecture.

---

## Now — v1.4.0 shipped (2026-06-12), validation in flight

v1.4.0 is live on Nexus (witness presence, fallback conversation memory, tester
feedback round 1). ProudSeal's round-2 playthrough is the validation pass for
everything that shipped untested in-game. Open items:

- [ ] Round-2 feedback triage (companions, witness_leaves, free-text privacy,
      fallback memory, death dating, Storyteller pacing, courier sea fix, no-key UX)
- [ ] Bug #1 (battle instead of chat after reload) — deferred pending a repro log
      with Enable Logging ON
- [ ] Stale lord intentions (announces marching on an already-conquered village) —
      check current allegiance when describing the destination
- [ ] Reply courier toward a player at sea (spawn side is fixed; the moving target
      is not)

---

## ~~Ollama as a third LLM provider~~ — SUPERSEDED (2026-06-22)

No dedicated Ollama provider: the **OpenAI-Compatible endpoint** (shipped v1.9.x)
already serves Ollama — point it at `http://localhost:11434/v1`, leave the key
blank, paste the model id. Same for LM Studio / vLLM. Nothing more to build.

---

## Negotiation framework (agreed 2026-06-12)

**The insight: a non-monetary negotiation is an informal quest inverted.** "Bring
me a courser, a fine sword and exceptional armor and I may join your clan" (the
She-Wolf case) is structurally a Sprint 11 quest — a verifiable deed (deliver
items) whose reward is the NPC's service (join_party) instead of gold. The whole
pipeline already exists: [QUEST] issuance in conversation, mechanical verification
(QuestDeedTracker — words never pay, verified deeds do), reward on completion.
The framework generalizes both axes:

- **What the NPC offers**: gold (today) → join_party, give_item, a service.
- **What the player pays**: a deed (today) → denars, items, a delivered prisoner,
  an act of war (see below).

**Proportionality is computed game-side, never trusted to the LLM.** The game
knows the value of everything: `ItemObject.Value`, the vanilla ransom model for
prisoners, the hiring price for a companion's service. The 75 % negotiation floor
generalizes to a currency-agnostic VALUE floor: total value delivered ≥ floor of
the service's value. The LLM negotiates the story; the bridge arbitrates the
amounts (RelationGate philosophy applied to barter).

**Phase 1 — ✅ DONE (2026-06-22).** `DeliverItems` + `JoinParty` reward shipped v1.6.x;
`DeliverPrisoner` shipped 2026-06-22 (both cases: hand over a held captive now, or a
capture-and-deliver task; via the give_prisoner action + `TransferPrisonerAction`,
verified by a real transfer; `PrisonerDeedTarget` pure-tested). Still open as optional
polish: a richer DeliverItems spec (category+tier+count instead of a value floor) and the
"joining wanderer equips the demanded gear" touch.
- Deed `DeliverItems` (category + tier + count: "a warhorse, a sword of tier 5+");
  verification = items in inventory, handed over like take_gold takes gold. Nice
  touch: the joining wanderer can EQUIP the gear that was demanded for them.
- Deed `DeliverPrisoner` (a named lord, or any lord of faction X) — detention is
  verifiable; `TransferPrisonerAction` is native.
- Reward `JoinParty` on quest completion.
- Item transfer: chat-native first (inventory check + removal at the verifying
  conversation — no UI needed). The vanilla barter screen (`BarterManager`) is a
  candidate for a tactile hand-over UX later, but it adds a mismatch risk between
  what the UI allows and what the deal specified — Phase 2 if the feel matters.

**Phase 2 — ✅ DONE (2026-06-22) — deed: player-declared war.** An NPC with many
enemies in a faction may demand the PLAYER declare war on it ("rid me of the
Vlandians and I am yours"). Self-balancing — the player takes on the war they
started. Shipped: `QuestType.DeclareWar`, issued via [QUEST] (target_faction,
refused if the player is already at war with it or it is their own), verified by
the WarDeclared event with the player's faction as AGGRESSOR (`WarDeedMatcher`
pure-tested; never satisfied by being attacked), reward join_party / gold+relation.
`cr.declare_war_quest`. (An NPC declaring war on another faction remains FORBIDDEN
until v2.0 — that is where the player could extract outsized advantage at no cost.)

**Phase 3 — yellow, gated: intimacy as currency.** Constraints agreed:
- Male NPC toward a female player only, AND filtered by the NPC's traits/archetype
  (the cynical gang leader, not the courtly lady's brother).
- The transaction puts the player in the DOMINATED position: the NPC profits from
  the player, knows it, and is likely to keep leveraging it — the event is
  recorded as exploitation, and future conversations may push for more. This is a
  persistent power dynamic, not a one-off payment.
- Gating: AdultLevel Explicit minimum; the persistent-leverage dynamic likely
  belongs at Hardcore (decide at implementation). Verification = the Intimacy
  event already detected and recorded by the pipeline.

**Deferred:** assassination as a deed waits for v1.3 (schemes builds the
mechanics); NPC-initiated faction wars wait for v2.0 (dispositions).

---

## v1.1 — Talk to everyone (commoners + settlement knowledge)

LLM conversations with generic townsfolk and villagers (non-Hero Agents — the
Townswoman, the dockworker). They are ephemeral by nature: **no memory, no
relation — ambient intel instead.** That trade-off is the design, not a limitation.

- **Settlement knowledge builder** (the load-bearing piece, built first): a game-side
  context block with what a commoner actually knows — who holds the town, current
  wars, which lords are present today (`LocationComplex` already tells us),
  security/loyalty/prosperity as street mood, recent raids, spotted hideouts nearby.
  Without this feed the commoner confabulates; with it, they're useful.
- **Rumor for coin**: the prompt teaches them to ask 10–30 denars (`take_gold`,
  already in the action pipeline) before sharing the juicier piece.
- **Slim LLM stack**: a second `NpcChatService` with quests, change_relation,
  [EVENT], discovery, and witnesses all disabled. Short prompt = cheap chats.
- Entry point on the commoner dialog token (not `hero_main_options`); transient
  synthetic profile (template name, settlement culture, persona from a small pool).
- Prototype path: `cr.rumor` console command before any dialog wiring.
- **Action vocabulary expansion** (tester suggestion): `give_troops` and `give_item`
  alongside the gold actions — same mold (verifiable, bounded, executed by the
  bridge; never a free-form world action). A lord granting you five recruits or a
  blade as reward is the natural next rung above `give_gold`.

## v1.2 — Rumor store (the foundation layer)

A persisted `WorldEventStore` (SyncData) of dated events: type, actors, place,
visibility, status. Three feeders, all cheap:

1. **Real campaign events** — `CampaignEvents` listeners: battles, sieges, marriages,
   executions, war declarations. Zero LLM. Include the **player's own battlefield
   performance** (kills, presence at a victory) recorded into the profiles of lords
   who fought alongside or against them — tester's immersion argument: "currently it
   is down to you having to tell people you fought well, which is a little boastful."
   NPCs who were there should simply know.
2. **Mod conversation outcomes** — insults before witnesses, furious
   `end_conversation`, public humiliation (already implemented, currently evaporates;
   this gives "word gets around" a literal home).
3. **Hero↔hero relation tracking** — native `GetRelation` between lords detects
   brewing rivalries mechanically and emits gossip beats.

Consumers: the commoner intel feature (its first visible client), NPC prompt
injection (filtered by geography/faction/recency — a Sturgian lord doesn't know
yesterday's Aserai gossip), and letters (an ally writes to warn you of a plot —
courier system already exists).

Rumors age, spread by region, and may distort with distance.

## v1.3 — NPC↔NPC rivalries and schemes

Active feuds: two hostile lords trying to harm each other.

**Governing principle: the simulation is mechanical; the LLM narrates only at the
player's point of contact.** A daily tick advances schemes probabilistically
(traits, relations, opportunity). The LLM is called only when the player touches
the affair — hears the rumor, is recruited as an agent, uncovers the plot.
Background LLM simulation would be an unacceptable API cost.

Schemes have a lifecycle: plotted → progressing → discovered / succeeded / failed,
with counterplay. Every state change feeds the rumor store.

## v2.0 — Player influence on the world (CK3-style)

Persuading a faction leader toward war, luring an enemy lord to defect,
assassination, espionage. The most powerful layer — and the most dangerous for
save balance — so it ships last, on a proven foundation. Two hard rules:

1. **No direct world action from a conversation.** Same philosophy as quests
   (verified deeds, never words): a successful conversation plants a *disposition*,
   not a war. The lord "brings it to his council"; days later the event lands (or
   doesn't), weighted by personality, relations, and kingdom state. An eloquent
   player must never trigger a world war in three messages.
2. **Build on native mechanics.** `ChangeKingdomAction` (defection),
   `DeclareWarAction`, `KillCharacterAction` all exist engine-side. The mod adds
   the decision layer and the narrative, not the mechanics.

---

## Also planned (unscheduled)

- **NPC-initiated interceptions on the campaign map.**
  A lord can move toward the player and open a CR conversation without the player
  seeking them out. `MobileParty.Ai.SetMoveEngageParty(MainParty)` makes the party
  give chase; when parties touch, hook the encounter to open the chat instead of the
  vanilla dialogue. One active pursuer at a time; strict per-NPC cooldown so it never
  becomes harassment.

  Trigger conditions (in rough priority order):
  - **Territorial patrol** — the most distinctive case: a lord of a different faction
    intercepts the player crossing their land. Bannerlord has no real borders; a
    suspicious vassal riding out to challenge a foreign traveler makes the world feel
    like it has actual sovereignty. Tone: interrogation / warning ("What brings you to
    Vlandian lands, stranger?"). Probability scales with faction hostility and the
    player's notoriety. No attack — purely a conversation, with consequences.
  - **Urgent personal matter** — relation threshold (love or deep grudge) crossed since
    last meeting; the NPC can't wait for the player to come to them.
  - **Quest follow-up** — the NPC wants to press a deadline, renegotiate terms, or
    deliver a reward in person.
  - **Heard something** — fed by the v1.2 rumor store: a lord who just received word of
    a player deed (a battle won on their land, an execution, a marriage) rides out to
    react while it is still fresh.

  This pairs naturally with v1.2 (rumor store gives lords reasons to care) and v1.3
  (a scheme participant intercepting the player to warn or threaten them).

- **Recruit a lord or lady into your clan through conversation** *(AI Influence
  veterans consistently cite this as one of the most valued features in that mod.)*
  A lord with deep enough trust in the player and no better prospects could be
  persuaded to leave their current clan and join the player's — becoming a clan
  member (`ChangeKingdomAction` transfers the Hero to the player's clan), not a
  companion slot, since lords bring troops, skills, and sometimes fiefs.

  Gates:
  - Relation ≥ high threshold (trust must be genuinely deep, not just friendly)
  - Player clan tier ≥ lord's clan tier (a duke does not follow a landless adventurer)
  - Lord is in a vulnerable position: landless, clan weakened or destroyed, or
    relation very low with their current liege — no one abandons comfort lightly

  Side effects to respect: the lord's original clan loses a member; that faction's
  standing toward the player takes a hit. Both are vanilla consequences and add
  weight to the decision.

  Aligns with v2.0 (political influence, defections). The `join_clan` action follows
  the same pattern as `join_party` — teach it, gate it game-side, execute it in the
  bridge. The LLM negotiates the story; the bridge enforces every condition.

- **Power differential context block.**
  NPCs currently receive relation, traits, and liege/vassal status — but not the raw
  military and political imbalance between their clan and the player's. A lord whose
  clan holds 2 fiefs while the player's kingdom controls 18, or who lost a battle
  against the player last week, should speak with calculated caution — not blind
  defiance and not blind submission.

  What to inject into `EncounterContext` / `EncounterContextBuilder`:
  - Player kingdom fief count vs NPC clan fief count (rough power ratio)
  - Whether the NPC's clan is at war with the player and losing
  - Whether the NPC recently lost a battle to the player's forces
  - Whether the NPC is currently a prisoner of the player's faction

  What this changes in the prompt: a `POWER BALANCE` block that teaches the NPC to
  read the room. A vassal who knows they would lose does not openly defy — they
  comply while nursing resentment, hedge their words, choose their battles. That
  calculated, reluctant deference is more realistic *and* more interesting than
  either blind arrogance or yes-man submission.

  This does NOT make NPCs servile. It makes them *afraid when fear is warranted* and
  *bold when they have the leverage* — the same calculation a real person would make.
  Relation remains the warmth axis; power differential is the fear/respect axis.
  Both feed the same character, differently.

- **Dynastic succession — memory reframing on player death. ✅ DONE (2026-06-22).**
  Shipped DETERMINISTICALLY rather than via an LLM pass (chosen over the per-NPC LLM
  rewrite below: no API cost/latency spike at succession, deterministic, save-safe).
  On `OnPlayerCharacterChangedEvent`, `Dynasty.SuccessionBehavior` reframes every stored
  profile: drops the predecessor's private events (`SuccessionMemoryPolicy` —
  flirt/intimacy/captivity/jealousy), keeps the political/relational, and marks the
  history inherited (`NpcProfile.InheritedFromName` + kinship via `SuccessionKinship`).
  `PromptBuilder.AppendInheritedNote` frames it ("the person before you is the HEIR of
  X, their father… you inherit the standing, not the private history"). Pure rules tested;
  `cr.succession` simulates it. OPTIONAL future polish: the per-NPC LLM rewrite for richer
  per-event dynastic prose (the original design below). 

  When the player character dies and an heir takes over, NPC memories are not wiped
  but rewritten through the lens of the new relationship. Trigger:
  `CampaignEvents.OnPlayerCharacterChanged` fires when Bannerlord hands control to
  the heir; the mod derives the kinship (`father / mother / brother / sister / spouse`)
  from the two Hero objects.

  For each NPC profile, an LLM reframing pass (same pattern as `LlmMemoryCompressor`)
  receives the event list + kinship label and rewrites the profile's history:
  - **World/political events** → transformed into dynastic memory:
    "Your father helped me at the siege of Epicrotea — I owe your house that debt."
  - **Personal/intimate events** → dropped. The heir does not inherit their parent's
    private history.
  - **Active obligations** (quests, alliances, pledges) → kept with an "inherited"
    marker so NPCs can acknowledge the open thread: "Your mother made me a promise.
    Does her blood intend to honor it?"

  The rewritten history replaces the raw event list with a compact `background` block
  in a dynastic voice. The new player inherits reputation, grudges, alliances, and
  debts — not memories. First meeting with a given NPC after succession resets the
  personal relationship history from that point forward.

  Console workaround until this ships: `cr.forget_all` + `cr.memory_add` per NPC.

- **Scenario bank** for captive scenes: Twine→JSON, one-use-per-campaign via
  SyncData, eligibility by kinks/gender, prompt injection.
  (When adding persisted campaign state, REMOVE
  `CaptiveEncounterService.ResetOnSessionLaunch` — SyncData(load) fires before
  OnSessionLaunched and would clobber loaded state.)
- **Image overlay** for scenes (`[SCENE]` parser).
- **Prompt caching split**: stable system prefix + dynamic second system message,
  so the `cache_control` breakpoint survives per-turn changes (Day, encounter).

- **Talk to a prisoner — bargain for their freedom** *(wished 2026-06-22, the inverse of the
  DeliverPrisoner deed)*. **Case A BUILT 2026-06-22** (untested): `AppendPrisonerFreedomBargain` (SDK,
  gated on `NpcIsCaptive`) teaches a captive lord to bargain; bridge `free_prisoner` action →
  `EndCaptivityAction.ApplyByReleasedByChoice` (guarded: only a lord you truly hold), reward via the
  LLM's own change_relation/give_gold/join_clan. `cr.capture` shortcut to get a test prisoner.
  OPEN: confirm the natural entry point (does the CR option appear in a prisoner-cell conversation, or
  must a token be hooked) — needs in-game check via cr.capture. **Case B BUILT 2026-06-22** (untested):
  `NpcIsPrisonerOfAnother` context flag + `AppendPrisonerRescueBargain` (the captive offers a rescue
  deed) + QuestFactory self-rescue (a `rescue_prisoner` with no named target → target = the giver),
  verified by the EXISTING battle-rescue path (`FreedPrisonerHeroIds`). Both entry points share the
  same open question (does the CR option appear in a prisoner-cell conversation).
  A lord held captive may, when the player speaks with them, offer a reward
  for their release. Two cases:
  - **(A) The player's own captive.** Talk to a lord you hold (your party / your settlement's dungeon).
    They offer "free me and I'll …"; the player accepts and a `free_prisoner` ACTION releases them
    (`EndCaptivityAction`) and grants the reward in one turn — the mirror of give_prisoner.
  - **(B) Another lord's captive.** Visit a settlement holding an enemy/neutral lord and speak with
    them; they ask to be broken out. This is a rescue DEED (like `RescuePrisoner`, but issued BY the
    prisoner), verified EVENT-DRIVEN when they are actually freed (the player defeats their captor or
    takes the fief) via `QuestDeedTracker`.
  - **Reward, balanced above ransom** (else the player would just ransom them): chiefly relation +
    **defection into the player's clan** (a freed, grateful lord joins — the AI-Influence draw), with
    gold optional. Reuses the bargain/quest framework + the join_clan execution.
  - **Entry point** (the only real enabler, and it is not a blocker): the mod already opens a CR chat
    programmatically with any Hero (captive scenes do this), so "speak with a prisoner" is just a
    settlement-menu / party option that lists the held lords and opens the chat — no vanilla
    conversation needed. The settlement prisoner list (`settlement.Party.PrisonRoster` heroes) supplies
    the candidates.
  - Pure/testable: the release-bargain rules (reward floor vs ransom value; case A immediate vs case B
    deferred) and the rescue-deed verification (the prisoner is freed AND the player caused it).

- **Player-authored character data** *(tester request: AI Influence parity — hand-craft a
  character's personality + backstory)*. Decided design (2026-06-21):
  - An editable `ModuleData/character_overrides.json`, one entry per NPC: `hero` (name) + optional
    `id` (StringId, disambiguates homonyms) + `traits` (Valor/Honor/Mercy/Generosity/Calculating,
    each −2..+2) + optional `backstory` (free text).
  - **Traits flow THROUGH the existing trait system, not around it**: the authored values REPLACE the
    vanilla traits via `Hero.SetTraitLevel` at session launch, so the prompt's derived personality,
    the trait-derived mod features (kinks…), AND the campaign AI all read ONE source of truth — no
    text-vs-traits conflict. `backstory` is roleplay flavor only, injected additively, making NO
    behaviour claim (behaviour stays driven by traits).
  - **No per-save snapshot** (vanilla values are recoverable from the game's native data). Instead an
    **MCM toggle "Use custom traits"** switches between the player's values and vanilla; players keep
    file copies to version their own sets.
  - **Auto re-derive** trait-derived features (kinks etc.) when an override applies, so an already-met
    NPC's frozen features update — convenient for modders/testers iterating.
  - Pure/testable: entry parse, name→`DefaultTraits` mapping, −2..+2 clamp, id/name resolution.
    Glue (in-game): the SetTraitLevel apply, the backstory prompt injection, the loader, the MCM toggle.
  - Console: `cr.backstory [name]` (show loaded override + effective traits), `cr.reload_lore` (reload
    the file without relaunch).

- **Companion missions — dispatch an agent, a report comes back** *(wished 2026-06-22)*.
  *(Pure foundation BUILT 2026-06-22, UNCOMMITTED, tests green — `CalradiaRemembers.Logic/
  CompanionMissions.cs`: the errand enum + governing-skill map + risk flag + the deterministic outcome
  resolver `Resolve(type, skill, roll, difficulty)` → Success/Partial/Failure (+ CompanionCaptured on a
  botched risky errand, the loop-closer to prisoner rescue), with `CanAttempt` skill floor, `DurationDays`,
  and `NewsItemsForResult` report sizing. 17 tests. TUNING FLAGGED for Gabriel's ratification — skill
  divisor, the score thresholds, the attempt floor, duration, the risk set.)*
  *("BRING ME THE NEWS" slice BUILT 2026-06-23 — a PERSISTED PHYSICAL JOURNEY (Gabriel's design),
  UNCOMMITTED, untested in game. `Missions/CompanionMission.cs` (state-machine model) +
  `CompanionMissionBehavior.cs`: the companion LEAVES the party as a real clan party
  (`MobilePartyHelper.CreateNewClanMobileParty` — vanilla-serialized, visible on the map, capturable) →
  rides to a town (from the pure `NewsDestinationSelector` on the `about` topic) → lingers (`DurationDays`) →
  rides back to the moving player (`TravelingParty.DirectToMovingPoint`) → rejoins (`AddHeroToPartyAction`)
  and reports. ANTI-EXPLOIT: quality rolled AT DISPATCH, news gathered AT ARRIVAL (`GetKnownEventsRatedAt`),
  both PERSISTED (JSON byte[]) — a reload can't re-roll or fetch instant news. CAPTURE-RESILIENT:
  `HeroPrisonerTaken` → AwaitingReunion; the news is delivered when the companion is freed and rejoins you
  (pairs with prisoner rescue). In-conversation dispatch wired (SDK `CanGatherNews` +
  `AppendCompanionMissionOffer` teach `gather_news`; bridge re-validates). Console: `cr.send_news [about]` /
  `cr.missions` / `cr.mission_return`. TESTING_GUIDE scenario P. The report is delivered as a CONVERSATION:
  on return the companion stashes a one-shot directive (SDK `EncounterContext.CompanionNewsReport` +
  `PromptBuilder.AppendCompanionNewsReport`, consumed by `ConsumeNewsReport`, persisted), and a banner nudges
  the player to speak with them; the companion opens by reporting in their own voice.)*
  *(SPY + STEAL errands BUILT 2026-06-23, UNCOMMITTED, untested in game — both ride the same journey
  skeleton (Roguery-governed, risky → the road party can be caught). Spy delivers INTEL about the target
  town (garrison/militia/prosperity/lord whereabouts) in the homecoming conversation; Steal grants LOOT to
  the player on reunion (pure `CompanionMissions.LootForResult`: full/40%/nothing by quality, +4 tests) and
  tells of it. Payload secured at arrival + persisted (anti-exploit). Console: `cr.send_spy [about]` /
  `cr.send_steal [about]`. TESTING_GUIDE P7.)*
  *(BARTER + ENVOY errands BUILT 2026-06-23, UNCOMMITTED, untested in game — all five errand types now ride
  the one journey. A pure `CompanionMissions.PayloadOf(type)` (News/Intel/Gold/Mood, +1 test) drives WHAT is
  gathered; the per-type voice stays in the behavior. Barter (Trade) returns a gold profit like Steal;
  Envoy (Charm) brings back a faction-mood read (wars, settledness) in conversation. Console: `cr.send_barter`
  / `cr.send_envoy`. TESTING_GUIDE P8. 338 tests.)*
  *(In-conversation dispatch GENERALIZED to all five errands 2026-06-23, UNCOMMITTED — one `gather_news`
  action now carries an `errand` keyword (news/scout/steal/trade/envoy) + `about`; the SDK section teaches
  the menu, the bridge `MapErrand` maps it, and the offer gate is now type-agnostic (per-errand skill checked
  at dispatch). The player can ask a companion in chat to go do any of the five. TESTING_GUIDE P4. NEXT
  (optional): consequences (caught-spying relation hit), then the PLANNED aggression/patrol + happiness work
  below.)*

  **PLANNED — aggression & patrol errands** *(wished 2026-06-23)*. The same journey, but the companion goes
  to FIGHT, not just gather: **patrol a sector** (hold a region, engage hostiles that enter), **hunt
  bandits** (seek and destroy bandit parties in an area), **attack** a named lord's party / **raid** a
  village or caravan. The defining difference from the gather errands: these need a real FIGHTING FORCE, so
  dispatch gains a troop-sourcing step — the companion either **recruits** their own (gold + time,
  Leadership-gated) or **takes troops the player assigns** from the main party at send-off (a transfer
  step). The OUTCOME is REAL combat, never a roll: the errand party fights through the engine; victory →
  loot/prisoners come home with them, defeat → the companion is taken (the existing capture-resilience
  path). Reuses the journey skeleton + capture; adds a target/sector resolver and the troop transfer. Risk
  scales with the force given. Pairs with happiness below (sending people to die unhappily costs you).

  **PLANNED — companion happiness & retention** *(wished 2026-06-23)*. Each companion carries a HAPPINESS
  score, distinct from stance (their regard for YOU): it measures their satisfaction in your service.
  Drivers, all deterministic and pure-testable: idle vs. employed time, errands that SUIT them (a rogue
  relishes a steal, a warrior a fight) vs. ones that don't, successes vs. failures/captivity, pay / share
  of loot, being kept from a loved one (ties to marriage + jealousy), and the player's treatment. It decays
  and recovers over time. Bands: content → restless → wants to leave. At the low end the companion RAISES it
  in conversation (a grievance), and if unaddressed they DEPART (leave the clan/party). Build order: a pure
  `CompanionHappiness` model (drivers → score → band) in CalradiaRemembers.Logic + tests FIRST; engine wiring
  reads game state and surfaces the grievance via the existing stance / letter / conversation paths. Reuses
  camp life (companions sour on each other) and missions (mission outcomes feed happiness).
  - **Surfaced on the encyclopedia** *(Gabriel, 2026-06-23)*: a "Contentment: <band> (N)" line on the
    companion's hero page — a sibling of the existing `EncyclopediaHeroPagePersonalRelationMixin` (which
    already shows "Personal regard"), gated to the player's OWN companions. The at-a-glance gauge; the
    conversation grievance stays the primary signal. ~20 lines once the score exists (reuse the mixin
    pattern + a banded `Describe(int)`).

  The player tasks a companion in conversation with an errand that resolves off-screen and returns
  as an in-fiction report. This is the CR answer to AI Influence's "world events" panel: instead of a
  free-floating UI feed, **world news has a source, a delay, and a risk** — the player *sends someone*,
  and *someone comes back*. It also finally gives Bannerlord companions a purpose, and it reuses three
  systems CR already owns: the courier cycle (dispatch → delay → return), the `WorldEventStore` (the
  matter of "bring me the news"), and the verified-deed framework (success/failure decided by the game,
  never by the LLM's word).

  **One pattern, several missions** — *dispatch in conversation → time delay → return with a report*
  (the courier cycle). The companion leaves the retinue (or a timer runs); they return via a parchment
  letter / a debrief conversation.

  | Mission | Player input | Output on return | Skill gate |
  |---|---|---|---|
  | **Bring me the news** | "what stirs in the realm?" | `WorldEventStore` events filtered by recency / faction / distance, told in the companion's voice | Scouting |
  | **Spy** (a lord / fief) | target | troop strength, whereabouts, a *secret* (becomes a memory) | Roguery |
  | **Steal & bring me the loot** | target | loot (gold / an item) — **risk of capture** | Roguery |
  | **Go barter** | the goods | a better sale price | Trade |
  | **Be my envoy** | faction + message | carries a statement / probes a faction's mood | Charm |

  **Governing principle (= CR's DNA): deterministic outcome (a skill roll), LLM narration.** The LLM
  colours the report; the *result* (loot, capture, intel) is computed game-side — same "quests that
  don't break" discipline, and cheap on tokens (one framing call on return, not a background sim).

  **Loop-closer:** "Steal" and "Spy" can **fail → the companion is captured**, which branches straight
  into the prisoner-rescue system (case B above). Features feed each other.

  Pairs with v1.2 (the rumor/event store supplies "the news") and the influence loop: the report
  is also where the player hears *their own deeds* reflected back, attributed to them.

- **Influence loop — NPCs hear of the PLAYER's deeds, attributed to them. ✅ DONE (2026-06-22).**
  The cheap, high-ROI lesson taken from AI Influence's "world events": the *feeling of influence* comes
  not from simulating a world but from reflecting the player's own acts back at them. The world-event
  store (v1.2) already recorded player feats (`InvolvesPlayer`); the consumer was drowning them in the
  neutral "WHAT YOU'VE HEARD" gossip. Now `EncounterContextBuilder.ResolvePlayerDeedsHeard` pulls the
  player's known deeds into a dedicated SDK section ("WHAT IS SAID OF THE ONE BEFORE YOU"), attributed to
  the very person the NPC addresses, each tagged with a **valence** (pure `PlayerDeedValence`:
  Wound/Boon/Neutral, from whether the deed's `WorldEvent.AggrievedFactionIds` touches the observer's
  faction vs. shares the player's). The feeders now record the harmed side on the three adversarial player
  deeds (battle won → loser's faction, execution → victim's, conquest → dispossessed). It colours the
  *conversation only* — persisted stance still moves solely on witnessed acts, so no double-count with the
  battle stance bumps (which touch only lords who were present). `cr.deed_heard [name]` seeds + echoes it.
  TESTING_GUIDE scenario O. Future polish: kin-level wound (not just faction); a light "kingdom rumour"
  chronicler line.

- **Camp life — companions who like or resent each other** *(wished 2026-06-22, Warband nostalgia)*.
  Companions form opinions about *each other* (not just the player) and bring them to the player —
  "I cannot abide riding with X" / "Y has my respect." It adds the "camp life" that made Warband's
  companions feel alive, and it is a natural extension of the existing hero↔hero relation tracking
  (v1.2 feeder #3) — the simulation is mechanical (native `GetRelation` between the two companions +
  trait compatibility drives the disposition), the LLM only narrates **at the player's point of
  contact** (a companion raises it in conversation). Same cost discipline as schemes: no background LLM.
  - A drifting companion↔companion relation crosses a threshold → a flagged "wants to talk to you about
    X" disposition (surfaced like an NPC-initiated approach, or on next conversation).
  - High mutual regard between two unmarried, eligible companions → **they may ask to marry** (vanilla
    `MarriageAction`; gated like the player-marriage system — family/clan rules already modelled). A
    wedding between your own people is a camp event worth a letter / chronicle line.
    - **DECIDED (2026-06-22): the couple STAYS in the player's service — the clan grows, it does not
      disperse.** The player invested time raising these companions; they keep them, see them as a
      couple, watch their children grow. So the marriage must NOT trigger the vanilla "leave to found a
      household" path — keep both heroes in the player's clan (companions, or promoted clan members),
      and let any children join the clan. The reward of the feature is a *growing household*, not a
      bittersweet departure.
  - Feeds the rumor/event store (a feud or a wedding among your retinue is news in its own right) and
    pairs with jealousy (a companion love-triangle is the same machinery turned inward).

- **Custom correspondent portraits** *(letter window — wished 2026-06-21)*. Show a hand-made painted
  portrait (the "letter from a noble lady" look) for a correspondent instead of the in-game 3D head
  render. Tiered: a custom image when one exists for that hero, else fall back to the current
  `CharacterImageIdentifierVM` render — so only notable NPCs need art and everyone else still works.
  - **Already well-placed**: portrait resolution is centralized in `LetterViewModel.ResolvePortrait(Hero)`,
    so this is a localized change — add a `UseCustomPortrait` flag + a custom-image binding, and the
    prefab shows either a raw image widget or the existing `ImageIdentifierWidget`.
  - **Lookup**: `Modules/CalradiaRemembers/Portraits/<heroStringId>.png`, with optional fallback tiers
    (culture+gender defaults) and/or a `portraits.json` map. Pure/testable: the resolver (which key
    wins, fallback order, missing-file handling).
  - **The real fork — how a PNG reaches Gauntlet** (needs a small spike before committing):
    - **(A) Pre-baked sprites**: ship the PNGs in the module's sprite atlas (`GUI/SpriteParts` +
      `SpriteData.xml`), reference by sprite name. Clean and fast, but static — modders adding a
      portrait must rebuild the atlas. (This is the first-time custom-sprite pipeline the parchment
      work deliberately avoided.)
    - **(B) Runtime texture load** (preferred for moddability): load the PNG from disk at open time
      and bind it to an image widget. Drop-in folder, no atlas rebuild — but needs the right engine
      texture-load API and lifetime handling; verify the exact call first.
  - Distinct from the parked "generated portraits compositing over the conversation tableau" idea
    (that one stays a separate mod); this is a bounded, CR-internal feature for the letter window.
