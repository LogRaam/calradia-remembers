# PROJECT_STATE.md — Calradia Remembers

**Last updated**: 2026-05-18
**Owner**: Gabriel Mailhot (LogRaam)
**Workspace**: `V:\OneDrive\Programmation\GIT Workspace\`

---

## Quick Resume Instructions for Claude

If you are reading this at the start of a new conversation:

1. Read this entire document before responding.
2. The user is mid-development on Phase 7 — Bannerlord mod integration.
3. The SDK Core (`NpcMemoryService.Core`) is mature and stable.
4. The mod (`Calradia Remembers`) is actively being built.
5. **Current position**: Sprint 7.3.C2 just finished (in-game validation done with `close_window`), proceeding to 7.3.C3.
6. The user prefers brief responses, prose over bullets, French for chat / English for code.
7. The user uses **Opus 4.7** for high-complexity sprints (dialogue, async, state machines) and **Sonnet 4.6** for boilerplate.
8. **Ask the user to confirm the model version before starting a task.** This is a hard preference.

---

## Project Overview

**Calradia Remembers** is a Bannerlord mod that gives NPCs persistent long-term memory across save sessions, using an LLM (Grok 4.3 via OpenRouter) for dialogue generation.

The mod consumes a separate SDK called **NpcMemoryService**, designed framework-agnostic so it can theoretically work with any game.

### Two Separate Solutions

| Solution | Location | Purpose |
|---|---|---|
| `LLM Roleplay Service` | `V:\OneDrive\Programmation\GIT Workspace\LLM Roleplay Service\` | The SDK Core + tests + ConsoleRunner |
| `Calradia Remembers` | `V:\OneDrive\Programmation\GIT Workspace\Calradia Remembers\` | The Bannerlord mod consuming the SDK |

The mod references the SDK via `ProjectReference` (not via compiled DLL).

---

## Architecture

### SDK Core (`NpcMemoryService.Core`)

- **Target**: `netstandard2.0` (so it works with .NET Framework 4.7.2 Bannerlord)
- **Language**: C# with `LangVersion=latest`, `Nullable=enable`
- **JSON**: Originally `System.Text.Json`, but the **mod** uses Newtonsoft.Json (already shipped by Bannerlord) for persistence
- **Polyfills**: `Polyfills/IsExternalInit.cs` contains compiler-required types (see "Pitfalls" below)

Key classes:
- `Models/NpcProfile.cs` — persistent NPC state (name, faction, clan, personality, events, reputation)
- `Models/NotableEvent.cs` — **primary constructor** `(int GameDay, NotableEventType Type, string Summary)`
- `Models/EncounterContext.cs` — volatile per-encounter state (Sprint 7.3.B addition)
- `Models/ChatSession.cs` — conversation history within one encounter
- `Models/WorldState.cs` — current world state (day, conflicts, rumors)
- `Services/NpcChatService.cs` — orchestrates one dialogue turn
- `Prompts/PromptBuilder.cs` — assembles system prompts with stability-ordered sections
- `Storage/INpcMemoryStore.cs` — abstraction over persistence
- `LlmClient/OpenRouter/OpenRouterClient.cs` — HTTP client to OpenRouter

### Bannerlord Mod (`CalradiaRemembers`)

- **Target**: `net472` (mandatory for Bannerlord)
- **Language**: C# with `LangVersion=10.0`, NO nullable annotations (avoid friction on net472)
- **Platform**: `PlatformTarget=x64` (matches Bannerlord's AMD64 DLLs)
- **Game install**: `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord`
- **Module folder**: `Modules\CalradiaRemembers\` with `bin\Win64_Shipping_Client\` subfolder

#### Folder structure

```
Calradia Remembers/
├── CalradiaRemembers.slnx
├── CalradiaRemembers/
│   ├── CalradiaRemembers.csproj
│   ├── NpcMemoryServiceSubmodule.cs        ← MBSubModuleBase entry point
│   ├── ModCheats.cs                         ← cheat console commands
│   ├── ModLog.cs                            ← in-game console + file logging
│   ├── Behaviors/
│   │   └── NpcMemoryCampaignBehavior.cs     ← CampaignBehaviorBase singleton
│   ├── Services/
│   │   └── LlmServices.cs                   ← OpenRouterClient + NpcChatService wiring
│   ├── GameState/
│   │   ├── IGameStateBridge.cs              ← read-only abstraction over TaleWorlds API
│   │   └── BannerlordGameStateBridge.cs
│   ├── Mapping/
│   │   ├── HeroProfileMapper.cs             ← Hero → NpcProfile (from traits)
│   │   └── EncounterContextBuilder.cs       ← live game state → EncounterContext
│   ├── Storage/
│   │   └── BannerlordNpcMemoryStore.cs      ← uses IDataStore + Newtonsoft.Json
│   ├── Diagnostics/
│   │   └── LlmDiagnostic.cs                 ← `campaign.test_llm` cheat handler
│   └── Dialogue/
│       └── LlmDialogFlow.cs                 ← AddPlayerLine / AddDialogLine registrations
└── ModuleFolder/
    └── SubModule.xml                         ← deployed to Modules\CalradiaRemembers\
