// Code written by Gabriel Mailhot, 20/11/2026.
// Lordfadooboo (Nexus, 20/11/2026): "i was given a castle that needs 20 sturgian soldiers. in order to complete the
// quest i had to join them as a mercenary in order to be able to donate soldiers to the garrison. but even when i
// donated 30 soldiers it still didnt progress and i had to use the quest debug command to finish it."
// A garrison task only closed through the conversation hand-over (reinforce_garrison). The game's own garrison screen
// (PartyScreenHelper.OpenScreenAsDonateGarrisonWithCurrentSettlement, "Manage Troops" for a member of the realm) adds
// the men in DonateGarrisonDoneHandler and raises no event for the player (OnTroopGivenToSettlement is raised only for
// lords of the AI, GarrisonTroopsCampaignBehavior), so CR never saw them. Read in the decompiled 1.5.7 binaries: the
// handler clears the donated roster at its end, so the men are counted in a Prefix and credited in the Postfix once
// the handler has returned true. Gabriel approved (20/11/2026): the men count even when the giver is away; the task is then
// done and the giver pays when the player next speaks with them, as for any deed the game verified.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic;
using HarmonyLib;
using Helpers;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Quests
{
   /// <summary>Counts the men the player leaves on a garrison's walls through the game's screen toward a garrison task.</summary>
   [HarmonyPatch(typeof(PartyScreenHelper), "DonateGarrisonDoneHandler")]
   public static class GarrisonDonationPatch
   {
      /// <summary>Counts the fit, non-hero men in the donation before the handler clears the roster.</summary>
      public static void Prefix(TroopRoster leftMemberRoster, out int __state)
      {
         __state = 0;
         try
         {
            __state = FitMen(leftMemberRoster);
         }
         catch (Exception ex) { ModLog.Error($"[QUEST] garrison donation prefix failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>Credits the men to every garrison task naming this place, once the game has put them on the walls.</summary>
      public static void Postfix(bool __result, int __state)
      {
         try
         {
            if (!__result || __state <= 0) return;

            Credit(Hero.MainHero?.CurrentSettlement, __state);
         }
         catch (Exception ex) { ModLog.Error($"[QUEST] garrison donation credit failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>The fit men of a roster: heroes and the wounded are not counted, as in the conversation hand-over.</summary>
      internal static int FitMen(TroopRoster roster)
      {
         if (roster == null) return 0;

         var fit = 0;
         for (var i = 0; i < roster.Count; i++)
         {
            TroopRosterElement e = roster.GetElementCopyAtIndex(i);
            if (e.Character == null || e.Character.IsHero) continue;
            fit += Math.Max(0, e.Number - e.WoundedNumber);
         }

         return fit;
      }

      /// <summary>
      ///   Adds <paramref name="fitMen" /> to the outstanding garrison tasks naming <paramref name="place" />, oldest
      ///   first, each taking only what it still needs; a task that reaches its number is stamped done. Returns one line
      ///   per task credited (cr.test all reads them).
      /// </summary>
      internal static List<string> Credit(Settlement place, int fitMen)
      {
         var lines = new List<string>();
         string here = place?.Name?.ToString();
         var store = NpcMemoryCampaignBehavior.Instance?.Store;
         if (string.IsNullOrWhiteSpace(here) || store == null || fitMen <= 0) return lines;

         var tasks = new List<(NpcProfile giver, InformalQuest quest)>();
         foreach (string id in store.ListIds())
         {
            NpcProfile profile = store.Get(id);
            if (profile?.ActiveQuests == null) continue;
            foreach (InformalQuest q in profile.ActiveQuests)
               if (q != null && q.Type == QuestType.ReinforceGarrison && q.IsOutstanding
                   && string.Equals(q.TargetSettlement, here, StringComparison.OrdinalIgnoreCase))
                  tasks.Add((profile, q));
         }

         int left = fitMen;
         var day = (int) CampaignTime.Now.ToDays;
         foreach ((NpcProfile giver, InformalQuest quest) in tasks.OrderBy(t => t.quest.IssuedOnDay))
         {
            if (left <= 0) break;

            int owed = ReinforceGarrisonPolicy.StillOwed(quest.RequiredItemCount, quest.DeliveredCount);
            int given = Math.Min(left, owed);
            GarrisonDonationCredit credit = ReinforceGarrisonPolicy.Credit(new GarrisonDonationFacts {
               Required = quest.RequiredItemCount, AlreadyDelivered = quest.DeliveredCount, FitMenDonated = given
            });
            if (!credit.Counted) continue;

            left -= given;
            quest.DeliveredCount = credit.Delivered;
            ModLog.Info($"[QUEST] garrison donation: {given} fit men left on the walls of {here} for {giver.Name} ({credit.Delivered}/{quest.RequiredItemCount}).");

            if (credit.Done)
            {
               // StampDeed tells the player itself ("Report back to ..."), and marks the journal entry done.
               QuestDeedTracker.StampDeed(giver, quest, $"Left {credit.Delivered} soldiers on the walls of {here} on day {day}", day);
               lines.Add($"done: {giver.Name} {credit.Delivered}/{quest.RequiredItemCount}");
            }
            else
            {
               store.Set(giver);
               string line = $"{credit.Delivered} of the {quest.RequiredItemCount} soldiers {giver.Name} asked for stand on the walls of {here}; {credit.StillOwed} still owed.";
               lines.Add(line);
               InformationManager.DisplayMessage(new InformationMessage(line, Colors.Green));
            }
         }

         return lines;
      }
   }
}
