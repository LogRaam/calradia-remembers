// Code written by Gabriel Mailhot, 05/08/2026.
// ASSEMBLY pillar, Council module, Partie 8 (COUNCIL_ACTIONS.md's Kimi review, "sponsor_ward", Kimi's design
// MINIMAL v1): the engine side of WardEligibilityPolicy, mirroring HostageCandidateResolver's own split between
// a pure predicate (Logic) and the Hero/Clan-reading glue that flattens live state into it (mod). The eligible
// pool is drawn from the MENTOR'S OWN CLAN companions (mentor.Clan.Companions) - since sponsor_ward is offered
// only at CouncilScope.ClanLords/Family, both of which always seat a member of Clan.PlayerClan (never a foreign
// house, unlike WarCouncil/Parley), this is always the player's own hired companions in practice. ExpulsionPolicy's
// own header already documents that Hero.IsPlayerCompanion is DISJOINT from both Family (blood/marriage kin) and
// ClanLords (Hero.IsLord members) in the ordinary case, so a seated mentor can never resolve to themselves or a
// fellow seated lord here - no separate "not the mentor" guard is needed.
#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.Assemblies;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Assemblies
{
   /// <summary>Resolves and validates sponsor_ward's own eligible-companion pool against live engine state.</summary>
   public static class WardCandidateResolver
   {
      /// <summary>
      ///   Every companion of <paramref name="mentor" />'s own clan that currently passes
      ///   <see cref="WardEligibilityPolicy.IsEligible" />. Used both by the offering fact (proving SOME ward is
      ///   possible) and, indirectly, by <see cref="ResolveByName" /> at the lift.
      /// </summary>
      public static IEnumerable<Hero> EligibleWardsOf(Hero mentor)
      {
         if (mentor?.Clan?.Companions == null) yield break;

         foreach (Hero candidate in mentor.Clan.Companions)
            if (candidate != null && IsEligible(candidate))
               yield return candidate;
      }

      /// <summary>True once <paramref name="mentor" />'s own clan has at least one eligible companion to sponsor: the offering fact (AnySeatedMemberCanSponsorWard).</summary>
      public static bool AnyEligibleWard(Hero mentor) => EligibleWardsOf(mentor).Any();

      /// <summary>
      ///   Resolves the named companion among <paramref name="mentor" />'s OWN eligible pool by a tolerant,
      ///   case-insensitive name match (mirrors <see cref="HostageCandidateResolver.ResolveByName" />).
      ///   Deliberately searches ONLY the eligible pool, never every companion or every living hero: an ineligible
      ///   companion (too old, already sponsored, imprisoned) or a stranger sharing the same name must never
      ///   resolve here, so the lift's own re-validation and this lookup can never disagree on WHO counts.
      /// </summary>
      public static Hero ResolveByName(string name, Hero mentor)
      {
         if (string.IsNullOrWhiteSpace(name) || mentor == null) return null;

         string query = name.Trim();

         return EligibleWardsOf(mentor).FirstOrDefault(h =>
            h.Name != null && h.Name.ToString().IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0);
      }

      #region private

      private static bool IsEligible(Hero candidate)
      {
         if (!Mapping.ActionGate.Allows(candidate, Logic.ActionKind.Ward)) return false;

         var facts = new WardCandidateFacts {IsAlive = candidate.IsAlive, IsFree = !candidate.IsPrisoner, IsYoungAdult = WardPolicy.IsWithinAgeBand(candidate.Age), IsAlreadySponsored = CouncilWardBehavior.Instance?.IsProtege(candidate.StringId) ?? false};

         return WardEligibilityPolicy.IsEligible(facts);
      }

      #endregion
   }
}