```

---

## Status of Each Phase / Sprint

### Phase 7.1 ✅ — Mod skeleton loads
- Validates: SubModule.xml loads, MBSubModuleBase hooks fire, behavior registers.
- 5 lifecycle messages confirmed in-game.

### Phase 7.2 ✅ — Persistence via IDataStore
- `BannerlordNpcMemoryStore` serializes via Newtonsoft.Json into a single `IDataStore.SyncData<string>` call.
- Test profile "Raganvad" survives save/load cycle. Persistence OK.

### Sprint 7.3.A ✅ — LLM plumbing
- `LlmServices` reads `OPENROUTER_API_KEY` from Windows env var at startup.
- `campaign.test_llm` cheat command sends fixed prompt to Grok 4.3, logs response.
- Validated end-to-end: real LLM call from inside Bannerlord works.

### Sprint 7.3.B ✅ — Bridge + Mapping + EncounterContext
- `EncounterContext` model added to Core with 4 fields (player status, war status, days since last meeting, scene type).
- `NpcChatService.ChatAsync` accepts optional `EncounterContext`.
- `PromptBuilder.BuildSystemPrompt` injects `EncounterContext.ToPromptDescription()` at the end (volatile position, preserves cache).
- `IPromptBuilder` interface updated accordingly.
- `IGameStateBridge` (read-only) implemented by `BannerlordGameStateBridge`.
- `HeroProfileMapper` derives personality from Bannerlord traits (Honor, Mercy, Valor, Generosity, Calculating) + culture + rank.
- `EncounterContextBuilder` produces EncounterContext from live state.
- Validated: real Hero personality + context picked up by LLM, second encounter references first.

### Sprint 7.3.C1 ✅ — Dialog flow stub
- `LlmDialogFlow.Register` adds player line "Let's talk freely, in a different manner..." at priority 110 in `hero_main_options`.
- `IsHeroEligible` condition filters non-Hero conversations.
- Validated: option appears in vanilla dialogue, placeholder line shown, dialogue closes correctly.

### Sprint 7.3.C2 ✅ — Sub-menu + TextInquiry popup
- NPC says "I'm listening." and enters `calradia_remembers_submenu` state.
- Sub-menu: **Speak** (opens TextInquiry popup) / **End conversation.** (closes dialogue).
- TextInquiry popup captures player message and logs it.
- After Confirm/Cancel, sub-menu loops back so player can speak again via silent `ResumeTag` line.
- **End conversation uses `close_window`** (not `hero_main_options`) — see "Dialogue UI lessons" below.
- NO LLM call in this sprint — pure UI plumbing.

### Sprint 7.3.C3 ⏳ — Next (LLM blocking)
- Wire LLM call when player confirms message (blocking wait — game freezes 5-10s).
- Capture vanilla intro line into ChatSession seed (research API: probably `Campaign.Current.ConversationManager`).
- Display LLM response in dialog box.
- Apply parsed [EVENT] / [MEMORY] / [REPUTATION] to NpcProfile.
- Persist via store.

### Sprint 7.3.C4 ⏳ — After C3 (non-blocking)
- Non-blocking pattern: "(Send a message and wait for the NPC's response)" + "View NPC's response" button.
- Notification popup + sound when LLM response ready.
- State machine: `ConversationOrchestrator` (Idle → AwaitingInput → SendingToLlm → ReadyToView → Idle).

### Sprint 7.4 ⏳ — Stabilization
- Error handling, retry, fallback messages.
- Cleanup of dev-only log messages.
- MCM settings exposure (model selection, API key as alternative to env var).

### Sprint 7.5 ⏳ — Vanilla Dialogue Choice Capture
- Capture the player's vanilla dialogue option selections (e.g., the 3 introduction options "I am Rugan", "My name is Rugan, sir", "Mark it down").
- Inject these into the LLM context so the LLM interprets the player's tone and adjusts the NPC's first impression.
- Likely requires Harmony to intercept `Conversation.OnConsequence` or similar.
- Strategy: append last 1-3 vanilla selections to `EncounterContext` or as a session seed.

### Sprint 7.6 ⏳ — Custom Gauntlet popup
- **Reason**: Bannerlord's `InformationManager.ShowTextInquiry` interferes with the dialogue state machine in a way that prevents clean return to `hero_main_options` (see "Dialogue UI lessons" below).
- AI Influence solves this by building their own Gauntlet UI Layer instead of using the system popup.
- We will replicate their approach: custom XML prefab + ViewModel + GauntletLayer.
- Benefits: clean UX (no need for `close_window` workaround), customizable visuals (avatar, history, etc.), foundation for the group chat panel in Sprint 8.

### Sprint 8 ⏳ — Multi-NPC awareness (witnesses in conversations)
**Important**: Not a separate "group chat mode" — this is an enrichment of the existing 1-1 dialogue.

**Core philosophy** (per user direction):
> The player remains at the center of attention. The player is the hero of the game. NPCs and witnesses are supporting cast, never carrying their own subplots.

**Mechanism**:
- The **primary** NPC is the one the player initiated dialogue with.
- **Witnesses** are NPCs physically present in the scene (companions, guards, other lords in the throne room, tavern patrons).
- The primary speaks by default. Witnesses intervene only when narratively justified.

**Witness intervention rules** (Variant A, user-validated):
- The player explicitly addresses a witness (*"Siga, tell him what you saw"*)
- The primary explicitly addresses a witness (*"Captain, what do you say?"*)
- The LLM judges intervention narratively necessary (strong emotional reaction)

**Architecture changes needed**:
- `ChatSession` extended with `PrimarySpeaker` + `AvailableSpeakers` (list of NPC IDs)
- `NpcChatService.ChatAsync` accepts `IReadOnlyList<NpcProfile> witnesses` (can be empty for pure 1-1)
- `PromptBuilder` gets new section listing witnesses with brief profiles
- `SectionResponseParser` parses `[DIALOGUE speaker="Name"]...[/DIALOGUE]` format
- Memory dispatcher: each NPC who spoke (or was significantly involved) gets a perspective-specific event in their profile
- Bannerlord side: `Location.GetCharacterList()` (or equivalent) to detect present NPCs

**Cost control**: Still ONE LLM call per turn, regardless of how many witnesses present. The LLM orchestrates the whole scene in a single response.

**Scope distinction from AI Influence's tavern group chat**:
- AI Influence does N LLM calls (one per NPC who speaks) with text-over-head display. Expensive and disjointed.
- Our approach: 1 LLM call orchestrates the entire scene. Cheaper, more coherent narratively, more flexible.

### Sprint 9+ (future) — Bidirectional vanilla integration
- LLM-emitted events (Betrayal, Agreement) trigger vanilla `ChangeRelationAction` and similar.
- Vanilla relationship changes propagate to NpcProfile.
- Result: NPCs' vanilla dialogue lines naturally shift in tone, matching what happened in LLM conversations.

---

## Key Design Decisions

### Why this architecture?

| Decision | Rationale |
|---|---|
| SDK Core agnostic (no Bannerlord refs) | Reusable for other games; testable without TaleWorlds dependencies |
| `IGameStateBridge` interface | Single integration point with TaleWorlds API; future write operations (ChangeRelation, etc.) added here |
| Newtonsoft.Json in mod (not System.Text.Json) | Bannerlord ships Newtonsoft, eliminates ~10 transitive DLL deps |
| `LangVersion=10.0`, no `Nullable=enable` in mod | Avoid net472 friction with init-only setters and nullable annotations |
| `PlatformTarget=x64` but NOT `<Platforms>x64</Platforms>` | The first sets compiler arch, the second creates solution configurations that conflict with Any CPU |
| EncounterContext injected at END of system prompt | Stable sections (identity, personality, history) come first → preserves prefix cache on Grok |
| Hero.StringId as profile key (not Hero ref) | Survives Hero death/disposal without breaking the save |
| Mod is **additive** to vanilla dialogue | Vanilla mechanics (quests, demands, recruitment) remain unchanged. Our option enriches without replacing. |
| Witnesses in dialogues, not "group chat mode" | The player stays the protagonist. Witnesses serve narrative, never carry their own threads. |

### Approach to AI Influence (the prior-art reference mod)

AI Influence was reverse-engineered (decompiled with dnSpy, UI XMLs inspected). Key findings:
- Uses official `CampaignGameStarter.AddDialogLine` API (no Harmony for dialogue).
- Has a "View NPC's response" pattern with non-blocking async (the goal for C4).
- Has a flaw: ignores the vanilla intro line, causing narrative rupture.
- Uses **custom Gauntlet popup** (`AIInfluenceTextQueryPopupManager.Show`) instead of `InformationManager.ShowTextInquiry`. This is why they don't suffer the dialogue state machine interference we discovered (Sprint 7.6 will replicate this).
- Does group conversations via N separate LLM calls with text-over-head — expensive. Our Sprint 8 design replaces this with a single multi-NPC orchestration.

**Our advantages** (vs AI Influence):
- We will capture the vanilla intro and inject it into LLM context (Sprint 7.5).
- Witnesses (Sprint 8) — multi-NPC scenes for ~1 LLM call instead of N.
- Future: vanilla relationship integration (Sprint 9+).

---

## Dialogue UI Lessons Learned

### The `close_window` mystery (Sprint 7.3.C2)

**Symptom**: When `End conversation` had `outputToken: "hero_main_options"`, the first option of the vanilla menu (typically the Dragon Banner quest "What is Neretzes' Folly?") auto-triggered without user click.

**Diagnostic process**:
1. Tested with priority=110 (option at top): bug present
2. Tested with priority=50 (option at bottom): bug still present, same quest fires
3. Conclusion: It's not positional click buffering. It's **auto-progression of the first menu option** after returning to `hero_main_options` from a sub-flow.

**Why AI Influence doesn't have this bug**:
They use a **custom Gauntlet popup** (`AIInfluenceTextQueryPopupManager`) instead of `InformationManager.ShowTextInquiry`. The system popup interacts with the dialogue state machine; the Gauntlet layer floats independently. When their popup closes, the conversation manager hasn't been touched.

**Our workaround for now**: `outputToken: "close_window"` — fully closes the dialogue. Player must re-click the lord to access vanilla menu options. This is acceptable for V1 but motivates Sprint 7.6.

### Token reference (Bannerlord dialogue states we use or know)

| Token | Meaning |
|---|---|
| `hero_main_options` | Standard main menu after NPC greeting (vanilla, our entry point) |
| `close_window` | Closes the dialogue completely |
| `start` | (reserved by Bannerlord, root of conversation tree) |
| Custom tokens we use | `calradia_remembers_entry`, `calradia_remembers_submenu`, `calradia_remembers_resume` |

---

## Pitfalls Already Encountered (Don't Repeat)

### 1. ReSharper renames polyfill namespaces

ReSharper's "Adjust namespaces to match folder structure" rule WILL break `Polyfills/IsExternalInit.cs`. The file MUST stay in `namespace System.Runtime.CompilerServices` regardless of folder.

**Mitigation**: file has `// ReSharper disable CheckNamespace` directive at top + warning comment.

