// Code written by Gabriel Mailhot, 06/08/2026.
// v1.34.2 resilience: log-only observability for the 1:1 action bridge (audit Pattern 1, downgraded to
// log-only). The bridge executes any parsed [ACTION] token without checking it was actually TAUGHT this turn.
// The full ENFORCING whitelist is deferred (real regression risk: the taught set must exactly mirror the
// teaching gates, or it refuses legitimate actions). This pure set instead lets the host OBSERVE and log an
// execution that was never taught, changing no behaviour.

#region

using System;
using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic.Actions
{
   /// <summary>
   ///   The eligibility facts that decide which conditional 1:1 [ACTION] verbs a single conversation turn
   ///   teaches. A plain, engine-free mirror of the flags the SDK <c>EncounterContext</c> carries and the
   ///   <c>PromptBuilder</c> gates each verb's teaching on: the host maps its live <c>EncounterContext</c>
   ///   onto this struct so <see cref="TaughtActionSet" /> stays pure and testable. Every field defaults to
   ///   false / not-offered, which yields only the always-on base set, exactly as an ordinary turn that
   ///   teaches nothing extra would.
   /// </summary>
   public struct TaughtActionFacts
   {
      /// <summary>The player is THIS NPC's captive (PlayerStatus == Captive): give_item and the court/scheme/divorce verbs are suppressed.</summary>
      public bool PlayerIsCaptive;

      /// <summary>The player HOLDS this NPC captive (PlayerStatus == NpcIsCaptive): the free_prisoner / execute_prisoner / turn_nemesis bargain is taught.</summary>
      public bool PlayerHoldsThisNpcCaptive;

      // ── personal-command batch (2026-08-05) ──
      public bool NpcCanBeAppointedGovernor;
      public bool NpcCanBeAssignedPartyRole;
      public bool NpcCanRejoinParty;
      public bool NpcCanBeDispatchedOnMission;
      public bool NpcCanBeGrantedStipend;
      public bool NpcCanSwearOath;

      // ── suzerain-command batch (2026-08-05) ──
      public bool NpcCanBeGrantedFief;
      public bool NpcCanHaveFiefRevoked;
      public bool NpcCanBeExpelledFromClan;

      // ── recruitment / service ──
      /// <summary>CompanionAskingPrice has a value: the NPC is a hireable wanderer, join_party is taught.</summary>
      public bool CompanionRecruitable;

      /// <summary>MercenaryOfferKingdom is set: join_as_mercenary is taught.</summary>
      public bool MercenaryOffered;

      /// <summary>PlayerIsMercenary: end_mercenary is taught.</summary>
      public bool PlayerIsMercenary;

      /// <summary>VassalOfferKingdom is set: join_as_vassal is taught.</summary>
      public bool VassalOffered;

      public bool NpcCanEscortPlayer;
      public bool NpcIsEscortingPlayer;

      /// <summary>NpcCanRideWithPlayer is set: the idle-lord retainer offer, ride_with_me is taught.</summary>
      public bool NpcCanRideWithPlayer;

      /// <summary>NpcIsRidingWithPlayer is set: this NPC currently rides as a retainer, part_ways is taught.</summary>
      public bool NpcIsRidingWithPlayer;

      public bool LordRecruitEligible;
      public bool CompanionOnErrand;

      // ── romance ──
      public bool ConsortEligible;
      public bool SecretLoverEligible;
      public bool LoveMatchEligible;

      /// <summary>Both matchmaker kin lists (MatchmakerNpcKin and MatchmakerPlayerKin) are present: arrange_marriage is taught.</summary>
      public bool MatchmakerAvailable;

      // ── duel ──
      public bool DuelChallengeAvailable;

      // ── prisoners ──
      /// <summary>HeldLordPrisoners is non-empty: the player holds a lord the NPC could ask handed over, so give_prisoner / deliver_prisoner are taught.</summary>
      public bool PlayerHoldsLordPrisoners;

      // ── captive-scene captor verbs ──
      public bool CaptorAllowsPersuasionRelease;
      public bool CaptorMayExecutePlayer;

      // ── schemes ──
      /// <summary>SchemeAgentTargetName is set: scheme_assist is taught.</summary>
      public bool SchemeAgentActive;

      /// <summary>SchemeTargetsThisNpc: scheme_heed is taught.</summary>
      public bool SchemeTargetsThisNpc;

      /// <summary>A standing pledge rival was resolved (pledge_against block was composed into ExtraActionTeachings).</summary>
      public bool PledgeAgainstAvailable;

      // ── divorce notes (host-composed, rendered verbatim by the SDK) ──
      /// <summary>SpouseDivorceDemandNote is set: accept_divorce / decline_divorce are taught.</summary>
      public bool SpouseDivorceDemandActive;

      /// <summary>PlayerEndOwnMarriageNote is set: end_own_marriage is taught.</summary>
      public bool PlayerEndOwnMarriageOffered;

      /// <summary>SpouseEstrangementNote is set: accept_divorce is taught.</summary>
      public bool SpouseEstrangementActive;

      // ── court-verb extension surface ──
      /// <summary>
      ///   ExtraActionTeachings is non-empty this turn. The host composes the court-verb family
      ///   (give_influence, lend_troops, give_troops, spend_influence, sway_opinion, release_prisoner,
      ///   buy_prisoner, sell_prisoner, end_marriage, make_amends, grudge_terms) into that single string, each
      ///   verb self-gated on its own live eligibility. For a log-only audit the presence of ANY of them is
      ///   read as teaching the WHOLE family, so a legitimately-taught court verb is never flagged: this is
      ///   deliberately coarse (it cannot see which exact subset was composed), which only ever makes the audit
      ///   quieter, never louder, matching the "when in doubt, log less" rule.
      /// </summary>
      public bool CourtVerbsOffered;
   }

   /// <summary>
   ///   BEST-EFFORT, LOG-ONLY. Returns the set of 1:1 [ACTION] type keys taught on a single ordinary
   ///   (non-council) conversation turn, so the host can OBSERVE, without refusing, an [ACTION] that executed
   ///   yet was never taught (audit Pattern 1, v1.34.2). Not an enforcement whitelist: it is intentionally
   ///   generous (any plausible teaching signal includes the verb) so the audit flags only genuinely-untaught
   ///   executions and never spuriously accuses a legitimate one. The mapping mirrors the SDK PromptBuilder's
   ///   own gates as of 2026-08-06; a gate the mapping does not yet cover simply risks a benign false-positive
   ///   log line, never a changed behaviour. The council [RESOLUTION] path has its OWN offering set
   ///   (CouncilOfferedResolutionKinds) and is out of scope here.
   /// </summary>
   public static class TaughtActionSet
   {
      /// <summary>
      ///   The always-on base actions the SDK teaches every ordinary 1:1 turn (LlmServices.BuildActionVocabulary):
      ///   change_relation, give_gold, take_gold, plus the conversation-flow controls end_conversation and
      ///   witness_leaves. request_privacy is added here too: it is taught whenever witnesses are present and,
      ///   like the other flow controls, is never a world-changing deed worth auditing.
      /// </summary>
      private static readonly string[] BaseActions =
      {
         "change_relation",
         "give_gold",
         "take_gold",
         "request_privacy",
         "end_conversation",
         "witness_leaves"
      };

      /// <summary>
      ///   The court-verb family composed into EncounterContext.ExtraActionTeachings, taught as a bundle when
      ///   <see cref="TaughtActionFacts.CourtVerbsOffered" /> is set (see that field's note on why this is coarse).
      /// </summary>
      private static readonly string[] CourtVerbs =
      {
         "give_influence",
         "lend_troops",
         "give_troops",
         "spend_influence",
         "sway_opinion",
         "release_prisoner",
         "buy_prisoner",
         "sell_prisoner",
         "end_marriage",
         "make_amends",
         "grudge_terms",
         "pledge_against"
      };

      /// <summary>
      ///   The set of action-type keys taught this turn (case-insensitive), given the turn's eligibility facts.
      ///   Always contains <see cref="BaseActions" />; each conditional verb is added only when its gating fact
      ///   is set, mirroring the SDK's per-verb teaching gates.
      /// </summary>
      public static ISet<string> ForTurn(TaughtActionFacts facts)
      {
         var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
         foreach (string a in BaseActions) set.Add(a);

         // give_item is taught in every ordinary turn where the player is not the captive (a prisoner has no
         // access to their inventory), see PromptBuilder.AppendGiveItem.
         if (!facts.PlayerIsCaptive) set.Add("give_item");

         // Personal-command batch.
         if (facts.NpcCanBeAppointedGovernor) set.Add("appoint_governor");
         if (facts.NpcCanBeAssignedPartyRole) set.Add("assign_party_role");
         if (facts.NpcCanRejoinParty) set.Add("rejoin_party");
         if (facts.NpcCanBeDispatchedOnMission)
         {
            set.Add("dispatch_mission");
            set.Add("gather_news"); // folded into dispatch_mission (2026-08-05); a legacy token still maps here.
         }
         if (facts.NpcCanBeGrantedStipend) set.Add("grant_stipend");
         if (facts.NpcCanSwearOath) set.Add("swear_oath");

         // Suzerain-command batch.
         if (facts.NpcCanBeGrantedFief) set.Add("grant_fief");
         if (facts.NpcCanHaveFiefRevoked) set.Add("revoke_fief");
         if (facts.NpcCanBeExpelledFromClan) set.Add("expel_from_clan");

         // Recruitment / service.
         if (facts.CompanionRecruitable) set.Add("join_party");
         if (facts.MercenaryOffered) set.Add("join_as_mercenary");
         if (facts.PlayerIsMercenary) set.Add("end_mercenary");
         if (facts.VassalOffered) set.Add("join_as_vassal");
         if (facts.NpcCanEscortPlayer) set.Add("follow_me");
         if (facts.NpcIsEscortingPlayer) set.Add("dismiss_escort");
         if (facts.NpcCanRideWithPlayer) set.Add("ride_with_me");
         if (facts.NpcIsRidingWithPlayer) set.Add("part_ways");
         if (facts.LordRecruitEligible) set.Add("join_clan");
         if (facts.CompanionOnErrand) set.Add("recall_companion");

         // Romance.
         if (facts.ConsortEligible) set.Add("take_as_consort");
         if (facts.SecretLoverEligible) set.Add("take_as_secret_lover");
         if (facts.LoveMatchEligible) set.Add("marry");
         if (facts.MatchmakerAvailable) set.Add("arrange_marriage");

         // Duel (the challenge is resolved on a dedicated path before the ordinary loop, but include it so a
         // stray token never reads as untaught when the venue was genuinely offered).
         if (facts.DuelChallengeAvailable) set.Add("challenge_duel");

         // Prisoners the player holds (give the matching captive over).
         if (facts.PlayerHoldsLordPrisoners)
         {
            set.Add("give_prisoner");
            set.Add("deliver_prisoner");
         }

         // Player holds THIS npc captive: the freedom bargain and its grim alternatives.
         if (facts.PlayerHoldsThisNpcCaptive)
         {
            set.Add("free_prisoner");
            set.Add("execute_prisoner");
            set.Add("turn_nemesis");
         }

         // Captive-scene captor verbs (also resolved on dedicated paths; kept for completeness).
         if (facts.CaptorAllowsPersuasionRelease) set.Add("release_player");
         if (facts.CaptorMayExecutePlayer) set.Add("execute_player");

         // Schemes.
         if (facts.SchemeAgentActive) set.Add("scheme_assist");
         if (facts.SchemeTargetsThisNpc) set.Add("scheme_heed");
         if (facts.PledgeAgainstAvailable) set.Add("pledge_against");

         // Divorce notes (host-composed directives rendered verbatim).
         if (facts.SpouseDivorceDemandActive)
         {
            set.Add("accept_divorce");
            set.Add("decline_divorce");
         }
         if (facts.PlayerEndOwnMarriageOffered) set.Add("end_own_marriage");
         if (facts.SpouseEstrangementActive) set.Add("accept_divorce");

         // Court-verb extension surface (bundle: see CourtVerbsOffered note).
         if (facts.CourtVerbsOffered)
            foreach (string v in CourtVerbs)
               set.Add(v);

         return set;
      }

      /// <summary>
      ///   True when <paramref name="actionType" /> is a conversation-flow control (privacy / end / a companion
      ///   leaving) or the always-on base regard/gold verbs, i.e. one the log-only audit must NEVER report on:
      ///   these are taught every ordinary turn and are not world-changing deeds, so a line for them would be
      ///   pure noise. The host uses this to suppress the audit for the base/flow family regardless.
      /// </summary>
      public static bool IsNeverAudited(string actionType)
      {
         if (string.IsNullOrEmpty(actionType)) return true;

         return string.Equals(actionType, "change_relation", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionType, "give_gold", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionType, "take_gold", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionType, "request_privacy", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionType, "end_conversation", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionType, "witness_leaves", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionType, "retire", StringComparison.OrdinalIgnoreCase);
      }
   }
}
