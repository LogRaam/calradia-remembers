# Calradia Remembers — Roadmap

## Mission

**Calradia Remembers étend Mount & Blade II Bannerlord là où la simulation s'arrête : dans la mémoire et la parole des personnages.**

Le mod repose sur trois engagements :

**1. Respecter l'esprit de la communauté.**
Le modding vit du partage et du respect — des créateurs envers les joueurs, des joueurs envers les créateurs, et de tous envers le jeu original. CR s'inscrit dans cette tradition : code ouvert, architecture extensible, documentation honnête sur ce qui est testé et ce qui ne l'est pas. Pas de magie noire, pas de promesses creuses.

**2. Enrichir Bannerlord sans le trahir.**
L'objectif n'est pas de créer un monde parallèle de roleplay déconnecté du jeu. Les NPCs de CR se souviennent, réagissent et parlent — mais ils restent des seigneurs de Calradia, pas des personnages de roman. Les actions qu'ils promettent arrivent dans le journal. Les relations qu'ils forgent s'inscrivent dans la carte politique. CR amplifie ce qui est déjà là ; il ne le remplace pas.

**3. Un code à la hauteur de l'ambition.**
Un mod qui dure s'appuie sur une architecture qui résiste. Logique pure isolée du moteur, seams injectés, tests qui documentent les comportements attendus — pas les détails d'implémentation. Quand Gabriel lit les tests, il lit la spec. Quand un comportement change sans le vouloir, un test le dit avant que le joueur ne le découvre.

---

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

Live on Nexus, or built + committed awaiting upload. **(untested)** = built green and committed but its
in-game wiring is not yet validated. The bulk of the items below shipped together in **v1.14.0** (packaged +
committed + pushed 2026-06-25; awaiting Nexus upload) — still untested in game unless validated since.

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

### Extension Surface — CR as the narrative-memory bus of a modlist *(wished 2026-06-25 — a new pillar)*

**Why this is the point, strategically** *(Gabriel, 2026-06-25):* this SDK is the purest expression of CR's
community pillar — and **the best possible answer to closed mods like AI Influence**. Where a black box says
"use my features or don't," CR says "build your own, and CR will give it a memory and a voice." Openness isn't
just an ethic here; it's the competitive moat. The mod that lets *everyone else* build on it wins the ecosystem
the obfuscated one can never reach.

**The strategic prize:** any mod that changes the world can *tell CR about it*, and CR turns that into NPC
awareness — chronicle, rumour, hearsay, in-conversation reference — with **no new plumbing downstream**.
CR stops needing to know about each mod in advance: other mods speak to it. This generalises the
"world-agnostic" goal of the scenario engine from *content packs* to *live interop*.

**The collaboration shape (ChatAI thread, 2026-06-25):** this is NOT about co-running CR with another LLM-chat
mod — two chat overlays on the same NPCs would conflict. The fit is a **complementary, non-chat companion mod**
(e.g. a "living realm" world-sim that makes lords quarrel/scheme/wager) that **feeds its outcomes into CR via
the SDK** and lets CR be the memory + voice. Their simulation, CR's remembering. That's how a collaborator
plugs INTO CR rather than beside it, without either mod dismantling itself.

The governing principle — and the thing that keeps it from becoming a coupling disaster — is that this is **not
"open the RAM"**. It is a narrow, one-way, **validated inbound contract**: outsiders *push facts in* through a
single door; they never read or mutate CR's internals. The day CR refactors `NpcProfile` or the store, third
parties don't break.

**Four disciplines (load-bearing — they protect the decoupled-architecture pillar):**
1. **Inbound, one-way.** Third parties *write* through the gateway; they don't read/mutate internals. (A public
   read API over memory is possible later, but writing is ~90% of the value.)
2. **A stable, versioned DTO at the boundary** = an anti-corruption layer. The public contract references
   neither CR's internal types nor raw Bannerlord types, so internals can change freely behind it. (This is the
   SDK's existing reason for being — here we add one stable façade on top.)
