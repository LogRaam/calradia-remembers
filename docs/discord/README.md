# Discord tutorials

The tutorials posted on the Calradia Remembers Discord server, written 10/11/2026 against the 2.8.8 work in progress
(mod `83b11f4`). Each file is cut into messages under Discord's 2,000-character limit, separated by
`===== MESSAGE N =====` lines that are not pasted. Discord markdown, no BBCode, no em dash.

**Keep them true.** When a change touches what a tutorial describes (a folder, a command, an MCM label, a file format,
a default), update the tutorial in the same commit, as for the CHANGELOG. The table says what each one rests on.

| File | Tutorial | Rests on |
|---|---|---|
| `getting-started.md` | Key, models and costs | `Foundation/CrSettings.cs` (LLM Provider, OpenRouter, OpenAI-Compatible groups, defaults); the cost tables of `NEXUS_DESCRIPTION.bbcode.txt` ("WHAT IT COSTS, MEASURED", v2.7.20) |
| `benchmark.md` | Benchmarks in the console | `Diagnostics/CostBench.cs`, `Logic` `CostScenarioFile` (sections, row format, $10 default cap), `cr.rp_bench`, `cr.action_bench`, `cr.memory_bench` |
| `portraits.md` | Chat portraits | `Portraits/ChatPortraitResolver.cs`, `Logic/ChatPortraitFolders.cs`, `cr.portrait_id`, `RuntimePortraitFactory` cache (a replaced file needs a restart) |
| `characters.md` | character_overrides.json | `ModuleData/character_overrides.json` (field list), `Lore/CharacterOverrideLoader.cs`, MCM "Use custom traits", `cr.reload_lore`, `cr.lore_status`, `cr.backstory` |
| `custom.md` | The Custom folder | `Mapping/NarrativeLoader.cs` (`CustomBaseFolder`, `ResolveCustomFirst`), `ModuleData/Custom/README.txt`, MCM "Custom Folder Path", `cr.lore_status`, `cr.reload_prompts` |
| `styles.md` | Narrative styles | `ModuleData/Styles/` (the shipped list and its README), `Styles/StyleCatalog.cs`, `Styles/StyleService.cs`, `MaxNarrativeStyleChars` (1500, 1200 at the council) |
| `scenario.md` | Scenario Composer | `Extension/ScenarioComposer.html` (vocabulary), `Scenarios/`, `EXTENSION_API.md` "Scenario packs", `cr.scenarios`, `cr.scenario_fire` |
| `history.md` | History Editor | `Extension/HistoryEditor.html`, `WorldEvents/AuthoredHistoryBehavior.cs`, `docs/design/AUTHORED_HISTORY.md`, `cr.history_file`, `cr.history_reload` |
| `tale.md` | Tale Editor | `Extension/TaleEditor.html`, `Extension/HOWTO_Quests.md`, MCM "Authored Quests (experimental)" (off by default), `cr.story_*` |
| `crvid.md` | Making a .crvid | `Extension/CrvidPlayer.html` (SSIM default 0.960, 768x1152, 30 fps), `HOWTO_Quests.md` "A picture that moves", `cr.video show` |
| `stations-folklore.md` | Stations and folklore | `ModuleData/stations/`, `ModuleData/folklore/`, `Logic/Folklore/FolkTalePolicy.cs` (`MaxTalesKnown` 6), `cr.station` |
| `prompt-profiles.md` | Prompt Composer | `Logic/PromptProfiles.cs` (profile.json, layouts, sections), `docs/maps/prompt-sections.md` (locks, shipped as `Docs/PROMPT_SECTIONS.md`), `docs/maps/prompt-pipelines.md` (shipped as `Docs/PROMPT_PIPELINES.md`), `cr.prompt_check` |
| `translation.md` | Translating the mod | `ModuleData/Languages/` (README, EN template), what is translatable today (the MCM page only) |
| `modders.md` | Plugging a mod into CR | `EXTENSION_API.md`, `Extension/HOWTO_*.md`, the `cr_*` data folders, `cr.api_probe`, `cr.heard` |
| `tales-play.md` | Playing the Tales | `Quests/AuthoredQuestBehavior.cs` (AskToAccept "Take it on"/"Not now", OpenScene waits for an open chat, CloseScene tells missed clues then moves on, AnnounceStage "Step complete"), `ChatViewModel.Exploration.cs` (the "Everything this place had to show you is found" note), `Logic/AuthoredQuests/SceneChoiceMenu.cs` ("Something else..."), MCM "Authored Quests (experimental)" (off by default), `cr.story_status/step/abandon` |
| `console.md` | Console commands for players | `Diagnostics/ConsoleCommandHelp.cs` (every command named exists there), `CalradiaRemembers/CONSOLE_COMMANDS_GUIDE.md`, MCM Diagnostics "Report button in the chat" (on by default) |

## Before 2.8.8 is live

Some lines say "from 2.8.8" for what that version brings: the Scenario Composer's new example, the CRVID Player in
`Docs/`, portraits in `Custom/ChatPortraits`, `Docs/PROMPT_SECTIONS.md`. Posted earlier, they describe what players do
not have yet:

- `prompt-profiles.md` needs `Docs/PROMPT_SECTIONS.md`: post it once 2.8.8 is live. Its line on `Docs/PROMPT_PIPELINES.md` needs 2.8.9.
- `crvid.md`: until then, attach `CalradiaRemembers/Extension/CrvidPlayer.html` to the post.
- Once 2.8.8 is live, the "from 2.8.8" mentions can go.
