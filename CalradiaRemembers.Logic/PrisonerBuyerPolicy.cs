// Code written by Gabriel Mailhot, 25/09/2026.
// Prisoner exchange, increment 4: who may want a captive the player holds. Until now only the captive's own clan leader,
// else his realm's leader. fkasad (Nexus, 25/09/2026) named the two who were missing: "one of my allies who, hating this
// prisoner, would like to buy him from me. Or a father wishing to buy back his daughter (a lady who changed clans upon
// marriage)." A daughter married out belongs to her husband's clan, so her father was nobody to the old rule; and a
// lord who hates a captive was never anybody.
//
// Pure: the host lists who could be interested, with what they are to the captive; this orders them and says what each
// wants. Family and realm want the captive BACK (freed home); an enemy wants him in his OWN hands.

#region

using System.Collections.Generic;
using System.Linq;
using NpcMemoryService.Core.Standing;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>What a prospective buyer is to the captive.</summary>
   public enum PrisonerBuyerRole
   {
      /// <summary>The head of the captive's own clan.</summary>
      House,

      /// <summary>A living parent of the captive in ANOTHER clan (a daughter married into her husband's house).</summary>
      Parent,

      /// <summary>The ruler of the captive's realm.</summary>
      Realm,

      /// <summary>A lord of the player's own realm who bitterly hates the captive and wants him in his hands.</summary>
      Enemy
   }

   /// <summary>One lord who might want the captive.</summary>
   public sealed class PrisonerBuyerCandidate
   {
      /// <summary>The lord's hero id.</summary>
      public string HeroId { get; set; } = "";

      /// <summary>What he is to the captive.</summary>
      public PrisonerBuyerRole Role { get; set; }

      /// <summary>Alive, a lord, free, not the captive, not the player, not riding with the player.</summary>
      public bool CanTreat { get; set; }

      /// <summary>His relation to the captive (vanilla, -100..100). Only an Enemy's is read.</summary>
      public int RelationToCaptive { get; set; }

      /// <summary>
      ///   Of the player's own clan. Such a lord asks in person and never sends an envoy to his own family (tashmetu,
      ///   27/09/2026: the player's own heir came "under a flag of truce" to buy the player's captive).
      /// </summary>
      public bool IsOfPlayersClan { get; set; }
   }

   /// <summary>Orders who may send an envoy about a captive, and says what each wants. Pure.</summary>
   public static class PrisonerBuyerPolicy
   {
      /// <summary>
      ///   The candidates who may treat, in the order an envoy is sent: the captive's house, then a parent of another
      ///   house, then the realm, then an enemy, the most hateful first. An "enemy" who does not bitterly hate the
      ///   captive (at or below <see cref="VanillaRelation.BitterTier" />) is nobody's buyer. One entry per lord.
      /// </summary>
      public static List<PrisonerBuyerCandidate> Order(IEnumerable<PrisonerBuyerCandidate> candidates)
         => (candidates ?? Enumerable.Empty<PrisonerBuyerCandidate>())
            .Where(c => c != null && c.CanTreat && !c.IsOfPlayersClan && !string.IsNullOrEmpty(c.HeroId))
            .Where(c => c.Role != PrisonerBuyerRole.Enemy || c.RelationToCaptive <= VanillaRelation.BitterTier)
            .OrderBy(c => (int) c.Role)
            .ThenBy(c => c.RelationToCaptive)
            .GroupBy(c => c.HeroId)
            .Select(g => g.First())
            .ToList();

      /// <summary>Family and realm want the captive back and free; an enemy wants him in his own custody.</summary>
      public static bool WantsCaptiveBack(PrisonerBuyerRole role) => role != PrisonerBuyerRole.Enemy;

      /// <summary>What the captive is to the lord, as the envoy's errand says it ("... holds X prisoner, {stake}.").</summary>
      public static string Stake(PrisonerBuyerRole role, string lordName, bool captiveIsFemale)
      {
         string lord = string.IsNullOrWhiteSpace(lordName) ? "the lord" : lordName;

         switch (role)
         {
            case PrisonerBuyerRole.House: return $"who is of {lord}'s own house";
            case PrisonerBuyerRole.Parent: return $"who is {lord}'s own {(captiveIsFemale ? "daughter" : "son")}";
            case PrisonerBuyerRole.Realm: return $"who is of {lord}'s own realm";
            default: return $"whom {lord} bitterly hates";
         }
      }
   }
}
