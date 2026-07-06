// Code written by Gabriel Mailhot, 01/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Letters;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Services;
using CalradiaRemembers.Witnesses;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>
      ///   cr.add_kink &lt;kink&gt; [name]
      ///   Adds a kink to the NPC's romantic profile so scene variety can be tested.
      ///   Apply BEFORE opening a captive scene (cr.crc_scene re-reads the profile on open).
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("add_kink", "cr")]
      public static string AddKink(List<string> args)
         => MutateKink(args, true, "cr.add_kink <kink> [name]");

      /// <summary>
      ///   cr.witness [witnessName]
      ///   Adds a named witness to the currently open chat session. Primary test path
      ///   for the multi-NPC feature — validates prompt injection, header UI, and the
      ///   request_privacy round-trip without needing a real settlement context.
      ///   The chat window must be open first (e.g. via cr.chat or a normal conversation).
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("witness", "cr")]
      public static string AddWitness(List<string> args)
      {
         if (ChatManager.ActiveViewModel == null)
            return "No chat window is open. Open a conversation first.";

         string nameFragment = args.Count > 0
            ? string.Join(" ", args).Trim()
            : null;

         if (string.IsNullOrWhiteSpace(nameFragment))
            return "Usage: cr.witness [name]";

         Hero witnessHero = Hero.AllAliveHeroes.FirstOrDefault(h =>
            h != Hero.MainHero && h.IsAlive && h.Name?.ToString().IndexOf(nameFragment, StringComparison.OrdinalIgnoreCase) >= 0);

         string displayName = witnessHero?.Name?.ToString() ?? nameFragment;
         var relation = "a lord present";
         if (witnessHero != null)
         {
            int rel = witnessHero.GetRelation(Hero.MainHero);
            relation = rel >= 20 ? "a lord you regard with trust"
               : rel <= -20 ? "a rival lord"
               : "a lord you know";
         }

         var entry = new WitnessEntry {Name = displayName, RelationToNpc = relation};
         ChatManager.ActiveViewModel.AddWitness(entry);

         return $"Witness '{displayName}' ({relation}) added to chat session.";
      }

      /// <summary>
      ///   cr.witnesses [name]
      ///   Runs WitnessResolver.DiagnoseWitnesses for the given NPC and prints a
      ///   detailed report: CurrentSettlement, StayingInSettlement, HeroesWithoutParty
      ///   names with profile flags, party leaders inside the settlement, and
      ///   available player companions. Use this to verify which collection holds
      ///   present lords during a Talk conversation (vs Visit).
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("witnesses", "cr")]
      public static string DiagnoseWitnesses(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         string report = WitnessResolver.DiagnoseWitnesses(hero, behavior.Store);

         // Console output is screen-only — mirror the report into the log so it can
         // be shared in bug reports / analyzed outside the game.
         foreach (string line in report.Split('\n'))
            ModLog.Info($"[cr.witnesses] {line.TrimEnd()}");

         return report;
      }

      /// <summary>
      ///   cr.escape_force
      ///   Makes the NEXT escape attempt in a captive scene succeed, so the full escape flow
      ///   (constrained captor reaction → freeing on scene close) can be tested despite the 10%
      ///   natural chance. Then, in the scene, type an escape attempt (e.g. "I lunge for the door
      ///   and bolt") so the LLM emits escape_attempt.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("escape_force", "cr")]
      public static string ForceEscape(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;
         behavior.GameStateBridge?.ForceNextEscapeSuccess();

         return "Armed: your next escape attempt in a captive scene will succeed. " + "Type an escape attempt in the scene to trigger it.";
      }

      /// <summary>
      ///   cr.release_info [name]
      ///   Shows the NEGOTIATED-RELEASE gate for an NPC captor: their Mercy, the heaviest live grudge they
      ///   hold against you, and whether they can be TALKED into freeing you (persuasion) or would accept a
      ///   RANSOM. A vendetta captor (heavy grudge / nemesis) refuses both. Use it to see which captors will
      ///   yield before testing the release in a captive scene (cr.crc_scene, then plead your case in chat).
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("release_info", "cr")]
      public static string ReleaseInfo(List<string> args)
      {
         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         int mercy = hero.GetTraitLevel(TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultTraits.Mercy);
         int grudge = HeaviestReleaseGrudge(hero);
         CaptorReleaseVerdict v = CaptorReleasePolicy.Resolve(mercy, grudge, isNemesisVendetta: false);

         return $"{hero.Name}: Mercy {mercy}, heaviest grudge vs you {grudge}. "
              + $"Persuasion {(v.AllowsPersuasion ? "OPEN" : "refused")}, Ransom {(v.AllowsRansom ? "OPEN" : "refused")}"
              + (v.RefusesAll ? " (vendetta: he will not let you buy or talk your way out)." : ".");
      }

      private static int HeaviestReleaseGrudge(Hero hero)
      {
         var grudges = CalradiaRemembers.Grudges.GrudgeBehavior.Instance?.GrudgesFor(hero?.StringId);
         if (grudges == null) return 0;

         string playerId = Hero.MainHero?.StringId;
         int max = 0;
         foreach (var g in grudges)
            if (!g.IsResolved && g.TargetId == playerId && g.Magnitude > max) max = g.Magnitude;

         return max;
      }

      /// <summary>
      ///   cr.nemesis
      ///   Lists the persistent nemesis roster (name, band, region, alive, recaptures, bound Hero) and whether
      ///   the player is currently a nemesis's captive. Dev diagnostic for validating the Nemesis pillar in
      ///   game: capture by a bandit party should mint/reuse a nemesis and bind a real Hero to him.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("nemesis", "cr")]
      public static string ShowNemesisRoster(List<string> args)
      {
         Nemesis.NemesisBehavior b = Nemesis.NemesisBehavior.Instance;
         if (b == null) return "NemesisBehavior not available (load a campaign first).";

         var lines = new List<string>();
         foreach (NemesisIdentity n in b.All)
            lines.Add($"- {n.Name} {n.Epithet} [{n.BandCultureKey}/{n.RegionId}] alive={n.IsAlive} " +
                      $"recaptures={n.RecaptureCount} encounters={n.TimesEncountered} hero={(string.IsNullOrEmpty(n.HeroStringId) ? "none" : n.HeroStringId)}");

         string held = b.PlayerHeldByNemesis() ? "YES — the player is currently held by a nemesis." : "no";

         return (lines.Count == 0 ? "No nemesis on record yet." : string.Join("\n", lines)) + $"\nPlayer held by a nemesis: {held}";
      }

      /// <summary>
      ///   cr.bandit_queen [solo|collective] [male|female]
      ///   Opens a captive scene with a forced FEMALE bandit chief (intent = spoil / PersonalDesire),
      ///   to test female-aggressor content (facesitting/smothering, reductive domination) without
      ///   waiting on the rare female-gang roll. Defaults: solo, and a MALE player (the classic
      ///   woman-dominates-a-man dynamic); pass 'female' for a woman-on-woman scene. The captor's
      ///   portrait is left blank rather than show a male troop face on a woman. Requires Hardcore.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("bandit_queen", "cr")]
      public static string LaunchBanditQueenScene(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;
         if (behavior.LlmServices is not {IsAvailable: true})
            return "LLM services not available. Set your OpenRouter API key first.";

         var collective = false;
         bool? playerGender = false; // default: male player (female-on-male)
         foreach (string arg in args ?? new List<string>())
            if (arg.Equals("collective", StringComparison.OrdinalIgnoreCase)) collective = true;
            else if (arg.Equals("solo", StringComparison.OrdinalIgnoreCase)) collective = false;
            else if (arg.Equals("female", StringComparison.OrdinalIgnoreCase)) playerGender = true;
            else if (arg.Equals("male", StringComparison.OrdinalIgnoreCase)) playerGender = false;

         LlmServices.TestPlayerGenderOverride = playerGender;
         behavior.LlmServices.Invalidate();

         List<WitnessEntry> witnesses = collective
            ? CaptiveEncounterService.BuildBanditWitnesses()
            : new List<WitnessEntry>();

         // Force a FEMALE chief and give her a real FEMALE face (a female troop/commoner portrait),
         // not a male troop. Falls back to a blank portrait if the game has no female character.
         NpcProfile captor = CaptiveEncounterService.BuildBanditCaptor(
            "bandit_queen_test", MBRandom.RandomInt(), "Brigands", true);
         CharacterObject queenFace = CaptiveEncounterService.ResolveFemaleBanditFace();
         ChatManager.OpenForCaptiveEncounter(null, captor, witnesses, collective, CaptiveSceneIntent.PersonalDesire, queenFace);

         string warn = ModSettings.AdultContentLevel != AdultContentLevel.Hardcore
            ? " (NOTE: set Adult Content Level to Hardcore in MCM for the CNC / female-aggressor rules.)"
            : "";

         return $"Female-chief bandit scene opened with {captor.Name} [spoil{(collective ? ", collective" : "")}, " + $"player {(playerGender == true ? "female" : "male")}].{warn}";
      }

      /// <summary>
      ///   cr.bandit_scene [extortion|intimidation|revenge|spoil] [female|male] [solo|collective]
      ///   Conjures a SYNTHETIC brigand captor (no real hero needed) and opens a captive scene —
      ///   tests the bandit-captivity path without being captured. Default intent: extortion.
      ///   'spoil' = the sexual CNC intent (Hardcore). female|male overrides player gender.
      ///   'collective' = the chief's men join in (group aggression). Default: solo.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("bandit_scene", "cr")]
      public static string LaunchBanditScene(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;
         if (behavior.LlmServices is not {IsAvailable: true})
            return "LLM services not available. Set your OpenRouter API key first.";

         bool? genderOverride = null;
         var intent = CaptiveSceneIntent.Extortion;
         var collective = false;
         foreach (string arg in args ?? new List<string>())
            if (arg.Equals("female", StringComparison.OrdinalIgnoreCase)) genderOverride = true;
            else if (arg.Equals("male", StringComparison.OrdinalIgnoreCase)) genderOverride = false;
            else if (arg.Equals("collective", StringComparison.OrdinalIgnoreCase)) collective = true;
            else if (arg.Equals("solo", StringComparison.OrdinalIgnoreCase)) collective = false;
            else if (arg.Equals("spoil", StringComparison.OrdinalIgnoreCase)) intent = CaptiveSceneIntent.PersonalDesire;
            else if (Enum.TryParse(arg, true, out CaptiveSceneIntent parsed)) intent = parsed;

         if (genderOverride.HasValue)
         {
            LlmServices.TestPlayerGenderOverride = genderOverride;
            behavior.LlmServices.Invalidate();
         }

         List<WitnessEntry> witnesses = collective
            ? CaptiveEncounterService.BuildBanditWitnesses()
            : new List<WitnessEntry>();

         CharacterObject banditFace = CaptiveEncounterService.ResolveBanditTroop(null);
         NpcProfile captor = CaptiveEncounterService.BuildBanditCaptor(
            "bandit_captor_test", MBRandom.RandomInt(), "Brigands", banditFace?.IsFemale);
         ChatManager.OpenForCaptiveEncounter(null, captor, witnesses, collective, intent, banditFace);

         string warn = ModSettings.AdultContentLevel != AdultContentLevel.Hardcore
            ? " (NOTE: adult level is not Hardcore — the captive prompt rules may not appear; set Hardcore in MCM.)"
            : "";

         return $"Bandit captive scene opened with {captor.Name} [{intent}{(collective ? ", collective" : "")}].{warn}";
      }

      /// <summary>
      ///   cr.captor_scene [interrogation|domination|torture|...] [name]
      ///   Opens a PLAYER-AS-CAPTOR scene: the named NPC (or the conversation NPC) is YOUR prisoner and you drive
      ///   the scene; they react as a captive of their nature. Forces the framing for testing (the NPC need not
      ///   truly be your prisoner). Non-Hardcore = a tense confrontation; Hardcore unlocks the explicit CNC layer.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("captor_scene", "cr")]
      public static string ForceCaptorScene(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;
         if (behavior.LlmServices is not {IsAvailable: true}) return "LLM services not available. Set your OpenRouter API key first.";

         var intent = CaptiveSceneIntent.Interrogation;
         var nameTokens = new List<string>();
         foreach (string arg in args ?? new List<string>())
            if (Enum.TryParse(arg, true, out CaptiveSceneIntent parsed)) intent = parsed;
            else nameTokens.Add(arg);

         Hero npc = ResolveHero(nameTokens);
         if (npc == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = behavior.Store?.Get(npc.StringId) ?? HeroProfileMapper.FromHero(npc);
         ChatManager.OpenForCaptorScene(npc, profile, intent);

         string warn = ModSettings.AdultContentLevel != AdultContentLevel.Hardcore
            ? " (NOTE: set Adult Content Level to Hardcore in MCM for the explicit CNC layer.)"
            : "";

         return $"Captor scene opened with {npc.Name} as your prisoner (intent {intent}).{warn}";
      }

      /// <summary>
      ///   cr.crc_scene [female|male] [solo|collective] [name]
      ///   Opens a captive-encounter chat immediately, forcing PlayerStatus = Captive
      ///   so Section B (CNC rules) lands in the prompt — without the player actually
      ///   being a prisoner. Requires Hardcore adult level for the CNC rules to appear.
      ///   - female|male: override the player gender for this scene (test the other path)
      ///   - solo|collective: private scene, or a collective scene with soldier witnesses
      ///   - name: captor name fragment (defaults to the current conversation NPC)
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("crc_scene", "cr")]
      public static string LaunchCaptiveScene(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         if (behavior.LlmServices is not {IsAvailable: true})
            return "LLM services not available. Set your OpenRouter API key first.";

         // Parse keyword args; the remainder is the captor name fragment.
         bool? genderOverride = null;
         var collective = false;
         var intent = CaptiveSceneIntent.Interrogation;
         var nameTokens = new List<string>();
         foreach (string arg in args ?? new List<string>())
            if (arg.Equals("female", StringComparison.OrdinalIgnoreCase)) genderOverride = true;
            else if (arg.Equals("male", StringComparison.OrdinalIgnoreCase)) genderOverride = false;
            else if (arg.Equals("collective", StringComparison.OrdinalIgnoreCase)) collective = true;
            else if (arg.Equals("solo", StringComparison.OrdinalIgnoreCase)) collective = false;
            else if (arg.Equals("desire", StringComparison.OrdinalIgnoreCase)) intent = CaptiveSceneIntent.PersonalDesire;
            else if (Enum.TryParse(arg, true, out CaptiveSceneIntent parsedIntent)) intent = parsedIntent;
            else nameTokens.Add(arg);

         Hero captor = ResolveHero(nameTokens);

         if (captor == null)
            return "No captor found. Pass a name fragment or start a conversation first.";

         NpcProfile captorProfile = GetOrCreateProfile(behavior, captor);

         // Apply the gender override (if any) and rebuild the PromptBuilder so it takes effect.
         if (genderOverride.HasValue)
         {
            LlmServices.TestPlayerGenderOverride = genderOverride;
            behavior.LlmServices.Invalidate();
         }

         var witnesses = new List<WitnessEntry>();
         if (collective)
            witnesses.AddRange(new[] {
               new WitnessEntry {
                  Name = "A soldier", RelationToNpc = "one of your men, present on your orders",
                  Persona = "Imposing — silent, obedient, watching"
               },
               new WitnessEntry {
                  Name = "Another soldier", RelationToNpc = "one of your men, present on your orders",
                  Persona = "Impassive — expressionless, unmoved"
               }
            });

         string adultWarning = ModSettings.AdultContentLevel != AdultContentLevel.Hardcore
            ? " (NOTE: adult level is not Hardcore — CNC rules will NOT appear; set it in MCM.)"
            : "";

         string genderNote = genderOverride.HasValue
            ? $" gender forced to {(genderOverride.Value ? "female" : "male")};"
            : "";

         ChatManager.OpenForCaptiveEncounter(captor, captorProfile, witnesses, collective, intent);

         return $"Captive scene opened with {captor.Name}.{genderNote} " + $"{(collective ? "collective" : "solo")} [{intent}].{adultWarning}";
      }

      /// <summary>
      ///   cr.crc_scene_staged [female|male] [solo|collective] [intent] [name]
      ///   Like cr.crc_scene, but STAGES the encounter as a 3D conversation scene (via CaptiveSceneStager) instead
      ///   of a bare overlay on the world map — the VRAM-crash fix. Starts a conversation with the captor, then
      ///   opens the captive chat over the loaded scene. Watch [VRAM]: it should stay flat (no naval map behind).
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("crc_scene_staged", "cr")]
      public static string LaunchCaptiveSceneStaged(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;
         if (behavior.LlmServices is not {IsAvailable: true})
            return "LLM services not available. Set your OpenRouter API key first.";

         bool? genderOverride = null;
         var collective = false;
         var intent = CaptiveSceneIntent.Interrogation;
         var nameTokens = new List<string>();
         foreach (string arg in args ?? new List<string>())
            if (arg.Equals("female", StringComparison.OrdinalIgnoreCase)) genderOverride = true;
            else if (arg.Equals("male", StringComparison.OrdinalIgnoreCase)) genderOverride = false;
            else if (arg.Equals("collective", StringComparison.OrdinalIgnoreCase)) collective = true;
            else if (arg.Equals("solo", StringComparison.OrdinalIgnoreCase)) collective = false;
            else if (arg.Equals("desire", StringComparison.OrdinalIgnoreCase)) intent = CaptiveSceneIntent.PersonalDesire;
            else if (Enum.TryParse(arg, true, out CaptiveSceneIntent parsedIntent)) intent = parsedIntent;
            else nameTokens.Add(arg);

         Hero captor = ResolveHero(nameTokens);
         if (captor == null)
            return "No captor found. Pass a name fragment or start a conversation first.";

         NpcProfile captorProfile = GetOrCreateProfile(behavior, captor);

         if (genderOverride.HasValue)
         {
            LlmServices.TestPlayerGenderOverride = genderOverride;
            behavior.LlmServices.Invalidate();
         }

         var witnesses = new List<WitnessEntry>();
         if (collective)
            witnesses.AddRange(new[] {
               new WitnessEntry {
                  Name = "A soldier", RelationToNpc = "one of your men, present on your orders",
                  Persona = "Imposing — silent, obedient, watching"
               },
               new WitnessEntry {
                  Name = "Another soldier", RelationToNpc = "one of your men, present on your orders",
                  Persona = "Impassive — expressionless, unmoved"
               }
            });

         Chat.CaptiveSceneStager.StageCaptiveEncounter(captor, captorProfile, witnesses, collective, intent);

         string adultWarning = ModSettings.AdultContentLevel != AdultContentLevel.Hardcore
            ? " (NOTE: adult level is not Hardcore — CNC rules will NOT appear; set it in MCM.)"
            : "";

         return $"Staging captive scene with {captor.Name} as a 3D conversation [{intent}]. " +
                $"The chat opens once the scene loads.{adultWarning}";
      }

      /// <summary>
      ///   cr.companion_scene [torment|assault|coerce] [victim:Name] [captorName]
      ///   Opens a COMPANION-VICTIM captive scene: the captor works on one of your companions while you are made
      ///   to watch (torment / assault) or are forced to act (coerce). Forces PlayerStatus = Captive so the rules
      ///   land without a real capture. Hardcore unlocks the explicit layer; otherwise it stays a tense scene.
      ///   - victim: a companion-name fragment; defaults to the first companion of your clan, else "your companion".
      ///   - captorName: captor name fragment (defaults to the current conversation NPC).
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("companion_scene", "cr")]
      public static string LaunchCompanionScene(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         if (behavior.LlmServices is not {IsAvailable: true})
            return "LLM services not available. Set your OpenRouter API key first.";

         CompanionSceneKind kind = CompanionSceneKind.CaptorTorments;
         string victimFragment = null;
         var nameTokens = new List<string>();
         foreach (string arg in args ?? new List<string>())
            if (arg.Equals("torment", StringComparison.OrdinalIgnoreCase)) kind = CompanionSceneKind.CaptorTorments;
            else if (arg.Equals("assault", StringComparison.OrdinalIgnoreCase)) kind = CompanionSceneKind.SoldiersAssault;
            else if (arg.Equals("coerce", StringComparison.OrdinalIgnoreCase)) kind = CompanionSceneKind.PlayerCoerced;
            else if (arg.StartsWith("victim:", StringComparison.OrdinalIgnoreCase)) victimFragment = arg.Substring("victim:".Length);
            else nameTokens.Add(arg);

         Hero captor = ResolveHero(nameTokens);
         if (captor == null)
            return "No captor found. Pass a name fragment or start a conversation first.";

         Hero victim = ResolveCompanionVictimHero(victimFragment);
         if (victim == null)
            return "No companion in your clan to use as the victim. Recruit one first, or pass victim:<name> matching a clan companion.";
         NpcProfile captorProfile = GetOrCreateProfile(behavior, captor);

         bool collective = CompanionScenePolicy.IsCollective(kind);
         bool coerced = CompanionScenePolicy.IsPlayerCoerced(kind);
         var intent = kind == CompanionSceneKind.CaptorTorments ? CaptiveSceneIntent.Torture : CaptiveSceneIntent.Domination;

         var witnesses = new List<WitnessEntry>();
         if (collective)
            witnesses.AddRange(new[] {
               new WitnessEntry {
                  Name = "A soldier", RelationToNpc = "one of your men, present on your orders",
                  Persona = "Brutal — eager to set upon the prisoner on your word"
               },
               new WitnessEntry {
                  Name = "Another soldier", RelationToNpc = "one of your men, present on your orders",
                  Persona = "Cold — does what you order without a flicker"
               }
            });

         string adultWarning = ModSettings.AdultContentLevel != AdultContentLevel.Hardcore
            ? " (NOTE: adult level is not Hardcore — the explicit layer will NOT appear; set it in MCM.)"
            : "";

         ChatManager.OpenForCaptiveEncounter(captor, captorProfile, witnesses, collective, intent, null, victim, coerced);

         return $"Companion scene opened: {captor.Name} vs {victim.Name} [{kind}].{adultWarning}";
      }

      /// <summary>The companion Hero to use as the scene's victim: a clan companion, name-matched if given, else the first.</summary>
      private static Hero ResolveCompanionVictimHero(string fragment)
      {
         List<Hero> companions = Clan.PlayerClan?.Companions?.Where(h => h is {IsAlive: true}).ToList() ?? new List<Hero>();

         if (!string.IsNullOrWhiteSpace(fragment))
         {
            Hero match = companions.FirstOrDefault(h =>
               h.Name?.ToString().IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0);
            if (match != null) return match;
         }

         return companions.Count > 0 ? companions[0] : null;
      }

      /// <summary>cr.kinks [name] — list the NPC's current kinks and the valid kink names.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("kinks", "cr")]
      public static string ListKinks(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         string current = profile.Romantic?.Kinks == null || profile.Romantic.Kinks.Count == 0
            ? "(none)"
            : string.Join(", ", profile.Romantic.Kinks);

         return $"{hero.Name} kinks: {current}\nValid: {ValidKinkNames()}";
      }

      /// <summary>cr.remove_kink &lt;kink&gt; [name] — removes a kink from the NPC's profile.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("remove_kink", "cr")]
      public static string RemoveKink(List<string> args)
         => MutateKink(args, false, "cr.remove_kink <kink> [name]");

      private static string MutateKink(List<string> args, bool add, string usage)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         if (args == null || args.Count == 0)
            return $"Usage: {usage}\nKinks: {ValidKinkNames()}";

         if (!Enum.TryParse(args[0], true, out Kink kink) || kink == Kink.None)
            return $"Unknown kink '{args[0]}'. Valid: {ValidKinkNames()}";

         List<string> nameArgs = args.Count > 1
            ? args.GetRange(1, args.Count - 1)
            : new List<string>();
         Hero hero = ResolveHero(nameArgs);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);

         if (profile.Romantic?.Kinks == null)
            return $"{hero.Name} has no romantic profile — cannot modify kinks.";

         bool changed;
         if (add)
         {
            if (profile.Romantic.Kinks.Contains(kink))
            {
               changed = false;
            }
            else
            {
               profile.Romantic.Kinks.Add(kink);
               changed = true;
            }
         }
         else
         {
            changed = profile.Romantic.Kinks.Remove(kink);
         }

         behavior.Store.Set(profile);

         string state = profile.Romantic.Kinks.Count == 0
            ? "(none)"
            : string.Join(", ", profile.Romantic.Kinks);

         if (!changed)
            return $"No change — {hero.Name} {(add ? "already has" : "does not have")} {kink}. Now: {state}.";

         return $"{kink} {(add ? "added to" : "removed from")} {hero.Name}. Now: {state}.";
      }

      private static string ValidKinkNames()
         => string.Join(", ", Enum.GetNames(typeof(Kink)).Where(n => n != nameof(Kink.None)));
   }
}
