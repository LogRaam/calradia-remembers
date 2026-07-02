// Code written by Gabriel Mailhot, 01/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Marriage;
using CalradiaRemembers.Quests;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>
      ///   cr.marry [name]
      ///   Forces a native marriage between the player and the named hero so the
      ///   consequence resolver (warm/hostile family) can be tested end to end. Pair with
      ///   cr.marriage_approve / cr.marriage_revoke to test both outcomes.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("marry", "cr")]
      public static string ForceMarry(List<string> args)
      {
         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";
         if (Hero.MainHero.Spouse != null) return $"You are already married to {Hero.MainHero.Spouse.Name}.";
         if (hero.Spouse != null) return $"{hero.Name} is already married to {hero.Spouse.Name}.";

         MarriageAction.Apply(Hero.MainHero, hero);

         return $"Married {hero.Name}. Check the quick-notice for the family's reaction (and cr.info on a kin).";
      }

      /// <summary>
      ///   cr.marriage_approve [name]
      ///   Seeds the family's blessing for the player to marry the named hero (approver =
      ///   their clan leader). Then marry them (cr.marry or in-game) to see the family warm
      ///   to you instead of turning hostile.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("marriage_approve", "cr")]
      public static string MarriageApprove(List<string> args)
      {
         if (MarriageApprovalBehavior.Instance == null)
            return "Marriage behavior not active. Start a campaign first.";

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         MarriageApprovalBehavior.Instance.Grant(
            hero, hero.Clan?.Leader, (int) CampaignTime.Now.ToDays);

         return $"{hero.Name}: family blessing seeded (by {hero.Clan?.Leader?.Name?.ToString() ?? "?"}). " + "Marry them now and the family will embrace the union instead of resenting it.";
      }

      /// <summary>cr.marriage_revoke [name] — removes a seeded blessing for the named hero.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("marriage_revoke", "cr")]
      public static string MarriageRevoke(List<string> args)
      {
         if (MarriageApprovalBehavior.Instance == null)
            return "Marriage behavior not active. Start a campaign first.";

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         bool removed = MarriageApprovalBehavior.Instance.Revoke(hero.StringId);

         return removed
            ? $"{hero.Name}: blessing revoked. Marrying them now would affront the family."
            : $"{hero.Name}: no blessing on record.";
      }

      /// <summary>
      ///   cr.marriage_status [name]
      ///   Reports whether the family has blessed a match with the named hero, plus the
      ///   basic eligibility the marriage path will check (spouse single, player single).
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("marriage_status", "cr")]
      public static string MarriageStatus(List<string> args)
      {
         if (MarriageApprovalBehavior.Instance == null)
            return "Marriage behavior not active. Start a campaign first.";

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         bool approved = MarriageApprovalBehavior.Instance.IsApproved(hero.StringId);
         string clan = hero.Clan?.Name?.ToString() ?? "(no clan)";

         return $"{hero.Name} — family: {clan}; blessing: {(approved ? "GRANTED" : "none")}; " + $"spouse free: {hero.Spouse == null}; player free: {Hero.MainHero.Spouse == null}.";
      }

      /// <summary>
      ///   cr.bargain_marriage [name]
      ///   Seeds a marriage_consent bargain on a clan head (or a parent): a deed (clear bandits
      ///   near a village) conditions the blessing for the player to marry one of their eligible
      ///   kin. Tests the full A.2b path without the LLM: then cr.quest_done + cr.quest_complete
      ///   → the blessing is recorded; verify with cr.marriage_status [spouse].
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("bargain_marriage", "cr")]
      public static string SeedMarriageBargain(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error))
            return error;

         Hero hero = ResolveHero(args);

         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         Hero spouse = FindEligibleCandidate(hero);

         if (spouse == null)
            return $"{hero.Name} has no unwed, eligible kin to offer (or you are already wed / no authority).";

         var village = Settlement.All.FirstOrDefault(s => s.IsVillage)?.Name?.ToString();

         if (village == null) return "No village found to anchor the deed.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);

         if (profile.ActiveQuests.Any(q => q.IsOutstanding))
            return $"{hero.Name} already has an outstanding task — settle or abandon it first.";

         int day = behavior.GameStateBridge.GetCurrentGameDay();
         var proposal = new QuestProposal {
            Type = QuestType.BanditClear,
            Description = $"Prove yourself worthy of {spouse.Name}'s hand: clear the bandits near {village}.",
            TargetSettlement = village,
            Reward = RewardGrant.MarriageConsent,
            MarriageSpouse = spouse.Name?.ToString()
         };

         InformalQuest quest = QuestFactory.Create(proposal, hero, day);

         if (quest == null) return $"Could not ground the marriage bargain for {hero.Name} → {spouse.Name}.";

         profile.ActiveQuests.Add(quest);
         behavior.Store.Set(profile);
         QuestJournalBridge.OpenMirror(quest, hero);

         return $"{hero.Name}: marriage bargain seeded — clear bandits near {village} to earn the blessing for " + $"marrying {spouse.Name}. Then: cr.quest_done, cr.quest_complete, then cr.marriage_status {spouse.Name}.";
      }

      /// <summary>First unwed, eligible kin of the NPC's house (clan + own children) the player could marry.</summary>
      private static Hero FindEligibleCandidate(Hero npc)
      {
         if (npc == null || Hero.MainHero == null) return null;
         var pool = new List<Hero>();
         if (npc.Clan != null) pool.AddRange(npc.Clan.Heroes);
         if (npc.Children != null) pool.AddRange(npc.Children);

         return pool.FirstOrDefault(h => h is {IsAlive: true} && MarriageProspectResolver.IsMarriageable(Hero.MainHero, h) && MarriageProspectResolver.HasAuthority(npc, h));
      }
   }
}
