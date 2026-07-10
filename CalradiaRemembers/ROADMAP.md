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

### Viable-quest referential, a spoken offer ALWAYS registers *(ratified 2026-07-08, 🔨 BUILT 2026-07-08, untested in game)*
Root cause of "the NPC offered a mission but no quest was recorded" (recurring player reports): the LLM was
taught 17 quest types in the ABSTRACT, blind to the world, so it invented targets `QuestFactory` refused, and
every refusal was SILENT (the spoken promise stood while the game registered nothing). Ratified answer =
PREVENTION over repair: before the conversation, `ViableQuestScanner` (mod) scans the live world for the tasks
THIS giver can truly hand out (a real still-infested lair near his ground via the factory's OWN primitives, a
real enemy lord/village/army when his realm wars, a real captive ally, a kinsman for a letter, so there is
ALWAYS something to do), and the prompt teaches ONLY that grounded menu, targets named verbatim
(`EncounterContext.ViableQuestMenu`; generic catalogue = SDK fallback). Registration then succeeds by
construction. Pure formatting core `QuestMenuComposer` (cap 8 lines, 5 tests); cached per giver per day.
Plus the pairing contract (a task EXISTS only with the [QUEST] block in the same reply) and NOTHING-IS-SILENT:
a malformed/truncated [QUEST] block is flagged (`ParsedResponse.QuestBlockMalformed`) and told to the player in
chat, as are the one-task-at-a-time and ungroundable refusals (`notify` in `IssueQuest`). `quest_menu` self-test.
**INCREMENT 2, PERSONAL SUPPLY QUESTS: 🔨 BUILT 2026-07-08 (untested in game).** New `QuestType.ProvideGoods`
(count-based, lords only for inc1, ratified A=count/B=lords 2026-07-08): a lord asks the player to bring a COUNT
of a category (horses / livestock / grain), handed over for real via the bag. Pure `ProvideGoodsPolicy` (category
normalization + count clamp, 4 tests); `SupplyItemMatcher` maps a category to runtime items (ItemType.Horse; the
livestock/grain `ItemCategory` StringIds); `ItemPickerService.ShowSupplyPicker` (category-filtered); bridge
`SettleSupplyQuest` tallies the COUNT and COMPLETES on delivery (takes exactly the required count, pays gold+regard,
closes, like the ProvideGold-on-payment fix); `QuestFactory` grounds it (unknown category refused); taught in
`AppendQuestInstructions` (category + required_count fields) and always on the viable-quest menu for a lord.
`supply_quest` self-test + SDK parser test. STILL OPEN (increment 2 remainder): **NOTABLES asking for supply
(a headman/merchant, bigger surface)**, and **MISSION/espionage types (scout a settlement/lord beyond scout_army;
each needs its own QuestDeedTracker verification).** Next per the plan: the editable appearance Encyclopedia field,
then the JPG/PNG portrait spike.

### Prisoner trade, both ways for gold *(ratified 2026-07-08, 🔨 BUILT 2026-07-08, untested in game)*
Player report ("we must be able to exchange prisoners, add them to the item+denars window") ratified by Gabriel
with BOTH directions and gold: it opens the bounty-hunting loop (a lord wants a man and names a price; a player
who wants a prisoner can simply buy him). Two `IConversationVerb`s: **buy_prisoner** (the NPC pays the player
for a hero captive the player's party holds; the sold captive plants the DeliveredPrisoner grudge, heavier when
sold to his enemy) and **sell_prisoner** (the player pays for a captive the NPC's clan holds, custody via
`CourtActionResolver`, purse checked). THE BRIDGE IS LAW on price: the LLM negotiates in-fiction but the figure
is clamped by the pure `PrisonerTradePolicy` (6 tests) around the game's OWN ransom valuation (floor 40%, cap
3x, absolute min 50): a silver tongue haggles within reason, never into absurdity. Teachings ride
`ExtraActionTeachings` (each verb gates itself on live holdings, captives + fair-price anchors named); a
**Sell Prisoner** chat button (Give Gold pattern: picker pre-fills the input); the viable-quest menu gains the
wartime `deliver_prisoner` bounty entry ("bring me any of their lords in chains and be paid"). `prisoner_trade`
self-test (real capture, real bridge purchase, clamp proven with an absurd ask, cleanup frees the lord).

**World & influence** — world-event/rumor store (battles, sieges, marriages, executions, wars) with
geo/faction/recency-weighted hearsay; the **Chronicle**; commoners + settlement intel; **NPC↔NPC schemes**;
**NPC-initiated interceptions** (a lord rides out to challenge you); **power-balance** context block; the
**influence loop** (NPCs speak of *your* deeds, attributed + valence-coloured: kin/faction Wound vs Boon)
and the **"talk of the realm"** line.

**Chronicle enrichment** *(built 2026-07-04, untested in game)* — the player Chronicle grown from a war
register into a dynastic biography: 14 new entry types (childbirth, dynastic succession, consort, joining/
leaving/founding a kingdom, a fief granted, tournament victory, capturing a lord, being captured/freed, a
large field battle without armies, a grudge made peace, a pledge fulfilled at the player's word, a famous
captive freed/delivered) each writing `WorldEvent.ChronicleLine` from its own hook, guarded against
double-counting (once-per-lord capture, custody-handoff dedup, hidden bastard births excluded). Entries now
group under yearly chapter headings in both the text render (`cr.chronicle`) and the illuminated viewer;
new empty-state invitation line. Pure additions: `CalradiaRemembers.Logic.ChronicleCalendar` (year math) and
`BattleNotability.PlayerBattleSizeThreshold` (150 combatants), both unit-tested.
**Notable intimate conquests (2026-07-04, player-requested, BUILT, NOT committed).** Two further entry types, both
gated on `ModSettings.AdultContentLevel >= Mature` AND the partner being a hero of standing (`IsLord`; never a
random bandit-camp captive), via the pure `IntimateChroniclePolicy` (tasteful register at Mature/Explicit, bawdier
at Hardcore, never explicit): **a captive conquest** (a lord/lady of standing taken in an intimate player-as-captor
scene, `ChatViewModel.RecordCaptiveConquestChronicleIfNeeded`, deduped per captive) and **an heir got** (conception
upon a hero of standing when the player is one of the two parents, `PregnancyService.RecordConceptionChronicleIfNeeded`
— the begetting beat, distinct from the childbirth entry `OnGivenBirth` already records so the two never duplicate).

**Stance with teeth** *(increment 1 built 2026-06-27, untested in game)* — the earned multi-axis posture now
ACTS, not just colours dialogue. Pure `StanceConsequencePolicy` maps a posture+traits to one warranted
consequence (ColdShoulder → Threat → Plot → Intercept → Assassinate / WarmWelcome → Favor → Warning), with
honour staying a lord's hand from murder/scheming and terror ruling out striking in person. **1a (conversation):**
`EncounterContext.StanceConsequenceHint` colours how the NPC ACTS this meeting (a cold-shoulder won't deal, a
secret killer stays falsely civil, a friend offers aid/would warn you). **1b (proactive):** `StanceConsequenceBehavior`
weekly turns OPEN enmity/regard into an unbidden letter (Threat/Intercept → cold/challenge; Favor/Warning → warm
overture); secret hatred (Plot/Assassinate) writes nothing. Self-tests `stance_letter` + chat `chat_stance_assassin`/
`chat_stance_favor`. OPEN: **increment 2** = wire Plot (scheme vs player) + Intercept (reuse InterceptionBehavior) +
Warning (warn of a real scheme). **increment 3 — the town ambush — WORKS in-game, still OFF behind the experimental
MCM toggle `TownAmbushEnabled` until polished:** Assassinate arms `NpcProfile.StanceAmbushArmedDay`; on entering a
town `TownAmbushBehavior` rolls `AmbushPolicy` → narrated inquiry (Draw / Slip away) → a fight at the gate. Console
`cr.ambush`. **2026-06-30 refinements (BUILT, NOT committed):** an elite chief of the paymaster's culture at the
player's clan tier leads bandits in the Blackened "stealth" set; force scales (N+1)² with the defending side (solo 4
/ two 9 / three 16 / four 25, no cap); companions are captured alongside the player; capture is armed even when the
player falls first; recurring captive scenes are dungeon/fortress-framed when held in a town/castle.
**The captivity arc (Gabriel 2026-06-27), the lose branch:** 3.2a lose → captured + convoyed to the commissioning
lord ✅; 3.2b arrival → the lord's reckoning audience ✅; 3.2c convoy **rescue ambush** by your companions (BUILT
2026-06-28); 3.2d **Hardcore companion-victim captivity scenes** — the captor torments/assaults a held companion
before the player, or coerces the player to do it (revenge driver). **Phase 2 BUILT 2026-06-30; Phase 3 ✅ SHIPPED
v1.26.0 (2026-07-07): auto-fire in CaptiveEncounterService (CompanionScenePolicy gate, 45%, variant pick) + the full
aftermath on scene close (pure `CompanionAbuseConsequencePolicy`): grief stance-crater on the companion's bond
(Trust/Affection, deeper when the player was coerced), a REVENGE grudge vs a NAMED captor (new append-only
`GrudgeSource.CaptorCruelty`, resolution=Deed, coexists with the vs-player CaptiveAbuse grudge), and the atrocity
chronicled as a world-event. `companion_revenge` self-test.** Reuses CR's captive-scene chat.
**In-game-test feedback round (Gabriel 2026-06-30, BUILT, NOT committed):** (a) the chat "scroll down to read more"
hint no longer flashes up the instant a scene's opening narration runs a line long — it appears only when a
substantial block of text sits below the fold (`MoreHintRevealMargin`). (b/c) **the captive companion is now a REAL
participant, not a name in the prompt:** threaded as a real Hero (a witness flagged `IsCaptiveVictim`), so they get
their own portrait, their own VOICE (cries/pleas/defiance via the existing `[WITNESS_REACTION]` channel — now also
collected on the Continue path, which previously left them mute mid-scene), a dedicated colour, and the CORRECT
gender in the prompt (`CaptiveVictimIsFemale` → pronoun rules; fixes the model borrowing the player's sex). The
victim is excluded from the captor's-men / humiliation tallies (`CountAggressorWitnesses`). (d) **Continue-sequence
quality:** the in-scene Continue stimulus + the "Please continue" resume text now push ADVANCE + fresh vivid
description + NO verbatim reuse (the repetition/flat faults Gabriel saw). The continuation stimulus is extracted to
`CaptiveSceneText.BuildContinuationStimulus` so the new self-tests exercise the exact runtime prompt. New LLM
self-tests `chat_captive_continue` (opener + 4 Continue beats → asserts every beat replied + NO 6-word verbatim
phrase reused across beats, logs description coverage for human judgement) and `chat_companion_continue` (same, plus
asserts the companion is actually VOICED in its own `[WITNESS_REACTION]` lines + a gender-correctness judge note).
**OPEN:** richer per-victim captivity memory at scene close (today the victim gets none — a Phase-2 revenge hook);
the companion-scene auto-fire still console-only.

**Captive scene replayability (Claude 2026-07-01, BUILT, NOT committed):** the same captor used to always run the
same scene, because intent selection was a deterministic priority ladder and the per-round scene "focus" pool was
one small, single-register list. Two fixes: (1) `CaptiveEncounterPolicy.SelectIntent` is now a WEIGHTED draw over
the six lord/sexual intents (Interrogation/PersonalDesire/Domination/Torture/Training/Reward), pure and seeded by
an injected `Func<int,int> rollPercent` (mirrors `SelectBanditIntent`): kinks and relation are leanings that boost
weight, not hard gates, so a sadist LEANS toward Torture but is not locked into it. Interrogation/PersonalDesire/
Domination/Torture always keep a floor weight above zero (no intent is ever impossible); Reward needs at least a
lukewarm relation and Training needs the Training kink to enter the pool at all; strong contempt (relation <= -15)
boosts Torture hard and zeroes Reward; strong warmth (relation >= 20) boosts Reward/PersonalDesire and cools
Torture (never below the floor). (2) The scene-focus text pool (`CaptiveSceneText`) is split in two: the DARK pool
grew from 14 to 26 varied entries (psychological manipulation, weaponized tenderness, corruption, forced
complicity, staged humiliation, hope-and-denial, sensory play, ritual, quiet dread, alongside the original
physical/dominance ones) serving every intent except Reward; a new 7-entry POSITIVE pool serves Reward only (real
pleasure, praise, favored-possession framing, earned privileges, gentling). Selection goes through a new pure
`CalradiaRemembers.Logic.SceneFocusSelector.PickIndex(poolSize, lastIndex, roll)`, which never repeats the
immediately-previous focus (state lives in `ChatViewModel._lastSceneFocusIndex`, reset to -1 on a fresh scene).
**Bugfix, same batch:** "Please continue" on a non-sexual scene (Reckoning/Extortion/Intimidation/Revenge) was
unconditionally injecting the sexual scene-focus setup on resume; the opener path already guarded this, the resume
path now mirrors the same guard. 12 new tests in `CalradiaRemembers.Tests` (weighted-draw invariants +
`SceneFocusSelectorTests`); the `chat_captive_continue`/`chat_companion_continue` self-tests still exercise the real
build calls and stay green. **OPEN:** in-game validation of the variety (same captor across several sessions);
tune the weighting constants against player feedback.

**Captive scene director variety (Claude 2026-07-01, BUILT, NOT committed):** the content-variety fix above
still walked the SAME fixed dramatic structure every time (Intro, Initiate, Intensify, Climax); the director now
varies the STRUCTURE too. `CaptiveSceneDirector`'s lead opening act is now chosen from a named `SceneArc`
(SlowBurn: Intro/RisingTension/Initiate/Intensify/Climax; Classic: today's baseline; Sudden: Intro/Initiate/Climax,
no build-up), drawn by a new pure `CaptiveSceneArcPolicy.Select` (weighted, same style as `SelectIntent`): a
bandit leans Sudden, a lord after Domination or Training leans SlowBurn, PersonalDesire is balanced between
SlowBurn and Classic, and Interrogation/Reward/Torture lean Classic; every arc keeps a floor weight so it can
still be drawn regardless of context. Two new SDK stages close a gap testing surfaced: `RisingTension` (the
captor circles, inspects, closes distance, builds dread through word/gaze/touch, institutionalizing the
"play with the prey" psychological build-up that was being skipped) and `Aftermath` (a single come-down beat
after the final Climax: the prisoner's state, parting words, any mark left, the room going cold again, before
Conclude). The tail is now uniform across every arc: ...Climax -> Aftermath -> Conclude, `Aftermath` a normal
beat the player advances through with Continue (`IsConcluding` still keys strictly off `Conclude`). Also added:
a per-stage textual intensity curve 1..5 (`PromptBuilder.StageIntensity`: Intro=1, RisingTension=2, Initiate=3,
Intensify=4, Climax=5, Aftermath=2, Conclude=1) injected as an "INTENSITY THIS BEAT" line in the stage directive,
so the model's graphic explicitness follows the beat instead of staying flat. Register (bandit crude-thug voice
vs. lord cold-command voice) stays a separate prompt concern keyed off `captorIsBandit`, unaffected by arc choice.
15 new tests (`CaptiveSceneArcPolicyTests` + `CaptiveSceneDirectorTests`) plus 3 new SDK tests pinning the
intensity curve. **OPEN:** in-game validation that RisingTension and Aftermath read well against a live model,
and that the arc distribution feels right per captor type.

**Fix: a dead quest-giver's quests now close (Claude 2026-07-01, BUILT, NOT committed, player-reported):** a quest
whose giver died mid-campaign lingered in the journal (including the awaiting-reward case: the deed was done and
the log said "return to X to collect", but X had died). `QuestJournalBridge.ReconcileOnSessionLaunch` already
closed dead-giver mirrors, but only at load, so nothing fired mid-session. New `QuestService.CancelQuestsOfDeadGiver`
closes every still-Active quest (Active covers both an outstanding task and one awaiting a reward) as Cancelled with
a death reason, with NO relation penalty and no broken-word (a giver's death is not the player's fault). Wired twice:
the `HeroKilledEvent` (mod `NpcMemoryCampaignBehavior.OnHeroKilled`) closes them the instant the giver is slain, and
a daily safety-net branch in `RunDailyMaintenance` catches any that slipped through (or a giver already dead in a
loaded save). Cancelled rather than Failed on purpose: the red-failure register would wrongly imply the player broke
their word. **OPEN:** in-game confirmation, and a call on whether Gabriel prefers the failure register after all.

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