Other polyfilled types (in `IsExternalInit.cs`):
- `System.Runtime.CompilerServices.IsExternalInit`
- `System.Runtime.CompilerServices.RequiredMemberAttribute`
- `System.Runtime.CompilerServices.CompilerFeatureRequiredAttribute`
- `System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute`

### 2. `NotableEvent` uses primary constructor

Don't use object initializer syntax `new NotableEvent { Type = ..., Summary = ..., GameDay = ... }`.

Use named arguments: `new NotableEvent(gameDay: 142, type: NotableEventType.FirstMeeting, summary: "...")`.

### 3. `BaseOutputPath` vs `OutputPath` in csproj

MSBuild appends Configuration\Framework\Platform to `BaseOutputPath`. Use `OutputPath` directly with `AppendTargetFrameworkToOutputPath=false` and `AppendRuntimeIdentifierToOutputPath=false`.

### 4. `<Configurations>` and `<Platforms>` in csproj

Don't set these in the mod csproj. They create solution configurations that don't match the .slnx and cause "unknown project configuration" errors. Let the solution manage configurations.

### 5. `<Nullable>enable</Nullable>` in net472 mod

Triggers "Feature 'nullable reference types' is not available" errors. Either set explicit `<LangVersion>10.0</LangVersion>`, or just remove nullable entirely from the mod (recommended).

