// Code written by Gabriel Mailhot, 01/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Quests;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>
      ///   cr.capture [name]
      ///   Takes the given lord prisoner into your party — a shortcut so you can test the prisoner
      ///   freedom-bargain without winning a battle. Imprison them in a fief's dungeon (or keep them in
      ///   your party) and speak with them: a captive lord may then bargain for their release.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("capture", "cr")]
      public static string CaptureLord(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";
         if (hero == Hero.MainHero) return "You cannot capture yourself.";
         if (hero.IsPrisoner && hero.PartyBelongedToAsPrisoner?.LeaderHero == Hero.MainHero)
            return $"{hero.Name} is already your prisoner.";

         TakePrisonerAction.Apply(PartyBase.MainParty, hero);

         return $"{hero.Name} is now your prisoner. Speak with them (in a fief dungeon, or test in conversation) " + $"— a captive lord may bargain for their freedom.";
      }

      /// <summary>
      ///   cr.quest_abandon [name]
      ///   Force-abandons the NPC's outstanding quest (as if the player told them they
      ///   quit), applying the broken-word penalty. Tests the abandon path without the LLM.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("quest_abandon", "cr")]
      public static string ForceQuestAbandon(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         int before = profile.ReputationWithPlayer;
         int day = behavior.GameStateBridge.GetCurrentGameDay();

         var fake = new ParsedResponse {Dialogue = "", QuestAbandoned = new QuestAbandonClaim()};
         QuestService.ApplyResponse(profile, hero, fake, day,
            behavior.GameStateBridge, behavior.Store,
            (text, positive) => ChatManager.ActiveViewModel?.AppendSystemMessage(text, positive));

         return $"{hero.Name}: abandon applied (regard {before}→{profile.ReputationWithPlayer}).";
      }

      /// <summary>
      ///   cr.quest_complete [name]
      ///   Simulates the NPC acknowledging the completed quest: closes the journal
      ///   entry with success and pays the promised reward. Requires cr.quest_done
      ///   first (the quest must already be in the awaiting-reward state).
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("quest_complete", "cr")]
      public static string ForceQuestComplete(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);

         if (!profile.ActiveQuests.Any(q => q.IsAwaitingReward))
            return $"{hero.Name} has no quest awaiting reward. Use cr.quest_done first.";

         int day = behavior.GameStateBridge.GetCurrentGameDay();
         var fake = new ParsedResponse {Dialogue = "", QuestCompleted = new QuestCompletionClaim()};

         QuestService.ApplyResponse(profile, hero, fake, day,
            behavior.GameStateBridge, behavior.Store,
            (text, positive) => ChatManager.ActiveViewModel?.AppendSystemMessage(text, positive));

         return $"{hero.Name}: quest completed — journal closed, reward paid.";
      }

      /// <summary>
      ///   cr.quest_done [name]
      ///   Force-stamps the NPC's outstanding quest as satisfied (bypassing real events)
      ///   so the giver will offer to reward it on the next exchange. Tests the
      ///   completion + reward path without fighting a battle.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("quest_done", "cr")]
      public static string ForceQuestDone(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         InformalQuest quest = profile.ActiveQuests.FirstOrDefault(q => q.IsOutstanding);

         if (quest == null) return $"{hero.Name} has no outstanding quest. Seed one with cr.quest first.";

         int day = behavior.GameStateBridge.GetCurrentGameDay();
         quest.SatisfiedOnDay = day;
         quest.Evidence = "(debug) forced complete on day " + day;
         behavior.Store.Set(profile);

         // Update the journal mirror so "Done" appears without a real battle.
         QuestJournalBridge.NotifyDeedDone(quest, hero);

         return $"{hero.Name}: quest '{quest.Type}' marked satisfied — journal updated. " + $"Use cr.quest_complete to pay {quest.RewardGold}g/+{quest.RewardRelation} and close the journal.";
      }

      /// <summary>
      ///   cr.declare_war_quest [name]
      ///   Seeds a declare_war bargain on the given NPC, targeting the first kingdom you can still
      ///   declare war on (not your own, not already at war). Then declare war on it (kingdom screen):
      ///   the deed is stamped done by the WarDeclared event, and the NPC pays out when you report back.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("declare_war_quest", "cr")]
      public static string SeedDeclareWarQuest(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         IFaction player = Hero.MainHero?.MapFaction;

         if (player == null) return "You belong to no faction — there is nothing to declare war with.";

         IFaction target = Kingdom.All?.Cast<IFaction>()
                                  .FirstOrDefault(k => k != player && !FactionManager.IsAtWarAgainstFaction(player, k));

         if (target == null) return "No eligible faction (you are at war with all of them, or there are none).";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         int today = behavior.GameStateBridge.GetCurrentGameDay();

         var quest = new InformalQuest {
            Type = QuestType.DeclareWar,
            Description = $"Declare war on {target.Name} for {hero.Name}.",
            TargetFactionId = target.StringId,
            TargetFactionName = target.Name?.ToString(),
            IssuedOnDay = today,
            Reward = RewardGrant.None,
            RewardGold = 1000,
            RewardRelation = 8,
            Status = QuestStatus.Active
         };
         profile.ActiveQuests.Add(quest);
         behavior.Store.Set(profile);
         QuestJournalBridge.OpenMirror(quest, hero);

         return $"{hero.Name} now wants you to declare war on {target.Name}. Declare it, then report back.";
      }

      /// <summary>
      ///   cr.deliver_prisoner [name]
      ///   Seeds a deliver_prisoner bargain on the given NPC targeting the first LORD you currently hold
      ///   (case a — an immediate hand-over). Then, in conversation with that NPC, offer to deliver them:
      ///   the NPC emits give_prisoner, the captive is transferred, and the reward (500g/+5) is paid.
      ///   Hold a lord prisoner first. (Case b — capture-and-deliver — arises in real play.)
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("deliver_prisoner", "cr")]
      public static string SeedDeliverPrisoner(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         Hero held = MobileParty.MainParty?.PrisonRoster?.GetTroopRoster()
                                ?.Select(el => el.Character?.HeroObject)
                                .FirstOrDefault(h => h is {IsLord: true, IsHumanPlayerCharacter: false});

         if (held == null)
            return "You hold no lord prisoner. Capture an enemy lord first, then run this to test the hand-over (case a).";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         int today = behavior.GameStateBridge.GetCurrentGameDay();

         var quest = new InformalQuest {
            Type = QuestType.DeliverPrisoner,
            Description = $"Hand {held.Name} over to {hero.Name}.",
            TargetHeroId = held.StringId,
            TargetHeroName = held.Name?.ToString(),
            IssuedOnDay = today,
            Reward = RewardGrant.None,
            RewardGold = 500,
            RewardRelation = 5,
            Status = QuestStatus.Active
         };
         profile.ActiveQuests.Add(quest);
         behavior.Store.Set(profile);

         return $"{hero.Name} now wants {held.Name} handed over. Talk to {hero.Name} and offer to deliver them.";
      }

      /// <summary>
      ///   cr.bargain_items [name]
      ///   Seeds a DeliverItems→JoinParty bargain on a recruitable wanderer, so the
      ///   pay-in-goods flow (bag icon → multi-select → bridge settles, the wanderer joins)
      ///   can be tested without waiting for the LLM to offer it. The required value is the
      ///   game-side floor (twice the vanilla hiring price). Open a conversation with the
      ///   wanderer first (or pass a name), then click the bag and hand over goods.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("bargain_items", "cr")]
      public static string SeedItemBargain(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         if (!RecruitmentPricing.IsRecruitable(hero))
            return $"{hero.Name} is not a recruitable wanderer — no item bargain possible (see cr.hire_info).";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);

         if (profile.ActiveQuests.Any(q => q.IsOutstanding))
            return $"{hero.Name} already has an outstanding task — settle or abandon it first.";

         var day = (int) CampaignTime.Now.ToDays;
         var proposal = new QuestProposal {
            Type = QuestType.DeliverItems,
            Reward = RewardGrant.JoinParty,
            Description = "Bring me goods worth my service, and I am yours."
         };

         InformalQuest quest = QuestFactory.Create(proposal, hero, day);

         if (quest == null) return $"Could not ground the bargain for {hero.Name}.";

         profile.ActiveQuests.Add(quest);
         behavior.Store.Set(profile);
         QuestJournalBridge.OpenMirror(quest, hero);

         int? hire = RecruitmentPricing.GetHiringPrice(hero);

         return $"{hero.Name}: item bargain seeded. Hire price {hire?.ToString() ?? "?"} denars → " + $"required goods value {quest.RequiredItemValue} denars (2×). " + "Open the chat, click the bag, and hand over goods to settle.";
      }

      /// <summary>
      ///   cr.quest [name]
      ///   Seeds a bandit-clear quest (near a real village, 10-day deadline, 300g/+3)
      ///   on the NPC, through the real issue path. Tests prompt injection of the quest
      ///   and the giver acknowledging it. Pair with cr.quest_done to test the reward.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("quest", "cr")]
      public static string SeedQuest(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         var village = Settlement.All.FirstOrDefault(s => s.IsVillage)?.Name?.ToString();

         if (village == null) return "No village found to anchor the quest.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         int day = behavior.GameStateBridge.GetCurrentGameDay();

         var proposal = new QuestProposal {
            Type = QuestType.BanditClear,
            Description = $"Clear the bandits troubling the roads near {village}.",
            TargetSettlement = village,
            DeadlineDays = 10,
            RewardGold = 300,
            RewardRelation = 3
         };
         var fake = new ParsedResponse {Dialogue = "", QuestGiven = proposal};

         QuestService.ApplyResponse(profile, hero, fake, day,
            behavior.GameStateBridge, behavior.Store,
            (text, positive) => ChatManager.ActiveViewModel?.AppendSystemMessage(text, positive));

         int outstanding = profile.ActiveQuests.Count(q => q.IsOutstanding);

         return $"{hero.Name}: seeded bandit_clear near {village} ({outstanding} outstanding quest(s)).";
      }

      /// <summary>
      ///   cr.quest_gold [amount] [name]
      ///   Seeds an outstanding ProvideGold quest on the NPC (e.g. a spouse's request for coin to
      ///   support their household) so the Give Gold button — and the take_gold stamping path — can be
      ///   tested deterministically, without waiting for the LLM to issue one or a child-support letter
      ///   to arrive. Any non-zero gold the player gives the giver satisfies it; [amount] is flavour for
      ///   the description (default 1000).
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("quest_gold", "cr")]
      public static string SeedProvideGoldQuest(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         // Optional leading amount; the remainder (or all of it) is the name fragment.
         int amount = 1000;
         IEnumerable<string> nameTokens = args;
         if (args is {Count: > 0} && int.TryParse(args[0], out int parsed) && parsed > 0)
         {
            amount = parsed;
            nameTokens = args.Skip(1);
         }

         Hero hero = ResolveHero(nameTokens);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         int day = behavior.GameStateBridge.GetCurrentGameDay();

         var proposal = new QuestProposal {
            Type = QuestType.ProvideGold,
            Description = $"{hero.Name} asks you for {amount} denars to help support their household.",
            DeadlineDays = 30,
            RewardRelation = 5
         };
         var fake = new ParsedResponse {Dialogue = "", QuestGiven = proposal};

         QuestService.ApplyResponse(profile, hero, fake, day,
            behavior.GameStateBridge, behavior.Store,
            (text, positive) => ChatManager.ActiveViewModel?.AppendSystemMessage(text, positive));

         int outstanding = profile.ActiveQuests.Count(q => q.IsOutstanding);

         return $"{hero.Name}: seeded ProvideGold (~{amount} denars). Open a chat and use the Give Gold " +
                $"button (or cr.take_gold <n> {hero.Name}) — any payment stamps it. " +
                $"({outstanding} outstanding quest(s); verify with cr.quests {hero.Name}).";
      }

      /// <summary>
      ///   cr.quest_scout [name]
      ///   Seeds a scout_army quest on the NPC, targeting their first enemy kingdom.
      ///   Pair with cr.quest_done to test the completion reward, or let proximity
      ///   detection fire naturally (OnHourlyTick) for a real-path test.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("quest_scout", "cr")]
      public static string SeedScoutQuest(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         IFaction enemy = hero.MapFaction != null
            ? Kingdom.All?.FirstOrDefault(k =>
               !k.IsEliminated && FactionManager.IsAtWarAgainstFaction(hero.MapFaction, k))
            : null;

         if (enemy == null)
            return $"{hero.Name} is not at war with any kingdom — cannot seed a scout_army quest.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         int day = behavior.GameStateBridge.GetCurrentGameDay();

         var proposal = new QuestProposal {
            Type = QuestType.ScoutArmy,
            Description = $"Find where the armies of {enemy.Name} are moving and report back.",
            TargetFaction = enemy.Name?.ToString(),
            DeadlineDays = 15,
            RewardGold = 400,
            RewardRelation = 3
         };
         var fake = new ParsedResponse {Dialogue = "", QuestGiven = proposal};

         QuestService.ApplyResponse(profile, hero, fake, day,
            behavior.GameStateBridge, behavior.Store,
            (text, positive) => ChatManager.ActiveViewModel?.AppendSystemMessage(text, positive));

         int outstanding = profile.ActiveQuests.Count(q => q.IsOutstanding);

         return $"{hero.Name}: seeded scout_army quest targeting {enemy.Name} ({outstanding} outstanding quest(s)).";
      }

      /// <summary>
      ///   cr.quests [name]
      ///   Lists the NPC's quests with their state.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("quests", "cr")]
      public static string ListQuests(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);

         if (profile.ActiveQuests.Count == 0) return $"{hero.Name} has no quests.";

         IEnumerable<string> lines = profile.ActiveQuests.Select(q =>
         {
            string state = q.IsOutstanding ? "outstanding"
               : q.IsAwaitingReward ? "DONE (awaiting reward)"
               : q.Status.ToString();
            string deadline = q.DeadlineDay.HasValue
               ? $", deadline day {q.DeadlineDay}"
               : "";

            return $"  [{q.Type}] {state}{deadline} — {q.Description}";
         });

         return $"{hero.Name} quests:\n" + string.Join("\n", lines);
      }
   }
}
