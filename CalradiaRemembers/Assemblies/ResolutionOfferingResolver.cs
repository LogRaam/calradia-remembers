// Code written by Gabriel Mailhot, 03/08/2026.
// ASSEMBLY pillar, Council module: the engine side of ResolutionWorldFacts. Reads live campaign state (a vacant
// governable fief, whether any seated member is available or rides in the player's own party) into the facts
// ResolutionEligibilityPolicy judges, so the set of resolution kinds a council turn TEACHES the LLM can never
// promise a deed the lift cannot honour (the exact empty-promise class the whole catalogue exists to prevent).
// This is the populator ResolutionCatalog and ResolutionEligibilityPolicy were built for; appoint_governor,
// assign_party_role, rejoin_party, mirroring assign_party_role's own TargetInPlayerParty fact exactly (no
// dedicated plumbing needed here, see ResolutionCatalog's DispatchOnMission entry) dispatch_mission, and, the
// fifth executor and the first that touches persisted save state, grant_stipend (its own PlayerCanAffordStipend
// fact, read from the player's live gold) are wired today (ResolutionCatalog.IsExecutable), so the remaining
// scoped kinds never surface here regardless of facts, and this reader stays minimal until they land.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.Assemblies;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

#endregion

namespace CalradiaRemembers.Assemblies
{
   /// <summary>Reads live campaign state into the OFFERED executable resolution kind keys for a council turn.</summary>
   public static class ResolutionOfferingResolver
   {
      /// <summary>
      ///   The kind keys (e.g. "quest", "appoint_governor", "assign_party_role", "rejoin_party") a council of
      ///   <paramref name="scope" /> may currently teach the LLM. <paramref name="anySeatedMemberAvailable" />
      ///   should be true once the council has at least one filled seat: the roster already applies
      ///   CouncilRosterPolicy's own availability gate before a member is seated (see CouncilRosterResolver.Seated),
      ///   so a seated member IS the TargetAvailable case ResolutionEligibilityPolicy asks about, and there is no
      ///   separate per-member re-check to do here. <paramref name="anySeatedMemberInPlayerParty" /> is the
      ///   narrower TargetInPlayerParty fact assign_party_role needs: a seat alone does not prove party
      ///   membership (a companion may be seated while governing a town), so the caller resolves each seat's live
      ///   Hero and checks PartyBelongedTo for real (see ChatViewModel.AnySeatedMemberInPlayerParty).
      ///   <paramref name="anySeatedCompanionOutOfPlayerParty" /> is the symmetric TargetOutOfPlayerParty fact
      ///   rejoin_party needs, closing bug d (a clan companion seated at the table while away is otherwise
      ///   unreachable by any executor, see ChatViewModel.AnySeatedCompanionOutOfPlayerParty). <paramref name="playerCanAffordStipend" />
      ///   is the fifth executor's own fact, grant_stipend (see ChatViewModel.PlayerCanAffordStipend): the player's
      ///   gold checked against StipendPolicy.MinTrancheCost, the cheapest possible tranche, since the real
      ///   proposed amount is not known until the LLM names one. <paramref name="playerCanDeclareWar" /> is the
      ///   WarCouncil's own fact, declare_war (see ChatViewModel.PlayerCanDeclareWar): the player leads a faction
      ///   AND at least one valid war target exists; the actual faction named is re-resolved and re-checked at
      ///   the lift regardless. <paramref name="playerCanMakePeace" /> is the Parley's own fact, make_peace (see
      ///   ChatViewModel.PlayerCanMakePeace): the player leads a faction AND it is currently at war with the
      ///   specific enemy the parley was convened against. <paramref name="anySeatedMemberHasGold" /> is Partie
      ///   1's own fact, give_gold (see ChatViewModel.AnySeatedMemberHasGold): at least one seated member
      ///   (ClanLords or WarCouncil only, see ResolutionCatalog's two GiveGold entries) currently holds gold of
      ///   their own; the real proposed amount is unknown at offer time and is re-clamped against the giver's
      ///   ACTUAL gold at the lift (CouncilLift.SettleGiveGold, GoldPledgePolicy.ClampPayable). <paramref name="anySeatedMemberCanGrantInfluence" />
      ///   is Partie 1's other own fact, give_influence (see ChatViewModel.AnySeatedMemberCanGrantInfluence): at
      ///   least one seated ally (WarCouncil only, see ResolutionCatalog's single GiveInfluence entry) has a
      ///   clan that could currently spare some influence under the reused 1:1 give_influence gates; the real
      ///   amount is NEVER an LLM choice (unlike give_gold's target_amount) and is computed fresh at the lift by
      ///   CourtActionPolicy.InfluenceGrant, re-checked against CanAffordInfluenceGrant (CouncilLift.SettleGiveInfluence).
      ///   Never throws: any
      ///   engine read failure degrades to
      ///   no vacant fief, so a failing lookup only WITHHOLDS a motion, never over-offers one the lift cannot honour.
      ///   <paramref name="anySeatedVassalOwnsFief" /> is Partie 2's own fact, revoke_fief (see
      ///   ChatViewModel.AnySeatedVassalOwnsFief): at least one seated WarCouncil vassal's own clan currently
      ///   owns a town or castle that could be stripped back to the crown; the real fief named is re-resolved
      ///   and re-checked at the lift regardless (CouncilLift.SettleRevokeFief, bridge is law).
      ///   <see cref="PlayerIsKingdomLeader" /> and <see cref="HasGiveableFief" /> are read directly here (no
      ///   roster is needed for either), grant_fief's and both kinds' own shared sovereignty fact.
      ///   <paramref name="anySeatedCompanionExpellable" /> is Partie 8's own fact, expel_from_clan (see
      ///   ChatViewModel.AnySeatedCompanionExpellable): at least one seated companion is currently expellable
      ///   per ExpulsionPolicy.CanExpel (player leads their own clan, the seat is a live companion, not the
      ///   player's own spouse); the same gate is re-checked at the lift regardless (CouncilLift.SettleExpelFromClan,
      ///   bridge is law). <paramref name="anySeatedMemberCanPledgeAgainst" /> is Partie 1's schemes kind's own
      ///   fact, pledge_against (see ChatViewModel.AnySeatedMemberCanPledgeAgainst): at least one seated member
      ///   (ClanLords or Family only, see ResolutionCatalog's two PledgeAgainst entries) currently holds no
      ///   pledge already outstanding. An APPROXIMATION, mirroring anySeatedMemberHasGold's own loose gate:
      ///   proving a genuine RIVAL also exists is left to the lift (CourtActionResolver.ResolvePledgeTarget,
      ///   CouncilLift.SettlePledgeAgainst, bridge is law). <paramref name="anySeatedAllyHasMarriageableKin" /> is
      ///   Partie 1's own marriage-as-currency fact, arrange_marriage (see ChatViewModel.AnySeatedAllyHasMarriageableKin):
      ///   WarCouncil only, the player's own clan holds an unwed kin AND at least one seated ally's own house
      ///   (a genuine other clan) offers one too. An APPROXIMATION like the others above: the exact pairing, hard
      ///   eligibility, and family consent are all re-validated at the lift regardless
      ///   (CalradiaRemembers.GameState.CourtActionResolver.TryArrangeMarriage, bridge is law).
      ///   <paramref name="anySeatedTributePayerAvailable" /> is Partie 8's own fact, tribute (see
      ///   ChatViewModel.AnySeatedTributePayerAvailable): Parley-only, the seated counterpart (the parley's own
      ///   enemy leader) is alive and leads a clan distinct from the player's own. An APPROXIMATION, mirroring
      ///   anySeatedMemberHasGold's own loose gate: proves only that SOME payer could plausibly pay, never the
      ///   real proposed amount or whether their live gold covers even one day of it; both are re-checked fresh
      ///   at the lift and at every daily tick regardless (CouncilLift.SettleTribute, CouncilTributeBehavior.Advance).
      ///   Partie 8's OTHER own kind, swap_fiefs (CouncilLift.SettleSwapFiefs), needs NO new parameter or plumbing
      ///   here at all: its own fact is the literal AND of grant_fief's PlayerIsKingdomLeader/HasGiveableFief and
      ///   revoke_fief's SeatedVassalOwnsFief, already fully read above via <paramref name="anySeatedVassalOwnsFief" />,
      ///   <see cref="PlayerIsKingdomLeader" />, and <see cref="HasGiveableFief" />; <see cref="ResolutionEligibilityPolicy.Meets" />
      ///   composes the three existing flags for it, so this reader stays untouched by that kind's own addition.
      ///   <paramref name="anySeatedPrisonerAvailableToRelease" /> rounds out the Parley toolkit (make_peace +
      ///   tribute + release_prisoner, 2026-08-04): Parley-only, the player currently holds a hero prisoner of
      ///   the SAME faction the parley was convened against (see ChatViewModel.AnySeatedPrisonerAvailableToRelease).
      ///   An APPROXIMATION, mirroring anySeatedTributePayerAvailable's own loose gate: proves only that SOME
      ///   real concession exists, never the exact captive the LLM will actually name; the named captive is
      ///   re-resolved and re-checked against the SAME faction at the lift regardless (CouncilLift.SettleReleasePrisoner,
      ///   bridge is law). <paramref name="anySeatedMemberCanLendTroops" /> is Partie 1's own fact, lend_troops
      ///   (COUNCIL_ACTIONS.md, Gabriel's own CONDITIONS; see ChatViewModel.AnySeatedMemberCanLendTroops):
      ///   ClanLords/WarCouncil only (mirroring anySeatedMemberHasGold's own two scopes), at least one seated
      ///   lord currently leads a party with troops to spare and is not at war with the player; the real tier
      ///   split (CouncilTroopLoanPolicy.ComputeLoan) is computed fresh at the lift from the donor's LIVE roster
      ///   regardless (CouncilLift.SettleLendTroops, bridge is law).
      /// </summary>
      public static IReadOnlyList<string> OfferedKindKeys(CouncilScope scope, bool anySeatedMemberAvailable, bool anySeatedMemberInPlayerParty = false, bool anySeatedCompanionOutOfPlayerParty = false, bool playerCanAffordStipend = false, bool playerCanDeclareWar = false, bool playerCanMakePeace = false, bool anySeatedMemberHasGold = false, bool anySeatedMemberCanGrantInfluence = false, bool anySeatedVassalOwnsFief = false, bool anySeatedCompanionExpellable = false, bool anySeatedMemberCanPledgeAgainst = false, bool anySeatedAllyHasMarriageableKin = false, bool anySeatedTributePayerAvailable = false, bool anySeatedPrisonerAvailableToRelease = false, bool anySeatedMemberCanLendTroops = false)
      {
         try
         {
            var facts = new ResolutionWorldFacts(
               targetInPlayerParty: anySeatedMemberInPlayerParty,
               targetAvailable: anySeatedMemberAvailable,
               playerHasVacantFief: HasVacantGovernableFief(),
               playerCanAffordStipend: playerCanAffordStipend,
               missionAvailable: false,
               targetOutOfPlayerParty: anySeatedCompanionOutOfPlayerParty,
               playerCanDeclareWar: playerCanDeclareWar,
               playerCanMakePeace: playerCanMakePeace,
               memberHasGold: anySeatedMemberHasGold,
               memberCanGrantInfluence: anySeatedMemberCanGrantInfluence,
               playerIsKingdomLeader: PlayerIsKingdomLeader(),
               playerHasGiveableFief: HasGiveableFief(),
               seatedVassalOwnsFief: anySeatedVassalOwnsFief,
               anySeatedCompanionExpellable: anySeatedCompanionExpellable,
               anySeatedMemberCanPledgeAgainst: anySeatedMemberCanPledgeAgainst,
               anySeatedAllyHasMarriageableKin: anySeatedAllyHasMarriageableKin,
               anySeatedTributePayerAvailable: anySeatedTributePayerAvailable,
               anySeatedPrisonerAvailableToRelease: anySeatedPrisonerAvailableToRelease,
               anySeatedMemberCanLendTroops: anySeatedMemberCanLendTroops);

            return ResolutionEligibilityPolicy.OfferedExecutableKeysForScope(scope, facts).ToList();
         }
         catch (Exception ex)
         {
            ModLog.Error($"[COUNCIL] ResolutionOfferingResolver.OfferedKindKeys failed: {ex.GetType().Name}: {ex.Message}");

            return Array.Empty<string>();
         }
      }