### 6. `CS0579 Duplicate assembly attributes`

SDK-style csproj auto-generates AssemblyInfo attributes. If a `Properties\AssemblyInfo.cs` already exists, this creates duplicates.

**Fix**: `<GenerateAssemblyInfo>false</GenerateAssemblyInfo>` in csproj.

### 7. `MSB3270 architecture mismatch`

Add `<PlatformTarget>x64</PlatformTarget>` (NOT `<Platforms>x64</Platforms>`) to match Bannerlord's AMD64 DLLs.

### 8. `HttpClient could not be found`

`System.Net.Http` is not auto-referenced in SDK-style net472 projects.

**Fix**: `<Reference Include="System.Net.Http" />` in an ItemGroup.

### 9. `SubModule.xml` case sensitivity

The module ID `Sandbox` (lowercase b) is used by Bannerlord, NOT `SandBox`. Mismatch causes "Missing 'SandBox'" error in launcher.

### 10. `async void` is an anti-pattern

Sonar S3168 flags this correctly. For fire-and-forget from synchronous contexts (like the cheat console), use:

```csharp
_ = SomeMethod().ContinueWith(t =>
{
    if (t.Exception != null)
        ModLog.Error($"Faulted: {t.Exception.GetBaseException().Message}");
}, TaskScheduler.Default);
```

