// Code written by Gabriel Mailhot, 25/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Logic;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Marriage
{
   /// <summary>
   ///   Matchmaker feature: builds the two "Name (age)" kin lists the arrange_marriage offer needs, the
   ///   player's own unwed kin and the conversation NPC's own unwed kin (of another clan). The mechanic
   ///   weds one of the player's OWN clan's unwed kin to a noble of ANOTHER clan, the player is neither
   ///   spouse, an alliance of two houses brokered through the NPC. Both lists are prebuilt game-side so
   ///   the LLM can only ever name real, eligible people.
   /// </summary>
   public static class MatchmakerResolver
   {
      /// <summary>
      ///   Lists the player's own unwed adult kin who could be married off, or null when the player's
      ///   clan is unknown or none qualify.
      /// </summary>
      public static string BuildPlayerKin()
      {
         try
         {
            if (Clan.PlayerClan?.Heroes == null) return null;

            var names = new List<string>();
            foreach (Hero h in Clan.PlayerClan.Heroes.ToList())
               if (h != null && h.IsAlive && h != Hero.MainHero && h.Spouse == null && h.Age >= MarriageEligibility.MinMarriageAge)
                  names.Add($"{h.Name} (age {(int) h.Age})");

            return names.Count == 0
               ? null
               : string.Join(", ", names);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[MATCHMAKER] player kin resolver failed: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   Lists the conversation NPC's own unwed adult kin, of a clan OTHER than the player's, that they
      ///   could give in marriage to one of the player's own kin. Null when the NPC has no authority to
      ///   give any (they neither head an outside clan nor are an unheaded parent) or none qualify.
      /// </summary>
      public static string BuildNpcKin(Hero npc)
      {
         try
         {
            if (npc == null) return null;
            var player = Hero.MainHero;

            bool isLeader = npc.Clan != null && npc.Clan.Leader == npc && npc.Clan != Clan.PlayerClan;

            var candidates = new List<Hero>();
            if (isLeader)
               foreach (Hero member in npc.Clan.Heroes.ToList())
                  if (IsGiveableKin(member, player))
                     candidates.Add(member);

            if (npc.Children != null)
               foreach (Hero child in npc.Children)
                  if (child != null && child.Clan != Clan.PlayerClan && IsGiveableKin(child, player) && !candidates.Contains(child))
                     candidates.Add(child);

            if (candidates.Count == 0) return null;

            var sb = new StringBuilder();
            for (int i = 0; i < candidates.Count; i++)
            {
               if (i > 0) sb.Append(", ");
               sb.Append($"{candidates[i].Name} (age {(int) candidates[i].Age})");
            }

            return sb.ToString();
         }
         catch (Exception ex)
         {
            ModLog.Error($"[MATCHMAKER] NPC kin resolver failed: {ex.Message}");

            return null;
         }
      }

      #region private

      /// <summary>Alive, unwed, adult, and not the player themselves.</summary>
      private static bool IsGiveableKin(Hero candidate, Hero player)
      {
         return candidate != null && candidate.IsAlive && candidate != player && candidate.Spouse == null
                && candidate.Age >= MarriageEligibility.MinMarriageAge;
      }

      #endregion
   }
}
