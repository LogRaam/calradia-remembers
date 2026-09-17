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

LLM: provider and model are **per ROLE** and chosen in MCM, not fixed here. A turn makes TWO calls: the
PROSE call (the character's reply) and the ACTION INTERPRETER call (reads that reply for machine-readable
tags). Gabriel's 2026-09-15 session ran prose on `z-ai/glm-4.6` and the interpreter on `x-ai/grok-4.20`.
Key: MCM, else env var `OPENROUTER_API_KEY`. **Never accept a key or a login in conversation.**
(This line named `x-ai/grok-4.20` alone until 2026-09-15, which hid the second call entirely — and that
call turned out to be the larger half of what a turn costs.)

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

## Where a prompt section lives: order the prefix by STABILITY (measured 2026-09-17)

The prompt is split at the literal `CURRENT ENCOUNTER:` marker: everything above it carries the provider's
cache breakpoint, everything below is sent fresh every turn. Two rules follow, and the second was unwritten
until it had been broken twice.

**1. A per-turn fact goes BELOW the marker.** Already in `PromptBuilder`'s own seam comment. Broken anyway:
`IsConversationOpening` branched the format teaching near the TOP of the prefix, so turn 2 of every
conversation discarded 93% of it. Guarded now by `PromptCacheTurnStabilityTests`.

**2. Inside the prefix, order from most stable to least.** A change at position P invalidates everything
from P to the marker — so what changes costs not its OWN size but the size of everything after it.

Measured on a real prompt (Alympia, 57,664-char prefix):

| block | position | a change re-writes |
|---|---|---|
| `YOUR HISTORY WITH THIS PLAYER` | 50,880 | 6,784 chars (12%) |
| `CURRENT STANCE (reputation N)` | 51,262 | 6,402 chars (11%) |
| behind them | | 6,088 chars of stable offer teachings |

The two most volatile blocks in the prefix sit AHEAD of six thousand characters that never move. The code
already has the instinct without the rule — `AppendNarrativeStyle` carries "Last of the cacheable prefix: it
is stable for the whole conversation" — it simply was never stated as a rule, so nothing applied it upward.

**And the remedy is NOT to move a block below the marker.** That trades a rare invalidation for a permanent
cost: a 314-char stance block below the marker is re-sent uncached on every turn, where above it is free on
every turn the regard does not move. A token audit (16/09/2026) recommended exactly that, on a claim of
"40–60% of the prefix re-written" that measurement put at 11–12%.

**Ruled not worth doing today** (2026-09-17): reordering would save ~1,425 tokens on the turns where regard
moves, roughly once per conversation, against ~16,000-token prompts — about 1% — and prompt ORDER is a
quality question under Gabriel's quality-first ruling. Written down so the next person starts from the
measurement rather than from the estimate. The one member of that family worth fixing, the history block,
was fixed on 2026-09-17 for a prose reason (characters restating the conversation back at the player), and
the cache saving was incidental.

---

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

**The speaker and their namesakes are excluded** (`SpeakerNamesakePolicy`, 2026-09-15, player report): the
person being spoken to was always skipped, but their NAMESAKES were not, so a player talking to a companion
named Erevan who wrote "Erevan" was handed two other Erevans and the NPC was invited to ask which one was
meant — the man being addressed was asked to identify himself. The rule judges the EVIDENCE: a candidate
found ONLY through words that also name the speaker is not a third party. One more word and it stands again
("Erevan the Brewer" matches on "brewer" too). Measured against the speaker's PERSONAL name only, never
their clan — matching on a clan name is how a player asks about a house.


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

