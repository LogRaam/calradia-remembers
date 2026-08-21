// Code written by Gabriel Mailhot, 06/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Letters;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Nemesis;
using CalradiaRemembers.Quests;
using CalradiaRemembers.Services;
using CalradiaRemembers.Witnesses;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

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
      ///   cr.dress &lt;item name fragment&gt;
      ///   Test aid for the captive re-dress feature: equips a matching GARMENT on a held prisoner's BATTLE set
      ///   (the set the captor scene renders), so the visual can be confirmed. Targets the NPC currently in
      ///   conversation, else the first female prisoner in the party. Conversation agents do not refresh live,
      ///   so (re)stage the captive scene AFTER running this to see her wearing it. Never equips a weapon.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("dress", "cr")]
      public static string Dress(List<string> args)
      {
         if (args == null || args.Count == 0)
            return "Usage: cr.dress <item name fragment>. Equips a garment on a held prisoner's battle set; (re)stage the captive scene to see it.";

         string fragment = string.Join(" ", args).Trim();

         Hero target = Hero.OneToOneConversationHero;
         if (target == null || !target.IsPrisoner)
            target = MobileParty.MainParty?.PrisonRoster?.GetTroopRoster()
               ?.Select(e => e.Character?.HeroObject)
               .FirstOrDefault(h => h != null && h.IsFemale);

         if (target == null)
            return "No held prisoner found. Talk to a prisoner, or capture one, first.";

         ItemObject item = MBObjectManager.Instance.GetObjectTypeList<ItemObject>()
            .FirstOrDefault(i => i?.Name != null
                                 && i.Name.ToString().IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0
                                 && GarmentSlotFor(i.ItemType) != EquipmentIndex.None);

         if (item == null)
            return $"No garment item matched '{fragment}'. Try a fragment of a dress / gown / armour name.";

         EquipmentIndex slot = GarmentSlotFor(item.ItemType);
         target.BattleEquipment[slot] = new EquipmentElement(item);

         return $"Dressed {target.Name} in {item.Name} (battle slot {slot}). Re-open the captive scene to see it (agents do not refresh live).";
      }

      /// <summary>The garment slot an item type belongs in, or <see cref="EquipmentIndex.None" /> if it is not a worn garment.</summary>
      private static EquipmentIndex GarmentSlotFor(ItemObject.ItemTypeEnum type)
      {
         switch (type)
         {
            case ItemObject.ItemTypeEnum.HeadArmor: return EquipmentIndex.Head;
            case ItemObject.ItemTypeEnum.BodyArmor:
            case ItemObject.ItemTypeEnum.ChestArmor: return EquipmentIndex.Body;
            case ItemObject.ItemTypeEnum.LegArmor: return EquipmentIndex.Leg;
            case ItemObject.ItemTypeEnum.HandArmor: return EquipmentIndex.Gloves;
            case ItemObject.ItemTypeEnum.Cape: return EquipmentIndex.Cape;
            default: return EquipmentIndex.None;
         }
      }

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

         // Bring-participants-to-a-captor-scene, both pickers, exactly like the real in-game entry point
         // (NemesisPrisonerDialogFlow): companions first (increment 1, one can be handed the prisoner to act),
         // other lord prisoners second (increment 2, one as coerced leverage). Each a no-op when there is
         // nobody to bring (cr.capture <name> a second lord, or have a companion in party, to test each).
         ChatViewModel opened = ChatManager.ActiveViewModel;
         if (opened != null)
         {
            WitnessResolver.ShowCompanionPickerIfNeeded(npc, opened.AddWitness);
            WitnessResolver.ShowBroughtCaptivePickerIfNeeded(npc, opened.AddWitness);
         }

         string warn = ModSettings.AdultContentLevel != AdultContentLevel.Hardcore
            ? " (NOTE: set Adult Content Level to Hardcore in MCM for the explicit CNC layer.)"
            : "";

         return $"Captor scene opened with {npc.Name} as your prisoner (intent {intent}).{warn}";
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
      ///   cr.execute_prisoner [name]
      ///   Nemesis pillar increment 2 (R2 execute, chat-driven entry point): exercises the
      ///   execute_prisoner bridge action directly, the same call BannerlordGameStateBridge.ExecuteAction
      ///   makes when the LLM emits it during a player-as-captor scene (NemesisPrisonerDialogFlow /
      ///   cr.nemesis_captor_scene). Targets the named hero, else the tracked nemesis currently held
      ///   prisoner (see cr.nemesis), else the current conversation partner. The bridge re-validates
      ///   custody itself, so this is not a forced test framing: the target must be a REAL prisoner
      ///   of the player.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("execute_prisoner", "cr")]
      public static string ForceExecutePrisoner(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

         Hero hero = args is {Count: > 0}
            ? ResolveHero(args)
            : null;
         hero ??= NemesisBehavior.Instance?.PlayerHeldNemesisPrisoner();
         hero ??= Hero.OneToOneConversationHero;

         if (hero == null)
            return "No prisoner found. Pass a name fragment, or hold a nemesis/hero prisoner first (cr.capture <name>).";

         NpcProfile profile = behavior.Store?.Get(hero.StringId) ?? HeroProfileMapper.FromHero(hero);
         ActionOutcome outcome = behavior.GameStateBridge.ExecuteAction(
            new GameAction {Type = "execute_prisoner"}, hero, new ActionContext(profile, null));

         return outcome?.Message ?? $"execute_prisoner produced no outcome for {hero.Name} (not currently your prisoner?).";
      }

      /// <summary>
      ///   cr.nemesis_bounty [lord name]
      ///   Nemesis pillar increment 3 (bounty missions test harness): force-offers (and immediately
      ///   accepts, on the giver's behalf) a bounty on the named lord's own region, minting or reusing a
      ///   tracked nemesis anchored there via the same MintCaptorFor path the real capture hook uses, so
      ///   the region-eligibility gate (NemesisBountyPolicy) and the reward math
      ///   (NemesisBountyRewardPolicy) both run for real, not simulated. Falls back to the first living
      ///   non-player lord when no name matches.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("nemesis_bounty", "cr")]
      public static string ForceNemesisBounty(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

         Hero lord = (args is {Count: > 0}
                        ? ResolveHero(args)
                        : null) ??
                     Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord);

         if (lord == null) return "No lord found. Pass a name fragment, or start a conversation with a lord first.";

         var roster = NemesisBehavior.Instance;

         if (roster == null) return "NemesisBehavior not available (load a campaign first).";

         string regionId = lord.HomeSettlement?.StringId ?? lord.CurrentSettlement?.StringId ?? lord.Culture?.StringId;

         if (string.IsNullOrEmpty(regionId))
            return $"{lord.Name} has no resolvable region (no home settlement, no current settlement, no culture), cannot anchor a bounty test.";

         var today = (int) CampaignTime.Now.ToDays;
         Hero nemesisHero = roster.MintCaptorFor("looters", regionId, today);

         if (nemesisHero == null) return "Could not mint or reuse a nemesis (no bandit template available in this campaign).";

         NemesisIdentity nem = roster.GetByHero(nemesisHero.StringId);

         if (nem == null) return $"{nemesisHero.Name} minted, but could not be resolved back to a tracked identity.";

         if (QuestService.HasActiveBountyFor(nem.HeroStringId, behavior.Store))
            return $"{nem.Name} {nem.Epithet} already has an active bounty on his head somewhere.";

         int gold = NemesisBountyRewardPolicy.ComputeGold(nem.RecaptureCount, nem.TimesEncountered);
         int relation = NemesisBountyRewardPolicy.ComputeRelation(nem.RecaptureCount);
         var displayName = $"{nem.Name} {nem.Epithet}";

         NpcProfile profile = GetOrCreateProfile(behavior, lord);
         InformalQuest quest = QuestService.IssueNemesisBounty(
            profile, lord, nemesisHero, displayName, gold, relation, today, behavior.Store);

         if (quest == null) return $"{lord.Name} already has an unfinished task for you, settle or abandon it first.";

         return $"{lord.Name} placed a bounty on {displayName} (region '{regionId}'): {gold}g/+{relation} for his capture or death. " +
                $"Capture or kill him, then report back to {lord.Name} (or use cr.nemesis_bounty_complete).";
      }

      /// <summary>
      ///   cr.nemesis_bounty_complete [giver name]
      ///   Force-completes the named giver's outstanding nemesis bounty (or the first active one found on
      ///   any giver, if no name matches): stamps the deed satisfied if not already, then runs the same
      ///   acknowledge-and-pay path as cr.quest_complete. Tests the report-back/reward leg without a real
      ///   capture or kill.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("nemesis_bounty_complete", "cr")]
      public static string ForceNemesisBountyComplete(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

         Hero giver = args is {Count: > 0}
            ? ResolveHero(args)
            : null;
         NpcProfile profile = giver != null
            ? GetOrCreateProfile(behavior, giver)
            : null;
         InformalQuest quest = profile?.ActiveQuests.FirstOrDefault(q => q.Type == QuestType.NemesisBounty && q.Status == QuestStatus.Active);

         if (quest == null)
            foreach (string id in behavior.Store.ListIds())
            {
               NpcProfile candidate = behavior.Store.Get(id);
               InformalQuest candidateQuest = candidate?.ActiveQuests.FirstOrDefault(q => q.Type == QuestType.NemesisBounty && q.Status == QuestStatus.Active);

               if (candidateQuest == null) continue;

               profile = candidate;
               quest = candidateQuest;
               giver = Hero.FindFirst(h => h != null && h.StringId == candidate.Id);

               break;
            }

         if (quest == null || profile == null || giver == null)
            return "No active nemesis bounty found anywhere. Seed one first with cr.nemesis_bounty [lord name].";

         int day = behavior.GameStateBridge.GetCurrentGameDay();
         if (quest.IsOutstanding)
         {
            quest.SatisfiedOnDay = day;
            quest.Evidence = "(debug) forced complete on day " + day;
            behavior.Store.Set(profile);
            QuestJournalBridge.NotifyDeedDone(quest, giver);
         }

         int rewardGold = quest.RewardGold;
         int rewardRelation = quest.RewardRelation;
         var fake = new ParsedResponse {Dialogue = "", QuestCompleted = new QuestCompletionClaim {Type = QuestType.NemesisBounty}};

         QuestService.ApplyResponse(profile, giver, fake, day,
            behavior.GameStateBridge, behavior.Store,
            (text, positive) => ChatManager.ActiveViewModel?.AppendSystemMessage(text, positive));

         return $"{giver.Name}: nemesis bounty force-completed, journal closed, reward paid ({rewardGold}g/+{rewardRelation}).";
      }

      /// <summary>
      ///   cr.nemesis_captor_scene [interrogation|desire|domination|torture|training|reward]
      ///   Nemesis pillar increment 2 (R3 test harness): opens a PLAYER-AS-CAPTOR scene against the
      ///   first tracked nemesis with a bound Hero, the inversion of his earlier captivity of the
      ///   player. Confirms his existing memory profile (built in increment 1) loads automatically, since
      ///   the scene keys off his Hero/StringId exactly like cr.captor_scene. Routes through
      ///   CaptiveSceneStager.StageCaptorScene (the VRAM-safe staged-conversation path, no live conversation
      ///   to ride from the console), unlike the real in-game entry point (NemesisPrisonerDialogFlow's
      ///   hero_main_options line, reached from the party-screen Talk on a held nemesis), which opens
      ///   directly via ChatManager.OpenForCaptorScene because it already rides a live conversation scene.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("nemesis_captor_scene", "cr")]
      public static string ForceNemesisCaptorScene(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;
         if (behavior.LlmServices is not {IsAvailable: true}) return "LLM services not available. Set your OpenRouter API key first.";

         var b = NemesisBehavior.Instance;

         if (b == null) return "NemesisBehavior not available (load a campaign first).";

         var intent = CaptiveSceneIntent.Interrogation;
         foreach (string arg in args ?? new List<string>())
            Enum.TryParse(arg, true, out intent);

         NemesisIdentity nem = b.All.FirstOrDefault(n => !string.IsNullOrEmpty(n.HeroStringId));

         if (nem == null) return "No nemesis has a bound Hero yet. Get captured by a bandit party first (or cr.nemesis to check the roster).";

         Hero nemesisHero = Hero.FindFirst(h => h != null && h.StringId == nem.HeroStringId);

         if (nemesisHero == null) return $"{nem.Name} {nem.Epithet}'s Hero could not be resolved (StringId {nem.HeroStringId}).";

         NpcProfile profile = behavior.Store?.Get(nemesisHero.StringId) ?? HeroProfileMapper.FromHero(nemesisHero);
         CaptiveSceneStager.StageCaptorScene(nemesisHero, profile, intent);

         return $"Staging captor scene with {nem.Name} {nem.Epithet} as YOUR prisoner (the inversion; intent {intent}). " +
                $"Alive={nem.IsAlive}. The chat opens once the scene loads.";
      }

      /// <summary>
      ///   cr.nemesis_execute [name]
      ///   Nemesis pillar increment 2 (R2 test harness): simulates an EXECUTION of the named tracked
      ///   nemesis, or the first living one on the roster if no name matches. Runs the exact same
      ///   NemesisResolutionBehavior.Resolve code path the real HeroKilledEvent hook uses.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("nemesis_execute", "cr")]
      public static string ForceNemesisExecuted(List<string> args)
         => ForceNemesisResolution(args, true);

      /// <summary>
      ///   cr.nemesis_kill [name]
      ///   Nemesis pillar increment 2 (R1 test harness): simulates a BATTLE death (not an execution) of
      ///   the named tracked nemesis, or the first living one on the roster if no name matches. Runs the
      ///   exact same NemesisResolutionBehavior.Resolve code path the real HeroKilledEvent hook uses.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("nemesis_kill", "cr")]
      public static string ForceNemesisKilledInBattle(List<string> args)
         => ForceNemesisResolution(args, false);

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

         CaptiveSceneStager.StageCaptiveEncounter(captor, captorProfile, witnesses, collective, intent);

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

         var kind = CompanionSceneKind.CaptorTorments;
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
         CaptiveSceneIntent intent = kind == CompanionSceneKind.CaptorTorments
            ? CaptiveSceneIntent.Torture
            : CaptiveSceneIntent.Domination;

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

         int mercy = hero.GetTraitLevel(DefaultTraits.Mercy);
         int grudge = HeaviestReleaseGrudge(hero);
         CaptorReleaseVerdict v = CaptorReleasePolicy.Resolve(mercy, grudge, false);

         return $"{hero.Name}: Mercy {mercy}, heaviest grudge vs you {grudge}. " +
                $"Persuasion {(v.AllowsPersuasion ? "OPEN" : "refused")}, Ransom {(v.AllowsRansom ? "OPEN" : "refused")}" +
                (v.RefusesAll
                   ? " (vendetta: he will not let you buy or talk your way out)."
                   : ".");
      }

      /// <summary>cr.remove_kink &lt;kink&gt; [name] — removes a kink from the NPC's profile.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("remove_kink", "cr")]
      public static string RemoveKink(List<string> args)
         => MutateKink(args, false, "cr.remove_kink <kink> [name]");

      /// <summary>
      ///   cr.nemesis [name]
      ///   No args: lists the persistent nemesis roster (name, band, region, alive, recaptures, bound Hero) and
      ///   whether the player is currently a nemesis's captive. Dev diagnostic for validating the Nemesis pillar
      ///   in game: capture by a bandit party should mint/reuse a nemesis and bind a real Hero to him.
      ///   With a name fragment: also previews the encyclopedia dossier that hero's page would render, via the
      ///   exact same NemesisDossierFactory.BuildFor the real page calls (discoverability piece, player report
      ///   SuperGraham339), printing "(hidden: not yet encountered)" when the gate would hide it.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("nemesis", "cr")]
      public static string ShowNemesisRoster(List<string> args)
      {
         var b = NemesisBehavior.Instance;

         if (b == null) return "NemesisBehavior not available (load a campaign first).";

         var lines = new List<string>();
         foreach (NemesisIdentity n in b.All)
            lines.Add($"- {n.Name} {n.Epithet} [{n.BandCultureKey}/{n.RegionId}] alive={n.IsAlive} " +
                      $"recaptures={n.RecaptureCount} encounters={n.TimesEncountered} hero={(string.IsNullOrEmpty(n.HeroStringId) ? "none" : n.HeroStringId)}");

         string held = b.PlayerHeldByNemesis()
            ? "YES — the player is currently held by a nemesis."
            : "no";

         string roster = (lines.Count == 0
                   ? "No nemesis on record yet."
                   : string.Join("\n", lines)) +
                $"\nPlayer held by a nemesis: {held}";

         if (args is not {Count: > 0}) return roster;

         Hero target = ResolveHero(args);
         if (target == null) return roster + "\n\nNo hero matches that name for a dossier preview.";

         NemesisDossierText dossier = NemesisDossierFactory.BuildFor(target);

         return roster + (dossier.HasDossier
            ? $"\n\nDossier preview for {target.Name}:\n{dossier.Label}\n{dossier.Items}"
            : $"\n\nDossier preview for {target.Name}: (hidden: not yet encountered)");
      }

      #region private

      private static string ForceNemesisResolution(List<string> args, bool wasExecuted)
      {
         var b = NemesisBehavior.Instance;

         if (b == null) return "NemesisBehavior not available (load a campaign first).";

         Hero nemesisHero = args is {Count: > 0}
            ? ResolveHero(args)
            : null;
         if (nemesisHero == null || b.GetByHero(nemesisHero.StringId) == null)
         {
            NemesisIdentity living = b.All.FirstOrDefault(n => n.IsAlive && !string.IsNullOrEmpty(n.HeroStringId));
            nemesisHero = living != null
               ? Hero.FindFirst(h => h != null && h.StringId == living.HeroStringId)
               : null;
         }

         if (nemesisHero == null) return "No living tracked nemesis found. Pass a name fragment, or capture the player once first (cr.nemesis to check the roster).";

         NemesisIdentity nem = b.GetByHero(nemesisHero.StringId);

         if (nem == null) return $"{nemesisHero.Name} is not a tracked nemesis.";
         if (!nem.IsAlive) return $"{nem.Name} {nem.Epithet} is already resolved (IsAlive=false).";

         NemesisResolutionBehavior.Resolve(nemesisHero, Hero.MainHero, wasExecuted);

         return $"{nem.Name} {nem.Epithet} resolved as {(wasExecuted ? "EXECUTED" : "KILLED IN BATTLE")} " +
                $"(IsAlive={nem.IsAlive}, deathDay={nem.DeathDay}).";
      }

      private static int HeaviestReleaseGrudge(Hero hero)
      {
         IReadOnlyList<Grudge> grudges = GrudgeBehavior.Instance?.GrudgesFor(hero?.StringId);

         if (grudges == null) return 0;

         string playerId = Hero.MainHero?.StringId;
         var max = 0;
         foreach (Grudge g in grudges)
            if (!g.IsResolved && g.TargetId == playerId && g.Magnitude > max)
               max = g.Magnitude;

         return max;
      }

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

         return companions.Count > 0
            ? companions[0]
            : null;
      }

      private static string ValidKinkNames()
         => string.Join(", ", Enum.GetNames(typeof(Kink)).Where(n => n != nameof(Kink.None)));

      #endregion
   }
}