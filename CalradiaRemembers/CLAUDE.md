# Calradia Remembers — CLAUDE.md

Context permanente pour Claude Code. Lire en priorité; réduit les lectures de fichiers par session.

---

## Project layout

Two repos, single deployment:

| | Path |
|---|---|
| **Mod** | `V:\OneDrive\Programmation\GIT Workspace\Bannerlord LLM project\Calradia Remembers\CalradiaRemembers\` |
| **SDK** | `V:\OneDrive\Programmation\GIT Workspace\Bannerlord LLM project\LLM Roleplay Service\NpcMemoryService.Core\` |
| **Bannerlord install** | `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\` |
| **Log** | `%USERPROFILE%\Documents\Mount and Blade II Bannerlord\Logs\CalradiaRemembers.log` |
| **GitHub** | `https://github.com/LogRaam/calradia-remembers` (private) |

Post-build xcopy: `"$(TargetDir)*.dll"` → `<BannerlordInstall>\Modules\CalradiaRemembers\bin\Win64_Shipping_Client\`

Module ID: `CalradiaRemembers`. Targets: `.NET Framework 4.7.2` (mod) / `netstandard2.0` (SDK).

LLM: OpenRouter, model `x-ai/grok-4.20`. API key: env var `OPENROUTER_API_KEY`.

Bannerlord: **v1.4.8 (War Sails)** since 2026-08-10. (This line said v1.3.15 until 2026-09-07; it was wrong.)

---

## Code style (non-negotiable)

- **Indentation**: SDK Core is 3 spaces throughout. The mod, `.Logic` and `.Tests` are MIXED per file (3-space: `EncounterContextBuilder`, `ChatManager`, `JealousyBehavior`, `PlayerDivorceConsentPolicyTests`; 4-space: `CompanionHappinessService`, `MeetingGapDescriber`, `MeetingGapDescriberTests`). **ALWAYS look at the file you are editing and match it. Never assume from the project.**
- **Braces**: Allman style
- **Usings**: wrapped in `#region` / `#endregion` at top of every file
- **Docs**: triple-slash XML (`///`) on all public types and methods
- **Comments**: only when the WHY is non-obvious — never explain what the code does
- **Naming**: PascalCase methods/properties, `_camelCase` private fields
- **C# net472**: no `Nullable enable`, no `record` types; use polyfill `IsExternalInit.cs` for `init` setters
- **File header**: `// Code written by Gabriel Mailhot, DD/MM/YYYY.` (first line, sprint note on 2nd if relevant)

### Unit tests carry their WHY (non-negotiable)

Gabriel reads the unit tests to validate the logic himself. The Gherkin name says WHAT the rule is; it never says why it exists or why it matters. So:

- **Every test file** opens (after the author line) with a header rationale: what this policy decides, what its answer feeds into, what breaks if it is wrong.
- **Every `[Test]`** carries a comment above it giving the **STAKE**: what concretely goes wrong without this rule, which player report or design call it guards, why a boundary sits exactly where it sits. Lead with the stake. Never write "This test verifies that…" (that only paraphrases the name).
- **NEVER INVENT A RATIONALE.** A plausible-but-wrong "why" is worse than no comment: Gabriel would read it and validate false reasoning. Derive it from the production code's own header/`///` docs, from who CONSUMES the result, from CHANGELOG.md or ROADMAP.md. If it is genuinely not derivable, write a plain factual note about what the boundary pins, and say so.

---

## Architecture

```
Player message
    │
ChatViewModel.ExecuteSendMessage
    │
    ├── profile.ClanRelationWithPlayer = bridge.GetPlayerRelation(npc)   [live vanilla clan relation, synced before prompt]
    ├── profile.ReputationWithPlayer is its OWN ledger (PERSONAL regard) — never synced from vanilla;
    │   moved only by change_relation actions / deterministic hooks (RelationGate, StanceService,
    │   GrudgeResolutionService, JealousyBehavior, DivorceProcessBehavior, CommitmentBehavior…)
    │
EncounterContextBuilder.Build(npc, profile, bridge, playerMessage)
    │   └── ContextualNameResolver.Resolve(npc, playerMessage)  ← scans Hero.AllAliveHeroes
    │
NpcChatService.ChatAsync(profile, world, session, playerMessage, context, token)
    │   ├── PromptBuilder.BuildSystemPrompt(profile, world, context)
    │   ├── LlmClient.SendAsync(request)
    │   └── SectionResponseParser.Parse(rawResponse)
    │           ├── [DIALOGUE]   → ParsedResponse.Dialogue
    │           ├── [EVENT]      → ParsedResponse.NewEventData
    │           ├── [REPUTATION] → ParsedResponse.Reputation  (disabled in mod via EnableReputationBlock=false)
    │           ├── [ACTION]     → ParsedResponse.Actions
    │           └── [DISCOVERY]  → ParsedResponse.Discovery   (gated: AdultLevel != Off)
    │
ProfileMutator.Apply(profile, response, gameDay)
    │   ├── Appends event (guard: no duplicate FirstMeeting, no empty summary)
    │   └── Appends DiscoveredTrait (dedup by Key, case-insensitive)
    │
bridge.ExecuteAction(action, npc, ActionContext)
    │   └── change_relation → RelationGate.Resolve → ApplyPlayerRelation (silent) + AddQuickInformation
    │
Store.Set(profile)   ← AFTER actions (persists LastRelationGainHour)
```

