// Code written by Gabriel Mailhot, 04/08/2026.
// ASSEMBLY pillar, Council module, Partie 8 (COUNCIL_ACTIONS.md's Kimi review, "give_hostage", v1 "invite
// d'honneur"): the engine side of HostageEligibilityPolicy, mirroring CourtActionResolver's own split between a
// pure predicate (Logic) and the Hero/Clan-reading glue that flattens live state into it (mod). Eligible
// relatives are drawn from the SAME close-kin pool CourtActionResolver.IsCloseKin already reads for
// pledge_against (spouse, children, siblings) MINUS parents (COUNCIL_ACTIONS.md names "enfant majeur, frere/soeur,
// epouse" only - a parent given as a hostage by their own child is not the "invite d'honneur" Kimi's design
// describes, and vanilla's own succession model already treats a parent as senior to the pledger, not a pledge
// the pledger has standing to make on their behalf).
#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.Assemblies;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Assemblies
{
   /// <summary>Resolves and validates give_hostage's own eligible-relative pool against live engine state.</summary>
   public static class HostageCandidateResolver
   {
      /// <summary>
      ///   Every relative of <paramref name="pledger" /> (spouse, children, siblings - COUNCIL_ACTIONS.md's own
      ///   named pool) that currently passes <see cref="HostageEligibilityPolicy.IsEligible" />. Used both by the
      ///   offering fact (proving SOME hostage is possible) and, indirectly, by <see cref="ResolveByName" /> at
      ///   the lift.
      /// </summary>
      public static IEnumerable<Hero> EligibleRelativesOf(Hero pledger)
      {
         if (pledger == null) yield break;

         foreach (Hero candidate in RawRelativesOf(pledger))
            if (IsEligible(pledger, candidate))
               yield return candidate;
      }

      /// <summary>True once <paramref name="pledger" /> has at least one eligible relative to give: the offering fact (AnySeatedHostageEligibleRelativeAvailable).</summary>
      public static bool AnyEligibleRelative(Hero pledger) => EligibleRelativesOf(pledger).Any();

      /// <summary>
      ///   Resolves the named hostage among <paramref name="pledger" />'s OWN eligible relatives by a tolerant,
      ///   case-insensitive name match (mirrors <see cref="CalradiaRemembers.GameState.CourtActionResolver.ResolvePledgeTarget" />).
      ///   Deliberately searches ONLY the eligible pool, never every living hero: an ineligible relative (a minor,
      ///   the clan's own chief, the sole heir) or a stranger sharing the same name must never resolve here, so
      ///   the lift's own re-validation and this lookup can never disagree on WHO counts.
      /// </summary>
      public static Hero ResolveByName(string name, Hero pledger)
      {
         if (string.IsNullOrWhiteSpace(name) || pledger == null) return null;

         string query = name.Trim();

         return EligibleRelativesOf(pledger).FirstOrDefault(h =>
            h.Name != null && h.Name.ToString().IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0);
      }

      #region private

      /// <summary>COUNCIL_ACTIONS.md's own named pool: an adult child, a sibling, or the pledger's own spouse. Deduplicated (a spouse who is also a sibling is not possible in-engine, but Distinct costs nothing).</summary>
      private static IEnumerable<Hero> RawRelativesOf(Hero pledger)
      {
         var pool = new List<Hero>();

         if (pledger.Spouse != null) pool.Add(pledger.Spouse);
         if (pledger.Children != null) pool.AddRange(pledger.Children);
         if (pledger.Siblings != null) pool.AddRange(pledger.Siblings);

         return pool.Where(h => h != null).Distinct();
      }

      private static bool IsEligible(Hero pledger, Hero candidate)
      {
         bool isClanChief = candidate.Clan != null && candidate.Clan.Leader == candidate;

         var facts = new HostageCandidateFacts(
            isAlive: candidate.IsAlive,
            isFree: !candidate.IsPrisoner,
            isAdult: !candidate.IsChild,
            isClanChief: isClanChief,
            isSoleHeir: IsSoleHeir(pledger, candidate));

         return HostageEligibilityPolicy.IsEligible(facts);
      }

      /// <summary>
      ///   COUNCIL_ACTIONS.md's own "pas heritier unique": true when <paramref name="candidate" /> is the
      ///   pledger's clan's ONLY OTHER alive adult member besides the pledger themselves (see
      ///   HostageEligibilityPolicy's own header for why this is the chosen approximation of vanilla's own
      ///   succession risk). A clan with two or more other adult members never trips this for any single one of
      ///   them.
      /// </summary>
      private static bool IsSoleHeir(Hero pledger, Hero candidate)
      {
         Clan clan = pledger?.Clan;
         if (clan?.Heroes == null) return false;

         List<Hero> otherAdults = clan.Heroes.Where(h => h != pledger && h is {IsAlive: true} && !h.IsChild).ToList();

         return otherAdults.Count == 1 && otherAdults[0] == candidate;
      }

      #endregion
   }
}