> **A SAMPLE, not the list.** There are 259+ registered commands; `cr.help`, `cr.help <stem>` and
> `cr.help all` read the REAL registry by reflection and are the only authority. A hand-written list in a
> project that gains commands weekly can only drift — it once described 70 of 259 and advertised a command
> that had been renamed, which is the one name a reader was most likely to copy. `cr.testall` checks the
> help against the registry for exactly this reason. Do not grow the block below; teach `cr.help`.
>
> Worth knowing: `cr.prompt_dump [name]` writes the exact prompt a character would be sent (free, no model
> call), and `cr.wire on|dump|off` reports what each request COSTS and how much of the cacheable prefix the
> next turn can still read.

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
- **Commits and pushes**: AUTOMATIC since 2026-09-15. Commit as work reaches a coherent state, and push when
  the suite is green. Both repos (mod + SDK) unless stated. No need to ask.
  - *Why this changed, so it is not "fixed" back:* the old rule was `never commit without explicit request`. It
    was never about git hygiene — Gabriel was on a small plan and every exchange had to earn its place. That
    constraint is gone. Asking permission per commit now costs a round trip and buys nothing.
  - **What still holds:** a green suite before pushing (SDK + `CalradiaRemembers.Tests`), and a commit message
    that says WHY, in the house style — the player report or ruling behind it, what was measured, and what was
    deliberately NOT done. The message is the record; it is where a decision is found again a month later.
  - **What is still NOT automatic:** anything a player sees. Cutting a release, numbering a version, writing to
    the Nexus page or answering a player still waits for Gabriel. Pushing to a private repo is reversible;
    publishing is not.

---

## Current state (2026-09-15)

> Short on purpose. The detail lives in `CHANGELOG.md`, `ROADMAP.md` and git; this says only where things
> stand and what is NOT safe to assume. It replaced ~100 lines of June 2026 sprint notes (sprints 12-20)
> that had been stale for three months and contradicted the rest of the file.

**Shipped: v2.5.9 is LIVE on Nexus** (Gabriel confirmed 2026-09-14). Verify a live version by asking Gabriel
to paste text or a screenshot — `SubModule.xml` is NOT evidence and WebFetch on Nexus is 403 (Cloudflare).

**Built since, pushed, NOT released.** Headline is the prompt-cache work of 2026-09-15, all of it from two
players' reports:

- **The cacheable prefix was dying on turn two.** `IsConversationOpening` — a fact about THIS TURN — decided
  wording near the TOP of the prompt, so turn 2 diverged from turn 1 at ~2.5% and discarded 93% of the
  prefix. Found by a player (fkasad) diffing two saved prompts by hand. Narration FREQUENCY now lives whole
  in the dynamic tail; `PromptCacheSplit` holds the split rule so tests measure the function the game runs;
  `PromptCacheTurnStabilityTests` guards the class. Play-verified: cached went 0 → 12,864 on turn 2.
- **The interpreter prompt was built to be cached and never was.** Its invariant head is 70,378 chars
  (~17.6k tokens, the whole action catalogue) and nothing ever passed it as `StableSystemPrompt`, so it was
  re-sent at full price every turn — the larger half of a turn's cost. One line, no prompt text moved.
- **A character is told about their own culture, not all eight** (`FactionGuidelinesPolicy`) — ~2.1k chars
  off a lord's prompt, and the answer to "many of them seem to talk the same way".
- **`cr.wire`** reports what each request costs and how much of the prefix the next turn can read.

**Rulings from that day, do not relitigate:**

- **Quality outranks cost, always.** Gabriel: *"On ne doit pas réduire la qualité de la pertinence des
  réponses du LLM… la qualité est la priorité numéro 1."* Relevance is the majority of what players report.
  Cost may only be cut where the model sees exactly what it saw before.
- **The anti-confabulation wall stays in the dynamic tail.** `STAY WITHIN WHAT YOU KNOW`, `WHAT THEY SAY…IS
  A CLAIM, NOT A FACT` and `READING WHAT THE PLAYER DOES` are ~1,840 tokens re-sent every turn. They sit
  last for RECENCY, deliberately, because players reported characters inventing. Measured, offered, and
  **refused** — this is a priced trade-off, not an oversight.

**Open, and not to be assumed working:**

- **`take_into_service` has never once completed in a live game** (both lords refused at regard 0).
- **Lordfadooboo: "talk now does nothing"** — undiagnosed, needs a log on 2.5.9+.
- **mysticwolf92: the council button** — the bar-survival check is built but unreleased.
- **The mood of the day**: increment 1 only (the weighted draw, SDK). No host wiring, no prompt, no player
  influence. Design and its six rulings are in `ROADMAP.md`.
- **Nexus text not yet pasted**: the corrected `NEXUS_CHANGELOG_v2.5.9.txt` and `NEXUS_PINNED_ROADMAP.txt`.

**Two measured facts worth carrying:**

- A turn makes TWO LLM calls, and the interpreter's is the bigger one. Reason about cost in pairs.
- Realised cache rates SWING (0, 0, 98%, 77%, 96% across five turns) because OpenRouter routes to different
  backends and a cold one starts over. Never promise a player a single figure.