**Hardening batch (2026-07-01, built by agents, untested in game unless noted):** a session of test/infra
fixes with no intended player-facing behavior change. The SDK test suite was silently not executing under
`dotnet test` (missing `Microsoft.NET.Test.Sdk` + adapter package), now fixed, SDK runs 170+ tests. Parser
hardening: the bracket-scrub is anchored (was catching stray mid-line brackets), plus full parser coverage
for [ACTION]/[QUEST]/aliases/[WITNESS_REACTION]/[DISCOVERY], and golden-ordinal tests pin `NotableEventType`
and `Kink` enum values against accidental reordering (a save-compat guard, since both persist by ordinal).
`ProfileMutator` is now the single mutation path, with a reputation clamp. `SafeRun.Step` now wraps every
campaign-tick subsystem so one throwing behavior can no longer freeze the whole tick. `JealousyBehavior` and
`MarriageApprovalBehavior` migrated off the native-string `SyncData` (the exact pattern behind the v1.6.2
save-corruption bug) onto the new `JsonCampaignState` helper, with legacy migration for existing saves. SDK
prompt overhaul: the cache-split fix moves per-turn directives to the dynamic tail, one global action
contract, a worked few-shot example, a letter-language fix, captive pacing consolidated, and a Lean-prompt-v2
pass trimming the format machinery for small/local models.
**This session's own slice:** `ModCheats.cs` (3496 lines, ~120 console commands) split into domain partial
classes, command names/strings/bodies untouched (pure reorg, files now under ~600 lines each).
`RomanticProfileBuilder`'s pure core (trait-based sketches, orientation, preference/kink pools, distinct-pick)
was extracted to `CalradiaRemembers.Logic.RomanticProfilePolicy`, behavior-preserving (same `Random` call
order, so existing NPCs' frozen kinks/preferences do not shift), with new Gherkin tests for the trait
correlations, orientation bands, pool distinctness, and determinism. The bandit-captor scene rolls
(gender-weighted spoil intent, collective-scene chance, participant cap) moved next to the lord-path
equivalents in `CaptiveEncounterPolicy`, tested the same way. The Extension Surface now honors
`ExternalWorldEvent.Lifetime`: a fed Deed is kept, under its own 50-entry cap, oldest dropped first (a
tuning choice, flagged for review, kept smaller than the player's own chronicle cap so a flood can't crowd
it out), instead of aging out as a rumour after 42 days. It also ships `Feed.ApiVersion` for reflection-based
capability detection by third-party callers, and the per-source send-quota dictionary is now lock-guarded
against concurrent callers.

**Dual model modes, roleplay + NSFW (2026-07-01, built, untested in game):** born from a player request
(hot-swapping models for NSFW versus story roleplay was blocked by the modal chat window). Two MCM fields:
the existing model becomes "Model (roleplay)" and a new optional "NSFW model" (LLM Provider group, works
with whichever provider is active, blank means the roleplay model serves everything). Structurally-explicit
scenes (captive encounters, player-as-captor scenes, companion-victim scenes) open on the NSFW model
automatically; ordinary conversations open on the roleplay model. A muted "Mode: Roleplay / Mode: NSFW"
button in the chat title bar shows the active mode and flips it (a manual override, e.g. a consensual scene
that escalates); the model is resolved live per request, so the switch applies on the very next exchange.
The mode is deliberately NOT reset on chat close, so the async captive-scene memory summary still resolves
the NSFW model after teardown. Pure selection rules in `CalradiaRemembers.Logic.ModelModePolicy` (Gherkin
tested); `cr.model [toggle]` reports and flips it from the console.

**MCM menu reorganized (2026-07-01, built, untested in game):** the options screen now reads in usage order:
all LLM configuration first (the shared LLM Provider group, then one section per provider: OpenRouter, NanoGPT,
OpenAI-Compatible), then gameplay (Content, Character, Companions), Diagnostics last. Fixed two GroupOrder
collisions (OpenRouter/Character both 3, NanoGPT/Companions both 4) that interleaved gameplay groups into the
LLM sections, and normalized every in-group Order to ascending (key, options, Test button last); the old negative
orders compensated a since-fixed MCM rendering flip and half the fields no longer followed them. Verify the
rendered order in-game once (an outdated MCM would show groups internally reversed; if so, flip the signs back).

**Both model fields consolidated in the shared group (2026-07-01, built, untested in game):** "Model (roleplay)"
joins "NSFW model" in the LLM Provider group, resolving the asymmetry where the NSFW model was provider-agnostic
but the roleplay model lived per provider. The per-provider "NanoGPT Model" and OpenAI-Compatible "Model" fields
are REMOVED; the shared field serves every provider (id pasted in the active provider's format, examples in the
hint). Provider sections now hold only connection concerns: key, endpoint, Reasoning Effort / Parameter
Compatibility, Test button. Resolution guard: for NanoGPT/Custom, a blank field or the untouched OpenRouter
default (x-ai/grok-4.20) resolves blank, so the existing provider-aware "set your model" in-chat guidance fires
instead of a cryptic API error. MIGRATION NOTE for the release changelog: NanoGPT and OpenAI-Compatible users
must re-enter their model id once in the shared field (their old per-provider value is orphaned in the config
json); OpenRouter users are untouched (same persistence key). The Test Connection / Test Endpoint buttons now
one-token-test EVERY configured model: roleplay first, then the NSFW model when set (a misspelled NSFW id must
fail on the Test button, not mid-scene when the mode flips); the verdict popup reports each model's status.

**Reasoning models no longer fail the Continue button (2026-07-01, built, untested in game, player-reported):**
with MiMo/GLM a scene continuation died on "[SCENE] continuation failed: Response contained no message content."
Root cause in the SDK client: a reasoning model can spend the ENTIRE completion budget thinking, returning a
null content with the reasoning text and finish_reason "length"; the parser turned that into a hard failure,
which also bypassed the existing length retry (it only fired on successes). Fix: such a body now parses as an
empty length-truncated success; CompleteAsync retries it once with a DOUBLED token budget (room for prose after
the thinking); if both attempts come back textless the player gets an actionable error naming Reasoning Effort.
Regression-pinned in new `OpenRouterResponseParsingTests` (SDK internals opened to the test suite via
InternalsVisibleTo). Grok was unaffected because it honors the low Reasoning Effort default. Second prong,
from Gabriel's own play observation (typing "*waiting to see what happens next*" avoided the failure): the
Continue stimulus was a six-line wall of meta-instructions that reasoning models treat as a planning puzzle,
which is what triggered the budget-eating deliberation in the first place. `BuildContinuationStimulus` now
opens with a short diegetic line (the scene waiting) and keeps only the two load-bearing constraints (advance
without recap, never repeat); the full anti-repetition rules already live in the system prompt. Third prong
(first in-game retest still showed silence): the retry now covers ANY empty successful reply, not just
length-truncated ones (reasoning models can close on reasoning alone with finish_reason "stop"), and the
continuation path logs its start (model + mode) and fails LOUDLY on an empty beat ("(The moment passes.)" +
error log) instead of silently rendering nothing.

**Per-mode Reasoning Effort (2026-07-01, built, untested in game):** the effort follows the mode like the
model does. The dropdown becomes "Reasoning Effort (roleplay)"; a new "Reasoning Effort (NSFW)" (OpenRouter
group) defaults to "Same as roleplay" (existing configs unchanged) and typically goes LOWER (Low/Off: fewer
moralizing refusals, faster scene beats) while roleplay can afford Medium/High for finer dialogue. Pure
`ModelModePolicy.ResolveReasoningEffort` (+4 tests, 629 total); `cr.model` reports the resolved effort.

**Captive pacing rebalance (2026-07-01, built, from Gabriel's play observation):** the morning pacing
consolidation overshot: with the "COMPLETE sequence" block folded away and AppendDialogueStyle suppressed in
scenes, the loudest surviving voice was the brevity rule ("a few sentences"), and Grok obeyed: terse one-line
beats, action without psychology (in-game report: the scene rushed forward and lost the menace/insult/
psychological register). Rebalanced with ONE authority intact: the brevity rule becomes "ONE BEAT PER TURN,
FULLY REALISED" (several spoken sentences of menace/taunting + one substantial [NARRATION] paragraph; the
enemy is REPETITION, not substance) and the stage directive header adds "a beat is a whole moment of the
story, not a single line". In-game re-validation needed: beats should be rich again WITHOUT the old 1-2-2-2
repetition returning. Cache measurements from the same session: cached: 64/768/7616/7616 across beats, i.e.
the stable prefix caches from beat 3 (the split fix works on Grok); the remaining ~2.9k/turn uncached is
history + dynamic tail (the automatic-prefix lever below). Grok's slowness = reasoning burn (3.4-4.8k
completion tokens for one-line replies): the per-mode Reasoning Effort (NSFW) = Off/Minimal is the intended
remedy.

