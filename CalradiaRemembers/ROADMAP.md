# Calradia Remembers — Roadmap

The long-term direction of the mod. Each layer is shippable on its own; the order is the architecture.
Full detail for shipped work lives in `CHANGELOG.md` and git history — this file tracks what is DONE at a
glance and the OPEN work ahead.

> **This document is load-bearing — keep it maintained throughout development.** The roadmap is not a
> formality: it is the project's **brainstorming surface** and its **strategic tracking** tool. Every design
> discussion, parked idea, deferred piece, open question, and shipped milestone belongs here, recorded as it
> happens — so the direction stays legible across sessions and nothing of value is lost. Update it the moment
> a feature is wished, decided, started, or finished; prune what is stale; verify against the code before
> calling something "shipped" or "a gap." A current roadmap is how we choose the next step with confidence.

Cleaned up 2026-06-23 (the many already-shipped "Also planned" entries collapsed into the summary below).

---

## ✅ Shipped & built

Live on Nexus, or built + committed awaiting the next package. **(untested)** = built green and committed but
its in-game wiring is not yet validated — these ride together in a future v1.14.0.

**Conversation & memory core** — persistent per-NPC memory across the save; in-character free-text LLM
dialogue woven into vanilla; relation + multi-axis **stance** (trust/respect/fear, "words nudge, deeds
prove"); hearsay about third parties; culture-aware via editable `ModuleData`; story log; encyclopedia
integration (personal regard, discoveries).

**Quests & negotiation** — informal quests verified by real game events (no ghost quests); join a kingdom as
mercenary; recruit a companion (pay in goods); **recruit a lord into your clan** (`join_clan`); the
negotiation framework — **deliver-a-prisoner** + **declare-war** deeds (Phases 1 & 2), and **Phase 3
"intimacy as currency"** (adult-gated, exploiter archetype, persistent leverage) **(untested)**; **talk to a
prisoner — bargain for freedom/rescue** (cases A & B) **(untested)**.

**World & influence** — world-event/rumor store (battles, sieges, marriages, executions, wars) with
geo/faction/recency-weighted hearsay; the **Chronicle**; commoners + settlement intel; **NPC↔NPC schemes**;
**NPC-initiated interceptions** (a lord rides out to challenge you); **power-balance** context block; the
**influence loop** (NPCs speak of *your* deeds, attributed + valence-coloured: kin/faction Wound vs Boon)
and the **"talk of the realm"** line.

**Companions (the 2026-06-23 trilogy, untested)** — **missions** (news / spy / steal / barter / envoy — a
real physical journey: the companion leaves as a clan party, rides to a town, returns and reports in
conversation; save-safe + capture-resilient + anti-exploit; **recall** — order an away companion to abandon
the errand and come home, by conversation *or* by letter, as a verified deed, not a hollow agreement);
**happiness & retention** (an encyclopedia
"Contentment" gauge, errand/idle drivers, an in-conversation grievance, reassurance, and departure when too
long unhappy); **camp life** (companion↔companion rapport from trait compatibility, voiced opinions, and
companion weddings that keep the couple in your clan).

**Dynasty & adult** — **dynastic succession** (the heir inherits standing/debts/grudges, not the private
history; deterministic); marriage politics (family-approval, consorts); **jealousy**; captor/captive scenes
(Hardcore); **player-authored character data** (`character_overrides.json`).

**Tech** — multi-provider LLM incl. local (OpenAI-compatible endpoint covers Ollama/LM Studio/vLLM);
**setting-agnostic overhaul support** (`setting.json` → reskin for The Old Realms & co. as a data pack);
parchment letter window + visible couriers; **prompt-cache split** (stable prefix cached, dynamic tail fresh).

---

## Open — not yet built

### Scripted scenarios — a modder-extensible world-reaction engine *(wished 2026-06-24 — a new pillar)*

**Reactive rules, NOT authored storylines.** A branching narrative (Twine-style) fights the sandbox; CR's
thesis is the opposite — the world reacts, then the *player and the game engine* write what comes next. So a
scenario is a **stateless reactive rule**: *when this happens → the world shifts like so → and here is how it
reads*. No passages, no branches, no authored "next beat". (Twine was considered and dropped for exactly this
reason — see also the captive scenario bank.)

A rule has **four fields** (plus two optional):

| field | role |
|---|---|
| **Trigger** | the in-game event that arms the rule |
| **Guards** *(optional)* | eligibility predicates — scope which rules are even applicable now |
| **Consequences** | a list of declarative world mutations |
| **World-event message** | the situation text, dropped into the World-Event store → told in-game |
| *fire-once* *(optional)* | one-shot + persisted, vs repeatable |
| *weight* *(optional)* | bias the random draw; default uniform |

**Random pools by shared trigger.** If several rules share a trigger (and their guards pass), **exactly one
is chosen at random**. That single mechanism gives the replayability we want without any branching: the
four Rhagaea-marriage outcomes below are just **four rules on the same trigger**, one drawn and **sealed**
(persisted, anti-save-scum, like `CompanionMission.Quality`).

- **Triggers** ride native `CampaignEvents` (hero death, marriage, war declared, settlement taken, clan
  destroyed…) plus CR's own deed events.
- **Consequences** are declarative primitives CR mostly already owns: `ChangeRelation`, `ChangeInfluence`,
  `StartWar`/`EndWar` (`DeclareWarAction`/`MakePeaceAction`), `GiveFief`, `SpawnWorldEvent`/`SpawnRumor`,
  `InjectMemory`, `SendLetter`. The engine executes them (verified deeds); the rule never authors dialogue.
- **The message channel is already shipped.** The World-Event message plugs straight into the World-Event
  store + Chronicle + influence loop: drop the event, and NPCs speak of it on their own, valence-coloured by
  their side. The LLM only narrates at the player's point of contact.

**A keyword DSL + a composer app (the moddable surface).** Triggers, guards and consequences are expressed as
a small **controlled vocabulary of keywords** (`ON_DEATH:Rhagaea`, `IF_PLAYER_RULES`, `DECLARE_WAR:A->B`,
`CHANGE_RELATION:Hero:+N`, `INJECT_MEMORY`, `SEND_LETTER`…) — guards AND-combined, no arbitrary expression
language, so the engine evaluator and the authoring stay trivial. The keyword vocabulary IS the modder-facing
API; it serializes to JSON in `ModuleData`. On top of it, a small standalone **script-composer app** lets an
author build a rule by *picking predefined keywords from dropdowns* (no hand-written JSON, validated by
construction) and exports the file. Pure/testable in `CalradiaRemembers.Logic`: the trigger-matcher, the
guard evaluator, the random-pool selection, and fire-once bookkeeping; only the CampaignEvent hooks and the
consequence executors are engine glue. Define the vocabulary first — the app is downstream sugar.

> **BUILT 2026-06-24 (Opus, 412 tests green, NOT committed) — pure core + a working engine-glue slice.**
> Pure (`CalradiaRemembers.Logic`, +34 tests): `KeywordToken` (VERB:payload language), `ScenarioRule` (four
> fields + fire-once/weight + `Parse` factory), `GuardEvaluator` (AND-combined, injected resolver),
> `ScenarioSelector` (trigger match incl. `*` wildcard, one-shot exclusion, guard filter, weighted random
> pool — RNG injected), `ScenarioArgs` (target:delta payload parse) — incl. the Rhagaea marriage as four
> rules on one trigger. Engine glue (mod, `Scenarios/`): `ScenarioContext` (resolve hero/faction refs by
> name or id), `ScenarioGuardResolver` (IF_PLAYER_RULES / IF_HERO_IS / IF_PLAYER_INVOLVED /
> IF_PLAYER_IS_FEMALE|MALE / IF_PLAYER_AT_WAR; unknown guard → fails), `ScenarioConsequenceExecutor`
> (DECLARE_WAR / MAKE_PEACE / CHANGE_RELATION; unknown → skipped), `ScenarioRuntime` (select → execute →
> world-event message → seal one-shot), `ScenarioPackLoader` (ModuleData/scenarios/*.json via Newtonsoft),
> `ScenarioBehavior` (CampaignBehaviorBase: feeders for ON_DEATH/ON_MARRIAGE/ON_WAR_DECLARED/ON_PEACE +
> persisted fired-once set). Registered in the submodule after WorldEventStore. Console `cr.scenarios` /
> `cr.scenario_fire <VERB> [hero]`. Sample pack `ModuleData/scenarios/example.json` = the Rhagaea-death→war
> flagship (deployed to the install). **UNTESTED in game.**
>
> **COMPOSER APP BUILT 2026-06-24** — `Tools/ScenarioComposer.html`, a single self-contained offline HTML
> tool: pick trigger / guards / consequences from dropdowns (the vocabulary mirrors the engine in one editable
> `VOCAB` block), fill message / fire-once / weight, live JSON preview, import/export, "Load Rhagaea example".
> Exports exactly the `ModuleData/scenarios/*.json` schema the loader reads (verified by a stubbed-DOM
> round-trip: DECLARE_WAR/MAKE_PEACE/CHANGE_RELATION + guards serialize byte-correct). This is the in-game
> authoring loop: compose → download → drop into `scenarios/` → `cr.scenarios` / `cr.scenario_fire`.
>
> **Extended 2026-06-24 (415 tests green, untested in game):** added consequences `CHANGE_INFLUENCE:Ref:+N`
> (clan influence) and `INJECT_MEMORY:Ref:text` (plant a memory on an NPC so they reference it in conversation
> — the "Calradia remembers" channel), with `ScenarioArgs.TryParseTargetRest` (+3 tests) and both mirrored
> into the app `VOCAB`. Added a **reentrancy guard** (`ScenarioRuntime._isFiring`): a consequence's own
> campaign event cannot cascade back into the engine. Shipped the **Rhagaea-marriage A/B/C/D content pack**
> (`ModuleData/scenarios/rhagaea_marriage.json`, deployed): four rules on `ON_MARRIAGE` (guards
> `IF_PLAYER_INVOLVED` + `IF_HERO_IS:Rhagaea`; B adds `IF_PLAYER_RULES`), one drawn at random; each keeps her
> in character and surfaces the union's nature through her OWN memory (consort / two-crowns / regent /
> suspicious). Test: `cr.scenario_fire ON_MARRIAGE Rhagaea`.
>
> **Still to grow:** more vocabulary (`GIVE_FIEF`, `SEND_LETTER`, …) — mirror each into the app `VOCAB`; a
> richer marriage outcome than memory-only (a dedicated prompt-flavor channel / claim grant); the visual
> composer could gain hero/faction autocompletion from a game export.

This is what makes CR **world-agnostic**: a total-conversion drops in its own rules (its lords, its triggers,
its consequences) as a content pack; Calradia's hand-authored rules are just the first pack.

**Flagship content — the Rhagaea marriage** (the case that birthed the idea). Vanilla marrying a *ruler* is
cold and out-of-character: she folds into your clan and a random AI clan inherits her throne. CR replaces it
with **four rules on the `ON_MARRIAGE:Rhagaea+Player` trigger, one drawn at random and sealed**, each keeping
Rhagaea true to herself (the empress-stateswoman never abdicates *for* you). Guards scope the pool; the result
is surfaced through her *voice* (prompt context), not a popup:
- **A — Prince-consort of the South**: *you* join *her*; she keeps the throne; you become her sword and
  influence multiplier. The "become emperor" fantasy is denied — correctly.
- **B — The pact of two crowns** *(guard: the player already rules)*: an alliance of two thrones, not a
  merger; the real tension is whose heir inherits what (her daughter Ira vs your line).
- **C — The regent's gambit**: she names you war-leader and keeps the crown; you *can* reach the throne, but
  only by winning the succession (with her blessing earned) or on her death — never by her surrender.
- **D — The suspicious empress** *(a tension layer)*: the marriage is conditional on loyalty to her
  restoration; CR tracks every move, and maneuvering for the throne behind her back turns her — grudge, war,
  or she leaves you.

Because she keeps her throne in A/B/C, the vanilla "empire destabilizes" feel-bad disappears — any crown
change becomes *earned and dramatic*, never a mechanical accident. Other rulers fall back to a generic consort
rule until they get their own pack. (Replayability is the explicit goal: marrying the empress should never
play out the same twice.) A second worked example — `ON_DEATH:Rhagaea` → consequence
`DECLARE_WAR: Northern Empire, Western Empire -> Southern Empire`, message "With the empress fallen, the
eagles turn on a leaderless South" — then the sandbox takes over.

### Companion aggression & patrol errands *(wished 2026-06-23 — the last big companion piece)*

The same journey skeleton, but the companion goes to FIGHT, not just gather: **patrol a sector** (hold a
region, engage hostiles that enter), **hunt bandits** (seek and destroy bandit parties in an area), **attack**
a named lord's party / **raid** a village or caravan. The defining difference from the gather errands: these
need a real FIGHTING FORCE, so dispatch gains a troop-sourcing step — the companion either **recruits** their
own (gold + time, Leadership-gated) or **takes troops the player assigns** from the main party at send-off (a
transfer step). The OUTCOME is REAL combat, never a roll: the errand party fights through the engine; victory
→ loot/prisoners come home, defeat → the companion is taken (the existing capture-resilience path). Reuses the
journey skeleton + capture; adds a target/sector resolver and the troop transfer. Risk scales with the force
given. Pairs with companion happiness (sending people to die unhappily costs you).

### Custom hand-made portraits — letter window, chat window, player *(wished 2026-06-21, broadened 2026-06-24)*

Show a hand-made painted portrait (the "letter from a noble lady" look) for a hero — NPC **or the player** —
instead of the in-game 3D head render, across **CR's own prefabs**. Tiered: a custom image when one exists for
that hero, else fall back to the current `CharacterImageIdentifierVM` render — so only notable heroes need art
and everyone else still works.
- **Surfaces**: (1) the **letter window** correspondent (already scoped below), (2) the **conversation/chat
  window** portrait, (3) the **player's own** portrait. All three are CR prefabs, so no tableau-compositing
  problem.
- **Shared resolver** (one place, reused by all surfaces): lookup `Modules/CalradiaRemembers/Portraits/<heroStringId>.png`,
  with optional fallback tiers and/or a `portraits.json` map. Pure/testable: which key wins, fallback order,
  missing-file handling. The letter window already centralizes this in `LetterViewModel.ResolvePortrait(Hero)`
  — add a `UseCustomPortrait` flag + a custom-image binding, and the prefab shows either a raw image widget or
  the existing `ImageIdentifierWidget`; the chat window mirrors the same.
- **The real fork — how a PNG reaches Gauntlet** (needs a small spike, shared by all surfaces): **(A)**
  pre-baked sprites in the module atlas (clean/fast, but static — modders rebuild the atlas), or **(B)**
  runtime texture load from disk (drop-in folder, no rebuild — but needs the right engine texture-load API +
  lifetime handling). (B) preferred for moddability; verify the call first.
- **Encyclopedia is EXCLUDED**: its page composites live widgets over the 3D tableau, so a custom image there
  needs a full encyclopedia rebuild — that is the parked "generated portraits" separate-mod territory, not
  this item.

### Captive-scene content
- **Scenario bank**: a bank of authored scene setups, one-use-per-campaign via SyncData, eligibility by
  kinks/gender, prompt injection. (Originally imagined as Twine→JSON; Twine is dropped — see the scripted
  scenarios pillar — in favour of plain keyword/JSON data, reusing the same composer/authoring approach.)
  (When adding persisted campaign state, REMOVE `CaptiveEncounterService.ResetOnSessionLaunch` — SyncData(load)
  fires before OnSessionLaunched and would clobber loaded state.)
- **Image overlay** for scenes (`[SCENE]` parser).

### Conception from any vaginal act — consensual scenes + female NPCs *(wished 2026-06-24)*

Today conception is **captive-only** and **player-only**: `impregnation_risk` is taught solely in the
captor/captive block, and `CaptivePregnancyService.TryConceive` hardcodes the mother as `Hero.MainHero`.
Generalize it so **any vaginal act carried to completion can conceive**, in **consensual** intimate scenes too,
for **both** a female player and a female NPC.
- **Female player** (male NPC partner): teach `impregnation_risk` in the consensual intimate block as well —
  the existing `TryConceive(father = NPC)` path already works (mother = MainHero).
- **Female NPC** (male player): NEW path — mother = the NPC, father = MainHero; eligibility checked on the NPC.
  **Decided: the child always belongs to the MOTHER's clan** (legitimate or bastard). The female-player paths
  already deliver correctly (mother = MainHero → player's clan). The wrinkle for the female-NPC path:
  `DeliverOffSpring` special-cases a MainHero *parent*, and the father is MainHero here, so it may pull the
  child into the *player's* clan despite the NPC mother — needs a spike (post-birth reassignment to the
  mother's clan, or confirm whether the special-case keys on the mother only). See the bastards note below.
- Resolve mother/father by sex (the female participant is the mother); same-sex → no conception. Keep the rest
  identical: ~15% roll, fertile age 18–45, not-already-pregnant guard, adult-gated, **vaginal only** (never
  oral/anal), and **silent** (discovered through the vanilla pregnancy/birth flow, never narrated in-scene).
- Work: drop the `MainHero` hardcode in `TryConceive` (take an explicit mother); add an `EncounterContext`
  flag ("a fertile female who could conceive is present in an intimate scene") to gate the prompt teaching
  outside captivity; teach the action in the consensual intimate block; extend `cr.preg` to target an NPC
  mother. Likely rename `CaptivePregnancyService` → `PregnancyService` (no longer captive-only).

**BUILT 2026-06-24 (untested in game), 378 tests green, NOT committed.** Pure `ConceptionResolver`
(Logic, mother/father-by-sex policy, +9 tests); `PregnancyService` (renamed, explicit mother); SDK
`ConceptionRisk` + consensual-block teaching; `EncounterContextBuilder.ResolveConceptionRisk`; bridge
resolves the role; `cr.preg mother <name>`. **ONE piece still open — the clan SPIKE**: verify in game which
clan the NPC-mother child lands in (`cr.preg mother <name>`, advance to birth). If `DeliverOffSpring` routes
it to the player's clan (MainHero father), add a guarded birth-time correction to move the child to the
mother's clan. Until verified, do not assume it lands correctly.

### Bastards — infidelity and rival claimants *(wished 2026-06-24)*

A child conceived outside marriage is not just a stat line — it can be a **grave event**, and CR should treat
it as one. Two consequence threads, both natural fits for systems CR already has:
- **Proof of infidelity** — a bastard born to a married mother (or fathered by a married player) is hard
  evidence of betrayal, and its severity is **culture-dependent** (reuse the monogame/polygame flavour of
  `jealousy_cultures.json`): some cultures shrug, others treat it as deep dishonour. Plugs straight into the
  **jealousy system** — the cuckolded spouse / staked NPC earns a grudge + grievance memory.
- **A rival claimant** — a bastard who survives to adulthood may press a **claim** (to a fief, a title, even a
  throne) and become a **natural enemy of the clan**. This is dynastic-succession + scripted-scenarios
  territory: a bastard's coming-of-age is a perfect **trigger** (`ON_COMING_OF_AGE:bastard`) whose
  consequences (a claim pressed, a defection, a war declared) flow as reactive rules.

Ties together pregnancy (the source), jealousy (the scandal), dynastic succession (the claim), and the
scripted-scenarios engine (the trigger → consequences). Design only.

### Lean prompt for small / local models *(wished 2026-06-23)*

Players on small local models (e.g. Gemma 3n E4B via LM Studio) hit the "(My words escape me…)" fallback —
the LLM call fails because CR's system prompt is large and their context window is short (the prompt
overflows, or the model returns nothing usable). An actionable on-failure notice now points them at the
cause (model / context / endpoint). The deeper fix: an optional **LEAN PROMPT mode** that trims the system
prompt to fit a small context — keep identity, persona, the core format rules, and the current encounter;
drop or shorten the heavy optional sections (full relationship lists, cultural traditions, the long
behavior guidelines, instruction verbosity). Gate it on an MCM toggle ("Compact prompt for small models")
or auto-detect from a configured context budget. The prompt-cache split already separates stable vs dynamic
— the lean mode would shrink the stable body. Pure/testable: the section-selection policy (what stays at a
given token budget).

### Stance — mechanical behaviour-gating *(stance ships narrative-only; the mechanical layer wished)*

Stance (Trust/Respect/Fear, plus Affection mirrored from relation) is fully wired into **perception**: deeds
move it, words nudge it (capped + cooled), fear decays, and `StanceDescriptor` feeds the prompt so the LLM
**role-plays** the posture. What's missing is mechanical **teeth** — stance thresholds changing **verified
outcomes**, not just tone:
- a **fearful** captive lord more likely to accept a freedom bargain / yield softer ransom terms;
- a high-**Respect** lord more open to `join_clan` or a war-council nudge;
- a low-**Trust** NPC refusing a deed that a relation score alone would have allowed.
Build on the existing deed bridges (prisoner bargain, `join_clan`, barter); the gate is a pure threshold policy
in `CalradiaRemembers.Logic` (testable), consulted by the bridge before it commits. "Words nudge, deeds prove"
— and now the posture **bites**.

### Jealousy — the competition loop and the duel *(v1 shipped; escalation deferred)*

v1 fires a one-shot reaction (staked NPC → grudge + grievance memory + JealousThreat letter, witness-gated,
culture-flavoured). Two deferred layers:
- **Competition loop**: rival suitors who actively and repeatedly contend for the same NPC over time (not a
  single reaction) — courtship as an ongoing contest the player can win or lose.
- **Duel & scheming**: a slighted rival may challenge the player or scheme against them — a real
  confrontation, not just a letter. Pairs with the scripted-scenarios engine (a jealousy threshold as a
  trigger) and the love-triangle tie-in noted under Camp life.

### Marriage — true polygamy and divorce *(monogamous marriage shipped)*

The marriage chain (family-approval, consorts, by-letter, jealousy) is shipped. Two genuinely-open extensions:
- **Culture-gated true polygamy** as lore — multiple full spouses, restricted to designated cultures (not the
  current consort workaround), with the family-approval currency scaled accordingly.
- **Divorce** — study `DivorceOfCalradia` first; the dissolution path and its standing / grudge / heir
  consequences, which dovetail with dynastic succession and jealousy.

### Smaller polish (deferred)
- **Negotiation Phase 1 polish**: a richer `DeliverItems` spec (category + tier + count — "a warhorse, a
  sword of tier 5+") instead of a value floor; the joining wanderer EQUIPS the gear demanded for them.
- **Companion happiness**: pay / share-of-loot driver; let the player ADDRESS a grievance more richly.
- **Camp life**: a child of a companion couple joining the clan as a chronicle/letter beat; the love-triangle
  tie-in with jealousy.
- **Influence loop**: family-across-clans kin detection (today it is clan-mate only); a richer per-event
  dynastic-succession LLM rewrite (the current succession is deterministic).
- **Dynastic succession**: the optional per-NPC LLM reframing for richer dynastic prose.
- **Log file rotation** *(wished 2026-06-24)*: `CalradiaRemembers.log` accumulates across sessions and can
  grow very large. The fix: truncate (or rename to `.bak`) the file **on session launch** so each play
  session starts fresh. **The Story Log (`CalradiaRemembers_story.log`) must NOT be touched** — it is an
  ongoing novel that grows deliberately across all sessions and belongs to the player.

---

## v2.0 — Player influence on the world (CK3-style)

Persuading a faction leader toward war, luring an enemy lord to defect, assassination, espionage. The most
powerful layer — and the most dangerous for save balance — so it ships last, on a proven foundation. Two
hard rules:

1. **No direct world action from a conversation.** Same philosophy as quests (verified deeds, never words):
   a successful conversation plants a *disposition*, not a war. The lord "brings it to his council"; days
   later the event lands (or doesn't), weighted by personality, relations, and kingdom state. An eloquent
   player must never trigger a world war in three messages.
2. **Build on native mechanics.** `ChangeKingdomAction` (defection), `DeclareWarAction`, `KillCharacterAction`
   all exist engine-side. The mod adds the decision layer and the narrative, not the mechanics.

NPC-initiated faction wars and assassination-as-a-deed both wait for this layer (where the player could
otherwise extract outsized advantage at no cost).

---

## Governing principles (carried across every layer)

- **The hero is the PLAYER.** Spend tokens on what surrounds the player, not on NPC↔NPC scenes the player
  isn't in. Background simulation is MECHANICAL (native relations, cheap ticks); the LLM narrates only at the
  player's point of contact.
- **Verified deeds, never words.** The game computes and verifies outcomes (value floors, real transfers,
  real combat, real events); the LLM negotiates the story. "Quests that don't break."
- **Open & editable.** Prompt/data files in `ModuleData` are the moddable surface; the code is the engine.