Key design decisions:
- `EnableReputationBlock = false` in mod — LLM cannot self-modify relation; uses `change_relation` ACTION
- `EncounterContext` rebuilt on every send (not cached) to reflect in-session events
- Discovery system and discovered-traits injection both gated by `AdultLevel != Off`

---

## Prompt sections (PromptBuilder, in order)

1. NPC identity + persona
2. Traits / behavior guidelines
3. Romantic profile (gated: AdultLevel != Off)
4. Discovered traits — what player already knows (gated: AdultLevel != Off)
5. Relationships (top-5 friends, top-5 enemies, family, liege, faction leader)
6. Cultural traditions
7. Current stance (relation score → natural language)
8. YOUR QUESTS (gated: EnableQuests — outstanding / done-ready / past, with verified evidence)
9. Narrative (world.txt, player_description.txt)
10. Response format instructions
11. Action instructions (change_relation / give_gold / take_gold)
12. Discovery instructions (gated: AdultLevel != Off)
13. Quest instructions (gated: EnableQuests — [QUEST] vocabulary + [QUEST_COMPLETE] rules)
14. CURRENT ENCOUNTER (scene, war status, player status, days since last meeting)
15. NAMES MENTIONED (only when playerMessage contains matching hero name tokens)
16. Conversation history (ChatSession.Messages)

---

## RelationGate policy (CalradiaRemembers.Logic/RelationGate.cs — pure, unit-tested)

| Case | Cap | Cooldown |
|---|---|---|
| Positive (routine) | +1 | 1 gain per 8 game-hours (`CooldownHours=8`) |
| Positive (intimacy event) | +3 | Bypasses cooldown |
| Negative | Uncapped | None |
| Conversational floor | −15 | — |
| Captive floor | −100 | Only when `GetPlayerStatus == NpcIsCaptive` |

`NpcProfile.LastRelationGainHour` persisted. Gate enforced in `BannerlordGameStateBridge.ExecuteAction`.

**Quest rewards bypass the gate** (`GrantQuestReward`): a completed quest is verified by real game events, so its promised relation (≤10, fixed at issue) is applied in full, ignoring cooldown/cap.

---

## Encyclopedia integration (UIExtenderEx)

Inject into `EncyclopediaHeroPage > InfoContainer`:

```csharp
[PrefabExtension("EncyclopediaHeroPage", "descendant::Widget[@Id='InfoContainer']")]
internal sealed class MyPatchA : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Append;
    public override int Index => 0;
    [PrefabExtensionInsertPatch.PrefabExtensionFileName(true)]
    public string FileName => "CR_MyXmlFile";
}
```

**UIExtenderEx ordering**: with two Append patches on the same container, **PatchB runs FIRST** (appears on top), PatchA appears below. Assign label/header to PatchB, data to PatchA.

**Mixin pattern**: `[ViewModelMixin]` on a class inheriting nothing; read `_hero` via reflection (see `EncyclopediaHeroPagePersonalRelationMixin` as reference).

---

## What a test is for (Gabriel's ruling, 2026-09-10, do not relitigate)

> "Les tests ne doivent pas proteger le code, mais doivent proteger les comportements attendus. Un programme
> informatique doit resoudre les problemes du monde et pour s'assurer que le programme le fait vraiment, on met
> des tests pour garantir que les comportements souhaites seront respectes."

A test written by reading the implementation cannot fail on that implementation. It does not protect anything: it
**industrialises the bug**. Kent Beck's order is the point, not a ceremony: the expectation comes from the problem
in the world, and the code is what makes it true.

**The proof, from this repository.** The Regard Warmth Cooldown shipped with `0` meaning "the resistance freezes
and warmth becomes permanently dearer", while the v2.5.1 changelog promised a player that `0` turned the cooldown
off. It had a test. It was green for months. Its name was:

```
GIVEN_decay_disabled_WHEN_resolving_far_later_THEN_the_tier_never_relaxes
```

`decay_disabled` and `the_tier_never_relaxes` describe the branch `if (windowHours <= 0.0) return;`. That name can
only be written with the code open. The name it needed was:

```
GIVEN_the_cooldown_turned_off_WHEN_resolving_THEN_warmth_never_becomes_dearer
```

which can only be written by thinking about the player, and which would have been RED on day one.

### The other half: the code must stay free to change

> "Le code doit pouvoir changer, evoluer, s'ameliorer, mais le comportement attendu ne doit jamais se degrader."
> (Gabriel, 2026-09-10)

The first half is defensive: a test bound to the implementation cannot fail on that implementation. The second
half is why it actually matters. A test bound to the implementation **freezes the code**. Every restructuring then
costs a test rewrite, and a suite that gets rewritten alongside the thing it guards has stopped guarding anything,
precisely when the guard was most needed.

So the property to aim for is: **restructure freely and the suite stays silent; change a behaviour and it goes
red.** Both directions matter. Silence on a refactor is what makes improvement affordable; red on a behaviour
change is what makes it safe. A suite that is noisy on refactors trains everyone to edit tests reflexively, which
is how a red that MEANT something gets edited away.

Both were demonstrated here on 2026-09-10, hours apart:

- **Silent, correctly.** `ArmOrRefuseDuel` was extracted out of `ResolveDuelChallenge`, a pure restructuring so
  the manual menu and the model could share one road. Not one existing assertion was touched. The suite had
  nothing to say because nothing a player would notice had moved.
- **Red, correctly.** `decayDays == 0` changed meaning, and a test failed. That failure was the suite working:
  behaviour really had changed, and reversing it had to be a decision somebody made on purpose.