### 11. `InformationManager.ShowTextInquiry` interferes with dialogue state machine

See "Dialogue UI Lessons Learned" section above. Sprint 7.6 will replace it with a custom Gauntlet popup.

---

## Conventions Adopted

### Logging

All mod logs go through `ModLog`:
- `ModLog.Info(message)` — writes to in-game console (top-left, briefly) AND to `%USERPROFILE%\Documents\Mount and Blade II Bannerlord\Logs\CalradiaRemembers.log`
- `ModLog.Error(message)` — same destinations, red color in-game

The in-game console history is accessible by pressing **Enter** in-game (Combat Log).

### Naming

- **Mod display name** (SubModule.xml `<Name>`): `Calradia Remembers`
- **Mod folder / Id**: `CalradiaRemembers`
- **Behavior namespace**: `CalradiaRemembers.Behaviors`
- **DLL name**: `CalradiaRemembers.dll`
- **Cheat log tag**: `NPC Memory Service` (legacy, kept for now)

User indicated:
- Player-facing notifications → use "Calradia Remembers"
- Internal/dev logs → "NPC Memory Service" is fine (or switch later)
- File logs → `CalradiaRemembers.log`

### Code style

- C# 10 features OK in mod (records, pattern matching, `is not null`, etc.)
- NO nullable annotations (`?`) in mod (net472 friction)
- Named arguments preferred for constructors with 2+ params (esp. NotableEvent primary ctor)
- 4-space indentation, opening brace on next line (Allman)
- File-scoped namespaces NOT used (legacy style with braces)
- Region tags `#region` / `#endregion` used in the Core, not in mod
- Comments in English, in code; chat conversation in French

### Testing

- Validation is primarily **in-game**, not unit tests, for the Bannerlord layer
- The Core has NUnit + FluentAssertions tests
- Each sprint is validated before the next starts
- The cheat command `campaign.test_llm` is the workhorse for testing the LLM stack without UI

---

## Environment

### Hardware
- Alienware Aurora R13 (RTX 3080)

