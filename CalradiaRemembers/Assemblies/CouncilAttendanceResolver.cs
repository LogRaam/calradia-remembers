// Code written by Gabriel Mailhot, 29/08/2026.
// Convergence Inc 2 of 5: the engine side of CouncilAttendancePolicy (Logic/Assemblies/CouncilAttendancePolicy.cs,
// Inc 1). Builds a council's candidate pool for a given scope (reusing CouncilRosterResolver's own pooling, so
// the enemy/parley lookup is never duplicated), projects each candidate hero into the pure CouncilAttendanceFacts,
// and runs the already-tested Decide to get a seated result. Deliberately does NOT touch CouncilRosterResolver,
// ChatManager.OpenForCouncil, or CouncilSessionBehavior: the live council still opens on CouncilRosterPolicy's
// verdicts, this resolver exists to be proven correct in isolation first. Wiring it into the live path is Inc 3.
//
// Naming note: CouncilRosterResolver already declares a `CouncilSeat` class in THIS namespace (Hero/Seated/Reason,
// the old prisoner/scope gate's own shape). Rather than touch that file, the new result type here is named
// CouncilAttendanceSeat to avoid a collision, and carries what later increments need (the represented faction,
// for a generated ambassador's identity) rather than just a bool.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic.Assemblies;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Assemblies
{
   /// <summary>
   ///   One seat CouncilAttendancePolicy actually granted (InPerson or Ambassador; Absent verdicts are dropped,
   ///   see <see cref="Resolve" />). RepresentedHero is always the real lord the policy judged; for an Ambassador
   ///   seat, RepresentedFaction is who a later increment generates the stand-in from (the hero's own house), a
   ///   own-faction hero is never an Ambassador (CouncilAttendancePolicy's own guarantee).
   /// </summary>
   public sealed class CouncilAttendanceSeat
   {
      public Hero RepresentedHero { get; set; }
      public IFaction RepresentedFaction { get; set; }
      public CouncilAttendance Attendance { get; set; }
      public CouncilAttendanceReason Reason { get; set; }
   }

   /// <summary>
   ///   Reads live campaign state into <see cref="CouncilAttendanceFacts" /> per candidate and runs the pure
   ///   <see cref="CouncilAttendancePolicy" />, so the attendance RULE stays unit-tested while only this thin
   ///   reader touches TaleWorlds. Mirrors AssemblyInvitationFactsBuilder's own contract: every read is
   ///   null-guarded and never throws, one candidate's odd state must never break the whole council.
   /// </summary>
   public static class CouncilAttendanceResolver
   {
      /// <summary>
      ///   No MCM setting exists yet for how many days' travel a council will wait on (Inc 5 wires it). Used as
      ///   the threshold until then; callers should NOT hardcode their own value, so the one knob moves in one
      ///   place once the setting lands.
      /// </summary>
      public const double DefaultThresholdDays = 1.0;

      /// <summary>
      ///   TUNABLE placeholder: the average map speed (map units/hour) assumed for a travelling lord's party when
      ///   no better estimate is available, used only to turn a raw map distance into a day count for
      ///   <see cref="CouncilAttendanceFacts.EtaDays" />. Set below CourierSpeedPatch.CourierSpeedFloor (6.5), which
      ///   is a BOOSTED floor applied only to our own courier parties, not the ordinary pace of an AI lord's
      ///   warband. Dial in from play-testing like every other Assembly distance constant (AssemblyDistanceTiers).
      /// </summary>
      public const double AverageLordPartyMapSpeedPerHour = 5.0;

      private const double HoursPerDay = 24.0;

      /// <summary>
      ///   Resolves the seated set for one council of the given scope: the candidate pool comes from
      ///   <see cref="CouncilRosterResolver.Resolve" /> (every hero it CONSIDERS for the scope, seated or not
      ///   under the OLD policy, which is exactly the pool this resolver re-judges under the NEW one), each
      ///   candidate is projected into <see cref="CouncilAttendanceFacts" /> and decided by
      ///   <see cref="CouncilAttendancePolicy.Decide" />. Only InPerson and Ambassador verdicts are returned
      ///   (an Absent candidate is simply not seated, and the caller has no use for an empty chair here the way
      ///   the OLD roster panel did with exclusion reasons); <paramref name="parleyTargetFactionId" /> is the
      ///   Parley scope's own extra parameter, ignored by every other scope. Never throws: a failure anywhere
      ///   (null campaign, an odd hero) degrades to an empty result rather than break a council screen.
      /// </summary>
      public static IReadOnlyList<CouncilAttendanceSeat> Resolve(CouncilScope scope, double thresholdDays, string parleyTargetFactionId = null)
      {
         var seats = new List<CouncilAttendanceSeat>();

         try
         {
            Clan playerClan = Clan.PlayerClan;

            List<Hero> pool = CouncilRosterResolver.Resolve(scope, parleyTargetFactionId)
               .Select(s => s.Hero)
               .Where(h => h != null)
               .ToList();

            foreach (Hero hero in pool)
               TryAppendSeat(seats, hero, playerClan, scope, thresholdDays, parleyTargetFactionId);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[COUNCIL] attendance resolve failed: {ex.GetType().Name}: {ex.Message}");
         }

         return seats;
      }

      /// <summary>
      ///   Convergence Inc 3a: the ONE seated-roster call both <c>ChatManager.OpenForCouncil</c> and
      ///   <c>CouncilConversationStager</c> (StageCouncil + ResolveAnchor) now go through, so the anchor a
      ///   staged conversation loads and the roster the council actually opens over can never diverge (they
      ///   used to share <see cref="CouncilRosterResolver.Seated" /> directly; this is the same sharing, now
      ///   pointed at the new policy). For the Parley scope, the seat is another faction's lord, which
      ///   <see cref="CouncilAttendancePolicy" /> resolves to Ambassador rather than InPerson, and seating a
      ///   generated ambassador stand-in is deliberately NOT built yet (Inc 3b), so Parley is left on the OLD
      ///   <see cref="CouncilRosterResolver.Seated" /> path, completely unchanged, until that lands. Every other
      ///   scope seats only InPerson verdicts (an own-faction lord too far away is now simply absent, the
      ///   "war council felt magic" fix); Ambassador seats for those scopes are also Inc 3b's job, not this one's.
      /// </summary>
      public static List<Hero> SeatedInPersonHeroes(CouncilScope scope, string parleyTargetFactionId = null)
      {
         // A trial seats the bench its case names (the liege and the vassals who hate the player most), wherever
         // they stand: they are summoned to judge (arrests, 26/09/2026).
         if (scope == CouncilScope.Trial) return Arrests.ArrestBehavior.Instance?.TrialBench() ?? new List<Hero>();

         if (scope == CouncilScope.Parley) return CouncilRosterResolver.Seated(scope, parleyTargetFactionId);

         return Resolve(scope, DefaultThresholdDays, parleyTargetFactionId)
               .Where(s => s.Attendance == CouncilAttendance.InPerson)
               .Select(s => s.RepresentedHero)
               .Where(h => h != null)
               .Distinct()
               .ToList();
      }

      /// <summary>
      ///   Debug/self-test seam: projects ONE real hero's facts (see <see cref="BuildFacts" />) and runs the pure
      ///   policy, returning the RAW result INCLUDING Absent (<see cref="Resolve" /> filters Absent out of the
      ///   production seated list). Lets a self-test assert a specific candidate's verdict and reason, a live
      ///   prisoner's for instance, without needing a whole scope's pool to happen to line up.
      /// </summary>
      public static CouncilAttendanceResult DebugDecideFor(Hero hero, CouncilScope scope, double thresholdDays, string parleyTargetFactionId = null)
      {
         if (hero == null) return new CouncilAttendanceResult(CouncilAttendance.Absent, CouncilAttendanceReason.OtherFactionNotInvited);

         return CouncilAttendancePolicy.Decide(BuildFacts(hero, Clan.PlayerClan, scope, parleyTargetFactionId), thresholdDays);
      }

      #region private

      private static void TryAppendSeat(List<CouncilAttendanceSeat> seats, Hero hero, Clan playerClan, CouncilScope scope,
                                        double thresholdDays, string parleyTargetFactionId)
      {
         try
         {
            CouncilAttendanceFacts facts = BuildFacts(hero, playerClan, scope, parleyTargetFactionId);
            CouncilAttendanceResult verdict = CouncilAttendancePolicy.Decide(facts, thresholdDays);

            if (verdict.Attendance == CouncilAttendance.Absent) return;

            seats.Add(new CouncilAttendanceSeat {
               RepresentedHero = hero,
               RepresentedFaction = hero.MapFaction,
               Attendance = verdict.Attendance,
               Reason = verdict.Reason
            });
         }
         catch (Exception ex)
         {
            ModLog.Error($"[COUNCIL] attendance projection failed for {hero?.Name}: {ex.GetType().Name}: {ex.Message}");
         }
      }

      // Reads one candidate's live state into the engine-free facts the pure policy judges. Every branch here
      // mirrors CouncilRosterResolver.FactsFor's / AssemblyInvitationFactsBuilder.Build's own reads, so the two
      // systems never quietly disagree on what "in your party" or "at war" means.
      private static CouncilAttendanceFacts BuildFacts(Hero hero, Clan playerClan, CouncilScope scope, string parleyTargetFactionId)
      {
         bool isInPlayerParty = hero.PartyBelongedTo == MobileParty.MainParty;
         bool isWithPlayerArmy = !isInPlayerParty && hero.PartyBelongedTo?.Army != null
                                 && MobileParty.MainParty?.Army != null
                                 && hero.PartyBelongedTo.Army == MobileParty.MainParty.Army;

         bool isParleyTarget = scope == CouncilScope.Parley
                               && ParleyFactionLookup.ByStringId(parleyTargetFactionId)?.Leader == hero;

         NpcProfile profile = NpcMemoryCampaignBehavior.Instance?.Store?.Get(hero.StringId);

         return new CouncilAttendanceFacts {
            // A DISABLED hero (taken out of the world: not yet in play, or set aside by a mod such as a custom family's
            // late parent) is as absent from the table as the dead, and has no whereabouts either.
            IsDead = !hero.IsAlive || hero.HeroState == Hero.CharacterStates.Disabled,
            IsPrisoner = hero.IsPrisoner,
            IsInPlayerParty = isInPlayerParty,
            IsPlayerFaction = IsPlayerFactionMember(hero, playerClan),
            IsParleyTarget = isParleyTarget,
            // Only actually consulted for own-faction candidates (CouncilAttendancePolicy.Decide), but the
            // player's own party and army travel together, so both read as "already here" (Gabriel's brief).
            EtaDays = isInPlayerParty || isWithPlayerArmy ? 0.0 : EstimateEtaDays(hero),
            RelationToPlayer = profile?.ReputationWithPlayer ?? 0,
            AtWarWithPlayer = hero.MapFaction != null && Hero.MainHero?.MapFaction != null
                              && FactionManager.IsAtWarAgainstFaction(hero.MapFaction, Hero.MainHero.MapFaction)
         };
      }

      // True for the player's OWN house: his own clan, or (same reading as CouncilRosterResolver's own
      // isAlliedLeader/WarCouncilCandidates) any other clan sharing the player's kingdom, a kingdom being one
      // faction, so "allied clan of the player's kingdom" and "same kingdom" are the same fact. An independent
      // player clan (no kingdom) reads no OTHER clan as its own faction, matching vanilla having no separate
      // "allied faction" stance below the kingdom itself (see ChatViewModel.Council.PlayerCanDeclareWar's own
      // discovery note).
      private static bool IsPlayerFactionMember(Hero hero, Clan playerClan)
      {
         if (hero?.Clan == null || playerClan == null) return false;
         if (hero.Clan == playerClan) return true;

         return playerClan.Kingdom != null && hero.Clan.Kingdom == playerClan.Kingdom;
      }

      // Turns a raw map distance (candidate to the player's current position, there being no separate "host
      // settlement" for an ordinary council, unlike the ASSEMBLY pillar) into a day estimate via
      // AverageLordPartyMapSpeedPerHour. A candidate with no locatable position (no party, no settlement) is
      // treated as distance 0, mirroring AssemblyInvitationFactsBuilder.DistanceToHost's own fallback: an
      // unlocatable hero is not punished on distance, regard decides instead.
      private static double EstimateEtaDays(Hero hero)
      {
         Vec2? from = hero.PartyBelongedTo?.GetPosition2D ?? hero.CurrentSettlement?.GetPosition2D;
         if (from == null) return 0.0;

         Vec2? to = MobileParty.MainParty?.GetPosition2D ?? Hero.MainHero?.CurrentSettlement?.GetPosition2D;
         if (to == null) return 0.0;

         float distance = from.Value.Distance(to.Value);

         return distance / (AverageLordPartyMapSpeedPerHour * HoursPerDay);
      }

      #endregion
   }
}