3. **Validation at the door.** Every external fact is normalised before it enters: magnitude clamped, **summary
   length bounded (the save-blob guard)**, hero/faction ids resolved-or-dropped, source tagged, lifetime
   defaulted. A careless modder can neither brick a save nor inject noise into prompts. **Plus dedup by event
   Id + a per-`SourceMod` rate/throttle** so a mod that calls `Record` in a loop (or re-emits the same event
   every tick) can't flood the store toward the save-blob limit — modelled on the ChatAI dev's
   `processed_world_event_ids` + per-category `last_generation_day` state (the one idea from his shared code
   that's a genuine upgrade; his `VerifiedFacts` concept was *considered and dropped* — CR's `Summary` already
   IS the authoritative fact injected as hearsay, so there's no recap-generation drift surface to pin).
4. **Source tag + relevance gating.** Each entry carries its `SourceMod` (debug / filter / mute a misbehaving
   source); CR's existing hearsay-locality logic decides *who* plausibly heard it, so NPCs don't recite
   everything.

**Volet A — external world-event ingestion (do this first; highest value, simplest).** A façade like
`CalradiaRemembers.Feed.Record(ExternalWorldEvent)` lets a mod push a fact into the **same** World-Event /
rumour store the Chronicle, influence loop and NPC-awareness already read. Once past the gateway, it follows
the exact path of CR's native events — a Pravend NPC who'd plausibly have heard mentions it on his own. The DTO
carries `Category`, `Summary`, `Day`, `Magnitude`, optional `HeroIds`/`FactionIds`, `Lifetime`
(`Rumor` ages out vs `Deed` kept), `SourceMod`. *Worked example:* a horse-racing mod logs winners and purses
as `Lifetime.Rumor`; CR's lords gossip about the grey mare of Lord Caladog with zero knowledge of CR's memory
system. Pure/testable in `CalradiaRemembers.Logic`: the validator/normaliser (clamp, bound, id-resolve,
default) and the DTO→`WorldEvent` mapping; only the public static façade + behavior registration is engine glue.

**Volet B — external mission registration (later).** A mod defines **a mission for the player** and registers
it into CR's verified-quest system, inheriting all of CR's guarantees: the **journal mirror**, NPC-facing
framing, reward gating, deadline/abandon lifecycle, and the **self-heal-on-load** that force-closes anything
dangling (no ghost quests from a third-party mod either). The hard part — and the line that preserves "the
bridge is law / no empty promises" — is **verification ownership**: CR must NOT pay a reward on a foreign mod's
unverified say-so. So the contract splits responsibility: **the external mod owns the completion signal** (it
fires `mission.Complete()` / supplies a predicate CR polls, because only it knows when its own deed is truly
done), while **CR owns everything else** — issuing, the journal entry, the in-conversation framing, the gated
reward, and lifecycle hygiene. That lets a mod define an arbitrary mission (win a tournament series, smuggle a
crate, survive a siege) without CR knowing its internals, and without weakening CR's "rewards only for real
deeds" promise. Pure/testable: the registration record + lifecycle state machine; engine glue is the journal
mirror + the completion-callback wiring.

> NOT built (wished 2026-06-25). Sequence: A before B. Both ride the SDK's existing engine-agnostic design.
> Linked to the scenario engine below (both are modder-extensible surfaces) and to the ChatAI-collaboration
> thread (an autonomous NPC↔NPC layer would write its outcomes back through Volet A so the "theater" leaves
> real marks the player later encounters — see also the four NPC↔NPC seams discussed but not yet scoped here).

**Long horizon — CR as core + swappable feature modules (dogfooding the surface)** *(wished 2026-06-25)*

The endgame: split CR into a **core** that owns the shared substrate (`NpcProfile`, the store, the bridge, the
prompt builder, the World-Event store, the SDK chat pipeline) plus a set of **official feature modules**
(`CR_Schemes`, `CR_Wedding`, …) that ride the *same public surface* a third party would use. The payoff is
dogfooding: if CR's own marriage is "just another consumer" of the API, then a third party's marriage mod is a
first-class citizen **by construction** — the strongest possible proof the extension surface is real, and a
direct expression of the community pillar.

**The hard truth that gates it — coupling is the identity, not an accident.** CR's features are NOT independent:
Wedding, Schemes, Jealousy, Pregnancy, Succession read/write the *same* `NpcProfile`, feed the *same* prompt,
share the *same* stores. That interplay (jealousy←pregnancy→succession→scheme) **is the product**. So a/b must
be distinguished, and the interplay protected at all costs (over-splitting into mods that no longer talk = the
worst failure):
- **(a) Internal modularity** — clean boundaries *within one shipped mod* (a `CR.Core` owning the substrate;
  features depend on it). Cheap, good regardless, and the **prerequisite** for everything below. CR already has
  the base (`CalradiaRemembers.Logic` + the SDK + per-feature folders).
- **(b) Deployment split** — separately-installable, swappable mods. The big move; do it **surgically**, only
  where a feature is genuinely substitutable and its support contract is bounded.

**Volet A + B are necessary but NOT sufficient for (b).** A detachable feature module needs a richer **Feature
Module SDK** beyond the event/mission feed:
1. **Contribute prompt sections** — registered, **ordered**, **token-budgeted** (the prompt has 16 ordered
   sections; N mods writing into it needs a priority registry or it's chaos).
2. **Contribute action vocabulary** + bridge handlers, through the conditional-teaching gate ("the bridge is law").
3. **Read/extend shared profile state through a contract** (not raw field access — that's the coupling we're
   trying to break).

**Sequence:** (a) internal modularity → Volet A + B → **pilot the split on the LEAST-entangled feature, not the
most.** → generalise only after the pilot holds.

**`CR_Schemes` is the pilot — and possibly the only feature that ever needs externalising.** It already has its
own store, its own daily tick, and already feeds rumours — the least entangled with the profile/prompt. Wedding
is the *worst* starting candidate (relations + family + consorts + prompt). Extracting Schemes as a separate mod
on the public surface becomes the **validation gate**: if it holds, the surface is proven; if it doesn't, we
learned cheaply. The working hypothesis (Gabriel, 2026-06-25): **most features should stay in core**, and the
deployment split is reserved for the few that genuinely earn it — Schemes may be the one.

**"Bring your own marriage" has two routes — prefer the light one.** (1) *Data route* — reshape marriage via
**scenario packs + config**, no code: CR is *already half-way* (the Rhagaea marriage is four rules in a data
pack), and the scripter/composer keeps growing its trigger+consequence vocabulary. For most modders, "your own
marriage" = authoring rules, not shipping an assembly. (2) *Assembly route* — replace the whole `CR_Wedding`
module; powerful but needs the full Feature Module SDK above. The data route tends to give ~80% of the value at
~20% of the cost; reserve the assembly route for cases that truly justify it.

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

> **BUILT 2026-06-24 (Opus, 412 tests green; committed `6b47a4d` + pushed 2026-06-25 in v1.14.0) — pure core + a working engine-glue slice.**
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

**BUILT 2026-06-24 (untested in game), 378 tests green; committed `6b47a4d` + pushed 2026-06-25 in v1.14.0.** Pure `ConceptionResolver`
(Logic, mother/father-by-sex policy, +9 tests); `PregnancyService` (renamed, explicit mother); SDK
`ConceptionRisk` + consensual-block teaching; `EncounterContextBuilder.ResolveConceptionRisk`; bridge
resolves the role; `cr.preg mother <name>`.

**CLAN SPIKE RESOLVED 2026-06-25 (decompiled `HeroCreator.DeliverOffSpring` + Dramalord, via ilspycmd).** Exact
vanilla rule: `mother == MainHero || father == MainHero → child to the PLAYER's clan; else (both NPC) → child to
the FATHER's clan` (culture random mother/father). So NPC-mother + player-father DOES land in the player's clan
(confirms the spike); two married NPCs go to the father's clan (vanilla patrilineality — leave it). **`Hero.IsPregnant`
has a PUBLIC setter** (Dramalord sets it from another assembly, no reflection) — CR never set it, which is why
the encyclopedia pregnancy ICON is absent and may be why delivery is gated. **Plan (Path A):** keep injecting
into the engine's `_heroPregnancies` (so vanilla `DeliverOffSpring` delivers with maternal mortality — player
exempt, NPC mothers 1.5% — and the arbitrary father, all FREE); add the one-liner `mother.IsPregnant = true`
(icon + delivery); and at `CampaignEvents.OnGivenBirth` set `child.Clan = mother.Clan` **for CR-tracked
pregnancies only** (vanilla NPC births keep their patrilineality). Unified rule = **child → mother's clan,
always** — self-handles the player-mother and legit-wife-mother cases (already in the player's clan). The
two-phase `cr.preg_birth` console test is the verdict on whether delivery happens at all; only if it never
delivers even with the flag do we fall back to Path B (a Dramalord-style own registry + own delivery).

### Bastards — a hidden birth as a grave consequence layer *(wished 2026-06-24, refined 2026-06-25)*

A child conceived outside marriage is not a stat line — it is a **grave event**, and the design discipline is
that almost nothing here is new code: it **assembles levers CR already owns**. Design only — nothing built.

**The unifying frame: a bastard is a SECRET with a truth-value.** The drama is the gap between what is
CONCEALED and what is KNOWN. Two axes drive everything: **concealed ↔ discovered**, and **real ↔ fabricated**.

**Four threads, each plugged into an existing system:**
1. **Hidden birth + discovery risk → scandal.** A bastard is a CONCEALED birth (`IsConcealed`). A discovery
   roll/trigger fires; on exposure → a **scandal**: a World-Event/rumour + a **Renown/Influence hit** (reuse the
   existing `ApplyPlayerPublicHumiliation` levers) + NPC gossip via the influence loop. Witnesses to the affair
   RAISE the discovery odds (reuse the witness system + jealousy's witness-gating).
2. **The mother (or any knower) blackmails for silence** — leverage → a demand (gold/favour) → pay OR be
   exposed. **Mirrors the Phase-3 `IntimacyLeverageHeld` pattern** → a `BastardSecretLeverage`, run through the
   conversation/negotiation framework. Refusing raises the discovery risk / they may leak it.
3. **The grown bastard's claim becomes public** — `ON_COMING_OF_AGE:bastard` emits a World-Event/rumour through
   the already-shipped channel, so the realm hears of the claim. The claim itself is **prestige-gated**: the
   bastard presses a claim on the FATHER's clan only when the MATERNAL house is **≤** the father's; a child of a
   grander house (e.g. the player sires one on Empress Rhagaea) follows the mother's dynasty and covets nothing
   of the player's lesser clan. Makes the trigger characterful, not mechanical.
4. **An NPC fabricates a FALSE bastard to smear the player** — a flavoured **`SchemeKind.Slander`** (SchemeStore
   already has it); warn/counter like the existing schemes.

**The elegant convergence:** the real-discovered bastard AND the fabricated one flow through the **same**
scandal/rumour channel; the **truth-value decides the player's DEFENCE** — a real one can only be SUPPRESSED /
bought-off (hence the blackmail), a fabricated one can be REFUTED (and if disproven the scheme backfires on the
plotter, like `scheme_warn`). Dramatic irony when a false accusation lands on a player who really *does* have a
hidden bastard.

**The secret-keeper set** *(decided 2026-06-25)*: knowers of a real bastard = the mother + the affair
**witnesses** (jealousy already tracks staked NPCs) + the grown bastard. ANY knower may blackmail or leak it,
and **discovery risk scales with the number of knowers**. Persisted as a knower-set per bastard.

Ties five systems together: **pregnancy** (the source) → **jealousy** (the scandal) → **witnesses** (who knows)
→ **dynastic succession** (the claim) → **scripted scenarios** (the trigger → consequences engine), surfaced via
the **world-event/rumour** channel. The clan a bastard is born into is settled by the pregnancy plan above
(child → mother's clan, always).

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
