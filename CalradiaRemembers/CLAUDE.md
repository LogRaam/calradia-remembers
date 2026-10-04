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

**Read `docs/maps/prompt-sections.md`. It is generated from `BuildSystemPrompt` and a test fails when it is stale.**
It lists every section in the order it is written, which side of the cache marker it falls on, what gates it, and
whether Compact drops it. A hand-written list of sixteen sections stood here until 2026-09-21; the real prompt had
grown to well over a hundred calls, and nothing had ever made the list keep up. That is the reason the maps exist.

---

## The maps: how to see the whole system (Gabriel, 2026-09-21)

`docs/maps/` holds views of the system generated from its own source: every type, every dependency between
namespaces, every rule the tests state (`RULES.md`), every numeric constant, every engine event listened to, the
prompt's sections in order, every verb with each place it must be declared, and everything static that can change.
`docs/maps/README.md` says what each one answers and what none of them can see.

- **They cannot go stale while the suite is green.** `MapsAreCurrentTests` regenerates each map and fails when the
  committed copy differs. A failure there is never a bug in the code: the code moved and the map did not.
  Regenerate with `.\Tools\Update-Maps.ps1` (it prints what moved, and the full diff of anything short
  enough to read), then commit the map WITH the change. Raw form:
  `CR_UPDATE_MAPS=1 dotnet test CalradiaRemembers.Tests -p:NCrunch=1 --filter "FullyQualifiedName~Maps"`.
- **Look before you add.** Before a new threshold, policy, guard, event listener, prompt section or verb, read the
  map that would show it if it already existed (`constants.md` opens with the names declared in more than one
  type; `engine-facts.md` shows the same CONCEPT under another name, by the fact of the game it reads: rank
  was a prestige score, a stature, a station and a tier, and all of them read `Clan.Tier`). Say in the commit message what you found, even when it is "nothing like it exists". Gabriel's words: "je
  ne vois pas si on est en train de creer un doublon parce que le concept existe deja ailleurs."