      // A town or castle the player's clan still owns, with no governor seated. Villages carry no Governor slot
      // at all (CouncilLift.SettleAppointGovernor's own re-check at the lift already excludes them the same
      // way), so restricting to IsTown/IsCastle here keeps the offering and the lift's own rule in lockstep.
      private static bool HasVacantGovernableFief()
      {
         Clan clan = Clan.PlayerClan;
         if (clan == null) return false;

         return Settlement.All != null && Settlement.All.Any(s =>
            (s.IsTown || s.IsCastle) && s.OwnerClan == clan && s.Town?.Governor == null);
      }

      // grant_fief's/revoke_fief's shared sovereignty gate (Partie 2, COUNCIL_ACTIONS.md's Kimi review): the
      // player must actually RULE a kingdom, not merely belong to one or lead an independent clan. Without this,
      // WarCouncil's own no-kingdom fallback (CouncilRosterResolver.WarCouncilCandidates seats the player's OWN
      // clan lords when there is no kingdom) would let a fief "move" within the player's own clan, mechanically vacant.
      private static bool PlayerIsKingdomLeader()
      {
         Clan clan = Clan.PlayerClan;

         return clan?.Kingdom != null && clan.Kingdom.Leader == Hero.MainHero;
      }

      // A town or castle the player's own clan currently owns, regardless of governor: grant_fief's own fact.
      // Unlike HasVacantGovernableFief (appoint_governor needs an UNGOVERNED fief), a crown fief may already
      // have a governor and still be perfectly giveable; SettleGrantFief itself releases that governor before
      // the transfer, mirroring the rejoin_party governor-release pattern.
      private static bool HasGiveableFief()
      {
         Clan clan = Clan.PlayerClan;
         if (clan == null) return false;

         return Settlement.All != null && Settlement.All.Any(s => (s.IsTown || s.IsCastle) && s.OwnerClan == clan);
      }
   }
}
