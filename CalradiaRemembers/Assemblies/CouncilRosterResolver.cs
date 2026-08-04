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
using CalradiaRemembers.Missions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

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
      ///   below is already scoped before the policy judges availability). <paramref name="parleyTargetFactionId" />
      ///   is the Parley's own extra parameter (ignored by every other scope): the StringId of the specific enemy
      ///   faction the parley was convened against, without which a Parley's candidate pool is empty (there is no
      ///   counterpart to seat).
      /// </summary>
      public static List<CouncilSeat> Resolve(CouncilScope scope, string parleyTargetFactionId = null)
      {
         var seats = new List<CouncilSeat>();

         try
         {
            foreach (Hero hero in CandidatePool(scope, parleyTargetFactionId))
            {
               CouncilEligibility verdict = CouncilRosterPolicy.Evaluate(scope, FactsFor(hero, parleyTargetFactionId));
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
      public static List<Hero> Seated(CouncilScope scope, string parleyTargetFactionId = null)
         => Resolve(scope, parleyTargetFactionId).Where(s => s.Seated && s.Hero != null).Select(s => s.Hero).ToList();

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

      /// <summary>
      ///   A seated member's true whereabouts relative to the player, in a short honest phrase (bug c: a
      ///   member seated purely on availability, with no distance test by design, could otherwise be voiced as
      ///   though they travelled at the player's side while actually staying behind). Reads the same live Hero
      ///   state the roster already touches into the pure <see cref="MemberPresenceDescriber" />, so the wording
      ///   itself stays unit-tested. Never throws: any resolution gap degrades to the "abroad" phrase rather
      ///   than surface an exception into the prompt-building path.
      /// </summary>
      public static string DescribePresence(Hero hero)
      {
         try
         {
            MobileParty party = hero?.PartyBelongedTo;
            bool inMainParty = party != null && party == MobileParty.MainParty;
            bool leadsOwnParty = !inMainParty && party != null && party.LeaderHero == hero;
            string settlementName = party == null ? hero?.CurrentSettlement?.Name?.ToString() : null;

            return MemberPresenceDescriber.Describe(new MemberPresenceFacts(inMainParty, leadsOwnParty, settlementName));
         }
         catch (Exception ex)
         {
            ModLog.Error($"[COUNCIL] DescribePresence failed for {hero?.Name}: {ex.GetType().Name}: {ex.Message}");

            return MemberPresenceDescriber.Describe(new MemberPresenceFacts(false, false, null));
         }
      }

      #region private

      // Who is even considered. Scoped BEFORE the policy runs so the refusal list stays meaningful: every
      // living lord in Calradia is "not of this council", and listing them all as refusals would be noise.
      private static IEnumerable<Hero> CandidatePool(CouncilScope scope, string parleyTargetFactionId = null)
      {
         // Parley is parameterized by a specific ENEMY faction, not the player's own house, so it is resolved
         // before the player-clan null-guard below (which every OTHER scope needs, but a parley does not: its
         // one candidate comes from the target faction, never Clan.PlayerClan).
         if (scope == CouncilScope.Parley) return ParleyCandidates(parleyTargetFactionId);

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
            case CouncilScope.WarCouncil:
               return WarCouncilCandidates(clan);
            default:
               return Enumerable.Empty<Hero>();
         }
      }

      // A parley's roster (author's decision): a single-seat pool, the LEADER of the specific enemy faction the
      // parley was convened against, the negotiating counterpart. No target stored (or it no longer resolves,
      // e.g. an eliminated faction) yields an empty pool: a parley with nobody to negotiate with cannot open.
      private static IEnumerable<Hero> ParleyCandidates(string parleyTargetFactionId)
      {
         IFaction target = ParleyFactionLookup.ByStringId(parleyTargetFactionId);
         Hero leader = target?.Leader;

         if (leader == null) yield break;

         yield return leader;
      }

      // Roster default (author's decision): the war council gathers the player's ALLIES, i.e. the leaders of
      // the OTHER clans in the player's kingdom. An independent clan (no kingdom, so no allies to gather) falls
      // back to the player's own clan lords instead, mirroring CandidatePool's ClanLords case exactly, so a
      // solo player can still convene a war council over his own house rather than seat nobody at all.
      private static IEnumerable<Hero> WarCouncilCandidates(Clan clan)
      {
         if (clan.Kingdom != null)
            return clan.Kingdom.Clans?
                      .Where(c => c != null && c != clan)
                      .Select(c => c.Leader)
                      .Where(h => h != null)
                  ?? Enumerable.Empty<Hero>();

         return clan.Heroes?.Where(h => h is {IsLord: true} && h != Hero.MainHero) ?? Enumerable.Empty<Hero>();
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

      private static CouncilMembershipFacts FactsFor(Hero hero, string parleyTargetFactionId = null)
      {
         Clan clan = Clan.PlayerClan;
         var player = Hero.MainHero;
         bool isFamily = player != null
                       && (hero == player.Spouse
                        || (player.Siblings?.Contains(hero) ?? false)
                        || ((player.Children?.Contains(hero) ?? false) && !hero.IsChild));

         // Mirrors WarCouncilCandidates exactly (a hero seated by that pool must also read true here): with a
         // kingdom, an allied leader is a DIFFERENT clan's own leader within the player's kingdom; without one
         // (independent clan, no allies), the same no-kingdom fallback as ClanLords applies.
         bool isAlliedLeader = clan != null
                             && (clan.Kingdom != null
                                ? hero.Clan != null && hero.Clan != clan && hero.Clan.Kingdom == clan.Kingdom && hero.Clan.Leader == hero
                                : hero.Clan == clan && hero.IsLord && hero != player);

         // Mirrors ParleyCandidates exactly (the one hero that pool ever returns must also read true here): the
         // leader of the stored parley target faction, resolved by StringId (never by name, unlike declare_war's
         // free-text target).
         bool isParleyCounterpart = ParleyFactionLookup.ByStringId(parleyTargetFactionId)?.Leader == hero;

         return new CouncilMembershipFacts(
            hero.IsAlive,
            hero.IsPrisoner,
            hero.PartyBelongedTo?.MapEvent != null,
            hero.IsPlayerCompanion,
            isFamily,
            clan != null && hero.Clan == clan && hero.IsLord && hero != player,
            hero.PartyBelongedTo?.Army != null,
            CompanionMissionBehavior.Instance?.IsAway(hero.StringId) ?? false,
            isAlliedLeader,
            isParleyCounterpart);
      }

      #endregion
   }
}