- **A character acting toward the player asks the one gate.** Any new path where an NPC takes the initiative (writes
  first, asks for an audience, rides out, threatens, sends killers) calls `Mapping.ActionGate.Allows(hero, ActionKind.X)`
  (alive, of age, free, not the player; `IsLord` is an occupation, never an age). `docs/maps/actions.md` lists every
  site; RatchetTests fails on a declared kind never asked, and on a letter added to `SentLetters` outside a gated file.
  Nexusrrr's baby son asked to discuss warfare (30/09/2026) because one path forgot the age. **Answering a summons is
  an action too** (Gabriel, 30/09/2026, after Maifailun's dead father sat at a council): a council seat asks the gate
  (`ActionKind.CouncilSeat`, with the council's own age, 13), and so should any new path that seats a character.
  **`Hero.IsAlive` is NOT presence**: a DISABLED hero reads alive, and `Clan.Heroes` keeps the dead and the disabled.
  Ask `Mapping.HeroPresence.IsInTheWorld` for presence; a ratchet counts every bare `IsAlive` read.
- **Read the map's diff before the code's.** A new line in `dependencies.md` is a namespace that has started to
  know another. A new line in `shared-state.md` is a lifetime somebody has to decide.
- **Two of the maps are contracts, not views.** `save-format.md` (every save key, every saveable type id) and
  `modder-api.md` (every public member under an `Extension` folder). A line removed from the API map is a breaking
  change for bridges already shipped: name it in the release notes under "For modders". A KEY removed from the
  save map is refused by the suite even in update mode, until `CR_ALLOW_SAVE_KEY_REMOVAL=1` declares it on purpose.
- The maps cover the SDK too and live in THIS repository, the umbrella. An SDK-only change can therefore turn a
  mod test red; that is intended, since both suites run on every change.

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

- **Tracing the code: DELEGATE IT.** Use the `cr-trace` agent (`.claude/agents/cr-trace.md`) for any question
  that means sweeping files rather than opening one you can already name: "where is X decided", "who sets Y",
  "what ordering picks Z". Several of these can run at once, in a single message, since they do not touch each
  other. It is read-only by design.
  - *Why:* a sizeable share of a working session goes on exactly this, and the answer is one paragraph. Keeping
    the search out of the main thread keeps room for the part that needs judgement.
  - *When NOT to:* if you already know the file, just read it. A delegated search that comes back with a file
    you could have opened directly cost more than it saved.
- **Building in parallel: almost never, and know why before you try.** Three things serialise this work and none
  of them is about speed.
  - **Git.** Two agents editing and committing in the same two repos collide, and the rhythm here is
    build, test, commit, push. Use an isolated worktree or do not write from an agent at all.
  - **The shared prompt budget.** Many changes land in the same cacheable prefix and are guarded by the same
    ratchets (Compact fit, prompt cache stability). Two agents each adding a block would blow a budget neither
    of them measured.
  - **The bar.** Most of the value in this repo comes from NOT taking the first plausible diagnosis: the duel
    venue was mis-diagnosed once and shipped, the archive verifier cried wolf twice, a new test failed for a
    good reason and the code was what was wrong. An agent working from a short brief returns the plausible
    answer with confidence, and the cost of a wrong fix here is a player writing "still broken on the version
    that was meant to fix it". That has already happened once.
  - *So:* delegate a BUILD only when the work is genuinely disjoint from everything else in flight AND has a
    spec precise enough that you would accept the result without re-deriving it.

---

## Current state (2026-09-23)

> Short on purpose. The detail lives in `CHANGELOG.md`, the `NEXUS_CHANGELOG_*.txt` files and git; this says
> only where things stand and what is NOT safe to assume.

**v2.5.20 is LIVE on Nexus (Gabriel, 2026-09-23)**, so 2.5.20 is frozen. **v2.5.24 is LIVE (Gabriel, 2026-09-26), frozen. v2.6.12 is OPEN: MOD `cca899c` (Invite button: a companion, travelling lord or prisoner joins; Request Private repeatable, hidden with no witness, granted at once when every witness is the player's own; Witnesses.PlayerCompany; cr.testall player_company; nerethi, Gabriel's design). MOD `3e199d3` (no lord camps at the gate of a besieged fief: siege ends the chase, hourly check; inside walls the day limit applies; MCM 'Lords Hold Their Course' default on; Fakade). MOD `4061fca` (cr.world_events / world_event_remove / world_event_secret / world_event_hide: RumorDiscretion veto inside WorldEventStore.Explain; cr.testall world_event_discretion; IdentityCrysys). v2.6.11 is LIVE (Gabriel, 2026-10-03), frozen (release `ee40a36`, verified rar `c5210832...`; testall 221 PASS after `68c8720`). It carried: SDK `be0aea8` (a settled matter stays settled: the conversation recap ends with a 'Settled:' line, and the dialogue style forbids re-raising what was asked and answered; Lean budget 7400; Lordfadooboo). MOD (newlyweds: WeddingDayBehavior records every wedding day, the spouse line says 'wed N days ago: you are newlyweds'; new save key; cr.testall wedding_day_line; raphareish). MOD `27d4c7e` (a new spouse's romantic status healed AT the wedding, not only at the next load; cr.testall spouse_status_at_wedding; brorespeta123, unconfirmed). MOD `0c6d9f5` (self-esteem: a skirmish is no feat, a win against the odds more; gains shrink near the top by MCM 'Self-Esteem Climb' 0..4, default 2; cr.testall self_esteem_climb; fkasad). MOD `0ba5a7a` (a duel's audience is whoever was present in the place, capped at 10, and its news weighs the opponent's station; fkasad). MOD (a house that rose in rebellion remembers it and its return, any house; WorldEventType.Rebellion; cr.testall house_rebellion_memory; FloatingMammal). MOD `a3bfe6e` (the offer bag finds the CURRENT speaker's task: OfferController read the DialogFlow.Profile cache, the last dialogue-menu partner, so a companion's grain task never settled; SpeakerProfileSourceTests allows one read; Lordfadooboo). tashmetu 02/10: MOD `90ad015` (cr.prompt_dump takes a hero id, lists namesakes), SDK `fd28e2a`/MOD `0c7730c` (VerbFacts.IsFemale; catalog text stays fixed, wording per character in TeachingText), MOD `2bff9fe` (a divorce another mod made is noticed daily, WorldEventType.Divorce + memory for each; cr.testall foreign_divorce), MOD `31a1d44` (six persona archetypes no longer word an ordinary trait mix as sadism; RetiredArchetypes re-derives stored ones; cr.testall retired_persona_refreshed; a +-2 trait now meets its archetype by direction plus an 'extreme' sentence, MOD `83c5038`, cr.testall extreme_trait_persona). MOD `70be558`+`f2012e8` (accents typed into any cr.* command repaired at the engine's console dispatch, Harmony on CommandLineFunctionality.CallFunction, cr group only; cr.testall console_accents; Shinelegacy). MOD `656201c` (cr.memory_repair_accents mends stored mojibake; cr.testall memory_repair_accents). SDK `e7c52ec`/MOD `1d0ddde` (a captive ruler or landed house head is told the world talks of her capture; such a capture is world news even without the player; cr.testall captive_notoriety; Lordfadooboo). MOD `91c195a` (Escape leaves a CR conversation one layer at a time; NO testall, needs one manual check: Escape must not also open the map options menu; EmperorHaterius). Strategic Assessment fixed (wars on one line, a realm's real battle record instead of 'weakest in war performance', the player's world name/World.txt/post-history; fkasad). The forced 'walk around' after a duel is AWAITING his [DUEL]/[CHAT STAGE] log lines (CR never sets NextLocation; not guessed). v2.6.10 is LIVE (Gabriel, 2026-10-01), frozen (release `0ab9198`, rar `76ee9c36...`): MOD `d06b3f3` (town duel: companions stand aside; the square waits for the town menu, defensive, the settlement crash is UNCONFIRMED until a [DUEL] log arrives; Zivkyn, Maifailun). MOD `3947c64`/SDK (capture tasks: a beaten-but-escaped target is recorded, the giver may settle for half or take a lord of the same realm, a lapse while he is a fugitive is no fault; fkasad). MOD `7615388` (CrMemoryEntry.Private for an injected memory said in confidence; warm-start MCM hint says "when you next speak"; tashmetu). MOD `d37b4bb`+`d3acfbe` (FAMINE: a hungry town remembers a large food sale as relief and a buy-up as hoarding, once a week; grain a governor/lord/notable asked for while his town starves goes into its stores, `Mapping.HeroSeats` one seat lookup; cr.testall town_food_memory + grain_relief_destination; fkasad, Lordfadooboo). MOD `da49fb0` (sway_opinion needs the target NAMED by the player in the conversation, `ActionContext.PlayerWordsThisConversation`; court cooldown markers out of memory prose, scrubbed at load; Maifailun). MOD `61a3c27`/SDK `49f57af` (earn_regard: win another's good graces, +10 from the baseline, credited on ProfileMutator.RegardChanged; cr.testall earn_regard_quest; Lordfadooboo). MOD `eb02994`/SDK `4096758` (plead_case, done by a real sway toward the giver; cr.testall plead_case_quest). MOD `b451a9f` (a town's needs read off its market price factor, delivered into its market; cr.testall town_needs_supply). MOD `580a978`/SDK `50b758b` (reinforce_garrison quest + verb taught only in that place; GarrisonStrength one home for the bands; cr.testall garrison_reinforcement; fkasad). MOD `1d53d71`/SDK `323f346` (a prison break is a rescue: Mapping.PlayerPrisonBreakPatch, a Harmony prefix on EndCaptivityAction.ApplyByEscape noting the player as facilitator, feeds the gratitude chain and names the prison in the task evidence; cr.testall prison_break_rescue). MOD `6dc06fe`/SDK `f41ecff` (ransom_envoy: purse advanced at 1.5x ransom value, change kept (Gabriel option A), done by freeing the man the player bought or handing him home (give_prisoner FREES an envoy's man), void + advance returned up to the player's purse if freed otherwise; cr.testall ransom_envoy_quest; fkasad). MOD `3a2b37a`/SDK `717de6f` (scout_town + share_intel verb taught only to a gang leader in a town some lord sent the player to scout; report = the town's true seat bands; cr.testall scout_town_quest). MOD `f4fe452`/SDK `511d297` (recruit_troops + deliver_recruits; one close for troop hand-overs, SettleTroopHandOver; cr.testall recruit_troops_quest; Lordfadooboo). MOD `007fc21`/SDK `b1f1699` (craft_weapon: a weapon the PLAYER forged (ItemObject.IsCraftedByPlayer) of the kind named, through the supply hand-over; the lord equips it; cr.testall craft_weapon_quest). MOD `dab830f`/SDK `996770d` (arrange_match: REAL marriage of two NPCs (Gabriel), her heart (vanilla Friend toward the suitor, via sway_opinion) then her family's consent (ArrangedMatchPolicy on the suitor's house), void if either weds another; cr.testall arrange_match_quest; fkasad). v2.6.9 is LIVE (Gabriel, 2026-09-30), frozen: MOD `c409be4` (the captive rule never blocks talking to a prisoner: held scenes and witnesses accept a captive). MOD `33a72ef`/SDK (no task taught to a character at war with the player; fkasad). MOD `0658486` (self-test regard changes reported as self_test; tashmetu). MOD `1aa4d78` (a private audience granted in character clears the room: the scene model's request_privacy survives the interpreter; cr.private; Lufaine, Maifailun). v2.6.8 is LIVE (Gabriel, 2026-09-30), frozen: MOD `8888d17` (a witness addressed by name in Japanese answers; yozakura12), MOD `520461e`/SDK `d406e7c` (a friendly clan warms a first meeting once, up to friendly regard, MCM default on; AddMemory thread documented; tashmetu). MOD `3b6bce3` (an intercepting lord is held on course with the AI lock, released on every end of the chase; Lordfadooboo). MOD `b85b7ac`/`3159483`/`272b03f`/`7fdfa6d` (ActionGate, renamed from InitiativeGate: every act asks it with a per-kind rule, council seats included; a disabled hero reads IsAlive, so HeroPresence.IsInTheWorld; bare-IsAlive ratchet 313; Maifailun). The mood of the day FINISHED + SELF-ESTEEM + the PERCEIVED IMAGE in the chat dossier (ROADMAP entry of 30/09; Lordfadooboo, Maifailun; untested in play). v2.6.7 is LIVE (Gabriel, 2026-09-30), frozen (a child never takes the initiative, one ActionGate for every path; a lone player rides alone in letters). v2.6.6 is LIVE (Gabriel, 2026-09-30), frozen. It carried, re-cut with everything planned for 2.6.7 folded in: regard by letter, the game's mercenaries and a fiefless vassal judged, landfall wait, provider-pin message, armed town duel, greater lords writing through their household, CrNpcMemory.RegardChanged, Stature; cr.testall green on it. v2.6.5 is LIVE (Gabriel, 2026-09-27), frozen. 2.6.5 carried the envoy fixes and MOD `83e006d` (no envoy from the player's own clan, the wanted captive never his own counterpart, no exchange verb inside one house; tashmetu) (tashmetu's envoy fixes MOD `a42535a`/SDK `dab41aa`, a gift needs the player's words, a plain yes closes at the last offer, the ceiling stays secret, exchange gold explained; SharedStateMap read-only views). v2.6.4 is LIVE (Gabriel, 2026-09-27), frozen (culture block by own people + cr_patch !block for tashmetu; refusal note says its reason is a rule not a story, four scene-like refusals made plain, TurnPaymentPolicy for every coin-moving verb, sell_prisoner price band; MOD `c913a5b`). v2.6.3 is LIVE (Gabriel, 2026-09-27), frozen (tashmetu's ransom report: a captive's price known before it is named, paid once, and explained; plain empty-purse refusal). v2.6.2 is LIVE (Gabriel, 2026-09-27), frozen (TransientPartyRegistry: couriers, sergeants and hunters put back where they stood after a save; every inline regard threshold on a named rung, "friendly regard" 10-14, CoolTier -6; council_seat.txt per-seat modder hook for tashmetu). v2.6.1 is LIVE (Gabriel, 2026-09-26), frozen (mercenary-departure grid; regard ledger typing and map work, no behaviour change). v2.6.0 is LIVE (Gabriel, 2026-09-26), frozen (courier secrets; arrests, trials, outlaws, brigands, off by default; override template match; lore_status Custom files). v2.5.26 is LIVE (Gabriel, 2026-09-26), frozen. v2.5.26 was (retired predecessor still lives in the heir note, cr.testall succession non-destructive, testall for the prisoner trade). v2.5.25 is LIVE (Gabriel, 2026-09-26), frozen (envoy fixes from fkasad's first audiences: reply budget, logging, gifts, own-side ransom frees, opening offer). v2.5.23 is LIVE (Gabriel, 2026-09-25), frozen. v2.5.24 was (prisoner exchange + envoy, capture in peace, vanilla marriages, player-switch, cr.testall green)** (2.5.21 skipped by his numbering): the fed-world-event work for tashmetu (`PlaceId`, named heroes hear it in the prompt, `cr.heard`) and the Actions panel Inc 1-2 shipped DORMANT (`ActionsPanelGate`, `cr.actions_panel on`; the Manuscript default skin has no panel, which would have killed the tile). Not run in game. 2.5.20 carried grudge_terms reachable at last, lord recruitment on the mod ledger, rescue gratitude gated on adulthood, the payment ceiling in one home, a conversion's own calendar, the conversation resume, the castle duel and the plural household. v2.5.19 is LIVE on Nexus (Gabriel, 2026-09-21): the prisoner trade decided by CUSTODY, and modder `{{variable}}` providers resolved on the game thread. v2.5.18 before it (role anchor, whose "you" in memories, the no-appointments guardrail, a chosen rescue counted). **DURABLE: once Gabriel says a version is live, that number is FROZEN. Open the next one; never re-cut a published number** (learned 2026-09-21, when I folded two fixes into an already-published 2.5.18 and had to reissue them as 2.5.19). Verify a live version by asking Gabriel
to paste text or a screenshot: `SubModule.xml` is NOT evidence and WebFetch on Nexus is 403 (Cloudflare).
v2.5.11 was built and never uploaded. The 2.5.14 archive was rebuilt three times before upload to fold in
three fixes found after the first cut; the shipped one is MOD `d082528` / SDK `86ca836`.

**The order of Gabriel's messages is not evidence of what is live either.** The 2.5.13 archive was deleted on
the assumption that it had never gone up, and the commit message said so. It had gone up. Ask.

**The last three releases were almost entirely player reports**, named in the changelogs: the prompt cache
dying on turn two, the interpreter prompt never cached, Compact fitting an 8k model, the duel crash (three
reports across three versions before it was right), the companion audience that opened nothing inside a town,
a letter reply that left no trace, a quest treated as an old errand seconds after it was agreed, and a lord
writing to a tournament loser as though he rode in that lord's retinue.

**In 2.5.13 (live):** matters ordered by what touches the speaker, then size, then the clock (fkasad's burning
village); and `ModuleData/cr_patch/<filename>`, so another module contributes prompt prose without overwriting
ours (tashmetu, Bellum Civile bridge). `!remove <text>` and `!replace` cover a total conversion.

**In 2.5.14:** a Background model role for summaries, compression and extraction, so a local server stops
evicting its own cache (fkasad). `take_into_service` now refuses all family, not only blood (reminensce).
`teach_skill` requires the teacher to know MORE than the player (found answering Zinced121212). A captive handed
over as a SALE to the lord who asked for him now settles his bargain (Lordfadooboo): two verbs moved a prisoner
and only one ever looked at the quest, the duel's shape again, now asked once in `DeliveryQuestMatcher`.

**Rulings, do not relitigate:**

- **Quality outranks cost, always.** Gabriel: *"On ne doit pas réduire la qualité de la pertinence des
  réponses du LLM… la qualité est la priorité numéro 1."* Cost may only be cut where the model sees exactly
  what it saw before.
- **The anti-confabulation wall stays in the dynamic tail**, ~1,840 tokens re-sent every turn, for RECENCY.
  Measured, offered, and refused: a priced trade-off, not an oversight.
- **A predicate whose name answers a different question than its body** is how the duel crash survived two
  fixes. `Settlement.CurrentSettlement` is set for a party merely STANDING ON a settlement; only
  `PlayerEncounter.InsideSettlement` means inside.

**Open, and not to be assumed working:**

- **Nobody has ever read a council sitting.** Several increments of content reach the table unjudged.
- **The companion audience inside a town is REASONED, not reproduced.** `cr.audience ask <name>` raises the
  real popup on demand; it has not been walked.
- **`take_into_service` has never once completed in a live game.**
- **A BACKGROUND role exists since 2026-09-19** (`ServiceRole.Housekeeping`), asked for by fkasad, who runs a
  local model and paid for its absence in evicted KV cache. Blank means the prose model, guarded by
  `cr.testall`'s `provider_routing`. **DURABLE, and I had this wrong for a day: `ModelOverride` was NOT already
  set by every request.** An inventory found it on exactly two (`NpcChatService`, and explicitly null in
  `ActionInterpreterComposer`) and on none of the ten housekeeping calls. The route taken instead is a second
  CLIENT, mirroring `InterpreterClient`, because a client carries provider, base URL and key as well as the
  model, which is what a player pointing background work at another server actually needs.
  - **`ChronicleIlluminator` is deliberately NOT on it.** It is background by shape and prose by purpose: the
    player reads the illuminated chronicle, and it is the only one of the family with no `ReasoningOverride`,
    at Creativity 0.7 and 1200 tokens. Moving it would quietly degrade something visible.
- **Raised and deliberately not built:** a grievance has no notion of having been answered (a lord asks why
  you killed his kinsman however often you explain); `RumorAboutPlayer` has no magnitude filter, so a cleared
  looter camp counts as much as a city falling; profession-shaped reasons to approach (Lordfadooboo offered
  to write them).
- **The mood of the day**: increment 1 only. No host wiring, no prompt, no player influence.
- **Nexus text not yet pasted**: `NEXUS_PINNED_ROADMAP.txt`.

**Two measured facts worth carrying:**

- A turn makes TWO LLM calls, and the interpreter's is the bigger one. Reason about cost in pairs.
- Realised cache rates SWING (0, 0, 98%, 77%, 96% across five turns) because OpenRouter routes to different
  backends and a cold one starts over. Never promise a player a single figure.