- **Red, WRONGLY, and this is the cost to learn from.** Widening `CanEscort` changed its signature, and SEVEN
  existing assertions had to be rewritten, including ones whose behaviour was untouched ("a lord who leads no
  party of his own is refused" is as true as it ever was). They broke on arity and parameter names. They were
  bound to the shape of the call.

**The structural cure, which is already the house style.** Give a policy a named FACTS OBJECT rather than a row of
positional booleans, so a test names the facts it cares about and says nothing about the others. Then adding,
removing or reordering a fact costs nothing to any test that did not depend on it. `EscortEligibilityPolicy` was
one of the older positional ones, which is exactly why it was the one that cost seven rewrites.

**Done, and now measurable (2026-09-10).** `Tools/Find-SwapRisk.ps1` reports every public member in
`CalradiaRemembers.Logic` that takes a run of consecutive same-typed parameters, which is the only place a silent
argument swap can hide (anywhere else the compiler catches it). It went from **99** public static methods with a
run of three or more, plus **29** more once constructors were counted, to **zero**. Run it after adding a policy.
A non-zero report is not a style complaint: it is a signature that will one day be called with two arguments the
wrong way round, compile, run, and give a plausible wrong answer.

Three fixes, in order of preference:

1. **Ask for the type that already exists.** Most of the worst cases were a caller unpacking a record into loose
   arguments to pass them straight back in. `CompanionRapport.Compatibility` took TEN ints that were two
   `RomanticTraits`; `AutonomousSchemePolicy.IsGroundedTarget` took four facts its only caller was reading off a
   `SchemeTargetCandidate`; `ConversationTranscript.Record` took five values it then assembled into a
   `TranscriptLine`; `AssemblyLedger.Propose` took five that were the fields of an `AssemblyResolutionEntry`.
2. **Give it a named facts object**, one field per fact, each doc line saying what the fact means AND what goes
   wrong when it is false. Two methods that ask about the same thing share one type, so they cannot drift.
3. **A facts object with a positional CONSTRUCTOR undoes itself.** `ResolutionWorldFacts` took twenty-six
   booleans. Use `{ get; init; }` and an object initializer; where the constructor normalised on the way in
   (a clamp, a `?? ""`), move that onto the `init` accessor rather than losing it with the constructor.

Two runs are left on purpose and say so in their own docs: `MapPoint(x, y)`, because every mapping API ever
written orders a point that way, and any pair whose operation is SYMMETRIC (`Compatibility(a, b)`,
`WithinRegion(anchor, party)`, `FactionPair.Matches`), where a swap cannot change the answer.

### A test of a signature is not a test

The same campaign turned up two tests that were guarding a SHAPE, and both were removed:

- `GIVEN_the_older_four_fact_call_WHEN_used_THEN_it_answers_exactly_as_it_always_did` — pinning an overload kept
  "so nothing that already calls this changes behaviour". Nothing called it. The overload also reintroduced the
  exact swap the facts object had removed.
- `GIVEN_the_two_argument_overload_WHEN_compared_to_the_three_argument_form_THEN_they_agree` — pinning a
  delegation whose real rule was already pinned by the tests directly above it.

Both re-checked that two spellings of one thing agree. Neither could finish the sentence "if this fails, a player
...". Convenience overloads that exist only to supply defaults are the same smell one level down: put the defaults
on the facts object as property initialisers and there is one method, one place the defaults are written, and
nothing left to disagree.

### The three checks, in order of how much they catch

1. **Could this test's name have been written before the code existed?** If the THEN clause names a return value, a
   null, a flag, a branch or a method, the answer is no. Rewrite it as what someone would notice. (Measured
   2026-09-10: 61 of 3889 tests in this repo still name a return rather than an outcome. The rest are already
   behavioural, so this is a discipline to hold, not a rewrite to schedule.)
2. **Where does the expectation come from?** For anything a player can see, it must be traceable OUTSIDE the code:
   a player's own words, the Nexus changelog, an MCM label, one of Gabriel's rulings. Quote the source in the test
   file's header, as nearly every test file here already does. If the only source is the implementation, stop.
3. **Finish the sentence "if this fails, a player ...".** If it cannot be finished, the test is describing code.
   Structural guards are allowed to assert structure (`IsPatched`, "no verb is taught and refused at once"), but
   their NAME and header still carry the world-level promise they protect, because that is what makes it obvious
   when the structure stops protecting it.

### The related failure: a guard that measures its own construction

Same root, different disguise. A test whose subject is built differently from production measures its own setup.
Three examples found within 24 hours, all of them green the whole time:

- `cr.help` described 70 of 259 commands. Nothing compared the hand-written list with the **registry**.
- The Lean prompt budget passed at 6,950 chars while the game sent ~22,500. The test built a `PromptBuilder` with
  **no ActionVocabulary**, so the entire GAME ACTIONS section was absent from everything it measured.
- The affection bar lived as a literal `50` in three places. All three agreed until one moved.

So: build the subject the way production builds it, and take the expectation from outside it.

## Where a panel lives: the one rule (measured 2026-09-07, do not relitigate)

**Never `ScreenManager.PushScreen`, and never a Gauntlet layer over the live map.** Both leave `MapState` active, so
`MapScreen` keeps frame-ticking underneath. Measured with `cr.tick_probe`, 4-second windows:

| host | settlement-visual ticks | map-input ticks |
|---|---|---|
| nothing pushed | 481 | 481 |
| `PushScreen` (what CR used to do) | 481 | 481 |
| `PushState` (what vanilla does) | **0** | **0** |

A pushed screen does not slow the map by any amount. A pushed state genuinely suspends it. Three separate defects
came from that one mistake: the siege-bombardment native crash under panels, the map stealing hotkeys while the
player typed a letter reply, and the fact that we froze the campaign clock by hand at all. Vanilla needs none of
those compensations.

**Choose by whether the thing needs a live 3D scene:**

| what you are building | host it as | examples |
|---|---|---|
| characters in a scene | a **Mission** | chat, council, duels |
| full-screen UI, no scene | a **GameState** with a bound screen, through `CrPanelHost` | letter book, Balance of Power |
| decoration of someone else's screen | an **overlay** layer (not hosting) | the BoP button on the vanilla clan screen |

`CrPanelHost` stays the single door for the second row. What changed is what it guarantees: it used to stack
compensations, it now suspends the map by the engine's own contract.

**Two hard requirements for a state-bound screen** (each cost a spike run to learn):
1. The screen MUST implement `TaleWorlds.Core.IGameStateListener`. `GameStateScreenManager.OnCreateState` ends with
   `RegisterListener((val is IGameStateListener) ? val : null)`, so omitting it hands the engine a null and the push
   dies with a `NullReferenceException`.
2. `[GameStateScreen(typeof(TState))]` comes from `TaleWorlds.MountAndBlade.View`, which ships in
   **`Modules/Native/bin/...`**, not in the game binaries. The engine constructs the screen and passes it the state,
   so **the STATE carries the payload** (prefab, ViewModel factory, layer priority).

---

## Bannerlord UI XML gotchas

| Pattern | Wrong | Right |
|---|---|---|
| Text alignment | `TextHorizontalAlignment="Left"` | `Brush.TextHorizontalAlignment="Left"` |
| Vertical layout | `LayoutMethod` on `ListPanel` (silently ignored) | `StackLayout.LayoutMethod="VerticalTopToBottom"` on `NavigatableListPanel` |
| Auto-hide | Custom visibility binding | `AutoHideRichTextWidget` — hides itself when `Text=""` |
| Full-width text | `WidthSizePolicy="CoverChildren"` | `WidthSizePolicy="StretchToParent"` |
| Text color | `FontColor="#RRGGBBFF"` | `Brush.FontColor="#RRGGBBFF"` |

Named brushes: `Encyclopedia.Stat.DefinitionText` (cream = label), `Encyclopedia.Stat.ValueText` (gold = data).

---

## ContextualNameResolver (Mapping/ContextualNameResolver.cs)

Scans `playerMessage` tokens (≥3 chars, not stop words) against `Hero.AllAliveHeroes` name parts. Categorizes from NPC perspective:

| Label | Condition |
|---|---|
| your spouse / father / mother / child | Family API |
| a very close friend (deep trust) | relation ≥ 30 |
| a friend (genuine regard) | relation ≥ 10 |
| an enemy (quiet distrust) | relation ≤ −10 |
| a bitter enemy (resentment) | relation ≤ −30 |
| a member of your clan | Same clan, relation = 0 |
| an acquaintance (…) | relation ≠ 0, below thresholds |
| not personally known to you | relation = 0, different clan |

`DescribeRelation(int r)` switch: ≥30→"deep trust", ≥10→"genuine regard", ≥−9→"neutral", ≥−29→"quiet distrust", _→"resentment"

Stop words include English, French, and Bannerlord connectors (`ibn`, `bint`, `of`, `de`, `al`, `van`, `von`).

---

## Sound events (validated in-game)

- Relation gain: `SoundEvent.PlaySound2D("event:/ui/persuasion/success")`
- Relation loss: `SoundEvent.PlaySound2D("event:/ui/persuasion/critical_fail")`
- Gold: vanille `AddQuickInformation` (native notification)
- Custom soundbank (`CalradiaRemembers.bank`) loads but strings bank does not auto-load — use vanille events

---

## Console commands (Alt+~, group `cr`, all via real pipeline)

```
cr.relation <delta> [name]           # change_relation via RelationGate
cr.relation_intimate <delta> [name]  # intimacy event — cap +3, bypasses cooldown
cr.relation_reset [name]             # clears LastRelationGainHour
cr.give_gold <amount> [name]
cr.take_gold <amount> [name]
cr.info [name]                       # relation, captive status, cooldown remaining
cr.discover [name]                   # seeds 3 test DiscoveredTraits (encyclopedia testing)
cr.quest [name]                      # seed a bandit_clear quest (issue→inject→complete path)
cr.quest_done [name]                 # force-stamp outstanding quest satisfied (test reward w/o battle)
cr.quest_abandon [name]              # force-abandon outstanding quest (test broken-word penalty)
cr.quests [name]                     # list NPC quests + state
cr.crc_scene [female|male] [solo|collective] [intent] [name]   # open a captive scene (Hardcore); intent = interrogation|desire|domination|torture|training|reward
cr.kinks [name]                      # list NPC kinks + valid kink names
cr.add_kink <kink> [name]            # add a kink (apply BEFORE opening a scene)
cr.remove_kink <kink> [name]         # remove a kink
```

---

## Workflow preferences

- **Language**: Gabriel writes in French; all code and files in English.
- **Before each task**: suggest the model (Sonnet 4.6 / Opus) and wait for explicit confirmation before implementing.
- **Scope**: pas de cleanup ou refactoring spontané pendant une feature. Refactoring et polish explicitement demandés sont en scope.
- **Commits**: never commit without explicit request. When asked: both repos (mod + SDK) unless stated.

---

## Current sprint state

> This is the only section that changes per sprint. Keep it short.

**Sprint 20 — join_party action** (2026-06-12) ⏳ built, untested in game:
- Gabriel's test: convinced a wanderer to join in chat — LLM agreed, nothing happened (broken promise). New `join_party` action closes it.
- **Conditional teaching** (request_privacy pattern): `EncounterContext.CompanionAskingPrice` (SDK) set by `EncounterContextBuilder.ResolveCompanionAskingPrice` ONLY when truly hireable (alive, free Wanderer, `CompanionOf == null`, no party, player clan below `CompanionLimit`); PromptBuilder then renders RECRUITMENT section with vanilla asking price + 75 % negotiation floor + `type: join_party / price: N` emission format.
- **Bridge execution** (`ExecuteJoinParty`): re-validates every guard ("the prompt is advice, the bridge is law"), clamps price to [75 %, 125 %] of `CompanionHiringPriceCalculationModel.GetCompanionHiringPrice`, checks gold, then native `GiveGoldAction` + `AddCompanionAction` + `AddHeroToPartyAction` — identical to a vanilla tavern hire.
- **Anti-empty-promise rule** in STAY WITHIN WHAT YOU KNOW: never AGREE to a concrete deed without a matching [ACTION] in the prompt — covers the whole class of bugs, not just hiring.
- `cr.hire_info [name]`: shows eligibility + asking/floor/cap. Test: meet a tavern wanderer → Chatroom → negotiate → they actually join (check party screen + gold deduction).

**Sprint 19 — Tester feedback round 1** (2026-06-12) ⏳ built, untested in game:
- **Fallback conversation memory** (the big one): the LLM mostly emits [EVENT] on a formal farewell — closing the window mid-exchange left NO memory (tester: "memories only click on the red text"). Now `ChatManager.Close` → `FinalizeConversationMemory()`: if Conversation mode, ≥2 completed player turns, and no [EVENT] this session → synchronous base event (`Other`) + async rich first-person transcript summary (new SDK `ConversationSummarizer`, mirrors the captive two-stage pattern).
- **Courier sea fix**: sender (or player) at sea → courier spawns at nearest landfall (`MobileParty.IsCurrentlyAtSea`, port-preferred via `Settlement.HasPort` ×3 distance score) instead of immobile in the water.
- **Death dating**: dead parents in RELATIONSHIPS now carry cause + recency ("the late Ingalther (who fell in battle 6 days ago)") from `DeathDay`/`DeathMark`; relationships rebuilt at conversation open (was session launch only). PromptBuilder events get recency suffixes ("- Day 91078 (Conflict, 6 days ago):") + Calradian calendar note (84-day year) + "do not invent how long ago" rule.
- **Console**: `cr.memory` shows "(N days ago)"; new `cr.memory_add <days_ago> <text> [name]` (construct prior relationships); `cr.discover` renamed `cr.discover_test`, seeds gender-adapted + "(test seed)"-marked (tester got "She…" on Derthert and thought discovery was broken).
- **Relationship Pacing**: MCM "Relationship Warmth Normal/Easier/Very Easy" → "Relationship Pacing Grounded/Warm/Storyteller" (tester insight: philosophy of interaction, not difficulty). Old persisted values still map. VeryEasy/Easier vocabulary now names lordly positives (pledge of support, praise of deeds, shared victory) + "when in doubt, emit the +1" (Derthert no-gain case).
- **NEXUS_DESCRIPTION**: cr.memory_add doc, profiles-persist-on-save note, "Where the mod is at" transparency section, pacing labels.
- **ROADMAP**: v1.1 + give_troops/give_item; v1.2 + player battlefield performance in nearby lords' profiles.
- NOT fixed (acknowledged): stale lord intentions (conquered village — needs ownership check at description time, minor), letter reply courier still targets a player at sea.

**Sprint 18 — Witness presence & companions** (2026-06-12) ⏳ built, partially validated:
- **VanillaDialogueRecorder fix** ✅ validated: buffer cleared on partner change + deferred clear on ConversationEnd (ConversationBegin unreliable in this engine build). Log: `dropped N stale line(s)`.
- **Witness same-location filter** ✅ validated: a witness must share the NPC's `LocationComplex` location (`lordshall`/`center`/`tavern`…; works in BOTH Talk and Visit paths — validated in-game). Unknown location = excluded.
- **Social rules** ✅ validated: notable NPC → no hero witnesses, 50 % chance of one synthetic flavor witness (gang enforcer / apprentice / customer, no HeroStringId); lord NPC → non-notable heroes only.
- **Companions**: physically-present companions in the loaded scene (≤ 15 m, max 2, `Mission.Current.Agents` scan — requires `TaleWorlds.DotNet` ref) auto-join, no picker; otherwise picker (max 2, "Go alone"); any hero in MainParty qualifies (incl. clan family). Prisoner → never. **Test pending** (needs a companion in party).
- **`witness_leaves` action**: player asks THEIR companion to leave in free text → LLM emits action → mod removes witness (IsPlayerCompanion gate, tolerant name match), transcript note, 15B routing reset. Handled in main path + 15C loop. **Test pending.**
- **Free-text privacy**: `request_privacy` now taught whenever witnesses present (not just on button click); resolution no longer gated on the button flag. Captive player: no privacy teaching, no Request Private button. **Test pending.**
- **Prompt improvements**: PlayerName/Gender/Clan injected from live Hero (THE PLAYER section; `player_description.txt` now ships empty = optional flavor); `EnableMemoryBlock=false` in mod ([MEMORY] never consumed — saves output tokens); flirt/intimacy [EVENT] tokens gated by AdultLevel; world.txt Nords line.
- **No-key UX**: `IsAvailable` also requires a model id (NanoGPT has no default); `DescribeUnavailability()` gives a provider-aware message in-chat.
- **ROADMAP.md** added (v1.1 commoners → v1.2 rumor store → v1.3 schemes → v2.0 CK3-style influence).
- **Architecture pass** (same day, behavior-neutral): `ChatViewModel` mode booleans replaced by one `ChatMode` enum + named predicates (`IsLiveConversation`, `IsCaptiveEncounter`, `AllowsPrivacyRequest`) — v1.1 commoners = add an enum value, review the predicates. Address detection extracted to `Chat/AddressedSpeakerDetector.cs` (pure, unit-testable). `WitnessResolver` decoupled from Chat (picker takes `Action<WitnessEntry>`); dedup by HeroStringId. `LlmServices.IsAvailable` derives from `DescribeUnavailability()` (single source of truth). csproj: all refs via `$(BannerlordGameBinaryDir)`/`$(BannerlordModulesDir)`, deploy targets merged into one MSBuild-native target. Dead `_progressLog` removed; SDK nullable warnings fixed. Builds: 0 warnings.

**Sprint 14 + 14b** ✅ validated in-game (2026-06-03) — last completed sprint.

Multi-NPC witnesses (14) + active witness reactions (14b):
- `WitnessEntry` (SDK): Name, RelationToNpc, IsPlayerCompanion, HeroStringId, **Persona** (archetype label)
- `WitnessReaction` (SDK) + `ParsedResponse.WitnessReactions`; parser handles `[WITNESS_REACTION]` blocks
- `EncounterContext`: +Witnesses, +PrivacyRequested
- `PromptBuilder.AppendWitnesses`: `WITNESSES PRESENT` w/ persona; `[WITNESS_REACTION]` format (gesture/spoken line, in-character, speaks up on provocation); `request_privacy` when pending. SCENE DISCIPLINE carve-out so a directly-addressed witness may still react
- `ChatSession.AddWitnessStatement`: injects `[Name]: ...` as user-role so the main NPC sees/reacts next turn (14b)
- Privacy gate in `AppendIntimacyConsentRules`: witnesses → intimacy blocked; except Hardcore+captive
- `WitnessResolver` (mod): settlement (HeroesWithoutParty + Parties) + campaign map; known-profile filter; `ResolvePersona` from profile.Trait; companion picker `ShowMultiSelectionInquiry`; `DiagnoseWitnesses`
- `ChatViewModel`: `_witnesses` + per-witness hash colors; `AppendWitnessReaction` (own portrait+color, injects to session); `ExecuteRequestPrivate`; `request_privacy` + **`end_conversation`** handlers
- `ChatMessageViewModel`: witness-color constructor overload
- XML: TitleBar 95px, witnesses line, "Request Private" button
- **`end_conversation` action**: NPC ends exchange on grave affront / refused-then-pressed advance / farewell → input locked, "Leave" button, paired negative `change_relation`
- `change_relation` vocab strengthened: crude advance (-5), lewd proposition before witnesses (-8), never-stay-neutral directive
- Console: `cr.witnesses` (diagnostic), `cr.witness` (seed), `cr.rep_set` (force rep)
- Verified: `Hero.CurrentSettlement` populates during Talk (Poros test); witnesses react in-character; relation drops + persists to encyclopedia

**Sprint 12c + 12d** ✅ validated in-game (2026-06-05) — last completed sprint.

Visible courier party (player→NPC) + NPC reply by courier:
- `CourierService`: spawn/tick/arrive/destroy; `SetMoveEngageParty` for party targets; Harmony speed floor 6.5 via `CourierSpeedPatch`; `HashSet<string> _activeCourierIds` for O(1) identity (rebuilt on session launch); `HourlyTickPartyEvent` for < 1h delivery latency; `TrySpawnReplyCourier` for NPC→player direction (uses `SetMoveGoToPoint` + hourly refresh to avoid `PlayerEncounter`)
- `CourierSpeedPatch`: Harmony postfix on `DefaultPartySpeedCalculatingModel.CalculateFinalSpeed`; uses `IsCourier(StringId)` — no display-string allocation on hot path
- `TaverneLetterBehavior`: 500-denar hire; searchable NPC selector (`MultiSelectionInquiryData`); hooked to `tavernkeeper_talk`
- `PlayerLetterService`: courier-driven delivery; date-based fallback; `HasTriggeredReply` prevents duplicate LLM calls
- `LetterGenerationService.QueueReplyToPlayerLetter`: async LLM decides PASS or DELAY:N; PASS reason logged; stores `PendingLetter` with `DispatchDay`; MobileParty spawn deferred to main-thread daily tick (`SpawnPendingReplyCouriers`)
- SDK: `PlayerLetter.HasTriggeredReply`, `CourierPartyId`, `IsLost`; `PendingLetter.DispatchDay`, `CourierPartyId`, `IsLost`; `LetterReason.PlayerLetterReply`; `LetterPromptBuilder.BuildPlayerLetterReplyDecisionMessage`

**Sprint 12e** ✅ — all NPC-initiated letters (tournament, romantic, spouse, birth, in-person reply) now arrive via a visible courier (every `PendingLetter` gets `DispatchDay = today`; `SpawnPendingReplyCouriers` picks them up).

**Sprint 15A + 15B** ⏳ built, in-game round-trip NOT yet verified (blocked earlier by API key outage):
- 15A (SDK `PromptBuilder.AppendWitnesses`): witnesses may react PROACTIVELY (strong opinion, known fact, persona, several turns of silence), not only when provoked; silence is also characterful.
- 15B (mod `ChatViewModel`): the player can address a witness directly (sticky `_currentSpeakerHeroId`). Detection = participant name in a *stage direction* (`*...*`) or vocative; `AddressStopWords` filters connectors. The turn is routed to that witness's profile (full `ChatAsync`), reply shown under their portrait/colour; `ResolveAddressedSpeaker`/`SpeakerForCurrent`/`BuildContextWitnesses`. Per-call attributed transcript (`_transcript`) rebuilds each speaker's `ChatSession` from `_messages` (own lines→assistant, others→`[Name]:` user) — replaces `DialogFlow.Session` for the exchange. Indicator "▸ Speaking to X" via `WitnessesText`. Title stays the main NPC. Scope: one routed speaker/turn; NPC→NPC chaining is 15C (not built). **To verify: address Martira → re-address Alympia → she references what Martira said.**

**Sprint 15C** ✅ built (2026-06-06) — NPC auto-responds to witness reactions, **looped, capped at 3** per player turn:
- `EncounterContext.IsWitnessExchangeTurn` + `IsLastWitnessExchange` (SDK): flag the LLM call as a NPC→witness exchange; the latter marks the final allowed exchange.
- `PromptBuilder.AppendWitnesses` (SDK): when `IsWitnessExchangeTurn`, injects "THIS TURN — A WITNESS HAS JUST SPOKEN" (react, don't default to player). When `IsLastWitnessExchange`, adds a CLOSING rule: no question to another NPC — close the beat or turn it to the player.
- `EncounterContextBuilder.Build` (mod): `isWitnessExchangeTurn` + `isLastWitnessExchange` params.
- `ChatViewModel` (mod): `WitnessExchangeCap = 3`; `_witnessExchangesThisRound` (reset on every player send) + `_npcWitnessExchangePending` (prevents premature input re-enable). `CollectWitnessReactions` helper (shared with main path; resolves hero ids). `RunNpcWitnessExchangeAsync` is a **loop**: each iteration fires one LLM call, displays reaction+reply, then collects fresh `[WITNESS_REACTION]`s to continue; terminates on cap, silence, or `end_conversation`. Reactions committed to transcript just before each call (no double-count). **In-game test pending.**

**Sprint 16** ✅ MCM mod options (distribution):
- `Settings/CrSettings.cs` (`AttributeGlobalSettings`, Global scope — NOT per-save): OpenRouter API Key, Model, Test Connection button, Adult Content Level dropdown (default Mature), Verbose Logging toggle.
- `Settings/ApiKeyTester.cs`: probes `/auth/key`, popup OK/401/error; works from main menu, no tokens.
- `ModSettings` is now a façade reading `CrSettings.Instance` (fallback Mature / false).
- **Live key + model** (no restart, no reload): `OpenRouterConfig.ApiKeyProvider`/`ModelProvider` + `ResolveApiKey/ResolveModel`, read per request; `LlmServices.IsAvailable` computed live; `ChatService` rebuilds when AdultLevel changes. Precedence: MCM value → `OPENROUTER_API_KEY` env var.
- MCM = hard dependency (`Bannerlord.MBOptionScreen` in SubModule.xml; `MCMv5.dll` reference).
- Known cosmetic: MCM native-options screen renders the list flipped (TaleWorlds ListPanel layout fix that MCM predates) — not our bug; fixed by an up-to-date MCM. Ordering attributes currently compensate.
- Security: API key was leaked via committed `ConsoleRunner/Properties/launchSettings.json` (auto-disabled by OpenRouter) — scrubbed; key now env-var/MCM only.

**Sprint 17** ✅ Captive encounter + CNC scenes (validated in-game 2026-06-07):
- **Section A** (SDK `PromptBuilder.AppendPlayerGenderContext`): when `PlayerIsFemale` + patriarchal faction (Vlandia, Northern/Western Empire, Aserai), the NPC applies cultural skepticism toward female authority. Gated by AdultLevel != Off.
- **Section B** (SDK `PromptBuilder.AppendCaptivePlayerRules`): when `PlayerStatus == Captive` + Hardcore, replaces standard consent rules with CNC framing. `IsCollectiveCaptiveScene` → witnesses are active participants; else **ALONE block** ("no guards present — use existing bonds, your own hands") so the LLM stops inventing guards in solo scenes. **BE INVENTIVE** block invites varied, non-conventional, non-penetrative acts steered by the NPC's kinks.
- **`CaptiveSceneIntent`** (SDK enum, 6): Interrogation, PersonalDesire, Domination, Torture, **Training** (one session of ongoing conditioning), **Reward** (positive register — the carrot). Each drives a distinct opening cue (`ChatViewModel.BuildCaptiveOpener`) + YOUR PURPOSE/ESCALATION block. Relation notes injected (≥15 prior interest survives / ≤-5 no goodwill / ≤-20 contempt). `EncounterContext.CaptiveIntent` + `IsFinalSceneBeat`.
- **Captor speaks first**: `ActivateCaptiveEncounterMode` fires an NPC-led opening turn via `RunLlmExchangeAsync(opener, suppressPlayerLine:true)` — synthetic stage cue, never shown/logged as a player line.
- **Scene continuation**: when the NPC emits `[NARRATION]` without ending its dialogue in a question, the scene pauses with **Continue / Try something** buttons (`IsScenePaused`/`IsNormalInputVisible`; `RunCaptiveSceneContinuationAsync` fires one beat per Continue; `MaxSceneContinuations=4`). SCENE ARC (build→peak→conclude), anti-looping, non-sexual peak clarified. `IsFinalSceneBeat` (cap-1) + the continuation stimulus both push "conclude now". CLOSING rule: `end_conversation` must narrate the prisoner released and returned to cell — never mid-act.
- **Kinks**: enum grown 13→28 (`Kink.cs`): +OrgasmControl, Chastity, FreeUse, Degradation, Objectification, PetPlay, Praise, ImpactPlay, SensoryDeprivation, FearPlay, MasterSlave, Breeding, Training, CorruptionKink, Prize. `DescribeKink` descriptions; `RomanticProfileBuilder.BuildKinkPool` trait correlations (Mercy/Honor/Calculating/Valor/Generosity); `CaptiveEncounterService.SelectIntent` + probability bonuses (FreeUse +10%, MasterSlave +5%). Console: `cr.kinks`, `cr.add_kink <kink> [name]`, `cr.remove_kink`. Existing NPC profiles keep frozen kinks — new kinks appear organically only on profiles created after this build, or via `add_kink`.
- **Public humiliation** (`IGameStateBridge.ApplyPlayerPublicHumiliation`): collective + humiliating intent (Torture/Domination/PersonalDesire/Training) → clan **Renown −2/witness (cap 12) + Influence −3/witness (cap 18)** via GainRenownAction + ChangeClanInfluenceAction. Only mutates the save when `IsPrisoner` (console test reports figures without touching it). Excludes Interrogation/Reward. Triggered in `ActivateCaptiveEncounterMode`. (Direction 2 — player aggresses an NPC → NPC loses standing — deferred: no aggressor scene exists yet.)
- **Captive scene memory**: at scene close (`ChatManager.Close` → `ChatViewModel.FinalizeCaptiveSceneMemory`, idempotent), a `NotableEventType.Captivity` event (new SDK type, last in enum to preserve save ordinals, protected from `LlmMemoryCompressor`) is recorded **synchronously** with a base summary (verb per intent + "before my soldiers"/"in private"), then `CaptiveSceneSummarizer` (SDK, one-shot `ILlmClient.CompleteAsync`, run async via `Task.Run`) generates a first-person past-tense rich summary from the transcript and **replaces** the base (`record with { summary }`, re-matched by value against a fresh `Store.Get`). 1 event/scene; capture guaranteed even if the async summary fails. Reinjected later via `AppendHistory`.
- **Witness label fix**: `CollectWitnessReactions` strips any parenthetical role (`StripWitnessRole`) and shows the canonical name — no more "A soldier (one of your men…)" leaking to the player.
- **`[NARRATION]` block** (SDK `ParsedResponse.Narration` + parser + `AppendCaptivePlayerRules` teaches it): neutral third-person narrator prose for physical action done TO the player (incl. soldiers on the captor's order) and the player's imposed bodily sensations — distinct from `[DIALOGUE]`. Since 2026-07-17 the parser reports `NarrationBeforeDialogue` and the render honors the emitted order instead of forcing dialogue-first. Mod `ChatViewModel.AppendNarration`: portrait-less pale-violet line, StoryLog (novel-style), transcript as `[Scene]: …`. Wired in the main path, the 15C loop, and the continuation loop.
- **Console**: `cr.crc_scene [female|male] [solo|collective] [interrogation|desire|domination|torture|training|reward] [name]` — intent accepts the short alias or the full enum name (`Enum.TryParse`). Forces Captive status + optional gender override without a real prisoner.
- **UI responsive**: chat panel `HeightSizePolicy=StretchToParent` + 100px top/bottom margins (was Fixed 880) so it adapts to UI Scale / aspect ratio; `MessagesArea` stretches to absorb the slack. Verified at min/max UI scale.
- **NOT YET BUILT**: scenario bank (Twine→JSON, one-use-per-campaign via SyncData, eligibility by kinks/gender, prompt injection), image overlay ([SCENE] parser). When adding persisted campaign state, REMOVE `CaptiveEncounterService.ResetOnSessionLaunch` — SyncData(load) fires before OnSessionLaunched and would clobber loaded state.

**Misc (built + validated)**:
- **Story Log** (`StoryLog.cs` + MCM `CrSettings.StoryLogging` in Diagnostics, off by default): clean novel-style transcript to `Logs/CalradiaRemembers_story.log`. Session header, lazy per-conversation intro, player/NPC/witness lines, relation/gold prose, letters, scene narration. Player always "You". Accumulates across sessions.
- **Enable Logging** (`CrSettings.EnableLogging`, **off by default** for Nexus): master switch in `ModLog` (`LoggingEnabled` fallback true so pre-MCM startup lines are kept).
- **`farewell` event → input lock**: a `NotableEventType.Farewell` event locks input + "Leave" button (silent — the NPC already said goodbye).
- **action_vocabulary refactor**: the 4 action definitions live as a `static readonly` list in `LlmServices` (deleted `ActionVocabularyLoader.cs` + `ModuleData/action_vocabulary.json`).

**Next**: Nexus release (mod page description in progress; min MCM version note; gitignore launchSettings.json); then scenario bank (Twine→JSON).
