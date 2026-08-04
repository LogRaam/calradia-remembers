// Code written by Gabriel Mailhot, 03/08/2026.
// The Parley's own faction resolver: the specific enemy faction a parley is convened against is carried by its
// StringId (CouncilSessionBehavior.CouncilSessionSnapshot.ParleyTargetFactionId, ChatViewModel.ActivateCouncil's
// stored field), not by the free-text name the LLM might name (that path is CouncilLift.ResolveFactionByName,
// declare_war's own resolver). Shared by the roster (CouncilRosterResolver.CandidatePool's Parley case), the
// offering (ChatViewModel.PlayerCanMakePeace), and the lift (CouncilLift.SettleMakePeace's PREFERRED target), so
// all three agree on exactly which faction "the parley" means, from convening through to make_peace.

#region

using System.Linq;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Assemblies
{
   /// <summary>Resolves a faction by its persisted StringId among live Kingdoms and independent Clans.</summary>
   public static class ParleyFactionLookup
   {
      /// <summary>
      ///   Null-safe: a null/blank id, or one that matches no live Kingdom or independent Clan (an eliminated
      ///   faction, or a save written before this field existed), resolves to null rather than throwing.
      /// </summary>
      public static IFaction ByStringId(string factionId)
      {
         if (string.IsNullOrWhiteSpace(factionId)) return null;

         IFaction kingdom = Kingdom.All?.FirstOrDefault(k => k != null && k.StringId == factionId);
         if (kingdom != null) return kingdom;

         return Clan.All?.FirstOrDefault(c => c != null && c.Kingdom == null && c.StringId == factionId);
      }
   }
}
