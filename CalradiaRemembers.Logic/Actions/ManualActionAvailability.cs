// Code written by Gabriel Mailhot, 21/08/2026.
// Manual action-injection menu: the availability half. Pure and engine-free, so it is fully unit-testable:
// given a plain snapshot of facts about the CURRENT conversation NPC and the player's own state, decides
// exactly which ManualActionCatalog entries the menu may offer right now. The mod-side resolver
// (ManualActionMenuController) fills a ManualActionFacts from live engine state, reusing the SAME policies
// and EncounterContext gates the Action Interpreter's own 1:1 teaching already computed, never re-deriving a
// gate that exists; the bridge re-validates everything again at injection ("the prompt is advice, the
// bridge is law" applies here exactly as it does to an LLM-proposed action).

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic.Actions
{
   /// <summary>
   ///   Plain facts about the current conversation NPC and the player's own state, gathered from the SAME
   ///   policies and EncounterContext fields the Action Interpreter's teaching gates already compute. No engine
   ///   types: every field is a bool, an int, or a list of plain strings, so this can be constructed and
   ///   asserted against in a pure unit test.
   /// </summary>
   public sealed class ManualActionFacts
   {
      /// <summary>True when the conversation NPC is a hero prisoner the PLAYER themselves currently holds.</summary>
      public bool NpcIsPlayerPrisoner { get; set; }

      /// <summary>Only meaningful with <see cref="NpcIsPlayerPrisoner" />: PrisonerRecruitmentPolicy says this captive can be turned.</summary>
      public bool CanRecruitPrisoner { get; set; }

      /// <summary>True when the conversation NPC rides as a companion of the player's own clan.</summary>
      public bool NpcIsPlayerCompanion { get; set; }

      /// <summary>Only meaningful with <see cref="NpcIsPlayerCompanion" />: they are away on a dispatched errand right now.</summary>
      public bool NpcIsAwayOnErrand { get; set; }

      /// <summary>Only meaningful with <see cref="NpcIsPlayerCompanion" />: their happiness reads Restless or Disgruntled (a live grievance to hear out).</summary>
      public bool CompanionIsDiscontented { get; set; }

      /// <summary>PartyCommandOfferPolicy.CanOfferDispatchMission for this NPC.</summary>
      public bool NpcCanBeDispatchedOnMission { get; set; }

      /// <summary>PartyCommandOfferPolicy.CanOfferAssignRole for this NPC.</summary>
      public bool NpcCanBeAssignedPartyRole { get; set; }

      /// <summary>PartyCommandOfferPolicy.CanOfferRejoinParty for this NPC.</summary>
      public bool NpcCanRejoinParty { get; set; }

      /// <summary>EscortEligibilityPolicy.CanEscort: this lord's own party could begin escorting the player.</summary>
      public bool NpcCanEscortPlayer { get; set; }

      /// <summary>This lord's own party is genuinely escorting the player right now (follow_me already in force).</summary>
      public bool NpcIsEscortingPlayer { get; set; }

      /// <summary>RideWithMeEligibilityPolicy.CanRideWithPlayer: this lord could ride inside the player's own party.</summary>
      public bool NpcCanRideWithPlayer { get; set; }

      /// <summary>
      ///   DuelEligibilityResolver.CanDuelNow: there is a venue and this character may be called out right now.
      ///   Offered so a player who wants to fight a lord does not depend on the model hearing them say it
      ///   (FloatingMammal, 2026-09-10: "I just want to beat up some lords in single combat"). Whether the lord
      ///   ACCEPTS is still DuelAcceptancePolicy's answer, unchanged: this opens the door, it does not walk
      ///   anybody through it.
      /// </summary>
      public bool NpcCanBeChallengedToDuel { get; set; }

      /// <summary>This lord genuinely rides in the player's own party right now (ride_with_me already in force).</summary>
      public bool NpcIsRidingWithPlayer { get; set; }

      /// <summary>FiefTransferPolicy.CanOfferGrant: the player rules a kingdom, this NPC is a vassal of a separate house in it, and a grantable fief exists.</summary>
      public bool NpcCanBeGrantedFief { get; set; }

      /// <summary>Only meaningful with <see cref="NpcCanBeGrantedFief" />: the player-clan crown fiefs on offer.</summary>
      public IReadOnlyList<string> GrantableFiefs { get; set; }

      /// <summary>FiefTransferPolicy.CanOfferRevoke: the player rules a kingdom, this NPC is a vassal of a separate house in it, and holds a revocable fief.</summary>
      public bool NpcCanHaveFiefRevoked { get; set; }

      /// <summary>Only meaningful with <see cref="NpcCanHaveFiefRevoked" />: this NPC's own fiefs that could be stripped.</summary>
      public IReadOnlyList<string> RevocableFiefs { get; set; }

      /// <summary>GovernorAppointmentPolicy.CanOfferAppointment: this NPC (of the player's own clan) could be seated over a vacant fief.</summary>
      public bool NpcCanBeAppointedGovernor { get; set; }

      /// <summary>Only meaningful with <see cref="NpcCanBeAppointedGovernor" />: the player-clan fiefs with no governor.</summary>
      public IReadOnlyList<string> AppointableFiefs { get; set; }

      /// <summary>StipendPolicy.CanOfferStipend: this companion could be put on a recurring daily wage.</summary>
      public bool NpcCanBeGrantedStipend { get; set; }

      /// <summary>CourtActionResolver.TryGiveTroops: this NPC's own party is genuinely under-strength and could accept reinforcement.</summary>
      public bool CanGiveTroops { get; set; }

      /// <summary>CourtActionResolver.TrySpendInfluence: this NPC's own clan is needy enough to accept the player's backing at court.</summary>
      public bool CanSpendInfluence { get; set; }

      /// <summary>PeaceMediationPolicy.CanMediate: this NPC is a faction leader at war, and the player carries enough stature and trust to broker peace.</summary>
      public bool PlayerCanMediatePeace { get; set; }

      /// <summary>Only meaningful with <see cref="PlayerCanMediatePeace" />: the enemy realms this leader could be brought to peace with.</summary>
      public IReadOnlyList<string> MediatablePeaceFactions { get; set; }

      /// <summary>NotableRecruitmentPolicy.Evaluate: this settlement notable could bequeath their post and join the player's clan.</summary>
      public bool CanRecruitNotable { get; set; }

      /// <summary>ResolveCompanionAskingPrice: this free wanderer could be hired, at this asking price (denars); null when not hireable.</summary>
      public int? CompanionAskingPrice { get; set; }

      /// <summary>The player's own party carries at least one item that could be handed over.</summary>
      public bool PlayerHasGiveableItems { get; set; }
   }

   /// <summary>
   ///   Decides, from a <see cref="ManualActionFacts" /> snapshot, exactly which <see cref="ManualActionCatalog" />
   ///   entries the manual menu may offer for THIS conversation right now. Every entry returned still passes
   ///   through the bridge's own re-validation on injection ("the prompt is advice, the bridge is law"): a stale
   ///   or borderline fact can at worst offer an entry the bridge then politely refuses, never apply one it
   ///   should not have.
   /// </summary>
   public static class ManualActionAvailability
   {
      public static IReadOnlyList<ManualActionEntry> GetAvailable(ManualActionFacts facts)
      {
         var result = new List<ManualActionEntry>();

         if (facts == null) return result;

         // Coin & goods: take_gold carries no precondition beyond a live conversation (already guaranteed by
         // the menu only ever opening in one), so it is always offered.
         result.Add(ManualActionCatalog.TakeGold);
         if (facts.PlayerHasGiveableItems) result.Add(ManualActionCatalog.GiveItem);
         if (facts.CanSpendInfluence) result.Add(ManualActionCatalog.SpendInfluence);

         // Prisoner: only ever shown when THIS NPC is a hero prisoner the player themselves holds.
         if (facts.NpcIsPlayerPrisoner)
         {
            result.Add(ManualActionCatalog.ExecutePrisoner);
            result.Add(ManualActionCatalog.FreePrisoner);
            if (facts.CanRecruitPrisoner) result.Add(ManualActionCatalog.RecruitPrisoner);
         }

         // Companion / party.
         if (facts.NpcCanBeDispatchedOnMission) result.Add(ManualActionCatalog.DispatchMission);
         if (facts.NpcCanBeAssignedPartyRole) result.Add(ManualActionCatalog.AssignPartyRole);
         if (facts.NpcIsPlayerCompanion && facts.NpcIsAwayOnErrand) result.Add(ManualActionCatalog.RecallCompanion);
         if (facts.NpcCanRejoinParty) result.Add(ManualActionCatalog.RejoinParty);
         if (facts.NpcCanEscortPlayer) result.Add(ManualActionCatalog.FollowMe);
         if (facts.NpcIsEscortingPlayer) result.Add(ManualActionCatalog.DismissEscort);
         if (facts.NpcCanRideWithPlayer) result.Add(ManualActionCatalog.RideWithMe);
         if (facts.NpcIsRidingWithPlayer) result.Add(ManualActionCatalog.PartWays);
         if (facts.NpcCanBeChallengedToDuel) result.Add(ManualActionCatalog.ChallengeDuel);
         if (facts.NpcIsPlayerCompanion && facts.CompanionIsDiscontented) result.Add(ManualActionCatalog.ReassureCompanion);

         // Governance.
         if (facts.NpcCanBeGrantedFief) result.Add(ManualActionCatalog.GrantFief);
         if (facts.NpcCanHaveFiefRevoked) result.Add(ManualActionCatalog.RevokeFief);
         if (facts.NpcCanBeAppointedGovernor) result.Add(ManualActionCatalog.AppointGovernor);
         if (facts.NpcCanBeGrantedStipend) result.Add(ManualActionCatalog.GrantStipend);
         if (facts.CanGiveTroops) result.Add(ManualActionCatalog.GiveTroops);

         // Diplomacy / recruitment.
         if (facts.PlayerCanMediatePeace) result.Add(ManualActionCatalog.MediatePeace);
         if (facts.CanRecruitNotable) result.Add(ManualActionCatalog.RecruitNotable);
         if (facts.CompanionAskingPrice.HasValue) result.Add(ManualActionCatalog.JoinParty);

         return result;
      }
   }
}
