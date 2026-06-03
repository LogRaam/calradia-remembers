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

LLM: OpenRouter, model `x-ai/grok-4.3`. API key: env var `OPENROUTER_API_KEY`.

Bannerlord: v1.3.15, Default Public (Mar 2026).

---

## Code style (non-negotiable)

- **Indentation**: 4 spaces in mod, 3 spaces in SDK Core
- **Braces**: Allman style
- **Usings**: wrapped in `#region` / `#endregion` at top of every file
- **Docs**: triple-slash XML (`///`) on all public types and methods
- **Comments**: only when the WHY is non-obvious — never explain what the code does
- **Naming**: PascalCase methods/properties, `_camelCase` private fields
- **C# net472**: no `Nullable enable`, no `record` types; use polyfill `IsExternalInit.cs` for `init` setters
- **File header**: `// Code written by Gabriel Mailhot, DD/MM/YYYY.` (first line, sprint note on 2nd if relevant)

---

## Architecture

```
Player message
    │
ChatViewModel.ExecuteSendMessage
    │
    ├── profile.ReputationWithPlayer = bridge.GetPlayerRelation(npc)   [sync before prompt]
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

## RelationGate policy (Mapping/RelationGate.cs)

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
```

---

## Workflow preferences

- **Language**: Gabriel writes in French; all code and files in English.
- **Before each task**: suggest the model (Sonnet 4.6 / Opus) and wait for explicit confirmation before implementing.
- **Scope**: no cleanup, no extra abstractions, no refactoring beyond the task.
- **Commits**: never commit without explicit request. When asked: both repos (mod + SDK) unless stated.

---

## Current sprint state

> This is the only section that changes per sprint. Keep it short.

**Sprint 14** ✅ build OK (pending in-game validation) — last completed sprint.

Multi-NPC witnesses:
- `WitnessEntry` model (SDK) + `EncounterContext.Witnesses` + `EncounterContext.PrivacyRequested`
- `PromptBuilder.AppendWitnesses`: injects `WITNESSES PRESENT` section; `request_privacy` ACTION instructions when pending
- Privacy gate in `AppendIntimacyConsentRules`: with witnesses → physical intimacy blocked; except Hardcore+captive
- `WitnessResolver` (mod): settlement branch (HeroesWithoutParty + settlement.Parties) + campaign map; filters known profiles; companion picker via `ShowMultiSelectionInquiry`; `DiagnoseWitnesses` for `cr.witnesses`
- `ChatViewModel`: `_witnesses` list, `WitnessesText`/`HasWitnesses`/`IsPrivateRequestVisible` properties, `ExecuteRequestPrivate` (auto-sends synthetic LLM message), `request_privacy` action handler
- XML: TitleBar 95px, witnesses line + "Request Private" button
- `LlmDialogFlow.OnPlayerSelectedChat`: `ResolveAutoWitnesses` → `SetWitnesses` → `ShowCompanionPickerIfNeeded`
- Console: `cr.witnesses [name]` (diagnostic), `cr.witness [name]` (seed), `cr.rep_set <v> [name]` (force rep)
- **⚠️ Unverified**: does `Hero.CurrentSettlement` populate during Talk? Run `cr.witnesses [name]` in Keep to confirm.

**Next**: in-game validation of WitnessResolver, then Sprint 12c — Visible courier party