**Retry observability + cache visibility (2026-07-01, built):** `LlmResponse.WasRetried` flows to
`NpcChatResult`; the [TOKENS]/[SCENE TOKENS] log lines say "RETRY fired" when the bounded second attempt
served the reply (token counts alone cannot reveal it: OpenRouter's completion_tokens includes reasoning
tokens, which is why Grok's beat looked like a retry when it was a single call). [SCENE TOKENS] and
[15C TOKENS] now also print the cached-token count like the main path.

### Prompt-cache: the automatic-prefix regime *(analyzed 2026-07-01, next cost lever, TO RATIFY)*

Two cache regimes exist. Anthropic-style EXPLICIT caching (cache_control breakpoint) is what CR optimizes
today, and the 2026-07-01 split fix serves it. But xAI/Grok and OpenAI use AUTOMATIC longest-common-PREFIX
caching over the whole request (system + history): the cache_control marker is ignored, and any per-turn
change in the system prompt's dynamic tail (scene stage directive, NAMES MENTIONED) cuts the common prefix
there, so the WHOLE conversation history is re-billed full price every turn on Grok, the default model.
Proposed fix (medium, SDK): move per-turn context out of the system tail into the wire-level LAST USER
MESSAGE (SillyTavern author's-note pattern), persisted in the wire history so the prefix stays append-only.
Each turn then bills only the new message at full rate; everything else becomes cache reads (4-10x cheaper).
Trade-off: past turns' context blocks stay in the sent history (~200-300 tokens/turn at cached price), and
the model receives encounter context as user-role content, a creative-core change that needs in-game
validation. MEASURE FIRST: play a Grok captive scene and read the new (cached:N) figures in [SCENE TOKENS];
if cached stays ~0 across beats, this is the biggest remaining cost lever. Also still open: compress the
Full-mode 16-type quest list (~300-400 tokens), the consolidated captive-prompt rebuild (~40% shorter), and
a second breakpoint splitting the universal header from the per-NPC block (Anthropic-style only).

---

## Open — not yet built

### Affect coherence reform *(review 2026-07-04 — ✅ SHIPPED v1.22.0)*

**✅ SHIPPED in v1.22.0.** The full P0/P1/P2 plan below was implemented and released: one canonical RegardBands
vocabulary, writers re-routed by semantics, the grudge tone arbiter (ruling 2), the grudge-hint discovery path
(`grudge_terms`), the Appreciations wiring + tarnish, and the numberless "Bearing" line (ruling 4). Kept here for
the design record.

A full inventory of every affect axis (clan relation / personal regard / stance Trust-Respect-Fear /
grudges / contentment / romantic / discoveries / appreciations) exposed the incoherences; Gabriel ruled:
1. **Both relation ledgers STAY** (vanilla = CLAN standing, ReputationWithPlayer = PERSONAL regard, as the
   chat header "Clan N / Personal N" always intended); the fix is ROUTING writers by semantics (personal
   wounds/mends -> personal; political/public -> clan), not merging.
2. **A strong grudge DOMINATES the prompt tone**: a concealed vendetta mutes stated warmth (cold courtesy
   wins); warmth becomes the nuance, not the co-equal.
3. **Trust/Respect/Reputation stay ETERNAL** (no drift): decay would read as the LLM losing its memory, a
   perceived bug. The time dimension lives in grudges/appreciations, which do decay.
4. **Stance becomes VISIBLE**: a numberless "Bearing" line on the hero page (fear/respect/trust in words),
   diegetically justified (a man's bearing shows).
Work plan: P0 = one canonical band table for personal regard (was 3 disagreeing vocabularies: encyclopedia
"wary" at -15 vs prompt "neutral"), stop double-rendering regard in the prompt (CURRENT STANCE +
AffectionPhrase said it twice), re-route writers (grudge resolution +4, jealousy hits, divorce
relief/gratitude nudges -> personal; scheme/bastard/battle stay clan), fix the stale CLAUDE.md sync note.
P1 = the grudge-hint discovery path (NOTHING sets ResolutionHintDiscovered today, the reconcile loop is
incomplete: asking the NPC what it would take must reveal the remedy), the tone arbiter (ruling 2), wire
Appreciations (store, plant sites, prompt note with the tarnish, "Kindnesses" encyclopedia section).
P2 = the Bearing line (ruling 4), consistent formation notifications (KNOWN grudge formation notifies).

### Appreciations — event-anchored kindnesses, the positive mirror of Grudges *(wished 2026-07-04 — ✅ COMPLETE 2026-07-04)*

Gabriel's idea: we have regard (a blended score) and grudges (specific wounds that tell a story); we should also
have APPRECIATIONS, the specific KINDNESSES the player did that marked an NPC positively, told as a story the
same way a grudge is. Completes the affect model: regard = the number, grudges = what was done against them,
appreciations = what was done for them, all three coexisting.

**Status (04/07/2026): PILLAR COMPLETE — the core shipped in v1.22.0, and both deferred sources now have a live
trigger, so every kindness source is symmetric to a grudge source.** KindnessInScene: a consensual Intimacy
event now plants it on the partner (excluding the exploiter-leverage dynamic, which is leverage, not a kindness).
And TroopsOrInfluenceSpentForThem is CLOSED — the last open Appreciations source now has a
live-plant path: `give_troops`/`spend_influence` (CalradiaRemembers.Verbs), the RECIPROCAL of the existing
`lend_troops`/`give_influence` verbs — the PLAYER reinforces a needy NPC's under-strength party or spends
their own clan's influence to back a needy lord's clan at court, instead of the NPC favouring the player.
Same emission model as every other verb: the LLM plays the NPC, so the action fires when the NPC ACCEPTS the
player's in-conversation offer, never when the player merely proposes it. Eligibility/amount math lives in
`CourtActionPolicy`/`CourtActionResolver` (mirrors give_influence/lend_troops exactly, reversed), gated by a
weakened-party or low-clan-influence "need" check plus a safety floor on the player's own reserve. Still no
backfill marker for this source (unchanged from the header note in `AppreciationBackfillRules` — nothing in
an existing save records a past troop/influence hand-off), so it stays live-plant-only going forward, same as
before this build.

**Design: a LEAN sibling, not a full mirror.** Reuse ~80% of the Grudge scaffolding (event-anchored model,
magnitude, decay, knowledge-gating, prompt line, an encyclopedia "Kindnesses"/"Debts of gratitude" section next
to Grievances, migration backfill from positive markers). But an appreciation has NO rigidity and NO resolution
mechanic (a grudge's distinctive loop was "make amends"; a kindness has no equivalent). Its lifecycle is
simpler: it forms, reinforces regard, and fades slowly. Sources: gifts/gold received from the player, being
freed/ransomed, a quest done for them, troops/influence the player spent on their behalf, a kindness in a scene,
the divorce Gratitude and the tier-2 positive reactions already produced by DivorceConsequencePolicy.

**The payoff is the cross-interaction with grudges.** The "likes you but resents you" texture becomes fully told
from BOTH sides at once (an appreciation over X coexisting with a grudge over Y). And a grave betrayal can
TARNISH standing appreciations (a fresh high-magnitude grudge sours old kindnesses), a rich narrative beat the
grudge-only model can't express. SEQUENCING: build AFTER the Grudge engine wiring is proven, as its own
increment, reusing the wired patterns.

### Grudges — resolvable, event-anchored grievances *(ratified 2026-07-03 — ✅ SHIPPED v1.22.0; the Amends/Gift/Deed reconciliation trio is complete)*

**Why this is a pillar.** The mod has three affect surfaces today and none does what a grudge is: RELATION is one
number that blends everything (a wrong is diluted by fondness), STANCE is a dispositional posture (fear, trust),
and MEMORIES contain the grievances but as passive text with no structured state and no path to closure. A
first-class Grudge is: anchored on a specific EVENT + coexisting with regard + RESOLVABLE. Gabriel's framing:
an NPC can like the player yet hold something against them (reconcilable); one who dislikes them AND holds a
grudge is far harder to win over. So the regard sets the grudge's RIGIDITY (how hard resolution is), and the
grudge colours the prompt separately from warmth ("you look on the player kindly, BUT you nurse a grievance for
X, and it tints every exchange until mended").

**Consolidation, not a bolt-on.** The mod already has grudges everywhere, each ad hoc and unresolvable: the
jealousy grudges, the bastard mother/claimant resentment, the scheme-backlash treachery, the breached pledge
("promised and did nothing"), and now the divorce's set-aside spouse. A pure `Grudge` (cause, magnitude,
resolution condition [amends/gift/deed/time], rigidity = f(regard)) + `GrudgePolicy` unifies them. Build order:
pure model + tests -> migrate ONE existing mechanic as the reference (jealousy or the breached pledge) -> prompt
surfacing -> generalize. The DIVORCE consequence matrix is the first big client: instead of flat relation
deltas, the divorce PLANTS signed grudges (the repudiated spouse's grudge is rigid or soft depending on their
prior regard for the player; a mutual-consent ex barely grudges at all; a family that never blessed the marriage
is RELIEVED, no grudge, possibly gratitude). This subsumes the signed-consequence taxonomy (spouse repudiated vs
mutual; ex-clan valued the alliance vs disapproved; NPC's own clan relieved vs outraged; the unblessed-marriage
family that WANTED the divorce).

**SAVE MIGRATION IS A HARD REQUIREMENT (Gabriel, 2026-07-03).** Players switching from the old system to the
new MUST be supported, silently if need be. The plan, binding for increment 1:
- **Additive persistence only**: the grudge store is a NEW byte[] SyncData key (Schemes/Commitments pattern); no
  existing key, ordinal, or field changes. Old save + new mod = missing key = empty store + backfill. New save +
  old mod = unknown key ignored = grudges lost but NOTHING breaks (rollback-safe both ways).
- **One-shot idempotent backfill** on session launch (stamped with a migration version): scan each profile's
  persisted NotableEvents and SYNTHESIZE grudges from the clear markers that already exist (jealousy grievances,
  bastard resentment, scheme treachery, breached-pledge summaries, divorce set-aside). Conservative: a grudge is
  only created from an unambiguous marker, never an interpretation.
- **Graceful degradation as the net**: a grievance the backfill misses behaves exactly as today (the memory is
  still in the prompt, the applied relation stays applied) — a partial migration failure is the status quo, not
  a regression.
- **No double-counting**: past relation hits are never replayed or refunded; the synthesized grudge adds
  RESOLVABILITY on top of what already happened.
- **Migration tests are a first-class deliverable**: synthetic old-style profiles in, asserted grudges out,
  idempotence (two passes = same result), empty case. The migrator lives in .Logic, pure and unit-tested.
- **Migration reality per source (read-only inventory, 2026-07-03)**: RELIABLE = jealousy
  (`NotableEventType.Jealousy` ord 11, mod-exclusive, but NOT compressor-protected so prunable, we now
  protect it), failed/abandoned quest (structured `InformalQuest.Status Expired/Abandoned` as the primary
  marker; pruned to 10), divorce set-aside spouse (`Betrayal` + "Was set aside by ... at the player's
  urging.", compressor-PROTECTED, the most durable). PARTIAL = bastard claim (Scandal WorldEvent,
  InvolvesPlayer=true, "presses a claim against their house", kept long-term). UNRECOVERABLE = scheme-warned
  (an anonymous relation delta only) and scheme-abetted beyond the 42-day rumor window: NOT backfilled,
  live-planted-only from the wiring increment on (the graceful-degradation net working as designed).

**PURE FOUNDATION EXTENDED overnight 2026-07-04 (test-first .Logic, UNCOMMITTED, NO engine wiring, 960 tests):**
RESOLUTION NOW WIRED player-side 2026-07-04 (UNCOMMITTED): make_amends conversation verb (Amends gesture, taught only when the NPC holds a KNOWN resolvable grudge) + gift routing via take_gold/give_item (Gift gesture) both flow through a shared GrudgeResolutionService.TryResolveDominant -> GrudgeResolutionPolicy.Evaluate -> Resolve (+4 relation nudge) / Softened (reduce Magnitude) / Rejected. Deed gesture WIRED 2026-07-04 (UNCOMMITTED): the verified-favour sites (free_prisoner, release_prisoner, quest completed for the giver) that already plant an appreciation now ALSO route a GrudgeGesture.Deed via GrudgeResolutionService.TryGrudgeDeedTail -> a real favour softens or fully mends a Deed-kind grudge. cr.grudge_resolve gained a deed keyword; grudge_resolve self-test Scenario C (DeliveredPrisoner = always Deed-kind). The reconciliation trio (Amends/Gift/Deed) is now COMPLETE. cr.grudge_resolve + grudge_resolve self-test. --- three pure policies now exist and are documented by their tests: (1) `GrudgeResolutionPolicy` (the "resolvable"
half: a matching gesture Amends/Gift/Deed Resolves or Softens a grudge, gated by rigidity + traits; vendetta
always Rejected; Time-kind only nudged); (2) `DivorceConsequencePolicy` (the full signed matrix: per-role
PlantGrudge/Relief/Gratitude/None from mutual-vs-repudiated x blessed-vs-unblessed x each clan's alliance
stake); (3) `Tier2GrudgeRules` (DeliveredPrisoner, CaptiveAbuse [absorbs the companion revenge driver],
SwayExposed [hidden], UnblessedMarriage [clears on divorce]). OPEN DESIGN QUESTION surfaced by all three:
`MarriageWasBlessed` / clan-valued-alliance have NO current source for NPC-NPC marriages (MarriageApproval only
tracks the PLAYER's marriages), needing a derivation (allied clans / same faction / conservative default) or a
new persisted flag. **RESOLVED 2026-07-06 (Gabriel, option A, conservative):** NPC-NPC marriages are treated as
blessed by default and never plant an UnblessedMarriage grudge, since those weddings happen off-screen and a
hidden grudge there is noise the player never perceives; only the PLAYER's OWN unblessed marriages ever plant
this source. Live-wired the same day: `MarriageApprovalBehavior.OnBeforeHeroesMarried` plants the grudge on
every family member its own relation swing already chills (the same disapproving kin, never one who warmed
despite the family's verdict); `DivorceProcessBehavior`/`SpouseDivorceDemandBehavior` clear it, via the new
`GrudgeBehavior.ResolveUnblessedMarriageGrudgesAgainst`, the moment the player's own marriage dissolves (either
path). `DivorceConsequencePolicy`'s own dormant Gratitude fold-in (reconstructing `MarriageWasBlessed` at
divorce time for the NPC-NPC case) stays dormant, out of scope here. cr.grudge_unblessed_test / cr.grudge_unblessed_clear
+ the grudge_unblessed_marriage self-test (real marry -> plant -> end_own_marriage -> clear). Wiring increments
(per policy) fully specified in the overnight morning report.

**The source catalog (Gabriel, 2026-07-03: "faire le tour des systèmes pour alimenter le grudge").** Tiered
by delivery stage; Tier 1 doubles as the MIGRATION BACKFILL list (their markers already sit in saves):
- *Tier 1, existing ad hoc resentments*: jealousy (the staked party: spouse-of-X / player's partner / rival /
  spurned admirer); scheme WARNED (the player tips the target -> the PLOTTER grudges the player for exposing
  the plot, today a one-shot -33); scheme ABETTED then discovered (the wronged target grudges the player);
  FAILED or ABANDONED quest (the giver: "you gave your word", intensity by giver traits + task weight, today a
  dry penalty with no path to redemption); divorce repudiation; bastards (the claim, unpaid blackmail, the
  Resentful mother).
- *Tier 2, new detections, high value*: the DELIVERED PRISONER ("you sold me to my enemy", give_prisoner);
  the CAPTIVE-SCENE VICTIM (NPC or companion, unifies the planned companion revenge driver, which becomes a
  client of this pillar instead of its own system); the SWAY TARGET who hears the Rivalry rumour ("you turned
  X against me", emergent from the existing rumour spread); the UNBLESSED-MARRIAGE family (grudges the player
  while the union lasts, relieved at its divorce, the signed matrix).
- *Tier 3, vanilla events, dose carefully*: the kin of an EXECUTED lord (a PERMANENT grudge, infinite rigidity,
  the vendetta end of the spectrum); battle defeats/captures probably TOO NOISY, sample or exclude.

**Trait-modulated susceptibility (the "jealous trait" point).** Traits drive FORMATION threshold, INTENSITY,
RIGIDITY (resolution cost), and DECAY: Possessive/MonogamousStrict romantic prefs = jealousy grudges form more
easily and hold harder (monogamous cultures amplify, reuse jealousy_cultures.json); high Calculating = forms
easily, never decays, resolution wants payment or power; high Mercy = forgiving (high threshold, fast decay,
a sincere amend suffices) while low Mercy tallies everything; high Honor = grudges broken WORD above all (failed
quests, oaths) yet honours amends rituals; low Valor = fearful grudges expressed through schemes, never to your
face. The player-regard interaction stays the master rule: regard sets how hard resolution is.

**Anti-noise guardrails.** Not every negative event is a grudge, or the system duplicates relation as noise: a
grudge requires SIGNIFICANT + ATTRIBUTABLE + a conceivable resolution path (or an explicit vendetta), a
magnitude floor, and a PER-NPC CAP (the 2-3 dominant grievances, never a bookkeeper's ledger).

**Surfacing (ratified 2026-07-03: yes, Encyclopedia, but KNOWLEDGE-GATED, never omniscient).** A "Grievances"
section on the hero's encyclopedia page (a mixin, the house pattern: PersonalRelation / DiscoveredTraits /
Happiness / Chronicle button), showing only grudges the player KNOWS: narrative text, no numbers, the cause + a
qualitative rigidity word ("a fresh wound", "an old bitterness", "unforgivable") + the resolution hint only if
discovered. Knownness model (precedent: DiscoveredTraits = the player knows what they have learned): (a) known
at birth when the cause was PUBLIC or aimed at the player (a divorce scandal, an openly pressed bastard claim, a
jealous-threat letter received, a quest the player themselves failed, a prisoner the player delivered); (b)
known once the NPC voices it (conversation or letter); (c) HIDDEN otherwise, a low-Valor/high-Calculating
schemer conceals theirs (the exposed-plot patron who grudges the warner stays smooth), preserving the
stance-assassin surprise pattern, and its late unmasking is a gameplay moment. The same knownness flag drives
the PROMPT in mirror: a known grudge may be named openly by the NPC; a hidden one instructs concealment
(courteous but cold, the validated assassin register). Console `cr.grudges [name]` shows everything (dev
truth), the encyclopedia shows only what the player earned.

Sequencing: the divorce ships first (v1.21.0) with SIMPLE interim consequences (DONE 2026-07-03: relations are
SET from pre-dissolution baselines, neutralizing the engine's hidden penalty, signed by mutual-consent-vs-
repudiated read from the spouse's RESISTANCE, since engine relations are symmetric per pair); the full signed
matrix lands WITH the grudge pillar so it is coded once.

### RP with teeth, giving conversations real consequences *(ratified 2026-07-02 — a new pillar)*

**Why this is a pillar.** CR's roleplay has always been rich in *voice* but thin in *stakes* outside the
handful of headline deeds (join_clan, marriage, prisoner bargains). A player can say almost anything to an
NPC and the world does not visibly answer back beyond `change_relation`. This pillar is the deliberate,
ongoing effort to close that gap: every increment adds conversation verbs that carry REAL vanilla
consequences, re-validated by the bridge ("the prompt is advice, the bridge is law"), never a hollow promise
the engine ignores.

**Increment 1 — four quick-win verbs. 🔨 BUILT 2026-07-02 (untested in game), 676 mod tests / 190 SDK tests
green.** `give_influence` (a lord lends the player political influence, scaled by relation, capped, clan
must afford it), `lend_troops` (a party-leading NPC hands over healthy regular soldiers, never heroes, never
below 60% of their own strength, bounded by the player's party room), `sway_opinion` (the player's case
genuinely moves the NPC's own regard for a named THIRD PARTY, for or against, with a per-pair cooldown and an
ambient world-event rumour), and `release_prisoner` (the player intercedes for a THIRD-PARTY hero prisoner an
NPC's clan holds, distinct from the existing `free_prisoner` case where the NPC is the player's OWN captive).
Architecturally this increment introduced the **generic extension surface** every future verb rides on:
`EncounterContext.ExtraActionTeachings` (SDK) — one host-composed, prompt-ready block rendered right after the
core GAME ACTIONS section (skipped in Lean mode), instead of a bespoke `EncounterContext` field and a
hardcoded `PromptBuilder` section per verb. The mod composes the block in `EncounterContextBuilder` from
`CourtActionResolver` (engine-coupled facts) + `CourtActionPolicy` (pure amounts/caps/cooldowns, tested).
Per-NPC (and per-NPC-and-target, for sway) cooldowns are derived by scanning the NPC's own `NpcProfile.Events`
for a hidden marker the action itself records — no new persisted field. Console: `cr.rp_verbs [name]`.
**Reciprocal court verbs (04/07/2026):** `give_troops` / `spend_influence` — the PLAYER-side mirror of increment
1's `lend_troops` / `give_influence`, letting the player reinforce a needy NPC's under-strength party or fund a
lord low on court influence, with a safety floor on the player's own reserve. They also CLOSE the Appreciations
pillar (the `TroopsOrInfluenceSpentForThem` source). Console: `cr.give_troops` / `cr.spend_influence`.

**Increment 2 — the TrackedCommitment engine. 🔨 Phase 1 (the mechanical spine) BUILT 2026-07-03 (untested in
game), 694 mod tests / 190 SDK tests green.** The symmetric of `InformalQuest`: instead of the NPC setting the
PLAYER a task, the NPC OWES the player something. The flagship (and, for Phase 1, only) kind is `pledge_against`:
a lord, moved by genuine and standing enmity, vows in conversation to move against a named rival — a POLITICAL
commitment that launches a real, existing scheme (`SchemeStore.ForcePlot`, slander or sabotage, reusing an
already-active scheme between the pair rather than erroring) and adds the "he promised YOU" accountability
layer schemes lacked on their own. **Never declares or causes a war** — see the dedicated future item below.
New pure `CalradiaRemembers.Logic.TrackedCommitment` (PromisorId/TargetId/Kind/IssuedDay/DeadlineDay/State) +
`CommitmentPolicy` (deadline = one season / 21 days, fulfilled = +6 relation/stance, breached = -12 relation +
a Trust crater). Eligibility in `CourtActionResolver.ResolvePledgeRivalName` reuses the kin-safe,
already-disliked target selection from `sway_opinion` (`CourtActionPolicy.SelectPledgeTarget`, new pledge-only
animosity floor), taught in `EncounterContext.ExtraActionTeachings` alongside the increment-1 verbs. The new
`CalradiaRemembers.Commitments.CommitmentBehavior` (byte[] SyncData, mirrors `SchemeStore`) holds every
outstanding pledge and detects fulfillment TWO ways — `OnSchemeResolved` (called from `SchemeStore.OnStateChanged`'s
Succeeded case and from `PlayerAssists`' success branch) when the launched scheme succeeds, or `OnMapEventEnded`
recognizing (never causing) any real battle with the promisor's party and the target/their faction on opposite
sides — and detects breach on a daily overdue sweep, retiring the pledge either way with a `StanceService`
kept/broken-promise call, a `NotableEvent`, and a `WorldEventStore` rumour ("X honored/broke their pledge to move
against Y"). One pledge in flight per lord. **Phase 2 ✅ BUILT (untested in game): the quest-journal mirror
(`CommitmentQuest`/`CommitmentJournalBridge`, a read-only `QuestBase` mirroring `CalradiaRemembersQuest`, opened
on Create, closed success/fail on fulfil/breach, `ReconcileOnSessionLaunch` anti-stuck pass), console
diagnostics (`cr.commitments`, `cr.pledge`, `cr.pledge_fulfill`, `cr.pledge_breach`), and the
`commitment_fulfilled`/`commitment_breached` self-tests in `cr.testall`. The mirror persists only
`_promisorId`/`_commitmentId` as additive SaveableFields on a new QuestBase subtype; an old save with no mirror
degrades gracefully (no journal entry for a pledge already in flight).**

**Increment 3 — backing the player in a KINGDOM DECISION** (a vote, a policy, a succession). **Needs a spike
first**: Bannerlord's kingdom-decision API must be scoped before this increment can be estimated — the mechanics
may or may not be reachable without heavy engine coupling. Do not start building before that spike lands. (The
former coalition-package pairing with `pledge_against` is superseded: that verb shipped as increment 2's
flagship, scoped to a political scheme, never a war — see the war-declaration item below for why kingdom
decisions and war are being kept structurally apart from the pledge system.)

**Increment 4 — headliners.** The two biggest, riskiest, most narratively powerful verbs, deliberately last:
**defection brokering** (the player persuades an NPC to broker ANOTHER lord's defection, not just their own —
a step beyond the existing `join_clan`) and **NPC matchmaking** (the player arranges a marriage between two
OTHER NPCs, reading and writing the same marriage-prospect machinery `MarriageProspectResolver` already
provides). Both need real design passes; neither is scoped yet.

**Future — war declaration as a leader-only strategic decision (Gabriel, 2026-07-02/03, not yet designed).**
*"War declaration = a leader-only strategic decision, NEVER a conversation or pledge trigger. A dedicated future
system where the FACTION LEADER evaluates the clan's world situation, interests, relative strength, open fronts,
economy, allies, the target's vulnerability, and their own risk appetite, and declares war ONLY when it advantages
them; a leader who LOSES a war loses credibility, so that reputational downside is part of the calculus. Needs the
kingdom-decision / diplomacy API spike. The increment-2 pledge is POLITICAL (a scheme) only and must never declare
war."* Extended (2026-07-03): a lord who is NOT the faction leader CANNOT declare war, but CAN try to INFLUENCE
their leader on the question — so the war-decision system is a **council-pressure model**: vassals lobby, the
leader decides; accumulated vassal pressure (weighted by influence) is one input to the leader's advantage/
credibility evaluation. Natural future verb: a vassal pledges to "press my leader for war against X" (adds
pressure to the leader's evaluation) rather than declaring war themselves — the bridge from the increment-2 pledge
system to this future war-decision system.

**Settlement sale: EVALUATED AND REJECTED (Gabriel, 2026-07-02): a fief is a lord's sovereignty and reason to
exist, not merchandise; no amount of gold makes selling it in-character. Do not revisit.**

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

**Volet A — external world-event ingestion (do this first; highest value, simplest). ✅ SHIPPED in v1.15.0 (Nexus 2026-06-27), validated in-game. + MUTABLE ENTRIES added 2026-06-27 (`Feed.Update` upsert-by-id, `Feed.Retract` remove-by-id) closing the Homesteads modder (Bhel)'s one-way-can't-change gap — a settlement that moves, an NPC renamed, a place destroyed.** A façade like
`CalradiaRemembers.Feed.Record(ExternalWorldEvent)` lets a mod push a fact into the **same** World-Event /
rumour store the Chronicle, influence loop and NPC-awareness already read. Once past the gateway, it follows
the exact path of CR's native events — a Pravend NPC who'd plausibly have heard mentions it on his own. The DTO
carries `Category`, `Summary`, `Day`, `Magnitude`, optional `HeroIds`/`FactionIds`, `Lifetime`
(`Rumor` ages out vs `Deed` kept), `SourceMod`. *Worked example:* a horse-racing mod logs winners and purses
as `Lifetime.Rumor`; CR's lords gossip about the grey mare of Lord Caladog with zero knowledge of CR's memory
system. Pure/testable in `CalradiaRemembers.Logic`: the validator/normaliser (clamp, bound, id-resolve,
default) and the DTO→`WorldEvent` mapping; only the public static façade + behavior registration is engine glue.

**Volet B — external mission registration. 🔨 BUILT 2026-06-27 (Opus, untested in game, 496 tests). Public API `CalradiaRemembers.MissionBoard.Register/Complete/Abandon` + DTO `ExternalMission` (plain strings, no Bannerlord types); reward route = A (giver-required, paid when the player REPORTS to the giver in conversation). Reuses the `InformalQuest` pipeline via new `QuestType.External` + `InformalQuest.ExternalId/ExternalSourceMod` (SDK, ordinal-safe), so it inherits journal mirror + framing + gated reward + deadline/abandon/self-heal for free. Pure `ExternalMissionRules` (bound text, clamp reward to CR caps, normalize deadline; 10 tests). Verification ownership: the external mod calls `Complete(id)` (the gate) → stamps `SatisfiedOnDay`/`Evidence` → giver acknowledges + pays on report. Self-test `mission_register` (register→dedup→complete) in cr.testall. Open: a giver-less (B-fallback) variant deferred; modder-facing docs + a v1.16 Nexus section pending.** A mod defines **a mission for the player** and registers
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

**Volet C (Actions): external mods register conversation VERBS.** External mods register conversation
VERBS (`IConversationVerb` internally / `ExternalVerbContract` at the SDK boundary), CR teaches + routes
them exactly like its own, validated at the door like `Feed.Record`. **Foundation (the VerbContract
abstraction) SPIKED 2026-07-03** on the four existing "RP with teeth" court verbs (`give_influence`,
`lend_troops`, `sway_opinion`, `release_prisoner`), no behavior change: each verb's eligibility (still
`CourtActionResolver`/`CourtActionPolicy`, reused not duplicated), teaching text, and execution now live
together in one `CalradiaRemembers.Verbs` class, registered on a shared `VerbRegistry`
(`EncounterContextBuilder` composes teaching from it, `BannerlordGameStateBridge.ExecuteAction` routes to
it by name) instead of being scattered across a resolver/builder/switch. SDK-side DTOs added
(`NpcMemoryService.Core.Extension`: `VerbFacts`, `ActionRequest`/`ActionResult`, `ExternalVerbContract`,
plain Funcs, no TaleWorlds dependency) plus a mod-side `ExternalVerbAdapter` proving an external contract
can sit in the SAME registry as CR's own verbs. **NOT yet built:** the public `Actions.Register(...)` entry
point and its `Feed.Record`-style validation pipeline (source id, quota, name-collision): that is the next
increment. The other ~20 conversation actions stay on the old switch-based path for now (incremental
migration later). **`end_marriage` (divorce, Phase 1) is now BUILT as the first verb DESIGNED for this
contract from the start** (2026-07-03, see Marriage below) — the architecture validation the contract was
missing, since the four court verbs only proved a MIGRATION, not a from-scratch addition.

> NOT built (wished 2026-06-25). Sequence: A before B before C. All ride the SDK's existing engine-agnostic
> design. Linked to the scenario engine below (all are modder-extensible surfaces) and to the ChatAI-
> collaboration thread (an autonomous NPC↔NPC layer would write its outcomes back through Volet A so the
> "theater" leaves real marks the player later encounters — see also the four NPC↔NPC seams discussed but
> not yet scoped here).

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

### A personal ledger of quests the PLAYER gives out *(wished 2026-07-01, BUILT 2026-07-02, untested in game)*

**BUILT 2026-07-02**: companion errands (gather news / spy / steal / barter / envoy) now mirror into the
vanilla quest journal, option (a) below: same pattern as `QuestJournalBridge`, applied to
`CompanionMissionBehavior`. `CompanionErrandQuest` (a read-only `QuestBase`, no dialogs/reward, saveable
fields = companion StringId + mission Id, type-definer id 2 under the existing base 300_000_000) is
opened on dispatch, its log line pushed on every phase change (Outbound/Lingering/Returning/AwaitingReunion),
and closed on homecoming (success) or recall (letter or immediate), all via the new
`CompanionErrandJournalBridge` (the sole place that opens/updates/closes it, mirroring
`QuestJournalBridge`'s contract exactly). Anti-stuck: `ReconcileOnSessionLaunch` closes any mirror whose
mission Id is no longer in `CompanionMissionBehavior`'s active list, wired next to
`QuestJournalBridge.ReconcileOnSessionLaunch` in `NpcMemoryCampaignBehavior.OnSessionLaunched`. The status-line
wording is pure logic in `CalradiaRemembers.Logic/CompanionErrandJournalText.cs` (14 Gherkin tests). The
LORDS extension below remains open/to-evaluate; the companion ledger stands on its own. **Not yet validated
in-game**, next test pass: dispatch a companion, open the quest journal, watch the entry update through the
phases, confirm it closes on return and on recall.

**The gap (original problem statement):** the mod tracks two opposite directions of tasking, but only surfaced
one. Quests a NPC gives the PLAYER get a real vanilla quest-journal mirror (via `QuestJournalBridge`). But
errands the PLAYER hands OUT, today the companion missions (gather news / spy / steal / barter / envoy, in
`CompanionMissionBehavior`), were recorded only in the save (the persisted `_active` list) and the console
(`cr.missions`). In-game the player's only trace was the send-off line, the companion physically away as a
party, and their report on return. Send two or three companions off and it was easy to forget who went where.
**The idea:** a player-facing ledger of the tasks the player has commissioned, so "I sent someone somewhere"
is legible.

**Open design questions (to settle before building):**
- **Surface.** Two candidates, both with existing precedent in the mod: (a) reuse the `QuestJournalBridge`
  pattern for a read-only vanilla quest-journal mirror per active errand, opened on dispatch, closed on
  return/recall (cheapest, familiar to players, but crowds the vanilla log with our own outbound tasks); or
  (b) a dedicated CR panel, like the illuminated Chronicle viewer or the letter window (cleaner separation, more
  UI work). Leaning (a) for a first pass, (b) if it feels cramped.
- **Naming/framing.** Keep it clearly DISTINCT from quests-given-to-the-player: this is the player's own
  order-book / errand ledger, not tasks owed to them.
- **Data already exists.** Companion-mission state is already persisted, so a first version is purely a
  presentation layer over `CompanionMissionBehavior._active` (+ closing on the return/recall paths). No new
  gameplay, low risk.
- **Extension to LORDS (to EVALUATE, not committed).** Gabriel floated also logging tasks given to certain
  lords. FIRST verify what "giving a lord a task" even means in CR today: the negotiation framework has the
  player REQUEST deeds of lords (deliver a prisoner, declare war, recruit into the clan), but those are
  one-shot conversation outcomes, not tracked ongoing errands. So the lord case may need a tracked-commitment
  concept to exist before it can be journaled. Evaluate whether it is a real, coherent surface or scope creep;
  the companion ledger stands on its own regardless.

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

### Companion war-weariness & honorable retirement *(wished 2026-06-30, Gabriel — design only, nothing built)*

**The idea:** a companion who fights at your side for years accumulates the TOLL of war, and a spent veteran
eventually wants out. This is a **SEPARATE axis from happiness** (`CompanionHappiness`) — and keeping them
separate is the whole point:

| | Happiness / contentment *(shipped)* | War-weariness *(this)* |
|---|---|---|
| Nature | mood, **reversible** (drifts back to baseline) | accumulated combat toll, **climbs** with blood spilled |
| Cause | pay, idleness, errands, treatment | verified battle events — wounds, captures |
| End state | **bitter** departure ("I quit") | **dignified** retirement ("I have done my part") |

A veteran can be **devoted AND worn out** — folding weariness into happiness would wrongly read them as
"disgruntled." So this is its own gauge, ending in an honorable exit, not a resentful one.

**Drivers — verified deeds, never words** (the mod's core discipline): the score rises on real game events —
**wounded in battle ≈ +1, taken prisoner ≈ +10** (tunable). **Valor / hardiness slows accrual** (a brave soul
wears slower; a gentle one faster). NB: a capture hits BOTH axes (happiness `Captured` −12 *and* weariness +10)
— realistic, and deliberate, not double-counting to remove.

**The fief / governor gate — the strategic heart *(Gabriel's key refinement)*:** a companion who is
**materially invested in the clan does NOT leave** — they hold a fief or **govern a settlement**
(`Hero.GovernorOf != null`, or lead a party), and draw benefit from your campaign, so they stay. Only the
**landless, roleless** companions are ever at risk. In play this asks the player to EXPAND and grant fiefs to
secure their veterans — early game they are at risk (tension), late game securing them rewards success, and
because fiefs are scarce there is always a pool at risk. This gate also **defends against a vanilla pitfall by
construction**: a landed clan member who leaves can spawn/join a rival faction — never allowing the landed to
leave sidesteps that entirely.

**Two differentiated OUTCOMES by landedness** (so weariness always lands narratively, but never destabilizes
the realm):
- **Landless + worn out** → asks to leave your service: a warm farewell, out of the clan (contrast to the
  happiness system's bitter departure). Nothing left to re-add.
- **Landed / governor + worn out** → asks to **step back from the field** and see to their fief: stays in the
  clan as a governor, off the battlefield. A tired veteran becomes a settled administrator.

**Coherence — soft enforcement, NOT a hard lock *(decided 2026-06-30)*:** the re-enrolment hole ("vanilla lets
the player just put the benched governor back in a party") is handled the CR way — the mod owns a persistent
"stepped back from war" flag on `NpcProfile`; a daily tick that finds a flagged retiree back in a party **sends
them home** ("he has seen enough war; he rides back to <town>"), and **forcing a spent veteran back repeatedly
CONVERTS weariness into genuine unhappiness** — the warm retirement curdles into a real departure and they leave
for good. You *can* drag them back (vanilla allows it), but you use them up. A hard Harmony button-block was
**considered and rejected** as invasive, patch-fragile, and un-CR: the world reasserts the fiction instead of
fighting the engine.

**Architecture (matches the pillar):** a pure policy in `CalradiaRemembers.Logic` (thresholds, trait scaling,
outcome-by-landedness, decay) — unit-tested; engine glue = campaign-event hooks for hero-wounded +
hero-prisoner-taken (player-clan only), persistence of the score + retired flag on `NpcProfile`, and a
conversation surface that reuses the existing happiness grievance/departure plumbing (a retirement request the
player answers).

**PURE CORE BUILT 2026-06-30 (Opus, +15 tests, green) — `CalradiaRemembers.Logic/WarWeariness.cs`:** the score
(0..100) with `ApplyWound` (+1) / `ApplyCapture` (+10), `Rest(step)` recovery, a Valor-scaled `RetireThreshold`
(clamped 16..72), `WantsToRetire`, a `WearinessBand` (Fresh → Seasoned → Weary → Spent) read RELATIVE to the
companion's own tolerance, `Retirement(isLanded)` → `RetirementKind.StepBackToGovern` / `LeaveService`, and a
`Describe` line for prompts/encyclopedia. **Design refinement made while building:** Valor **raises the
retirement TOLERANCE** rather than literally slowing per-event accrual (the +1 wound has no room to scale at
integer resolution) — same felt effect (a brave companion retires later), cleaner and fully testable. NEXT =
the engine glue (event hooks + `NpcProfile` persistence + the retirement conversation + the soft-enforcement
tick), still to build.

**Forks resolved 2026-06-30 (Gabriel confirmed) — the v1 build:**
- **Decay:** YES, slow recovery at real REST (`Rest`, ~2/week suggested). Not monotonic.
- **Player lever:** NO "one more campaign" persuasion in v1 (the retirement request re-presents if ignored); a
  fast-follow.
- **"Invested" gate:** governor (`Hero.GovernorOf != null`) **OR** leads a party.
- **Wear sources:** v1 = wounds (+1) + captures (+10) only; other sources (witnessing a companion die, long
  campaigns, defeats) deferred.

**AUDIENCE-AS-CONVERSATION BUILT 2026-07-01 (Opus, untested in game) — the popup now opens a real chat.** Both
retention popups (the happiness grievance AND the war-weariness retirement) became a two-road choice: **"Let us
speak of it…"** opens a real CR conversation the companion requested, and **"I've no time for this."** turns them
away with a bite (a `Mistreated` happiness hit + the existing escalation/cooldown). The shared seam is
`ChatManager.OpenForCompanionAudience(hero, profile, reason, landed)` → `ChatViewModel.ActivateCompanionAudience`
(a normal conversation, opened programmatically from a map popup, carrying the profile in `_audienceProfile`
since there is no vanilla `OneToOneConversationHero`; the companion speaks FIRST via an NPC-led opener). SDK:
`CompanionAudienceReason` (None/Grievance/Retirement) + `EncounterContext.CompanionAudience` /
`AudienceRetirementIsLanded` + `PromptBuilder.AppendCompanionAudience` — the grievance leans on the existing mood
note + `reassure_companion`; the retirement gets its own surface teaching the **`retire`** action AND the
persuasion framing (**this is where the deferred "one more campaign" lever now lives** — the player can talk a
war-weary veteran into staying, in their own words). `retire` is a chat-flow control (intercepted in the VM, not
a bridge action): it flags the grant and executes at CLOSE via `WarWearinessBehavior.ExecuteRetirement` (never
removes a hero mid-conversation) — landless leaves, landed steps back. Console `cr.audience [retire|grievance]
[name]` opens either surface directly. **This supersedes the "player lever: NO in v1" fork — the conversation
naturally IS the lever.**

**ENGINE GLUE BUILT 2026-06-30 (Opus, untested in game) — v1 complete + shippable:** `NpcProfile.WarWeariness`
(int) + `SteppedBackFromWar` (bool) persist per companion (save-safe defaults). `WarWearinessService` moves the
score to/from the profile. `WarWearinessBehavior` (registered after the happiness behavior, gated by the same
`CompanionContentment` MCM toggle): captures via `HeroPrisonerTaken` (+10), wounds via a daily `IsWounded`
not-wounded→wounded transition (+1, one per wound episode, covers auto-resolved battles), a weekly `Rest` (−2)
so heavy fighting outruns recovery while light service heals. When a companion is spent, ONE retirement inquiry
per week: **landless → warm departure** (`RemoveCompanionAction.ApplyByFire` + honorable message); **landed
(governor / party-leader) → step-back flag + message** (stays in the clan). **Soft enforcement:** a stepped-back
veteran found back in the MAIN party earns a `Mistreated` happiness hit each week and, after 3 weeks dragged
back, the retirement BREAKS (flag cleared → the contentment system may then let them leave). Decline a request →
a 14-day cooldown before it re-presents. Console: `cr.weariness [name]` (show score/band/tolerance/standing),
`cr.weary <value> [name]` (set), `cr.weary_tick` (run a weekly pass). **DEFERRED (post-v1):** physically ejecting
a stepped-back companion from the field on grant (v1 sets the flag + relies on the drag-back enforcement rather
than hero-party surgery); a farewell letter for the landless retiree (v1 departs immediately); a dedicated MCM
toggle (v1 rides `CompanionContentment`); the persuasion "one more campaign" lever; the extra wear sources.

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

**RATIFIED 2026-07-04 (Gabriel): start with the CHAT WINDOW surface, drop-in folder, custom-if-exists-else-3D.**
Confirms the tiered fallback exactly ("thumbnail present -> show it; absent -> today's 3D render", silent on a
missing/unreadable file). Naming decision REFINES the resolver key: try `Portraits/<Hero.StringId>.png` FIRST
(unique, rename-proof), then `Portraits/<normalized-name>.png` as a friendly fallback, plus a dev console command
`cr.portrait_id` that prints the exact filename to drop for the NPC currently in conversation (StringId is opaque
otherwise). Format: square PNG, ~256x256.

**SPIKE ✅ GREEN, validated in-game 2026-07-09 (fork RESOLVED: B, runtime load).** A hand-made 445x805 PNG loaded
from disk at runtime rendered in a Gauntlet widget, and VRAM stayed FLAT over 2x300 load+release cycles (RTX 3080,
dedicated memory steady). Full proven recipe (engine texture from bytes -> SpriteCategory with sheets injected +
IsLoaded forced -> SpritePart SheetID=1 + reflected UVs -> SpriteGeneric -> `.Sprite` set by widget Id after
LoadMovie, never @-bound; `Texture.Release()` on close) lives in [[feedback-bannerlord-ui-patterns]] and the
reference implementation in `Portraits/RuntimePortraitFactory.cs` + `PortraitSpikeManager.cs`
(`cr.portrait_spike` / `cr.portrait_spike_stress`, shipped inert in v1.29.1). **The feature (chat-window surface
first, per the ratified order) is now UNBLOCKED and ready to build**: shared resolver + LetterViewModel/chat
wiring, reusing the spike factory.

### Player action narration — interpret the player's `*…*` and first-person deeds *(wished 2026-06-28, player feedback)*

A recurring roleplay frustration: a player types a physical action — `*I take her by the wrist*`, or first-person
"I grab her arm" — and the NPC, never told to read it as narration, treats it as nonsense and pushes back ("what
are you talking about, you crazy fool"). CR teaches the NPC to use `*asterisks*` for ITS OWN actions but never
teaches the model to INTERPRET the *player's* narrated actions. Add a PromptBuilder block defining the player's
`*…*` (and plain first-person physical statements) as in-world action the NPC reacts to in character — distinct
from speech. Pure-ish prompt work, **high value / low effort**. Caveat: a free NPC may still legitimately DEFY
coercion (that is correct behaviour); the bug is the model not understanding the input at all. Pairs with the
player-as-captor scene below for the captivity use-case. (Model choice still dominates for adult refusals.)

**BUILT 2026-06-28 (untested in game).** `PromptBuilder.AppendPlayerActionNarration` (SDK) injected near the end
(high recency, beside the language mirror) of BOTH the main NPC and commoner prompts: defines the player's
`*…*` and plain first-person physical statements as real in-scene actions to ENGAGE with (never gibberish, never
"what are you talking about"), with explicit examples, while keeping the NPC free to welcome/resist/refuse. Test:
chat any NPC and type `*I take you by the wrist*` or "I put my hand on your shoulder".

### Player feedback — time-gap chatter, group-scene recall, romance override, post-history hook *(2026-06-29)*

A player on GLM 5.1 / NanoGPT raised three things (and asked about a donation channel):

- **NPCs harp on the time since the last conversation** ("Four days since we last spoke...") — map travel eats
  days fast, so a short gap should not be remarked. **BUILT 2026-06-29 (untested in game).** Pure tested
  `MeetingGapDescriber` (CalradiaRemembers.Logic) gates it: nothing at or under ~1 week (`RemarkThresholdDays = 7`),
  and a longer gap is phrased on the Calradian calendar ("a season" / "a year") as plain awareness. SDK
  `EncounterContext.MeetingGapNote` replaces the old unconditional "You last saw the player N days ago." line,
  rendered by `PromptBuilder.AppendEncounterContext` with explicit "do not open with it / do not dwell" guidance.
  (+6 tests.) Test: talk to a companion a few days after the last chat — they no longer lead with the count; a
  long absence may be acknowledged once, in passing.

- **A companion does not recognise they took part in a group conversation.** Real gap, not the LLM: at close,
  only the MAIN NPC's profile was written. **BUILT 2026-06-29 (untested in game, first slice).** New
  `ChatViewModel.FinalizeWitnessMemories` (called from `ChatManager.Close`) records a light "Was present while
  {player} spoke with {mainNPC}..." event into each HERO witness's own profile, so a later one-to-one reflects
  the shared scene. First slice records only witnesses the player has already met (existing profile);
  synthetic/bandit witnesses and the main NPC are skipped. OPEN: get-or-create a profile for a stranger witness;
  optionally a richer per-witness summary (deferred — would cost N extra LLM calls per scene).

- **Romance / courtship override per character, independent of traits and culture.** TO DESIGN. Today the
  romantic profile is DERIVED from the five traits (`RomanticProfileBuilder`), so making a character
  "casual/open" forces a low Honor value. Proposed: an optional `romantic` block in `character_overrides.json`
  (orientation, monogamy/openness, devoted/casual, optional kinks) that OVERRIDES the derived `RomanticProfile`
  when present — slots into the existing player-authored-characters system (`Use custom traits`). Needs design:
  which fields are overridable, how it interacts with re-derivation for already-met NPCs, and the JSON shape.

- **A SillyTavern-style post-history / jailbreak hook.** A player asked where to put RP / censorship-bypass
  framing "pre or post history" (SillyTavern terms). CR sent everything in the system prompt (all pre-history),
  with no modder-editable last-word slot. **BUILT 2026-06-29.** New editable `post_history_instructions.txt`
  (ModuleData, ships empty) injected verbatim at the VERY END of the system prompt (after the language mirror,
  highest recency) for BOTH the NPC and commoner prompts — the analog of SillyTavern's post-history field.
  SDK `PromptBuilder.PostHistoryInstructions` + `AppendPostHistoryInstructions`; mod `NarrativeLoader` loads the
  file; `LlmServices` wires it; loads on campaign start / `cr.reload_lore`. +3 tests (presence + ends-with proof,
  NPC and commoner). Documented in the Nexus "Make it your own" section (positioned next to behavior_guidelines).

- **Donations / feedback channel (Ko-fi):** DONE 2026-06-29 — Ko-fi page (ko-fi.com/lograam) created by player
  request; a centered "Support the work" section added at the top of the Nexus description.

### Player-requested — wardrobe by talking, image generation, Hotscenes compat *(wished 2026-06-29, player feedback)*

A player asked whether three things are realistic to even want. Feasibility read (most realistic first):

- **Wardrobe by conversation (undress / "leave them in their shift") — MOST realistic; do this one.** Bannerlord's
  Equipment API changes a hero's CIVILIAN outfit at runtime (clear/replace the Body/Cape/Head/Gloves/Leg slots). A
  new adult-gated action verb (e.g. `change_outfit` / `undress`) emitted by the LLM and executed by the bridge
  ("the bridge is law": re-validate, adult-gate, apply via `Equipment`) lets the player ask an NPC to strip or
  dress down, and it actually shows on the 3D model in conversation and in captive scenes. Fits CR's action model
  cleanly. Open questions: persistence (revert to the prior outfit after the scene?) and how far to undress
  (Bannerlord renders a default underwear body when the slots are cleared).

- **Image generation — feasible backend, the DISPLAY is the blocker.** Sending a scene/portrait prompt to an image
  endpoint (the player's own SD, or an API) is easy HTTP. Showing a runtime-generated image in the Gauntlet
  chat/scene window is the hard part — the same texture-load-from-disk spike already noted under "Custom hand-made
  portraits" and the parked "generated portraits" idea. Slow and costly per image, so strictly opt-in. Sequence it
  AFTER the portrait PNG→Gauntlet spike lands (it unlocks both). An optional add-on, not core.

- **Hotscenes compat — possible but the hardest; research-gated.** Hotscenes plays animated adult scenes between
  agents inside a MISSION; CR scenes are a chat overlay on the map, not a mission. A real bridge means opening a
  scene with the two heroes and triggering the right Hotscenes animation at the right beat — heavy, and entirely
  dependent on what hooks/API Hotscenes exposes (unknown; may be closed). Most honestly delivered as a SEPARATE
  optional bridge mod once Hotscenes' surface is understood. Do not promise it; research first.

### Captive-scene content
- **Scenario bank**: a bank of authored scene setups, one-use-per-campaign via SyncData, eligibility by
  kinks/gender, prompt injection. (Originally imagined as Twine→JSON; Twine is dropped — see the scripted
  scenarios pillar — in favour of plain keyword/JSON data, reusing the same composer/authoring approach.)
  (When adding persisted campaign state, REMOVE `CaptiveEncounterService.ResetOnSessionLaunch` — SyncData(load)
  fires before OnSessionLaunched and would clobber loaded state.)
- **Image overlay** for scenes (`[SCENE]` parser).
- **Player-as-captor ("tyrant") scene** *(wished 2026-06-28, player feedback)* — today the explicit captive
  scenes frame the player as the PRISONER (`PlayerStatus == Captive` + Hardcore). Players using captivity-event
  mods hold NPCs prisoner and want the inverse: an explicit scene where the PLAYER is the captor of an NPC they
  have taken. The captor direction was noted "deferred — no aggressor scene exists yet" (Sprint 17). Reuse the
  scene director + intents (Interrogation/Domination/…) with the roles swapped; standing damage lands on the NPC,
  not the player. Now requested by more than one captivity-mod player.
  **BUILT 2026-06-28 (untested in game):** a player-LED scene (NOT the NPC-driven director) — `EncounterContext.IsCaptorScene`
  + `AppendPlayerCaptorSceneRules` frame the NPC as the player's prisoner reacting per their nature (per-intent;
  Hardcore CNC layer; reads the player's `*…*` as the captor's deeds). Mod: `ChatViewModel.ActivateCaptorSceneMode`
  (normal conversation path = full memory/actions, flagged), `ChatManager.OpenForCaptorScene`, console `cr.captor_scene
  [intent] [name]`. Intents/variants validated via self-tests (cr.test chat_captor_*): proud vs craven (Trait-driven),
  torture (breaks under the lash), CNC, male-captor/female-prisoner, and a GROUP CNC (player + companions, collective
  branch in the prompt). **OPEN — the in-game entry point (NOT playable yet, console-only):** a "talk to your prisoner"
  button on the party/prisoner screen to open the scene, AND a companion SELECTOR (like the captive-encounter witness
  picker) to bring 1-2 allies into a collective scene. Plus NPC standing effects (the inverse of public humiliation).

### Conception from any vaginal act — consensual scenes + female NPCs *(wished 2026-06-24 — ✅ BUILT, in the live pregnancy system)*

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

### Bastards — a hidden birth as a grave consequence layer *(wished 2026-06-24 — ✅ SHIPPED v1.15.0)*

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
4. ~~An NPC fabricates a FALSE bastard to smear the player~~ — **DROPPED 2026-06-26 (Gabriel): a rumour about a
   bastard the player knows doesn't exist reads as a *bug* (a phantom child), not a deliberate accusation — the
   line is too thin.** "An enemy smears you" is already covered by the generic **`SchemeKind.Slander`** schemes,
   which invent no phantom data. Don't re-litigate.

A real bastard's defence is to SUPPRESS it — keep it hidden, buy the mother's silence (the blackmail) — because
once it surfaces (the seasonal age-roll, an unmet blackmail, or coming of age) it is *true* and the scandal
sticks. (A *fabricated* bastard was considered as a fourth thread and **dropped** — see thread 4 — because a
phantom child reads as a bug, not an accusation.)

**The secret-keeper set** *(decided 2026-06-25)*: knowers of a real bastard = the mother + the affair
**witnesses** (jealousy already tracks staked NPCs) + the grown bastard. ANY knower may blackmail or leak it,
and **discovery risk scales with the number of knowers**. Persisted as a knower-set per bastard.

Ties five systems together: **pregnancy** (the source) → **jealousy** (the scandal) → **witnesses** (who knows)
→ **dynastic succession** (the claim) → **scripted scenarios** (the trigger → consequences engine), surfaced via
the **world-event/rumour** channel. The clan a bastard is born into is settled by the pregnancy plan above
(child → mother's clan, always).

### Lean prompt for small / local models *(wished 2026-06-23 — ✅ SHIPPED, Lean v2 ~1490 tokens, v1.20.0)*

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

**BUILT 2026-07-01 (Opus, untested in game) — the MCM-toggle version.** Pure `LeanPromptPolicy` (SDK
`NpcMemoryService.Core.Prompts`, +5 tests in NpcMemoryServiceTests): `LeanPromptLevel` (Full / Lean),
`PromptSection` (Relationships / WorldNarrative / CulturalBackground / AuthoredBackstory / FullBehaviorGuidelines),
`Include(section, level)`, `MemoryEventLimit(level)` (Full = all, Lean = 6 most-recent), `UseFullBehaviorGuidelines`.
Wiring: `EncounterContext.LeanLevel`; `PromptBuilder.BuildSystemPrompt` drops the four heavy flavour sections in
Lean, swaps the long behaviour block for a short built-in `AppendLeanBehaviorGuidelines`, and caps memory events;
mod `CrSettings.LeanPrompt` MCM toggle ("Compact prompt for small models", LLM Provider group) → `ModSettings.LeanPrompt`
→ `EncounterContextBuilder`. Console `cr.prompt_size [name]` reports the prompt's char/≈token size and mode, so a
player can toggle it and see the reduction. **DEFERRED:** the budget-based / auto-detect variant (v1 is the manual
toggle); trimming the commoner prompt (already short); a lean variant of the captive-scene rules.

### Stance — mechanical behaviour-gating *(all 4 axes 🔨 BUILT 2026-07-04→05, untested in game)*

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

**RATIFIED 2026-07-04 (Gabriel: "beaucoup de valeur, dans la philosophie de CR"), two structural rulings:**
1. **Gate on the PROVEN level, not on flattery.** "Deeds prove" is a hard rule at the gate. NOTE (verified in
   code 2026-07-04): the ANTI-FLATTERY GUARANTEE ALREADY EXISTS in `StanceGate` — a spoken round caps at
   `WordCap` = ±2 per 8h, a proven deed at `DeedCap` = ±30. Reaching a gate at 40 by words alone would take
   ~20 accepted rounds over ~160 in-game hours (and Fear decays underneath). So the gate reads `stance.Fear`
   DIRECTLY — no separate "proven floor" store, no new SyncData key. The earlier A/B storage question is moot.
   DECAY CAVEAT: unlike Trust/Respect/Affection (ETERNAL, no decay — forgetting reads as a bug), **Fear is the
   one stance axis that fades** (`FearDecayPerDay` = 2, toward 0). So Fear-based coercion leverage is
   inherently TEMPORARY by design: a lord cowed today yields softer terms, but ~20 days of calm erase the scare
   and the leverage expires. This is a feature (terror recedes), not a bug — the fiction should own it.
2. **One axis at a time, FEAR first.** Additive per the save-migration hard rule; Fear already has its
   receptacle (the graduated FEAR stance register + hidden Grudges as the resentment sink). Then Trust,
   Respect, Affection as later increments.
Axis→domain grid (each axis owns one family of consequences, kept disjoint for lisibility + pure-policy testing):
- **Fear → coercion**: high → accepts tribute / yields a prisoner / backs down, BUT plants a HIDDEN grudge
  instead of regard (feeds the existing stance-assassin / hidden-hatred pattern); low + low Respect → refuses
  flat, may escalate.
- **Trust → delegated risk**: gates verbs where the NPC EXPOSES themselves (companion mission, lending troops
  without surety, sensitive info, accepting marriage). Low Trust → "prove it first."
- **Respect → political weight**: modifier on influence/negotiation (threat credibility, negotiating as an
  equal vs. snubbing, weight in a scheme).
- **Affection → personal loyalty**: gate for the romantic AND for staying loyal despite a poor CLAN standing,
  distinct from clan regard.
Increment plan: **Fear-1** = wire the coercion gate into the freedom-bargain / ransom bridge (fearful captive
yields softer terms + hidden-grudge tail), pure `StanceGatePolicy` in `.Logic`, tested first; thresholds TO
RATIFY before any code.

**Fear-1 🔨 BUILT 2026-07-04 (untested in game), 1141 mod tests.** `CalradiaRemembers.Logic/StanceGatePolicy.cs`
(`ResolveCoercion(fear, respect, honor, valor, calculating)`): `YieldFearFloor=40` (raised by positive
Honor/Valor at `+5`/level each, lowered by positive Calculating at `-5`/level, floor clamped ≥
`RefuseFearCeiling`), `RefuseFearCeiling=15` + `RefuseRespectCeiling=10` (both must hold to refuse flat),
`RansomSoftenMultiplier=0.75`, `CoercedGrudgeMagnitude=30` (+`5`/level of positive Calculating). Wired into
`BannerlordGameStateBridge.ExecuteFreePrisoner`: reads the captive's live Fear/Respect (`StanceService.ToStance`)
+ Honor/Valor/Calculating off the live Hero; `RefusesFlat` blocks the release outright (bargain declined, captive
stays put); `YieldsSofterTerms` proceeds with the release and additionally plants a HIDDEN `GrudgeSource.CoercedRelease`
(new value, appended — ordinal 13) via `GrudgeBehavior.TryPlant`, on top of (never instead of) the existing
deed-gesture/appreciation/chronicle channels. A profile-less captive (no stance data — the `cr.test free_prisoner`
self-test path) skips the gate entirely (`Neutral`) rather than defaulting Fear/Respect to 0/0, which would
otherwise satisfy the refuse-flat thresholds by construction and block every ordinary release. **SOFTER TERMS
delivered PROMPT-side (2026-07-04):** since `free_prisoner` carries no numeric ransom (the LLM emits its own
freeform `give_gold` in the same response), the 0.75 multiplier is NOT clamped in the bridge; instead a new
additive `EncounterContext.CaptiveFearCoerced` (computed in `EncounterContextBuilder.ResolveCaptiveFearCoerced`
via the SAME `StanceGatePolicy.ResolveCoercion`, so prompt-advice and bridge-law never disagree) makes
`PromptBuilder.AppendPrisonerFreedomBargain` instruct the frightened captive to offer a LARGER sum / harsher
terms. Prompt is advice, bridge is law (the hidden grudge), both consult the identical policy. Console:
`cr.stance_set <trust> <respect> <fear> [name]` (already existed) seeds the axes to exercise yield/refuse in
game. Next (DONE, see Respect-1 below): Respect (political weight), then Affection (personal loyalty), per the
axis→domain grid above.

**Trust-1 🔨 BUILT 2026-07-04 (untested in game), 1148 mod tests.** Trust → DELEGATED RISK: gates the verbs where
the NPC exposes THEIR OWN resources to the player. `StanceGatePolicy.WillDelegateRisk(trust, relation)` =
`trust >= DelegatedRiskTrustFloor (20) || relation >= DelegatedRiskRelationBypass (30)`. Wired into
`GiveInfluenceVerb` + `LendTroopsVerb` (the NPC lending their OWN influence/troops to the player; the reciprocal
`SpendInfluenceVerb`/`GiveTroopsVerb` are untouched), at BOTH surfaces: `IsEligible` (advice — not taught below
the gate, so the LLM never promises it) and `Execute` (law — re-reads live Trust/relation, refuses in character
"Prove yourself first"), reading Trust via `StanceService.ToStance` and relation via `profile.ClanRelationWithPlayer`.
**OR-relation soupape (Gabriel ratified 2026-07-04):** an established ally (vanilla relation ≥ 30) still lends
even with 0 CR-tracked Trust, so the gate never silently narrows a shipped feature; an unproven stranger must
still earn Trust (kept quest +15, gift +4, capped word nudges) to cross the floor. Profile-less NPCs were already
excluded from these verbs (`Profile` is a hard prerequisite), so no new blocking for untracked heroes. Trust-only
for now (no trait modulation yet). Console: `cr.stance_set 25 0 0 <name>` seeds Trust to test. **OPEN SEAM:** the
companion risky-errand gate (Spy/Steal) is left commented-out in `CompanionMissionBehavior.Dispatch` — freshly
recruited companions sit at Trust 0 AND relation ~0, so gating would lock an already-shipped feature; needs its
own design (gate only the riskiest missions, or seed companion Trust on recruit) before wiring.

**Respect-1 🔨 BUILT 2026-07-05 (untested in game), 1159 mod tests.** Respect → POLITICAL WEIGHT: gates
`join_clan` (recruiting a lord into the player's clan). A lord who holds the player in contempt will not throw in
their lot, however cordial the relation. Wired at BOTH surfaces: `EncounterContextBuilder.ResolveLordRecruitEligible`
(advice — now takes `profile`, adds the Respect check on top of the existing `LordRecruitment` gate, so a
contemptuous lord is never taught the action) and `BannerlordGameStateBridge.ExecuteJoinClan` (law — now takes
`profile` off `ActionContext`, re-reads live Respect/relation, refuses in character "does not respect you enough to
throw in their lot with you. Earn their esteem first."). Profile-less lords skip the Respect check entirely
(`join_clan` never depended on a stance profile before this increment, so an untracked lord is not newly narrowed).
**Site-specific HIGH soupape (Gabriel ratified 2026-07-05):** the naive shared soupape was DEAD here —
`LordRecruitment.MinRelation` (60) already exceeds the general `RespectRelationBypass` (30), so it always fired and
the Respect floor never bound. Fixed with a dedicated `StanceGatePolicy.CommandsRespectToJoinClan(respect, relation)`
= `respect >= RespectFloor (20) || relation >= RecruitRespectRelationBypass (80)`: an adored lord (relation 80+)
still joins on relation alone, but a merely-eligible lord (60..79) must ALSO respect the player (Respect ≥ 20), so
the floor genuinely binds in that band. The general `CommandsRespect` (bypass 30) stays reserved for future
lower-gated Respect sites (sway_opinion weight, threat credibility). Side-effect fix while wiring this:
`GiveInfluenceVerb`/`LendTroopsVerb` (Trust-1) would not actually compile — `profile.ClanRelationWithPlayer` is
`int?`, passed where `WillDelegateRisk` expects `int` — fixed with `?? 0`. Console: `cr.stance_set 0 25 0 <name>`
seeds Respect to test (Respect is the 2nd arg); use relation 60-79 to see the floor bind.

**Affection-1 🔨 BUILT 2026-07-05 (untested in game), 1161 mod tests.** Affection → PERSONAL LOYALTY despite a
cold clan standing (Gabriel ratified 2026-07-05: the "activator", not a floor). Affection is not a deed-earned
axis; it mirrors the NPC's personal regard (`ReputationWithPlayer`), which already gates romance/marriage, so
its novel mechanical role is a THIRD door onto the delegated-risk gate: a lord who personally adores the player
lends their own troops/influence out of personal loyalty even without proven Trust or a warm clan relation.
`WillDelegateRisk(trust, relation, affection)` now = `trust >= 20 || relation >= 30 || affection >= AffectionLoyaltyFloor (40)`,
reading affection from `profile.ReputationWithPlayer` at all four `GiveInfluenceVerb`/`LendTroopsVerb` sites.
NOT applied to `join_clan` (its upstream `LordRecruitment.MinRelation` = 60 already gates relation, so an
Affection override there would be moot). Console: `cr.rep_set <n> [name]` (personal regard) + `cr.stance_set 0 0 0 <name>`
(zero the earned axes) + a cold relation, then a lend request should still succeed by personal loyalty.

**✅ STANCE MECHANICAL LAYER COMPLETE (4/4 axes, 2026-07-04→05, all untested in game):** Fear → coercion
(freedom-bargain), Trust → delegated risk (lend troops/influence), Respect → political weight (join_clan),
Affection → personal loyalty (delegated-risk third door). All pure in `StanceGatePolicy` (.Logic, fully tested),
consulted at both the advice (prompt/eligibility) and law (execution) surfaces, with `cr.stance_set`/`cr.rep_set`
seeding for in-game tests. OPEN follow-ups noted in place: companion risky-errand Trust gate (seam), and later
Respect sites (sway_opinion weight / threat credibility) that the general `CommandsRespect` bypass already awaits.

### Player feedback round — SuperGraham339 (2026-07-05), triage + 2 new ratified pillars

A rich living-feedback dump. UNIFYING THEME under ~half the points: the LLM lacks ground-truth game facts and
hallucinates (army size, vanilla quests it issued, gang-leader rivalries, player backstory/appearance, "Christ").
So half the list is ONE class of fix: feed the missing fact into context + hook unhooked entry points.

**Quick wins 🔨 BUILT 2026-07-05 (untested in game), 1167 mod tests:**
- **Diacritic folding in name matching** — pure `CalradiaRemembers.Logic\NameNormalization.Fold` (lowercase +
  NFD decompose, drop NonSpacingMark, recompose), 6 tests. Applied in `ContextualNameResolver` (both message
  tokens and hero name parts) and `ModCheats.ResolveHero` (rewritten to fold+tokenize: every folded query token
  must substring some folded name part, so "Heloise the Finger" also disambiguates shared epithets). "Héloïse"
  now matches a typed "Heloise".
- **Player party troop count in context** — additive `EncounterContext.PlayerPartyTroopCount` (default 0), set in
  `EncounterContextBuilder.ResolvePlayerPartyTroopCount` ONLY when `hero.PartyBelongedTo == MobileParty.MainParty`
  (a present party member/companion; non-party NPCs get 0 = no metagame count). Rendered when > 0: "The player
  currently leads N soldiers in their party." (SEAM flagged: it is conversation-stable, could later hoist into the
  cacheable prompt prefix.)
- **Anachronism guardrail** — a light bullet in both full and lean behaviour guidelines: Calradia has its own
  faiths/gods/history, never reference Earth's religions/deities/saints/historical figures (no Christ/Rome/
  Napoleon). Does not curb RP agency. (SEAM: skipped when a station/modder `BehaviorGuidelinesOverride` is active,
  same as every built-in bullet.)

**Bugs — triaged 2026-07-05:**
- **No Encyclopedia button in captive dialogue — RESOLVED BY DIAGNOSIS, no separate fix.** The button
  (`CalradiaRemembersChat.xml` `EncyclopediaButton`) is gated only by `IsEncyclopediaVisible => _npcHero != null`,
  with NO extra captive-mode masking. A real-Hero captor (a lord) already shows it in the captive scene; a
  SYNTHESIZED bandit captor (no Hero, like Ranulf) correctly hides it because there IS no encyclopedia page for a
  non-Hero. So this is the SAME root cause as the named-bandit-memory bug and is fixed automatically by the Nemesis
  pillar (Hero captor -> page exists -> button reappears). Building a separate UI fix would be wrong.
- **Chat-granted player freedom is not made real in vanilla (Gabriel clarified 2026-07-05).** The case is: the
  player NEGOTIATES their release OR ESCAPES via chat, the LLM plays it narratively, but the vanilla captivity never
  ends, so captivity resumes. "The prompt is advice, the bridge is law": a bridge action must apply
  `EndCaptivityAction`. State of play: (a) ESCAPE already has a real mechanic (`escape_attempt` ->
  `ResolvePlayerEscapeAttempt` -> `_pendingEscape` -> `FinalizePendingEscape` -> `EndCaptivityAction.ApplyByEscape`,
  deferred to scene close). If a chat escape failed to take, the likely cause is a narration/roll DESYNC (the LLM
  narrated success while the ~10% roll failed) or `escape_attempt` never emitted; verify at build. (b) NEGOTIATED
  RELEASE has NO action at all: `free_prisoner` is the inverse (the PLAYER frees THEIR captive via
  `EndCaptivityAction.ApplyByReleasedByChoice(npcHero, MainHero)`). FIX = add the mirror: a captor-side
  "release the player" action that, when the captor is genuinely persuaded (gated, not trivial), applies
  `EndCaptivityAction.ApplyByReleasedByChoice(captor, MainHero)`, deferred to scene close like the escape, taught in
  the captive-scene / lord-audience prompt. Build-ready design; needs SDK teaching + bridge execute + deferred
  finalize + persuasion gate. Pure gate BUILT+tested: `CaptorReleasePolicy.Resolve(mercy, grudge, isNemesisVendetta)`
  -> `{AllowsPersuasion, AllowsRansom}` (vendetta grudge >= 40 or nemesis refuses both; else ransom always, persuasion
  if Mercy >= 1 OR light grudge < 15). **🔨 ENGINE WIRING BUILT 2026-07-05 (untested in game, 1241 tests, mod 0 err),
  persuasion path:** SDK `EncounterContext.CaptorAllowsPersuasionRelease/RansomRelease` (additive) +
  `PromptBuilder.AppendCaptorReleaseRule` teaches `release_player` to the captor (shared across bandit-menace /
  lord-reckoning / CNC captive scenes, only when the gate allows); `EncounterContextBuilder.ResolveCaptorReleaseVerdict`
  computes the flags via `CaptorReleasePolicy` when `PlayerStatus == Captive` (Mercy off the captor Hero, heaviest
  live grudge vs player off `GrudgeBehavior.GrudgesFor`); bridge `CommitCaptorRelease(captor)` re-validates the gate
  and sets a deferred `_pendingRelease`, `FinalizePendingRelease` applies `EndCaptivityAction.ApplyByReleasedByChoice(MainHero, captor)`
  at scene close (mirrors `_pendingEscape`); `ChatViewModel` detects `release_player` -> `ResolveCaptorRelease`
  (renders the parting line, commits, concludes the scene) and calls `FinalizePendingRelease` in `FinalizeCaptiveSceneMemory`.
  **OPEN follow-up: the RANSOM-in-gold path** (the `ransom: N` amount + gold deduction) is deferred; the flag is
  computed but this increment teaches/handles PERSUASION release only. Needs in-game validation.

**CAPTIVITY ESCAPE BALANCE (analysed + ratified 2026-07-05).** Full escape surface in Hardcore: (a) scene
`escape_attempt` = flat 10% per attempt (`EscapeChance = 0.10`), accumulating over scenes (1-0.9^N: ~27% by 3
scenes, ~65% by 10); (b) vanilla spontaneous escape = ~30% of native rolls survive (`VanillaCaptivityPatch`
suppresses 70% in Hardcore), a recurring drip that tends to near-certain over a long captivity ("time frees you");
(c) vanilla ransom = fully OFF in Hardcore; (d) negotiated release (above) = near-at-will for a disposed captor, 0
for a vendetta/nemesis; (e) rescue/battle = situational. NET: ordinary captivity is easily escapable (fine); a
NEMESIS could still be waited out via the vanilla drip. GABRIEL'S RULING: a nemesis keeps SOME escape chance (never
a perfect cage), but it DECAYS with each recapture as he grows more careful, and he says so in-fiction ("you slipped
me once, not again"). Pure `NemesisEscapeChancePolicy.EscapeChance(recaptureCount)` BUILT+tested (base 10%,
-2.5%/recapture, floor 2%, never 0). Engine wiring (nemesis increment): fully suppress the vanilla drip for a
nemesis captor (so the decaying scene-escape is the only luck exit), source `recaptureCount` from a new
`NemesisIdentity` field, feed it to the scene-escape roll, and voice a "not this time" beat when count > 0.

- **NPCs unaware of VANILLA quests they issued — 🔨 FIXED 2026-07-05 (untested in game, mod 0 err).** New additive
  `EncounterContext.NativeQuestNote`, built in `EncounterContextBuilder.ResolveNativeQuestNote(hero)`: scans
  `Campaign.Current.QuestManager.Quests` for ONGOING quests whose `QuestGiver == this NPC`, EXCLUDING CR's own
  mirrors (`CalradiaRemembersQuest`, `CommitmentQuest`) so nothing double-surfaces, and formats a note ("TASKS FROM
  THE REALM'S AFFAIRS YOU SET THIS PLAYER ... speak as the one who set it, never baffled"). `PromptBuilder.AppendNativeQuestNote`
  renders it, gated non-Captive alongside `AppendActiveQuests` (same guard that stops a bandit captor mistaking a
  quest for one they gave). The giver now acknowledges their own native quest.
- **Rival gang-leaders framed as friends-by-culture — 🔨 FIXED 2026-07-05 (untested, 1244 tests, mod 0 err).** Pure
  `ThirdPartyDescriptor.Categorize` gained an `areRivalGangLeaders` param (defaulted, so no caller churn): after
  kinship, two same-turf gang leaders are labelled "a rival gang leader (a competitor for the same turf, not a
  friend)", overriding the friendly/neutral tiers (a genuine ENEMY relation is stronger and kept). +3 tests.
  `ContextualNameResolver.AreRivalGangLeaders` computes it (both `Occupation.GangLeader`, same `CurrentSettlement`,
  different heroes). The NPC is now told the rivalry instead of hallucinating a tribal friendship; the wrong −8
  `change_relation` for offering to move against a rival should self-correct (hurting a named RIVAL reads as a
  favour). If the −8 persists in game, the `change_relation` vocabulary is the follow-up lever.
- **Alley-placed companion gives generic vanilla replies (#5) — ALREADY HOOKED, likely already fixed.** `LlmDialogFlow.EntryHubTokens`
  already includes `alley_player_owned_start` alongside `hero_main_options`, added (with a comment citing a prior
  player report) precisely because the alley-manager conversation branches from "start" and never passes through
  `hero_main_options`, so CR's "A word with you." was missing there. So SuperGraham's report most likely predates
  this fix; needs in-game RE-VALIDATION on the current build. The "maybe more broadly impacting" residual = if any
  OTHER conversation context surfaces unhooked, add its hub token to `EntryHubTokens` (the clean extension point).

**PILLAR: Nemesis — the persistent named bandit boss (ratified 2026-07-05).** Root cause of "Ranulf the Faceless
Fox keeps his NAME but has no memory (`cr.memory`/`cr.info` empty)": the bandit captor is a transient troop, not a
Hero, so no `NpcProfile` attaches. FIX = whenever ANY bandits take the player
prisoner (NOT only the town-ambush arc — every bandit capture), the chief is a PERSISTENT MINOR-LORD Hero instead
of a jetable troop — a Hero has a profile, so memory/grudges persist for free and he becomes a foe you can hunt,
owe, and kill. The boss belongs to the CAPTURING bandit faction (Looters / Forest Bandits / Mountain Bandits /
Sea Raiders / Desert Bandits / Steppe Bandits, per the vanilla bandit clans), so his culture/flavour matches who
took you. Shape RATIFIED = a REGIONAL RECURRING ROSTER keyed by faction+region: on capture, if a living nemesis
of that band exists in the region he IS the captor (you recross Ranulf); else mint a new one; killing him fires a
world-event and he does not return. Increment 1 = just the persistent captor Hero + memory (fixes the bug, unlocks
the rest), fired from the general bandit-capture hook; the town-ambush arc's existing "elite chief of the
paymaster's culture" becomes one caller of the same machinery, not the only trigger. Later
increments ABSORB the player's own wishlist: owe-money-to-the-boss -> collector party comes (courier-like), be
delinquent -> his men hunt you, kill him -> head on a pike + world-event on "the fate of those who cross me";
also folds in rival lords sending hit teams / notables sending thugs. Ties to captivity arc, stance-with-teeth
(town ambush / Intercept / Assassinate), world-events, and interop with the "named bandits" mod (Nexus 10376) the
player cited. See [[project-ambush-captivity]], [[project-stance-system]].

**MAP-PRESENCE MECHANIC, ACTIVE HUNT: 🔨 BUILT 2026-07-08 (untested in game).** The materialization is live:
`NemesisBehavior` brackets a player battle (`MapEventStarted`/`MapEventEnded`); when the player engages a bandit
band of a living, latent, non-turned nemesis's culture near his region anchor (`FindHuntableNemesisNear`, culture
match plus `NemesisMaterializePolicy.WithinRegion` proximity, or culture-only when his region is not a real
settlement), the ratified PITY-TIMER `NemesisRevealPolicy.ShouldReveal(EncountersSinceReveal, roll)` (15% rising
to a certainty after 6 qualifying misses) decides; on a reveal `TryMaterializeInto` mint-or-reuses his Hero and
`AddHeroToPartyAction`s him INTO that band with a screen notice ("your hunt has found him"), a miss just builds
pity credit. He fights and can be killed (existing `NemesisResolutionBehavior.OnHeroKilled` closes the arc) or
captured (existing prisoner dialog). On MapEventEnded, if he survived un-captured he is `DetachToLatent`'d
(removed from the band's roster) so he keeps NO standing party. **No nemesis exists until a real CAPTURE mints one
(`OnHeroPrisonerTaken`), so merely defeating bandits never spawns a hunt** (Gabriel's point A, already satisfied).
**RESPAWN COOLDOWN (Gabriel 2026-07-08): killing a nemesis rolls `RespawnBlockedUntilDay` = death + 1..3 Calradian
years; `NemesisRosterPolicy.IsCultureRespawnBlocked` then blocks a fresh mint of that CULTURE (`FindOrMint` returns
null, the same bandits take the player as a faceless captor until the cooldown passes).** `nemesis_hunt`
self-test; unit tests for `WithinRegion`, `IsCultureRespawnBlocked`, and the existing `NemesisRevealPolicy`. STILL
OPEN (later): the rumour/world-event "he still raids near X" nudge layer between encounters; region-breadth tuning.
Original ratified design below.

**MAP-PRESENCE MECHANIC — RATIFIED 2026-07-05 (Gabriel), resolves the persistence-vs-pursuit tension.** THE
TENSION: for it to be an adventure the boss must survive the vanilla world's constant bandit-clearing (a random
lord must not erase your nemesis before you can hunt him), yet the player wants to PHYSICALLY pursue and kill him.
A standing map party can't do both (AI-destructible vs chaseable). RESOLUTION (Gabriel's, cleaner than a
dormant/summon or protected-party approach): **the nemesis has NO persistent party on the map at all — he lives in
RUMOURS and world-events (a latent identity: Hero + faction-band + capture-region affinity).** He MATERIALIZES
only at an encounter: when the player engages a bandit party OF HIS BAND'S FACTION, IN THE REGION where that
player was captured, there is a RANDOM CHANCE that party turns out to be led by the nemesis. So the hunt is
EMERGENT and diegetic: the player goes bandit-clearing in his territory and, after some empty bands, eventually
runs into him leading one. Consequences: no standing party ⇒ nothing for the AI to attrit ⇒ persistence is free
and hack-free; his death is PLAYER-GATED by construction (he only ever exists in a fight the player chose to
start); killing/capturing him at that encounter resolves the arc (world-event, head on a pike, or the player
becomes HIS captor). Design knobs to lock at build time: (a) the attach-chance and its PACING (rising / pity-timer
so he is guaranteed eventually, per "intercepte plusieurs bandes sans le trouver, mais à un moment donné il tombe
sur lui"); (b) region breadth (exact capture region vs a wider area) and whether player-initiated only or also
when bandits engage the player; (c) the TECHNICAL SPIKE — attaching the persistent nemesis Hero as the LEADER of
an ordinary vanilla bandit party at encounter time so he really fights, can be captured, and can be killed. The
rumour/world-event layer keeps him present between encounters ("the Faceless Fox still raids near X, and still
means to collect what you owe") and can nudge the player toward his region; the debt/collector and "his men hunt
you" wishes ride on top as world-events, no standing party required.

**BOTH ENCOUNTER DIRECTIONS ARE CHAT-FIRST + SYMMETRIC (Gabriel 2026-07-05):** the nemesis identity attaches to a
qualifying (faction + capture-region) vanilla bandit party in EITHER outcome, and both open in CR chat, not a raw
battle. (1) WIN/INTERCEPT: when the player engages such a party and the roll reveals the nemesis, the encounter
OPENS WITH A CONVERSATION with him (reuses the map-conversation entry), confront him, settle the debt, taunt,
before it breaks to a fight / capture / kill (and if the player LOSES that fight, he naturally takes the player,
looping to direction 2). (2) LOSE/CAPTURED: if a matching-faction band in the capture region defeats and captures
the player, the nemesis IS the captor and the captive scene runs with HIM, and because he is a persistent Hero
with a profile he REMEMBERS the player, the debt, and the prior scene (exactly the "Ranulf keeps his name but not
his memory" bug fixed by construction). The capture direction needs NO bespoke scene code (Gabriel 2026-07-05):
it is the EXISTING captivity event verbatim, and the captor's history with the player injects ITSELF through the
standard prompt-history path (`AppendHistory` over the captor's `NotableEvents`) purely because the captor is now
the persistent nemesis Hero rather than a nameless troop. The ONLY requirement is the attach mechanic making him
the captor Hero; the memory, the debt recall, the "what we did to each other last time" all come for free. One
machinery (attach the persistent nemesis Hero as the vanilla band's leader), two chat-first faces: hunter and
hunted swap places across encounters.

**ENGINE-FEASIBILITY SPIKE (read-only, 2026-07-05): VERDICT = BUILDABLE-WITH-CAVEATS.** The load-bearing mechanic
is ALREADY SHIPPING CODE: `AmbushCaptivityBehavior.SpawnCaptors` (`Stance\AmbushCaptivityBehavior.cs:683-739`)
already mints a real persistent bandit-faction Hero (`HeroCreator.CreateSpecialHero` from a bandit template into
`Clan.BanditFactions` "looters"), makes him the party `LeaderHero` (`LordPartyComponent.CreateLordParty`), fills
the band with vanilla looter troops, and round-trips his StringId across save/load via `SyncData` (`_chiefId`).
Today it deliberately KILLS the chief in `ClearConvoy` (`:234-237`, "so we don't leak a hero into the world").
THE NEMESIS IS LITERALLY THAT CODE MINUS THE KILL, PLUS IDENTITY REUSE. Seam map:
- **Captor-side chat-first = FREE.** `ChatManager.OpenForCaptiveEncounter(captorHero, profile,…)` (`Chat\ChatManager.cs:141-184`)
  already opens the captive scene with a real Hero speaking first (called from `AmbushCaptivityBehavior.TryOpenLordAudience`);
  `CaptiveEncounterService.cs:186` resolves the captor as `captorParty.LeaderHero` and routes a hero-led band to the
  PROFILED-Hero branch (not the synthetic-brigand branch), so the boss's memory flows via `AppendHistory` with zero
  new scene code. A persistent-Hero captor can also be forced virtually via the `ArrivedPaymasterFor` override pattern
  (`:84-93`).
- **Profile/memory = FREE.** `HeroProfileMapper.FromHero(boss)` + `store.Set` (`Mapping\HeroProfileMapper.cs:18-44`,
  `Storage\BannerlordNpcMemoryStore.cs:37,53`) gives any Hero a persisted profile; `CaptiveEncounterService` already
  mints a captor lord's profile on first capture. `cr.memory`/`cr.info` then work on him identically to a lord.
- **THE ONE NEW SEAM = WIN/intercept chat-first against a bandit party.** Vanilla routes bandit map-contact to the
  attack/leave encounter menu, not `hero_main_options`, and CR's interception explicitly EXCLUDES bandits
  (`InterceptionBehavior.cs:307`). Build it by intercepting the bandit encounter menu (`GameMenuOpened` handler like
  `AmbushCaptivityBehavior.cs:354-388`, or a Harmony `GameMenu.SwitchToMenu` prefix like `Letters\VanillaCaptivityPatch.cs:36-78`)
  and opening `ChatManager` directly pre-battle, then dismissing the encounter cleanly via
  `ChatManager.EndMapEncounterIfNeeded` (`:330-345`). Medium effort, solid precedent, no blocker.
TOP RISKS to guard at build time: (1) do NOT leak the persistent bandit Hero into vanilla bandit-boss/ransom/notable
systems (despawn his PARTY between encounters, keep the HERO alive for re-attach, keep him out of native reaping);
(2) save/load must re-attach the SAME Hero to a NEW regional band next encounter (needs the roster SyncData behaviour +
the pure `NemesisRosterPolicy`/`NemesisRevealPolicy`); (3) pre-battle interception of a hostile party is where the
historical army-encounter save-corruption crashes lived, so reuse the `PlayerIsSubordinateInArmy` /
`EndMapEncounterIfNeeded` guards. Store the boss's `Hero.StringId` on `NemesisIdentity` so the identity survives while
his party is despawned.

**INCREMENT-1 HOOK GAP (Gabriel 2026-07-05):** the persistent bandit chief is ALREADY created for the scripted
town-AMBUSH path (`SpawnCaptors`), but NOT for the case the design actually centres on: the player LOSING a
VANILLA map encounter to a bandit party and being taken prisoner the ordinary way. That plain-defeat capture is the
NEW hook increment 1 must add: when a bandit party defeats+captures the player, run the roster/reveal roll and, on a
hit, attach the persistent nemesis Hero as the captor (mint-or-reuse per faction+region) so CR's existing captivity
scene runs with HIM and his memory. So the two entry points are: (a) ambush arc = already builds a chief, just make
it persistent + identity-backed; (b) vanilla lost fight = the genuinely new capture hook. Both then converge on the
same attach-Hero-as-captor machinery.

**EXPANDED VISION (Gabriel brainstorm 2026-07-05): the vendetta has FOUR resolutions + bounty missions, nearly all
FREE because the boss is a real Hero.** This may be the FEDERATING pillar: it pulls in captivity, stance, grudges,
world-events, quests, and captor-scenes at once.
- **Battlefield:** he is NOT a separate elite troop, HE is the elite, a named Hero agent leading the band; equip and
  skill him as a real threat with a solid escort. (Loadout is TUNE.)
- **Resolution 1 KILL in battle** -> Hero dies -> world-event. Nearly free.
- **Resolution 2 EXECUTE:** capture him (he is a Hero, so vanilla take-prisoner works) then the vanilla behead option
  -> head on a pike + world-event. Nearly free (verify the execute option surfaces on a bandit-faction hero).
- **Resolution 3 IMPRISON + TORTURE in chat:** the INVERSION, the player is now the captor, reusing CR's existing
  captor-side captive scenes, and loaded with his full vendetta memory ("remember my finger?"). Nearly free.
- **Resolution 4 SPARE + TURN (recruit):** a Stealth/Rogue fantasy, beat him then spare him, his stance turns
  (Respect/Fear up, grudge resolved) and he can be recruited as a companion. The DEEPEST branch, the only
  not-free one: needs a HARD stance/grudge-gated turn path (reuses the just-built stance mechanical layer) + a
  bandit->clan faction conversion. Design the turn gate before building.
- **Bounty MISSIONS (the world hook):** a lord tasks the player to capture or execute the nemesis, verified by the
  real capture/execution event; reuses the quest/negotiation framework + world-events. Ties to Extension Surface.
SEQUENCING: increment 1 (persistent Hero captor + memory + attach + chat-first + escape-decay) unlocks the core;
then RESOLUTION (kill/execute/torture, nearly free); then bounty missions; then the TURN branch (most gated, on
stance). Each layer is standalone and shippable. See [[project-nemesis]].

**INCREMENT 1 🔨 CODE-COMPLETE 2026-07-05 (committed, NEEDS IN-GAME VALIDATION, mod 0 err / 1244 tests):**
- **Persistence** — `NemesisBehavior : CampaignBehaviorBase` + `NemesisRoster`: save-safe byte[] JSON roster
  (additive key, degrades to empty, mirrors GrudgeBehavior), registered in the submodule. `FindOrMint` = the
  regional recurring roster (reuse the living nemesis of a band+region, else mint a latent identity via
  `NemesisNameGenerator`).
- **Capture hook** — `OnHeroPrisonerTaken`: when a bandit party takes the PLAYER, reuse/mint the nemesis, mint or
  reuse his persistent bandit-faction Hero (the proven `SpawnCaptors` pattern MINUS the throwaway kill), name him
  after the identity, register a memory profile (so `cr.memory`/`cr.info` work on him = the "Ranulf has no memory"
  bug fixed by construction), and record him as the active captor. `RecaptureCount` bumps on re-capture.
- **Captor override** — `CaptiveEncounterService` now resolves `captor = ambushPaymaster ?? nemesisCaptor ?? LeaderHero`,
  so the captive scene runs with the nemesis Hero and his memory (the profiled-lord CNC path).
- **Escape-decay** — the scene-escape roll uses `NemesisEscapeChancePolicy` (decays with `RecaptureCount`) against a
  nemesis; `VanillaCaptivityPatch` fully suppresses the vanilla escape drip while a nemesis holds the player
  (`PlayerHeldByNemesis`), so you cannot wait him out; never a perfect cage (2% floor).
- Defensive throughout (degrades to the vanilla bandit scene on any failure). Commits: persistence, FindOrMint,
  capture hook, escape-decay. **STILL OPEN (polish):** the "not this time" prompt beat when RecaptureCount > 0
  (needs an EncounterContext note); the physical band-leadership vs the virtual override (chose the override,
  lower-risk); band-culture/region keys are coarse first-pass (TUNE); the WIN/intercept chat-first side (the
  spike's one new seam) is a later increment. **The whole flow needs an in-game play-through to validate.**

**INCREMENT 2 (R2 execute / R3 torture entry point), REDIRECTED 2026-07-06, mod builds 0 warning, 1271 tests:**
a first pass built a recurring daily-tick popup menu (Torture / Execute / Leave) for a nemesis held prisoner.
Gabriel redirected: no clickable menu. Instead the player takes the initiative from the party-screen prisoner
"Talk" action, the same `hero_main_options` hub any hero conversation reaches (vanilla already lets you converse
with a hero prisoner you hold; no engine block to work around). `NemesisPrisonerDialogFlow` adds one extra line
there, shown only when the conversation partner is the tracked nemesis `NemesisBehavior.PlayerHeldNemesisPrisoner()`
returns, opening a PLAYER-AS-CAPTOR chat (`ChatManager.OpenForCaptorScene`, rides the live conversation scene
already open, VRAM-safe) instead of an ordinary chat. From there it is a CHAT: the player describes what they do
(interrogate, threaten, torture, spare, execute), and the LLM detects `execute_prisoner` exactly like every other
CR action. SDK teaches it in `PromptBuilder.AppendPlayerCaptorSceneRules` (generic: "the prisoner you hold", no
nemesis-specific text); the mod bridge (`BannerlordGameStateBridge.ExecuteExecutePrisoner`) re-validates custody
before calling the real `KillCharacterAction.ApplyByExecution`, which `NemesisResolutionBehavior` (closure deed for
a tracked nemesis) and `ExecutedKinGrudgeBehavior` (a real lord's kin vendetta) both already react to, unchanged.
Removed: `NemesisCaptivityPromptBehavior`, `NemesisCaptivityPromptPolicy` (+ its tests), the `cr.nemesis_captivity_prompt`
command and self-test. Console: `cr.nemesis_captor_scene` (unchanged, staged), `cr.execute_prisoner [name]` (new).
Self-test: `nemesis_execute_prisoner` (replaces `nemesis_captivity_prompt`, now exercises the real bridge action).
**Untested in game.**

**INCREMENT 3 (bounty missions, the world hook) 2026-07-06, mod builds 0 warning, 1291 tests:** a lord tasks
the player to hunt down a specific active nemesis for a reward, anchoring the personal vendetta in the wider
world. Reuses the quest framework end to end rather than adding new structure: a new `QuestType.NemesisBounty`
(appended last, save-safe) built and issued directly (never via `[QUEST]`/the LLM, since the target and reward
are already fixed, deterministic facts by the time the offer shows) through `QuestService.IssueNemesisBounty`,
journaled through the existing `CalradiaRemembersQuest` mirror exactly like any other quest. OFFER: a new
`NemesisBountyDialogFlow` registers on the same `hero_main_options` hub as `LlmDialogFlow`/`NemesisPrisonerDialogFlow`;
gated by `NemesisBountyPolicy` (pure, tested) on the PRIMARY rule only, the issuing lord's own region (home
settlement, else current settlement, else culture as a last resort) must match the nemesis's `RegionId` AND he
must be a living, already-manifested (Hero-bound) foe AND no bounty on him is already active anywhere
(`QuestService.HasActiveBountyFor`, store-wide). The optional "past victim" rule (a lord this nemesis has
personally wronged may also offer, regardless of region) is FLAGGED, not built: CR has no persisted record of
which lords a given nemesis has preyed upon. Accepting shows an accept/decline `InquiryData` naming him and the
reward; declining leaves the offer available again later (nothing is marked "declined"). VERIFIED BY THE REAL
DEED, never roleplay: `QuestDeedTracker`'s two existing hooks (`OnHeroPrisonerTaken`, `OnHeroKilled`) were simply
WIDENED to also recognize `QuestType.NemesisBounty` alongside their ordinary `CapturePrisoner`/`ExecuteEnemy`
cases, so a capture OR a kill (battle death or execution alike) by the player satisfies the same quest,
whichever comes first; `NemesisResolutionBehavior`'s own vendetta-closure listener on the same `HeroKilledEvent`
is untouched and fires independently. REWARD: `NemesisBountyRewardPolicy` (pure, tested) scales gold and
personal relation by his notoriety (`RecaptureCount`, `TimesEncountered`), both clamped to a sane ceiling; paid
on report-back to the issuing lord exactly like any other quest (bypasses the RelationGate cooldown/cap, same
as every other verified quest reward). No new persisted state: the bounty lives entirely inside the giver's
existing `ActiveQuests` list (additive enum value only). Console: `cr.nemesis_bounty [lord name]` (force-offers
and accepts, minting/reusing a nemesis for real in the lord's own region so the real gate and reward math both
run), `cr.nemesis_bounty_complete [giver name]` (force-completes and pays). Self-tests:
`nemesis_bounty_capture`, `nemesis_bounty_execute` (both mint a nemesis anchored to a real lord's region, issue
through the real path, then satisfy the quest through a REAL `TakePrisonerAction`/`execute_prisoner` bridge
call, not a simulated stamp). **Untested in game.**

**PILLAR: Player appearance — derived + editable (ratified 2026-07-05).** Kills the backstory/appearance
hallucination AND resolves the mutilation-persistence tension (a hallucinated "missing finger" must never
auto-persist; the player AUTHORS it if they want). Two volets: (1) DERIVE the reliably-available attributes from
`BodyProperties` (height, build/weight, age, gender, hair colour/style; NOTE eye colour and fine proportions are
baked in the packed face code and not reliably readable, so skip those); (2) an EDITABLE per-hero prose descriptor
in the Encyclopedia ("a mysterious air, moves with grace, limps on the left leg, missing a finger"), save-persisted
so it follows the heir on a character change. Together they render a "THE PLAYER'S APPEARANCE" prompt block and
RETIRE `player_description.txt` (replaced by the in-game field). Extends [[project-player-authored-characters]].
**Pure composer 🔨 BUILT 2026-07-05 (overnight, untested, uncommitted):** `AppearanceComposer.Compose(facts, authoredProse, voice)`
+ `AppearanceFacts` carrier, bands the 0..1 body sliders + age into phrases, omits unknowns, appends authored prose,
returns "" when nothing stands out. Banding thresholds are const/flagged TUNE (height 0.35/0.65, build 0.3/0.6/0.85,
age 25/40/60). **FRAMING (Gabriel 2026-07-05): the appearance is read BY the speaking NPC, so voice matters.** Now
GENERALIZED to serve BOTH targets via `AppearanceVoice` (Gabriel ratified scope = player + speaking NPC, witnesses
deferred): `Subject` (default, THIRD person, "A tall, powerfully built man in his middle years, with dark hair.") for
the PLAYER and any other described hero, rendered under a naming heading, never "you"; `Self` (SECOND person, "You are
a tall... man in your middle years, with dark hair.") for the SPEAKING NPC's OWN body, so it grounds its self-image
and never invents contradictory looks. Renamed `PlayerAppearanceComposer` -> `AppearanceComposer`. 29 tests.
**✅ SHIPPED v1.26.0 (2026-07-07), wired end to end:** `AppearanceFactsExtractor` reads the live hero (gender, age,
build/weight off `BodyProperties`); `FaceColorReader` resolves HAIR + SKIN to a real RGB (face-key offsets via
`MBBodyProperties.GetParamsFromKey`, sampled on that race/gender/age's colour gradient) and classifies them into
words; EYE colour is banded HEURISTICALLY from its raw offset (the engine exposes NO eye palette, verified by
reflection; `cr.appearance` prints raw offsets for calibration, bands are TUNE). `AppearanceProseBehavior` persists
the authored per-hero prose (byte[] JSON, save-safe); `EncounterContextBuilder` fills `PlayerAppearance` (Subject)
+ `NpcSelfAppearance` (Self); PromptBuilder renders both plus the bandit-captor "what you can SEE" line. Console
`cr.appearance [clear|<text>]` is the interim edit surface; `appearance` self-test. **Editable Encyclopedia field
✅ SHIPPED + validated in-game 2026-07-08:** on the PLAYER'S OWN hero page (`EncyclopediaHeroPageAppearanceMixin` +
`HeroPageAppearancePatch` + `CR_HeroAppearanceButton.xml`) a "How others see you" line shows the full composed look
(derived + note) and an "Add more info" button opens a **custom Gauntlet popup** (`AppearanceEditorManager` /
`AppearanceEditorViewModel` / `CalradiaRemembersAppearanceEditor.xml`, mirroring ChronicleManager) with a title,
explanation, a single-line field, and a LIVE wrapped preview of the final line; Confirm saves to
`AppearanceProseBehavior`. PLAYER-ONLY on purpose (an NPC note would orphan if they left the clan). The custom
popup (not `ShowTextInquiry`) fixes the Backspace-navigates-BACK bug and allows a larger box; see
[[feedback-bannerlord-ui-patterns]]. `cr.appearance` kept as the quick surface. STILL OPEN: height via
`GetScaleFromKey` (readable, not yet banded); retiring the legacy `player_description.txt` (still cohabits).

**Notes / no-action:** the captive-scene violence (stabbing, finger) LANDS and he wants MORE explicit interpersonal
scenes (a sensitivity toggle is a maybe, not a restriction); "it is easy to overpower the AI" but he PREFERS
agency over any restriction (do NOT add guardrails that curb RP agency); raw stream-of-consciousness feedback IS
welcome (a light bug/wish/note structure would help even more).

### Town ambush — works end-to-end behind the toggle; refining *(stance increment 3; still shipped OFF)*

The Assassinate stance consequence (a lord whose hatred turned murderous) springs a Warband-style STREET FIGHT
when the player walks into a town. **WORKS END-TO-END IN GAME 2026-06-29** (validated by Gabriel): the bandits
surround and attack, a lost fight convoys the player to the paymaster for the reckoning audience, and the
paymaster then visits the prisoner for recurring captivity scenes. It still ships **OFF by default** behind the
experimental MCM "Town Ambush" toggle while the last refinements land (shipped in v1.17.4 in that state).

History: the first build opened a roster alley fight, which the engine framed as a SIEGE with the attackers
spawning ~300m off at the walls. Rearchitected 2026-06-29 (Warlord / War-Sails-style): drop the player into the
real town-CENTRE walkable scene (`PlayerEncounter.LocationEncounter.CreateAndOpenMissionController("center")`),
inject the cutthroats as hostile `LocationCharacter`s at the townsfolk spawn points
(`LocationCharactersAreReadyToSpawnEvent` + `Location.AddLocationCharacters` with `CharacterRelations.Enemy`), and
start a real fight from a **Harmony postfix on `Mission.AfterStart`** (the one reliable "fully started" seam) via
`MissionFightHandler.StartCustomFight`. Both prior crashes (PatrolAgentBehavior NRE; "Collection was modified" from
adding a behavior mid-`AfterStart`) are resolved.

DONE (validated): bandits spawn near the player and attack (real-time poll + `MissionFightHandler.StartCustomFight`,
the no-aggressors fizzle now logs loudly); the gating decision is a pure tested `AmbushPolicy.DecidePlayerTownEntry`
(the trigger path used to be silent — armed-but-disabled now logs); navmesh-aware ~20m cutthroat placement; player
teleported to the walkable-area centroid (town centre, not the gate); captivity map fast-forwards at 3x after the
audience (one-shot un-pause in the real-time tick); the paymaster runs the recurring captivity scenes even when
custody stays with the band; all captive scenes use a neutral player-POV narrator.

OPEN — the refinements before removing the toggle:
1. **Custody transfer to the lord** when he sits in his town: `TakePrisonerAction` on his in-settlement party does
   not "hold" (`held=False`), so the player stays the band's prisoner (harmless now that the paymaster visits
   regardless, but the proper lord-imprisonment / ransom disposition is unfinished). **Decided 2026-06-30: SHIP the
   band-custody fallback as the intended disposition for a town-sitting lord (a hired band holds you until ransom /
   escape) — not a blocker.**
2. ~~**Fleeing townsfolk**~~ **MOOT + REPLACED — DONE 2026-06-30.** The brawl plays at the GATE (outside the walls),
   so there is no townsfolk crowd — but there are a gate guard or two. `AmbushFightMissionLogic.SendBystandersFleeing`
   now scatters them when the fight starts: any human agent in the scene who is not a combatant (guard / stray
   civilian) is made **invulnerable** (a slain guard = a crime / odd mission end), sent **running away from the
   player** on the navmesh (reusing the file's navmesh helpers), then **faded out** ~2.5s later. UNTESTED in game.
3. Then **remove the toggle** — the last step, gated on Gabriel **playtesting the 2026-06-30 refinement lot in game**
   (force scaling / elite chief / companions-captured are BUILT but UNTESTED). Removal = `CrSettings.TownAmbushEnabled`
   + `ModSettings.TownAmbushEnabled` + the two gates in `TownAmbushBehavior` (l.118 `IsEntryBlocked`, l.165
   `DecidePlayerTownEntry`).
Console `cr.ambush`.

### Jealousy — the competition loop and the duel *(v1 shipped; escalation deferred)*

v1 fires a one-shot reaction (staked NPC → grudge + grievance memory + JealousThreat letter, witness-gated,
culture-flavoured). **Plausibility gate SHIPPED v1.29.1** (player report: a male player did a male lord a service
and the lord's wife sent threats): pure `JealousyTriggerPolicy`, a same-sex pair is never a rival courtship
(matches the opposite-sex romance/marriage model), a flirt stamped on a quest turn-in is mislabeled gratitude
(ignored), spoken flirtation needs a repeated pattern (>= 2 flirt/intimacy events); intimacy counts at once for a
plausible pair. Deferred layers:
- **WISHED (Gabriel 2026-07-09): the SUSPICIOUS SPOUSE — repeated favors read as courtship.** A player who
  repeatedly does services for an opposite-sex married lord could rouse the spouse's SUSPICION even with no flirt
  at all (the wife watching a young champion keep doing her husband favors). A deliberate separate trigger, NOT
  the flirt path: keyed on repeated Collaboration events / high regard with an opposite-sex married NPC, own
  thresholds, likely a milder register (cold words, a warning letter) before real grudges. Design carefully so
  ordinary questing does not spam suspicion (the v1.29.1 report was exactly a false positive on a quest turn-in).
- **Competition loop**: rival suitors who actively and repeatedly contend for the same NPC over time (not a
  single reaction) — courtship as an ongoing contest the player can win or lose.
- **Duel & scheming**: a slighted rival may challenge the player or scheme against them — a real
  confrontation, not just a letter. Pairs with the scripted-scenarios engine (a jealousy threshold as a
  trigger) and the love-triangle tie-in noted under Camp life.

### Marriage — true polygamy and divorce *(monogamous marriage shipped; divorce arc ✅ COMPLETE — Phase 1/2a/2b/escalation, v1.22.1; true polygamy still OPEN)*

**WISHED (player 2026-07-08, note to build LATER): marry a NOTABLE, and on marriage they join your CLAN.** A
player leaving a conflicting mod asked whether CR could "marry notables then make them family or clan members".
Today it CANNOT: `MarriageProspectResolver` draws candidates only from `npc.Clan.Heroes` / the NPC's children, so
a notable (gang leader, merchant, artisan, village headman, `Clan == null`) is never a marriage prospect. Design
notes (Gabriel 2026-07-08): (a) a notable needs **NO family blessing** (there is no clan head to consult, so it is
a direct courtship of the notable themselves, gated only on regard + the notable's own consent, not the
family-approval currency lords require); (b) the value proposition DIFFERS by target and should be surfaced so the
choice matters, **marrying a LORD grants influence/alliance benefits (political allies, a house tie)** while
marrying a notable is a lesser, more personal match (settlement/economic ties, not political power). Build sketch:
a notable-candidate pool for the prospect resolver + prompt block (the notable NPC offers THEMSELVES, no blessing
path), and on marriage promote the notable into the player's clan as a Hero (mirror `AddCompanionAction` /
clan-join, detaching them from their settlement post). A real chantier (notables are not lords: settlement
detachment, clan status, no `OriginClan`), so a separate increment.

**WISHED (player greenarcher13, 2026-07-10, note to build LATER): an OOC (out-of-character) channel to correct the
AI mid-conversation.** The player suggested a way to tell the AI "OOC: you're sending letters that aren't
applicable, please remove all references to them and any relationship modifiers." Genuinely useful as an escape
hatch when the world state drifts, but non-trivial and abuse-prone: it would let the player edit memory/stance
directly through free text, so it needs (a) a distinct parse path (an `OOC:` prefix the LLM never treats as
in-character), (b) a bounded, auditable set of effects (forget a named event, clear a specific pending letter,
nudge a stance back), NOT arbitrary "give me +100 relation", and (c) a world-consistency guard so it corrects
drift without becoming a cheat console. Design before building. Note: the specific case that prompted this (a
married spouse still demanding child support) is fixed deterministically in v1.30.0, so the OOC channel is for the
GENERAL class of AI drift, not that bug. RELATED follow-up: an already-ACTIVE child-support `ProvideGold` quest
whose giver later became the player's spouse is not retroactively cancelled today (only the pending LETTER is
healed); a quest-level heal (close a `ProvideGold` quest when its giver is now `Hero.MainHero.Spouse`) would fully
close greenarcher13's loop for saves where the quest already opened.

**FIXED v1.30.0 (player suggestion, ApocalyptikGreen 2026-07-10): a prisoner cannot deny a private audience.** A
held captive now yields the room by force when the player asks to speak privately (button or free text), rather
than the LLM being allowed to refuse. `ChatViewModel.NpcIsPlayerPrisoner()` gates both the button short-circuit
and the free-text `request_privacy` resolution.

**Divorce arc COMPLETE (shipped v1.22.1):** Phase 1 (persuade an NPC to divorce their NPC spouse), Phase 2a (the
player ends their OWN marriage in chat), Phase 2b (an unhappy spouse DEMANDS a divorce), and the escalation (a
spouse refused too long BEGINS the estrangement themselves, walking out at a deadline unless the player genuinely
repairs the bond, with a save-safe on-load heal). All chat-driven, no button. Still OPEN: TRUE POLYGAMY.

**Player-reported bugfix (2026-07-04 — ✅ SHIPPED v1.22.1), 1116 mod / 242 SDK tests green.** Two live
v1.22.0 bugs: (1) the marriage scene never triggered after an NPC accepted — the eligibility gate checked ONLY the
mod's own personal-regard ledger (`ReputationWithPlayer >= 50`), ignoring vanilla clan relation players had actually
built; broadened both synced gate sites (`EncounterContextBuilder.ResolveLoveMatchEligible` + the bridge's
`ExecuteMarry`) to pass on EITHER, via one shared `MarriageEligibility.MarriageEligibilityThreshold` const, and
added an anti-empty-promise guard in `PromptBuilder` (marriage is family-gated, cannot be settled in one
conversation) for the non-eligible + still-unmarried case, dropped in Lean. (2) after `cr.marry` the NPC still
treated the player as a third-party spouse (infidelity framing, the ≥30 stranger-resistance trust gate) instead of
their own spouse — new additive `EncounterContext.NpcSpouseIsPlayer` renders a distinct marital-intimacy block
(natural, not infidelity, no stranger thresholds) when the NPC's `Hero.Spouse == Hero.MainHero`.

The marriage chain (family-approval, consorts, by-letter, jealousy) is shipped. One genuinely-open extension,
one BUILT:
- **Culture-gated true polygamy** as lore — multiple full spouses, restricted to designated cultures (not the
  current consort workaround), with the family-approval currency scaled accordingly.
- **Divorce, Phase 1 (NPC case) — BUILT 2026-07-03 (Sonnet, untested in game, NOT committed), 755 tests green.**
  A player tried to convince an NPC to leave their spouse; the NPC "agreed" but nothing happened ("soon, soon"),
  the classic empty-promise pattern (no verb, no executor). Design framing (Gabriel, 2026-07-03): **a marriage IS
  an alliance, so a divorce is a loss of influence and power**, and BOTH families oppose it fiercely — never "talk
  once and it is done". Built: `end_marriage` starts a multi-day **estrangement process**
  (`DivorcePolicy.ProcessDurationDays` = 15) the spouse may resist — a daily roll (Honor/Calculating raise it) can
  Delay (push the end day back, capped) or Abort the process outright — rather than an instant flip; reaching the
  end day re-validates every guard and, on success, dissolves the marriage for real (`MarriageDissolutionService`)
  and records a **culture-scaled scandal** world-event (Nordic/Sturgian 0.5x, default 1.0x) plus relation hits (the
  ex-spouse toward the NPC and toward the player, and the ex-spouse's own clan toward the player). Gates: the
  marriage must be genuinely unhappy (relation ≤ -5) and the bond with the player deep (≥ 30); refuses while either
  party is pregnant or a prisoner, or while the spouse who would have to relocate clans heads their own clan, leads
  a party, or governs a settlement (Phase 1 REFUSES rather than reassigning those roles). **This is also the first
  verb DESIGNED for the new `IConversationVerb` contract** (`CalradiaRemembers.Verbs` / `VerbRegistry`, not merely
  migrated onto it like the four RP-with-teeth court verbs) — validating that the contract makes a brand-new verb a
  clean "implement the interface, register it" addition with no switch statement or prompt-builder method to touch.
  **Technical crux, RESOLVED by an in-game reflection spike on 1.3.15** (the earlier note that `Hero.Spouse` "has no
  public setter" was WRONG): `Hero.Spouse`'s setter IS public and self-cleaning (nulls both sides, appends each to
  the other's `ExSpouses`); clan restore for the spouse who joined the other's clan mirrors `Hero.AfterLoad()`'s own
  fixup ladder (OriginClan, else smallest same-culture clan, else any — never clanless); `Romance`'s internal
  `SetRomanticState` is cleared via cached reflection, non-fatal on failure. No dependency on `DivorceOfCalradia`
  needed. **Phase 2b (the headliner: the unhappy spouse DEMANDS) — BUILT 2026-07-04 (Sonnet, untested in game,
  NOT committed), 1090 mod / 230 SDK tests green.** Answers Gabriel's 2026-07-03 question ("est-ce qu'un NPC
  malheureux peut demander le divorce au joueur?"): the player's OWN spouse, if deeply unhappy, raises the demand
  entirely through conversation, no UI button. Pure gate `SpouseDivorceDemandPolicy.ShouldDemand` (new
  `CalradiaRemembers.Logic`): fires when the relation reads flatly miserable (≤ -20), OR a fresh wound just struck
  (a recent jealousy grudge, or a discovered bastard-scandal world-event) even at a merely moderate relation, OR a
  live grudge against the player already clears a magnitude bar (40) — never while either party is pregnant or a
  prisoner, never while a demand is already pending. `SpouseDivorceDemandBehavior` (weekly tick) evaluates the
  player's current spouse, sets a persisted flag (additive byte[] SyncData, spouseId → day the demand began) and
  queues an unbidden `LetterReason.SpouseDivorceDemand` letter in the spouse's voice ("I can no longer bear this
  marriage"). In conversation, a new `EncounterContext.SpouseDivorceDemandNote` (host-composed, mirroring
  `GrudgeNote`'s field pattern rather than routing through `ExtraActionTeachings`, since it names one NPC's single
  demand, not a set of independently-eligible verbs) teaches `accept_divorce` / `decline_divorce`. Accept dissolves
  the marriage via a new `MarriageDissolutionService.TryDissolvePlayerMarriage` (a thin wrapper over the existing
  `TryDissolve` — "the player never relocates" falls out of the pre-existing clan-leader invariant for free, no new
  guard code needed) with SOFT mutual-consent consequences (a modest relation settling, no scandal-magnitude grudge
  storm — this was the spouse's own wish). REFUSAL is registered two ways: an optional `decline_divorce` action for
  a firm in-character refusal (fires immediately), and the PRIMARY path, a grace-period timeout (12 days) swept
  weekly — a demand nobody ever explicitly answers is read as a refusal too, so the mechanic never depends on the
  LLM choosing to emit a negative action. Either way: a `GrudgeSource.Divorce` grudge plants on the spouse, and the
  flag clears so the demand can resurface fresh later. Console: `cr.spouse_divorce_demand` / `_accept` / `_decline`
  / `_info` / `_tick`; mechanical `spouse_divorce` self-test (marries the player to a test spouse when unmarried,
  exercises accept then a second decline scenario). **Phase 2a (the player PROACTIVELY ends their own marriage) —
  BUILT 2026-07-04 (Sonnet, untested in game, NOT committed), mod 1096 / SDK 234 green.** Also chat-driven, no
  button: talking to their own spouse, the player states plainly they want to end it, and a new
  `EncounterContext.PlayerEndOwnMarriageNote` (taught only when `hero == Hero.MainHero.Spouse`, coexists cleanly
  with a simultaneous Phase-2b demand) teaches `[ACTION] end_own_marriage`. The bridge decides MUTUAL vs REPUDIATED
  via a pure `PlayerDivorceConsentPolicy.SpouseConsents` (mutual iff the spouse had a pending 2b demand OR their
  relation is at/below `DivorcePolicy.UnhappyThreshold` = -5 — Phase 1's exact "unhappy" bar, asked from the other
  side), dissolves IMMEDIATELY via `TryDissolvePlayerMarriage`, and applies consequences through a REFACTORED shared
  `DivorceProcessBehavior.ApplyDivorceConsequences(divorcer, spouse, mutualConsent, ...)` — ONE implementation now
  serves both the Phase-1 NPC-NPC path and this player path (no duplicated family/friends resolution). Repudiated ->
  the spouse + their immediate adult family + their 3 best friends take a `GrudgeSource.Divorce` grudge against the
  PLAYER, spouse relation SET via the direct `ExSpouseNpcHit` (the player is the divorcer, not a third-party
  orchestrator, so no separate `ExSpousePlayerHit`); mutual -> soft. Records a Scandal (repudiated) or Other (mutual)
  world event + chronicle line, clears any pending 2b flag. Console `cr.end_own_marriage`; mechanical `player_divorce`
  self-test (same best-effort seeding-skip as `spouse_divorce`). Judgment call flagged: `ActionOutcome.IsPositive` is
  true for both tiers (the deed succeeded), so a repudiation still cues the positive sound/colour — revisit if the UI
  should mark the wronging as negative. **Escalation (the refused spouse begins ending it themselves) — BUILT
  2026-07-04 (Sonnet, untested in game, NOT committed), mod 1107 / SDK 238 green.** Answers the follow-up Gabriel
  flagged: a spouse refused enough times, still miserable, stops merely demanding and BEGINS an estrangement DESPITE
  the player, on a deadline, rather than waiting forever on their consent. Owner-ratified shape lands SIMPLER than the
  originally-sketched "same resistance process as Phase 1 with the player resisting": no daily Honor/Calculating rolls
  here, just two pure gates in `SpouseDivorceDemandPolicy` — `ShouldEscalate` (refusal count ≥ `RefusalsBeforeEscalation`
  = 2 AND relation still ≤ `DivorcePolicy.UnhappyThreshold`, never while pregnant/prisoner/already-escalating) and
  `IsReconciled` (relation ≥ `ReconciliationThreshold` = 10 — deliberate HYSTERESIS above the misery bar, so a genuine
  warming calls it off, not a nudge that merely clears the misery reading again). `SpouseDivorceDemandBehavior` now
  owns a running per-spouse refusal COUNT alongside the demand flag (both additive byte[] SyncData, a shared
  `SyncDictionary` helper serializes all three spouseId→int dictionaries under their own save keys) and an escalation
  record (spouseId → deadline day, `today + DivorcePolicy.ProcessDurationDays`); `RegisterRefusal` increments the
  count and, when `ShouldEscalate` clears, calls `BeginEstrangement` instead of leaving the demand to resurface —
  clears the demand flag, sets the deadline, queues a `LetterReason.SpouseDivorceEscalation` letter ("I have waited
  long enough"). The weekly tick gained `SweepEscalations`: reconciled (checked BEFORE the deadline, so it always
  wins even on the deadline's own day) calls it off via `ResolveReconciliation` (clears the record + refusal count,
  a relief profile note); deadline passed walks out for real via `ResolveWalkOut`, reusing the SAME
  `MarriageDissolutionService.TryDissolvePlayerMarriage` every other divorce path shares (its own live
  re-validation IS the anti-double-dissolve guard — an already-dissolved marriage just fails here and the record is
  left for a retry next week). Walk-out consequences are deliberately MODERATE (not the tiered family/friends
  storm): ONE `GrudgeSource.Divorce` grudge on the spouse, a Scandal world event + chronicle line framed as the
  SPOUSE's own departure ("weary of waiting... ended their marriage... and departed"), a Betrayal profile note.
  `ExecuteAcceptDivorce`/`ExecuteEndOwnMarriage` (the bridge) now also recognize an active estrangement (not just a
  pending demand) as consent and call `CancelEstrangement` on success. New SDK `EncounterContext.SpouseEstrangementNote`
  (host-composed, mirrors `SpouseDivorceDemandNote`'s field pattern, rendered right after
  `PlayerEndOwnMarriageNote`, dropped in Lean) teaches ONLY `accept_divorce` (no `decline_divorce`: an estrangement
  under way is not something to refuse in words) plus reconciliation-through-deeds framing. Console
  `cr.spouse_divorce_escalate` (+ `_info` now reports ESTRANGING/deadline); mechanical `spouse_estrangement`
  self-test (force-escalate → force the deadline to today → tick → assert dissolved + grudge; second test spouse →
  force-escalate → raise relation above the reconciliation threshold → tick → assert marriage SURVIVES). NOTE:
  Phase 1 explicitly REFUSES the case where the NPC's spouse IS the player (guard + test added 2026-07-03, because
  the romance waiver would otherwise have opened the player's own marriage to end_marriage by accident, a path never
  designed for it) — Phase 2b is the dedicated feature for that case. Still open: letters/journal-mirror surfacing
  the estrangement beyond the initial letter (today's `[SPOUSE_DIVORCE]` log line and the announcing letter are the
  only visible cues, no reminder as the deadline nears); a `chat_spouse_divorce`/`chat_spouse_estrangement` LLM
  self-test (today's are mechanical only, see `cr.testall`); Khuzait cultural nuance research.

### Smaller polish (deferred)
- **Chat composer — a true MULTI-LINE editing box** *(wished 2026-07-01; the "best" option, deferred)*: a player
  asked for a bigger input for long messages. SHIPPED 2026-07-01 was **Option 2** — a "<" button beside the small
  input opens a left panel showing a **wrapped, read-only REVIEW** of the message (a `RichTextWidget`, which wraps,
  in a `ComposeScrollablePanel` so a long message scrolls), updated live; editing stays in the small box on the
  right. **Definitively established that a true multi-line EDITOR is NOT achievable with native widgets:**
  Bannerlord's `EditableTextWidget` is single-line — no multiline/wrap property on the widget or its `Brush`
  (confirmed by reflection), it does not auto-wrap, and Enter does not insert a newline (tested in game). The
  **Editable Encyclopedia** mod (decompiled, un-obfuscated) does NOT have an editable multi-line box either: it
  fakes multi-line for READ-ONLY display via a `WordWrap(text, charsPerLine)` helper (inserts `\n`) rendered by a
  `TextWidget`, and its editor is a single-line enlarged inquiry. So Option 1 needs a **custom widget from
  scratch** (wrap layout + a caret that navigates wrapped lines + click-to-position + selection + scroll + newline
  input) — a sizeable, fragile UI build, hard to iterate (no automated UI tests). Deferred. A cheaper middle
  ground if ever wanted: a **Send-time `WordWrap`** that inserts soft `\n` into the single-line text so the sent
  message (and any later re-open) reads multi-line, without a real editor.
- **One "give" control for items AND gold** *(wished 2026-06-28, BUILT 2026-06-28, validated in game)*: the
  gift-icon button now opens a single "Give" picker with a "Gold" entry pinned atop the inventory items;
  picking Gold prompts for a denar amount. The separate Give Gold button is retired (`IsOfferGoldVisible`
  forced false). `ItemPickerService.ShowItemPicker(onItemSelected, onGoldSelected)`.
- **Grounded "where do I find a companion?"** *(wished + BUILT 2026-06-28, validated in game)*: an NPC asked
  where to recruit was inventing a fictitious wanderer (the "Oros" bug). Now, gated by the pure
  `RecruitIntentDetector`, the host injects ONE REAL hireable wanderer and their town, drawn at random
  (`EncounterContext.KnownWanderers`, `PromptBuilder` A WANDERER FOR HIRE section): a single name is more
  credible than one person knowing every wanderer's comings and goings. Eligible = wanderers in the NPC's own
  town (certain, "in the tavern", preferred) or of the NPC's culture (heard of); the prompt forbids inventing a
  name OR claiming to know of others, and falls back to "taverns of towns" when none are known. Only injected
  when the player actually asks (no token cost otherwise). Console self-test `cr.test chat_recruit_companion`
  (in `cr.testall`): deterministically asserts the gate fired + a wanderer was gathered, then logs the reply to
  judge the grounding. Pure `RecruitIntentDetector` has 5 unit tests.
- **Ambush bandit placement on the navmesh** *(wished + BUILT 2026-06-28, untested in game)*: the old
  `AmbushFightMissionLogic` ring was raw geometry (cos/sin at the player's Z), so thugs could land between walls
  with no path to the player. Now each thug is placed on a point that is BOTH on the navmesh AND reachable: from
  the player's valid `WorldPosition`, `SetVec2` slides the candidate onto the navmesh, then
  `Scene.GetPathDistanceBetweenPositions` confirms a real path (rejecting blocked / disconnected spots and ones
  reachable only by a long detour). A fan of angle offsets + a few radii (ideal ~20m) dodges a wall; a
  toward-the-player navmesh fallback covers the rare total miss. `cr.ambush` to test in game.
- ~~**Fleeing townsfolk during the town ambush**~~ *(wished 2026-06-28)* — **RESHAPED + DONE 2026-06-30 (untested
  in game).** The brawl plays at the GATE, outside the walls (the player spawns there; snapping to the walkable
  centroid dragged them past the gatehouse), so there is no townsfolk crowd to scatter — only the gate guard or
  two. Rather than SPAWN civilians, `AmbushFightMissionLogic.SendBystandersFleeing` makes the guards/civilians
  ALREADY present flee: at fight-start, every human agent that is not a combatant (not the player, an ambusher, or
  an ally) is set `Agent.MortalityState.Invulnerable` (a slain guard = a crime / odd mission end), sent running
  away from the player via `SetScriptedPosition` onto a navmesh point + `SetWatchState(Alarmed)`, then `FadeOut`
  ~2.5s later (`TickBystanderFlee`). No spawn, no team plumbing, no win/lose interference. `cr.ambush` to see it.
- **Configurable LLM response timeout** *(player bug 2026-06-28, BUILT)*: a player on longer messages hit
  "LLM Call Failed in 100008ms: A Task was cancelled" — the HttpClient default 100s timeout firing when a long
  prompt makes a slow/reasoning model think past it. Now the per-request timeout is enforced in the SDK via a
  linked `CancellationTokenSource` (so EVERY caller is bounded), independent of the HttpClient ceiling (raised
  to 15 min so it never pre-empts). New MCM setting "LLM Response Timeout (s)" (15-600, default 120,
  `ModSettings.LlmResponseTimeoutSeconds` → `OpenRouterConfig.TimeoutSecondsProvider`). The opaque "A task was
  canceled" is replaced by a clear, actionable message naming the timeout and the setting. Only a genuine caller
  cancellation (chat closed mid-call) still propagates.
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
