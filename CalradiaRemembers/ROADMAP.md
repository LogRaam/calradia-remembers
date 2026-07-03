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
before the player, or coerces the player to do it (revenge driver). **Phase 2 BUILT 2026-06-30 (console-testable
`cr.companion_scene`, NOT committed); NEXT = auto-fire in CaptiveEncounterService + the revenge consequence (grief +
stance crater + world-event).** Reuses CR's captive-scene chat.
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