### Software stack
- **OS**: Windows 11 Home
- **IDE**: Visual Studio 2022 (with ReSharper installed — beware namespace adjustments)
- **Bannerlord version**: Default Public Version (released 2026-03-02)
- **Bannerlord install**: `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord`
- **LLM provider**: OpenRouter
- **Model**: `x-ai/grok-4.3` (previously grok-4.1-fast, now deprecated)

### API Key

`OPENROUTER_API_KEY` is set as a Windows User-level environment variable. Both ConsoleRunner and mod read it from `Environment.GetEnvironmentVariable("OPENROUTER_API_KEY")`.

### Build / Deploy

The mod csproj has an `AfterTargets="Build"` step (`DeployToBannerlord`) that copies all output DLLs to `Modules\CalradiaRemembers\bin\Win64_Shipping_Client\`. So F5 in VS = build + deploy in one action.

---

## How to Resume

When the user signals they want to continue:

1. Confirm you have read this document.
2. Confirm the model version (Opus 4.7 or Sonnet 4.6) before any task.
3. Most likely next task: **Sprint 7.3.C3** — wire the LLM call into the dialogue flow in blocking mode.
4. For 7.3.C3, ask for the current `ChatSession.cs` and `NpcChatService.cs` if not already in context, plus `LlmDialogFlow.cs` (current state, with `close_window` on End conversation).
5. Refer back to the Pitfalls list whenever encountering compilation errors.

The user is **autonomous** and **technically expert**. Don't over-explain. Be direct, brief, and offer choices when there's real ambiguity.

---

## Project Files To Re-Upload at Start of New Conversation

If starting fresh, the user can re-upload these files for the most current state:

**Mod (Calradia Remembers)** — likely all relevant in the workspace:
- `CalradiaRemembers.csproj`
- `Behaviors/NpcMemoryCampaignBehavior.cs`
- `Services/LlmServices.cs`
- `GameState/IGameStateBridge.cs` + `BannerlordGameStateBridge.cs`
- `Mapping/HeroProfileMapper.cs` + `EncounterContextBuilder.cs`
- `Dialogue/LlmDialogFlow.cs`
- `Storage/BannerlordNpcMemoryStore.cs`
- `Diagnostics/LlmDiagnostic.cs`
- `ModCheats.cs`
- `ModLog.cs`
- `NpcMemoryServiceSubmodule.cs`
- `ModuleFolder/SubModule.xml`

**Core (NpcMemoryService.Core)** — most recently touched:
- `Models/EncounterContext.cs`
- `Services/NpcChatService.cs`
- `Prompts/PromptBuilder.cs` + `IPromptBuilder.cs`
- `Polyfills/IsExternalInit.cs`

Older Core files (likely unchanged): `Models/NpcProfile.cs`, `Models/NotableEvent.cs`, `Models/ChatSession.cs`, `Models/WorldState.cs`, `Storage/INpcMemoryStore.cs`, `LlmClient/OpenRouter/OpenRouterClient.cs`, `Parsing/SectionResponseParser.cs`.

---

## Personal Notes (user-provided context)

- Gabriel is a Senior Subject Matter Expert at Desjardins, primarily focused on architecture, development practices, and complex organizational problems.
- His AI research background (2003-2010) is academic; he is not currently leading AI practices at his workplace.
- He builds in C# and PowerShell.
- He values: layered architecture, microservice mindset, cohesion + decoupling balance.
- He prefers: pushback over agreement, honest critique over flattery, brief responses over long ones.
- He writes on scripting.pro (Hugo) and LinkedIn — targets senior practitioners.
- The mod ecosystem he's developed in includes Skyrim, Starfield, and now Bannerlord.
- He owns *LogRaam's Deadly Spears* (a sister Bannerlord mod) and uses its csproj as reference for setup.

### Vision philosophy for Calradia Remembers

- The **player is the protagonist**. NPCs serve narrative, never carry their own subplots.
- The mod is **additive** to Bannerlord vanilla — no replacement of mechanics, only enrichment.
- The LLM is a **narrative tool**, not a simulation engine.
- Memory persistence is the **core value proposition** — what differentiates Calradia Remembers from AI Influence.
- **Coherence between mod and vanilla** is a design priority — events from LLM should eventually impact vanilla relationships and vice-versa.
