// Code written by Gabriel Mailhot, 01/10/2026.
// The places a character answers for, closest first: what he governs day to day, then the fiefs his house holds when
// he leads it, then, for a notable who answers for nowhere else, home. Two readers ask this: the seat-standing pack
// (what he knows of those places) and the grain relief (which hungry place the grain he asked for feeds). The
// engine-facts map found the same lookup written twice on 01/10/2026; one home keeps them from drifting apart.

#region

using System.Collections.Generic;
using NpcMemoryService.Core.Knowledge;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>The places a character answers for, closest first.</summary>
   public static class HeroSeats
   {
      /// <summary>Each place with the role he holds it in, closest first. Empty, never null.</summary>
      public static List<(Settlement Place, SeatRole Role)> ClosestFirst(Hero hero)
      {
         var seats = new List<(Settlement, SeatRole)>();
         if (hero == null) return seats;

         Settlement governed = hero.GovernorOf?.Settlement;
         if (governed != null) seats.Add((governed, SeatRole.Governor));

         if (hero.IsClanLeader && hero.Clan?.Fiefs != null)
            foreach (Town fief in hero.Clan.Fiefs)
            {
               Settlement held = fief?.Settlement;
               if (held != null && held != governed) seats.Add((held, SeatRole.Owner));
            }

         // A notable answers for nowhere but home, and knows it better than anyone above them does.
         if (seats.Count == 0 && hero.IsNotable && hero.HomeSettlement != null) seats.Add((hero.HomeSettlement, SeatRole.Notable));

         return seats;
      }
   }
}
