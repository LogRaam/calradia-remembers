// Code written by Gabriel Mailhot, 21/07/2026.
// ASSEMBLY pillar, Council module: the engine side of the seating rules. Reads live Hero state into the flat
// facts CouncilRosterPolicy decides on, so the RULE stays pure and unit-tested while only this thin reader
// touches TaleWorlds. Deliberately has no distance test, matching the policy's own contract: convening your
// own house is abstract, your people answer the ban wherever they stand. That is what keeps this first slice
// clear of the party-travel layer the Political mode will need.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.Assemblies;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Assemblies
{
   /// <summary>One seated (or refused) member, with the reason so a roster can explain an empty chair.</summary>
   public sealed class CouncilSeat
   {
      public Hero Hero { get; set; }
      public bool Seated { get; set; }
      public CouncilExclusionReason Reason { get; set; }
   }

   /// <summary>Gathers the heroes a council of a given scope would seat, from live campaign state.</summary>
   public static class CouncilRosterResolver
   {
      /// <summary>
      ///   Every hero considered for a council of this scope, seated or not. The caller shows the refusals
      ///   (a companion held prisoner is worth naming, an unrelated lord is not, hence the candidate pool
      ///   below is already scoped before the policy judges availability).
      /// </summary>
      public static List<CouncilSeat> Resolve(CouncilScope scope)
      {
         var seats = new List<CouncilSeat>();

         try
         {
            foreach (Hero hero in CandidatePool(scope))
            {
               CouncilEligibility verdict = CouncilRosterPolicy.Evaluate(scope, FactsFor(hero));
               seats.Add(new CouncilSeat {Hero = hero, Seated = verdict.Eligible, Reason = verdict.Reason});
            }
         }
         catch (Exception ex)
         {
            ModLog.Error($"[COUNCIL] roster resolve failed: {ex.GetType().Name}: {ex.Message}");
         }

         return seats;
      }

      /// <summary>The seated members only, which is what a session actually opens over.</summary>
      public static List<Hero> Seated(CouncilScope scope)
         => Resolve(scope).Where(s => s.Seated && s.Hero != null).Select(s => s.Hero).ToList();

      /// <summary>
      ///   Re-checks a SINGLE hero's council eligibility against CURRENT world state, used at the council's
      ///   LIFT, where a member who pledged something at the table may since have died, been taken prisoner,
      ///   or left the roster (dismissed, no longer a clan lord). Wraps the exact same policy the original
      ///   seating (<see cref="Resolve" />) used, so a re-validation and the original seating never diverge.
      /// </summary>
      public static CouncilEligibility EvaluateNow(CouncilScope scope, Hero hero)
      {
         if (hero == null) return new CouncilEligibility(false, CouncilExclusionReason.NotOfThisCouncil);

         return CouncilRosterPolicy.Evaluate(scope, FactsFor(hero));
      }

      #region private

      // Who is even considered. Scoped BEFORE the policy runs so the refusal list stays meaningful: every
      // living lord in Calradia is "not of this council", and listing them all as refusals would be noise.
      private static IEnumerable<Hero> CandidatePool(CouncilScope scope)
      {
         Clan clan = Clan.PlayerClan;

         if (clan == null) return Enumerable.Empty<Hero>();

         switch (scope)
         {
            case CouncilScope.Companions:
               return clan.Companions?.Where(h => h != null) ?? Enumerable.Empty<Hero>();
            case CouncilScope.Family:
               return ImmediateFamily();
            case CouncilScope.ClanLords:
               return clan.Heroes?.Where(h => h is {IsLord: true} && h != Hero.MainHero) ?? Enumerable.Empty<Hero>();
            default:
               return Enumerable.Empty<Hero>();
         }
      }

      // The player's spouse, siblings, and grown children. Children who are still minors are excluded here
      // rather than in the policy: a child is not refused a seat, they were never a candidate for one.
      private static IEnumerable<Hero> ImmediateFamily()
      {
         var player = Hero.MainHero;

         if (player == null) yield break;

         if (player.Spouse is {IsAlive: true}) yield return player.Spouse;

         if (player.Siblings != null)
            foreach (Hero sibling in player.Siblings.Where(s => s is {IsAlive: true}))
               yield return sibling;

         if (player.Children != null)
            foreach (Hero child in player.Children.Where(c => c is {IsAlive: true, IsChild: false}))
               yield return child;
      }

      private static CouncilMembershipFacts FactsFor(Hero hero)
      {
         Clan clan = Clan.PlayerClan;
         var player = Hero.MainHero;
         bool isFamily = player != null
                       && (hero == player.Spouse
                        || (player.Siblings?.Contains(hero) ?? false)
                        || ((player.Children?.Contains(hero) ?? false) && !hero.IsChild));

         return new CouncilMembershipFacts(
            hero.IsAlive,
            hero.IsPrisoner,
            hero.PartyBelongedTo?.MapEvent != null,
            hero.IsPlayerCompanion,
            isFamily,
            clan != null && hero.Clan == clan && hero.IsLord && hero != player);
      }

      #endregion
   }
}
