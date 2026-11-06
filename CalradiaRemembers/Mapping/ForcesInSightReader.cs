// Code written by Gabriel Mailhot, 05/11/2026.
// The host side of ForcesInSight. 3chmidt (Nexus, 05/11/2026): on the open map a lord did not notice "an enemy army with
// 1800 soldiers" five meters away. Reads the parties the player's own party can see (the engine's spotting range and
// fog, MobileParty.IsVisible), folds an army into its leader, leaves out the speaker's own party and army and the
// player's, and hands the pure policy what the eye reads. Only on the open map: inside a settlement, nothing is said.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.War;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>The forces in sight of a conversation held on the open map.</summary>
   public static class ForcesInSightReader
   {
      /// <summary>The lines for the prompt, nearest first; null when the talk is not on the open map or nothing is in sight.</summary>
      public static IReadOnlyList<string> Read(Hero speaker)
      {
         try
         {
            MobileParty main = MobileParty.MainParty;
            if (main == null || Hero.MainHero == null || Hero.MainHero.IsPrisoner || main.CurrentSettlement != null) return null;
            if (!SeesWhatThePlayerSees(speaker, main)) return null;

            List<string> lines = ForcesInSight.Lines(Sighted(speaker, main));

            return lines.Count == 0 ? null : lines;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[SIGHT] forces in sight failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>The speaker stands where the player does: in his party or army, or his own party beside the player's.</summary>
      internal static bool SeesWhatThePlayerSees(Hero speaker, MobileParty main)
      {
         if (speaker == null) return false;
         MobileParty his = speaker.PartyBelongedTo;
         bool rides = his == main || speaker.PartyBelongedToAsPrisoner == main.Party || his?.Army != null && his.Army == main.Army;
         float? distance = his == null ? (float?) null : his.GetPosition2D.Distance(main.GetPosition2D);

         return ForcesInSight.SpeakerSeesWhatThePlayerSees(rides, distance);
      }

      /// <summary>Every force the player's party can see now, as the speaker would read it.</summary>
      internal static List<SightedForce> Sighted(Hero speaker, MobileParty main)
      {
         var found = new List<SightedForce>();
         float range = main.SeeingRange;
         MobileParty speakerParty = speaker?.PartyBelongedTo;
         IFaction speakerSide = speaker?.MapFaction ?? Hero.MainHero?.MapFaction;

         foreach (MobileParty p in MobileParty.All)
         {
            if (p == null || !p.IsActive || p.IsMainParty || p == speakerParty || p.CurrentSettlement != null || !p.IsVisible) continue;
            if (p.IsVillager || p.IsCaravan || p.IsMilitia || p.IsGarrison) continue;
            if (!p.IsBandit && p.LeaderHero == null) continue;

            Army army = p.Army;
            if (army != null)
            {
               // The whole host is named once, through its leader; the speaker's own army and the player's are no news.
               if (army.LeaderParty != p || army == speakerParty?.Army || army == main.Army) continue;
            }

            float distance = p.GetPosition2D.Distance(main.GetPosition2D);
            if (distance > range) continue;

            found.Add(new SightedForce {
               Leader = p.IsBandit ? "" : p.LeaderHero?.Name?.ToString() ?? "",
               Banner = p.IsBandit ? p.Name?.ToString() ?? "" : p.MapFaction?.Name?.ToString() ?? "",
               Men = army != null ? army.TotalManCount : p.MemberRoster?.TotalManCount ?? 0,
               IsArmy = army != null,
               Side = SideOf(p, speakerSide),
               Distance = distance
            });
         }

         return found;
      }

      private static SightedSide SideOf(MobileParty p, IFaction speakerSide)
      {
         if (p.IsBandit) return SightedSide.Outlaws;
         IFaction side = p.MapFaction;
         if (side == null || speakerSide == null) return SightedSide.Neutral;
         if (side == speakerSide) return SightedSide.Friendly;

         return FactionManager.IsAtWarAgainstFaction(side, speakerSide) ? SightedSide.Enemy : SightedSide.Neutral;
      }
   }
}
