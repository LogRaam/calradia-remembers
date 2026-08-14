// Code written by Gabriel Mailhot, 10/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.Dynasty;
using CalradiaRemembers.GUI;
using CalradiaRemembers.Jealousy;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Lore;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Quests;
using CalradiaRemembers.Scenarios;
using CalradiaRemembers.Services;
using Newtonsoft.Json;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>
      ///   cr.chat_screen [on|off] — toggles how the chat is hosted (applies to the NEXT chat opened).
      ///   on  = pushed screen: the map is suspended (no VRAM leak) and other screens/menus take focus natively.
      ///   off = overlay (original): the map stays live behind the chat, but input is grabbed and the naval map
      ///   leaks VRAM. Flip it to compare the two head to head without restarting.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("chat_screen", "cr")]
      public static string ChatScreenToggle(List<string> args)
      {
         bool on = args is {Count: > 0}
            ? !string.Equals(args[0], "off", StringComparison.OrdinalIgnoreCase)
            : ChatManager.HostMode != ChatManager.ChatHostMode.PushedScreen;

         ChatManager.HostMode = on
            ? ChatManager.ChatHostMode.PushedScreen
            : ChatManager.ChatHostMode.Overlay;

         return on
            ? "Chat host = PUSHED SCREEN (map suspended, native focus). Opens on the NEXT chat."
            : "Chat host = OVERLAY (original: live map behind, input grabbed). Opens on the NEXT chat.";
      }

      /// <summary>
      ///   cr.scenario_fire &lt;VERB[:payload]&gt; [heroName]
      ///   Manually fires a trigger to test scenarios without waiting for the real event. An optional hero
      ///   name becomes the event's primary hero (and their faction a context faction); the player is always
      ///   included. E.g. cr.scenario_fire ON_DEATH Rhagaea.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("scenario_fire", "cr")]
      public static string FireScenario(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;
         if (args == null || args.Count == 0) return "Usage: cr.scenario_fire <VERB[:payload]> [heroName]";

         ScenarioRuntime runtime = ScenarioBehavior.Instance?.Runtime;

         if (runtime == null) return "Scenario engine not ready.";

         string verb = args[0];
         List<string> nameArgs = args.Count > 1
            ? args.GetRange(1, args.Count - 1)
            : null;
         Hero hero = nameArgs != null
            ? ResolveHero(nameArgs)
            : null;

         if (nameArgs != null && hero == null) return $"No hero matched \"{string.Join(" ", nameArgs)}\".";

         var heroes = new List<Hero>();
         if (hero != null) heroes.Add(hero);
         if (Hero.MainHero != null) heroes.Add(Hero.MainHero);

         var factions = new List<IFaction>();
         if (hero?.MapFaction != null) factions.Add(hero.MapFaction);

         var ctx = new ScenarioContext(heroes, factions);
         ScenarioRule fired = runtime.Fire(KeywordToken.Parse(verb), ctx);

         return fired != null
            ? $"Fired '{fired.Id}' on {verb}."
            : $"No rule fired for {verb}.";
      }

      /// <summary>
      ///   cr.geo [something you might say about a place]
      ///   Prints the geography-grounding note CR would inject for the NPC you are talking to: the encyclopedia
      ///   blurb of where they stand, plus a fact line and blurb for any settlement your text names. Use it to
      ///   confirm the LLM is now told the real lay of the land (report: NPCs invented where places sit).
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("geo", "cr")]
      public static string Geography(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;

         var hero = Hero.OneToOneConversationHero;

         if (hero == null) return "Talk to an NPC first (cr.geo reads the geography note for your conversation partner).";

         string message = args is {Count: > 0}
            ? string.Join(" ", args)
            : null;
         string note = SettlementContextResolver.Resolve(hero, message);

         return string.IsNullOrEmpty(note)
            ? $"{hero.Name}: no geography note (not in a settlement, and your text named no known place). Try: cr.geo tell me about <place>."
            : $"{hero.Name} would be told:\n{note}";
      }

      /// <summary>
      ///   cr.impression [name]
      ///   Prints the "first impression" opening disposition CR would inject for a barely-known noble (or the
      ///   current conversation partner): how their traits and your standing colour their opening tone. Blank
      ///   means a neutral opening, an own-clan lord, or an already-known NPC (their stance governs).
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("impression", "cr")]
      public static string Impression(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or talk to one first.";

         NpcProfile profile = behavior.Store.Get(hero.StringId);
         string note = FirstImpressionResolver.Resolve(hero, profile, behavior.GameStateBridge);

         return string.IsNullOrEmpty(note)
            ? $"{hero.Name}: no first impression (neutral opening, your own clan, or already known)."
            : $"{hero.Name} opens as:\n{note}";
      }

      /// <summary>
      ///   cr.journal_check
      ///   Diagnostic: prints how many CalradiaRemembers quests are registered in
      ///   Campaign.Current.QuestManager and lists their StringIds. Use to verify
      ///   that cr.quest actually created a QuestBase mirror in the journal.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("journal_check", "cr")]
      public static string JournalCheck(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;

         QuestManager qm = Campaign.Current?.QuestManager;

         if (qm == null) return "QuestManager not available.";

         List<CalradiaRemembersQuest> crQuests = qm.Quests.OfType<CalradiaRemembersQuest>().ToList();

         if (crQuests.Count == 0)
            return "No CalradiaRemembers quests in QuestManager. (OpenMirror may have failed — check the log or try cr.quest again.)";

         IEnumerable<string> lines = crQuests.Select(q =>
            $"  {q.StringId}  giver={q.QuestGiver?.Name}  isSpecialType={q.IsSpecialQuest}");

         return $"{crQuests.Count} CR quest(s) in QuestManager:\n" + string.Join("\n", lines);
      }

      /// <summary>cr.scenarios — list the loaded scripted-scenario rules and which one-shots have fired.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("scenarios", "cr")]
      public static string ListScenarios(List<string> args)
      {
         if (!TryContext(out _, out string error)) return error;

         ScenarioRuntime runtime = ScenarioBehavior.Instance?.Runtime;

         if (runtime == null) return "Scenario engine not ready.";

         var sb = new StringBuilder();
         sb.AppendLine($"Loaded scenario rules: {runtime.Rules.Count}");
         foreach (ScenarioRule r in runtime.Rules)
            sb.AppendLine($"  [{r.Id}] {r.Trigger}  guards:{r.Guards.Count} consequences:{r.Consequences.Count}{(r.FireOnce ? " (once)" : "")}");
         sb.Append($"Fired one-shots: {(runtime.FiredRuleIds.Count == 0 ? "none" : string.Join(", ", runtime.FiredRuleIds))}");

         return sb.ToString();
      }

      /// <summary>
      ///   cr.mapsuspend [on|off] — toggles the fix that stops the host map scene from rendering behind the chat
      ///   overlay (default ON). Turn it OFF to reproduce the VRAM climb, ON to confirm it goes flat. Applies to
      ///   the NEXT chat opened.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("mapsuspend", "cr")]
      public static string MapSuspendToggle(List<string> args)
      {
         bool on = args is {Count: > 0}
            ? !string.Equals(args[0], "off", StringComparison.OrdinalIgnoreCase)
            : !MapSceneSuspender.Enabled;

         MapSceneSuspender.Enabled = on;

         return on
            ? "Map-scene suspend ON (fix active) — the map stops rendering behind the next chat. Watch [VRAM] go flat."
            : "Map-scene suspend OFF — the map renders behind the chat again (reproduces the leak).";
      }

      /// <summary>
      ///   cr.model [toggle]
      ///   Reports the active model mode (Roleplay or NSFW), the model id it resolves to, whether an explicit
      ///   (consensual/CNC) model is configured in Mod Options, and whether the in-chat mode button would show. Pass "toggle" to flip
      ///   the mode (the same override as the button). Reads settings only; no campaign required.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("model", "cr")]
      public static string ModelModeInfo(List<string> args)
      {
         // This increment flips Roleplay <-> ConsensualIntimate (the consensual-escalation case); Cnc is
         // reserved for automatic scene routing (the NEXT increment).
         if (args is {Count: > 0} && string.Equals(args[0], "toggle", StringComparison.OrdinalIgnoreCase))
            LlmServices.ActiveModelMode = LlmServices.ActiveModelMode == ModelMode.Roleplay
               ? ModelMode.ConsensualIntimate
               : ModelMode.Roleplay;

         bool anyExplicitModel = !string.IsNullOrWhiteSpace(ModSettings.ConsensualModel)
                                 || !string.IsNullOrWhiteSpace(ModSettings.CncModel);
         bool nsfwPinConfigured = !string.IsNullOrWhiteSpace(ModSettings.OpenRouterNsfwProviderPin);
         bool toggleVisible = ModelModePolicy.ToggleVisible(
            anyExplicitModel, nsfwPinConfigured, ModSettings.AdultContentLevel != AdultContentLevel.Off);

         string nsfwPart = anyExplicitModel
            ? $"explicit model configured (consensual '{ModSettings.ConsensualModel}', CNC '{ModSettings.CncModel}')"
            : "no explicit model configured (the roleplay model serves everything)";

         string effort = ModelModePolicy.ResolveReasoningEffort(
            LlmServices.ActiveModelMode, ModSettings.ReasoningEffort, ModSettings.NsfwReasoningEffort);

         // The provider the request would actually go to, which the mode now moves as well as the model: a
         // player routing ordinary chat to the cheapest provider and explicit scenes to an uncensored one has
         // no other way to see WHICH of the two a flip just selected.
         string pin = ModelModePolicy.ResolveProviderPin(
            LlmServices.ActiveModelMode, ModSettings.OpenRouterProviderPin, ModSettings.OpenRouterNsfwProviderPin);
         string pinPart = string.IsNullOrWhiteSpace(pin)
            ? "No provider pinned for this mode (OpenRouter routes freely, so a ':floor' model id can pick the cheapest)"
            : $"Providers pinned for this mode: {pin}";

         return $"Model mode = {LlmServices.ActiveModelMode}, resolves to '{LlmServices.ActiveModelId}', " +
                $"reasoning effort {effort}. " +
                $"{nsfwPart}. {pinPart}. In-chat mode button {(toggleVisible ? "visible" : "hidden")}. " +
                "Use 'cr.model toggle' to flip the mode.";
      }

      [CommandLineFunctionality.CommandLineArgumentFunction("open_chat", "campaign")]
      public static string OpenChat(List<string> args)
      {
         try
         {
            ChatManager.Open();

            return "Chat window opened.";
         }
         catch (Exception ex)
         {
            ModLog.Error($"open_chat failed: {ex.GetType().Name}: {ex.Message}");

            return $"Failed to open chat: {ex.Message}";
         }
      }

      /// <summary>
      ///   cr.profile_check [name]
      ///   Builds (or fetches) the NPC's profile and runs the exact serialize→deserialize
      ///   round-trip the save uses, reporting success or the precise failure. The Newtonsoft
      ///   exception message carries the JSON path (e.g. "Path 'Romantic.Kinks[0]'"), which
      ///   pinpoints the field that breaks saves for freshly-created profiles. No save needed.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("profile_check", "cr")]
      public static string ProfileCheck(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         try
         {
            string json = JsonConvert.SerializeObject(profile);
            JsonConvert.DeserializeObject<NpcProfile>(json); // prove it reads back

            return $"{hero.Name}: profile round-trips OK ({json.Length} chars, {profile.Events.Count} event(s)).";
         }
         catch (Exception ex)
         {
            ModLog.Error($"[PROFILE CHECK] {hero.Name} FAILED round-trip: {ex.GetType().Name}: {ex.Message}");

            return $"{hero.Name}: ROUND-TRIP FAILED — {ex.GetType().Name}: {ex.Message}";
         }
      }

      /// <summary>
      ///   cr.prompt_size [name]
      ///   Reports the size of the system prompt CR would send for this NPC (characters + a rough token estimate)
      ///   and whether the "Compact prompt for small models" mode is on. Toggle it in Mod Options and re-run to see
      ///   the reduction — the fix for a small local model that overflows its context and returns nothing.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("prompt_size", "cr")]
      public static string PromptSize(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;
         if (behavior.LlmServices is not {IsAvailable: true}) return "LLM services not available. Set your OpenRouter API key / endpoint first.";

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         var world = new WorldState {CurrentDay = behavior.GameStateBridge.GetCurrentGameDay()};
         EncounterContext ctx = EncounterContextBuilder.Build(hero, profile, behavior.GameStateBridge);
         string prompt = behavior.LlmServices.ChatService.PromptBuilder.BuildSystemPrompt(profile, world, ctx);

         return $"{hero.Name}: system prompt = {prompt.Length} chars (~{prompt.Length / 4} tokens), " +
                $"mode = {(ModSettings.LeanPrompt ? "Lean (compact)" : "Full")}. Toggle 'Compact prompt for small " +
                "models' (Mod Options -> LLM Provider) and re-run to compare.";
      }

      /// <summary>
      ///   cr.reload_lore
      ///   Reloads ModuleData/character_overrides.json and re-applies (or restores) the authored traits
      ///   per the current "Use custom traits" toggle — no relaunch needed.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("reload_lore", "cr")]
      public static string ReloadLore(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;

         var behavior = CharacterLoreBehavior.Instance;

         if (behavior == null) return "Character lore behavior is not active.";

         behavior.ReloadAndReapply();

         return $"Reloaded character_overrides.json ({CharacterOverrideLoader.Set.Count} entr(ies)); " + $"custom traits {(ModSettings.UseCustomTraits ? "applied" : "off (vanilla restored)")}.";
      }

      /// <summary>
      ///   cr.lore_status
      ///   On-demand diagnosis for character_overrides.json, independent of the "Enable Logging" MCM toggle
      ///   (that switch gates ModLog.Info/Error, so a player editing the file with logging off otherwise gets
      ///   zero feedback). Reports whether custom traits are on, which path the loader looked at and whether
      ///   it existed, how many entries loaded and the last parse error (if any), and for each authored entry
      ///   whether it currently resolves to a live hero, so a typo in "hero" or "id" is visible immediately.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("lore_status", "cr")]
      public static string LoreStatus(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;

         var sb = new StringBuilder();

         sb.AppendLine($"Use custom traits: {(ModSettings.UseCustomTraits ? "ON" : "OFF")}");
         sb.AppendLine($"File: {CharacterOverrideLoader.LastLoadPath}  (exists: {(CharacterOverrideLoader.LastLoadFileExisted ? "yes" : "no")})");
         sb.AppendLine($"Loaded: {CharacterOverrideLoader.LastLoadCount} override(s)");
         sb.AppendLine($"Last parse error: {CharacterOverrideLoader.LastLoadError ?? "none"}");
         sb.AppendLine("Resolution:");

         IReadOnlyList<CharacterOverride> entries = CharacterOverrideLoader.Set?.All;

         if (entries == null || entries.Count == 0)
         {
            sb.Append("  (no authored entries loaded)");

            return sb.ToString();
         }

         foreach (CharacterOverride entry in entries)
         {
            string label = !string.IsNullOrWhiteSpace(entry?.Hero) ? entry.Hero : entry?.Id ?? "(unnamed entry)";

            bool matched = false;
            try
            {
               matched = Hero.AllAliveHeroes.Any(h =>
                  h != null &&
                  ((!string.IsNullOrWhiteSpace(entry?.Id) && string.Equals(h.StringId, entry.Id, StringComparison.OrdinalIgnoreCase)) ||
                   (!string.IsNullOrWhiteSpace(entry?.Hero) && string.Equals(h.Name?.ToString(), entry.Hero, StringComparison.OrdinalIgnoreCase))));
            }
            catch
            {
               /* diagnostic only, never throw */
            }

            sb.AppendLine(matched
               ? $"  {label} -> matched"
               : $"  {label} -> NO MATCH (check the exact in-game name, or add an \"id\")");
         }

         return sb.ToString().TrimEnd();
      }

      /// <summary>
      ///   cr.orientation &lt;straight|bi|gay|lesbian|pan|asexual&gt; [name]
      ///   Changes an NPC's sexual orientation (player request: enable e.g. a female-female romance the rolled
      ///   orientation blocked). Persists on the profile; for a permanent change add "orientation" to that NPC's
      ///   entry in character_overrides.json (re-synced live each turn).
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("orientation", "cr")]
      public static string SetOrientation(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;
         if (args == null || args.Count == 0)
            return "Usage: cr.orientation <straight|bi|gay|lesbian|pan|asexual> [name]";

         SexualOrientation? orientation = OrientationParser.Parse(args[0]);

         if (orientation == null)
            return $"Unknown orientation '{args[0]}'. Try: straight, bi, gay/lesbian, pan, asexual.";

         string nameFragment = args.Count > 1
            ? string.Join(" ", args.GetRange(1, args.Count - 1)).Trim()
            : null;
         Hero hero = !string.IsNullOrWhiteSpace(nameFragment)
            ? Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.Name != null && h.Name.ToString().IndexOf(nameFragment, StringComparison.OrdinalIgnoreCase) >= 0)
            : Hero.OneToOneConversationHero;

         if (hero == null) return "No NPC found. Pass a name fragment or talk to one first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);

         if (profile.Romantic == null) return $"{hero.Name} has no romantic profile to change.";

         profile.Romantic.Orientation = orientation.Value;
         behavior.Store.Set(profile);

         return $"{hero.Name}'s orientation is now {orientation.Value}. For a permanent change, add \"orientation\": \"{args[0]}\" to their character_overrides.json entry.";
      }

      /// <summary>
      ///   cr.backstory [name]
      ///   Shows the authored override for an NPC (if any) — its backstory and the effective vs vanilla
      ///   levels of the five personality traits — so you can confirm the file took effect.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("backstory", "cr")]
      public static string ShowBackstory(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         CharacterOverride ov = CharacterOverrideLoader.For(hero);


         string Fmt(TraitObject t)
         {
            return $"{hero.GetTraitLevel(t):+#;-#;0} (vanilla {hero.CharacterObject.GetTraitLevel(t):+#;-#;0})";
         }


         var sb = new StringBuilder();
         sb.AppendLine($"{hero.Name} — custom traits: {(ModSettings.UseCustomTraits ? "ON" : "OFF")}");
         sb.AppendLine($"  Valor {Fmt(DefaultTraits.Valor)} | Honor {Fmt(DefaultTraits.Honor)} | Mercy {Fmt(DefaultTraits.Mercy)}");
         sb.AppendLine($"  Generosity {Fmt(DefaultTraits.Generosity)} | Calculating {Fmt(DefaultTraits.Calculating)}");
         if (ov == null)
         {
            sb.AppendLine("  No authored override for this NPC.");
         }
         else
         {
            sb.AppendLine($"  Override matched (id={ov.Id ?? "—"}).");
            sb.AppendLine(ov.HasBackstory
               ? $"  Backstory: {ov.Backstory}"
               : "  Backstory: (none)");
         }

         return sb.ToString();
      }

      /// <summary>
      ///   cr.succession [predecessor]
      ///   Simulates dynastic succession with the current player as the heir: reframes every stored NPC
      ///   memory (drops the predecessor's private events, keeps the political/relational, marks it
      ///   inherited). The predecessor defaults to your father/mother; pass a name to use a specific hero.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("succession", "cr")]
      public static string SimulateSuccession(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;

         var heir = Hero.MainHero;

         if (heir == null) return "No player hero.";

         Hero predecessor = args is {Count: > 0}
            ? ResolveHero(args)
            : heir.Father ?? heir.Mother;

         if (predecessor == null)
            return "No predecessor found — pass an NPC name, or play a character who has a parent.";
         if (predecessor == heir)
            return "The predecessor and the heir cannot be the same hero.";

         int n = SuccessionBehavior.Instance?.Apply(predecessor, heir) ?? 0;

         return $"Succession simulated: {n} profile(s) reframed as the heir of {predecessor.Name}. " + $"Talk to an NPC — they should speak of {predecessor.Name} as your kin; private events are gone.";
      }

      [CommandLineFunctionality.CommandLineArgumentFunction("test_llm", "campaign")]
      public static string TestLlm(List<string> args)
      {
         var behavior = NpcMemoryCampaignBehavior.Instance;

         if (behavior == null)
            return "Calradia Remembers behavior not active. Start a campaign first.";

         if (behavior.LlmServices is not {IsAvailable: true})
            return "LLM services not available. Check OPENROUTER_API_KEY environment variable.";

         _ = LlmDiagnostic.Run(behavior.LlmServices, behavior.Store, behavior.GameStateBridge)
                          .ContinueWith(t =>
                          {
                             if (t.Exception != null)
                                ModLog.Error($"LLM diagnostic faulted: {t.Exception.GetBaseException().Message}");
                          }, TaskScheduler.Default);

         return "LLM diagnostic started. Watch the combat log and the log file for results.";
      }

      /// <summary>
      ///   cr.jealousy [flirt|intimacy|marriage] [name]
      ///   Resolves and immediately fires a jealousy reaction as if the player had just made a
      ///   romantic advance of that tier (default flirt) toward the named NPC (or the current
      ///   conversation partner). Bypasses the witness/probability/cooldown gates so the resolution
      ///   and consequence are testable without building a full romance: it prints the inferred
      ///   jealous party and applies the grudge + grievance memory + a threatening letter.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("jealousy", "cr")]
      public static string TriggerJealousy(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error))
            return error;
         if (JealousyBehavior.Instance == null)
            return "Jealousy system is not active.";

         var tier = RomanticActTier.Flirt;
         var nameTokens = new List<string>();
         foreach (string arg in args ?? new List<string>())
            if (arg.Equals("flirt", StringComparison.OrdinalIgnoreCase)) tier = RomanticActTier.Flirt;
            else if (arg.Equals("intimacy", StringComparison.OrdinalIgnoreCase)) tier = RomanticActTier.Intimacy;
            else if (arg.Equals("marriage", StringComparison.OrdinalIgnoreCase)) tier = RomanticActTier.Marriage;
            else nameTokens.Add(arg);

         string nameFragment = nameTokens.Count > 0
            ? string.Join(" ", nameTokens).Trim()
            : null;
         Hero courted = !string.IsNullOrWhiteSpace(nameFragment)
            ? Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.Name != null && h.Name.ToString().IndexOf(nameFragment, StringComparison.OrdinalIgnoreCase) >= 0)
            : Hero.OneToOneConversationHero;

         if (courted == null)
            return "Usage: cr.jealousy [flirt|intimacy|marriage] [name]\n" + "  Resolves and fires a jealousy reaction as if the player romanced this NPC.";

         return JealousyBehavior.Instance.DebugForce(courted, tier);
      }

      /// <summary>
      ///   cr.jealousy_comp
      ///   Reports the player's current partner household (native spouse + committed consorts) with
      ///   each one's culture, sharing-tolerance and how long since they were last attended to, then
      ///   forces one competition pass so the v1.5 ongoing-rivalry loop can be tested without waiting.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("jealousy_comp", "cr")]
      public static string TriggerJealousyCompetition(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error))
            return error;
         if (JealousyBehavior.Instance == null)
            return "Jealousy system is not active.";

         return JealousyBehavior.Instance.DebugCompetition();
      }

      /// <summary>
      ///   cr.vram — logs one GPU-memory reading right now (game dedicated / shared / system dedicated).
      ///   Works with or without a chat open, and paused. Use it to spot-check VRAM during the control test.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("vram", "cr")]
      public static string Vram(List<string> args)
      {
         string line = VramProbe.Describe();
         ModLog.Info($"[VRAM] spot | {line}");

         return line;
      }

      /// <summary>
      ///   cr.vram_noportrait [on|off] — suppresses every live character portrait in the chat (text-only). Run a
      ///   captive scene with it ON and watch the [VRAM] trend: flat means the portrait tableau was the leak;
      ///   still climbing means the naval map scene behind our overlay is the leak. Discriminating test only.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("vram_noportrait", "cr")]
      public static string VramNoPortrait(List<string> args)
      {
         bool on = args is {Count: > 0}
            ? !string.Equals(args[0], "off", StringComparison.OrdinalIgnoreCase)
            : !PortraitTextureGuard.SuppressForDiagnostics;

         PortraitTextureGuard.SuppressForDiagnostics = on;

         return on
            ? "Portraits SUPPRESSED (text-only). Open a captive scene and watch [VRAM]: flat = tableau was the leak."
            : "Portraits restored.";
      }

      /// <summary>
      ///   cr.vram_watch [on|off] — toggles the frame-loop VRAM heartbeat that runs even with NO chat open and
      ///   the game paused. Turn it on, sit on the map without opening a chat, and watch the [VRAM] watch lines:
      ///   flat means our overlay drives the leak; climbing means the map screen leaks on its own.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("vram_watch", "cr")]
      public static string VramWatchToggle(List<string> args)
      {
         bool on = args is {Count: > 0}
            ? !string.Equals(args[0], "off", StringComparison.OrdinalIgnoreCase)
            : !VramWatch.Enabled;

         VramWatch.Enabled = on;

         return on
            ? "VRAM watch ON — a [VRAM] watch line every 10s, even with no chat open. Sit on the map and watch the trend."
            : "VRAM watch OFF.";
      }
   }
}