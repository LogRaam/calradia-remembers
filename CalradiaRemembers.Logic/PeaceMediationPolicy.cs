// Code written by Gabriel Mailhot, 06/08/2026.
// mediate_peace (third-party war mediation): the pure gate deciding whether the player, acting as a MEDIATOR in
// a 1:1 conversation with a FACTION LEADER, may broker peace between that leader's faction and a NAMED enemy
// faction. This is the NEW third-party diplomacy verb: today make_peace exists only as a council Parley motion
// for the player's OWN faction, so there was no way to broker peace between OTHER realms in conversation. In v1
// only the talked-to leader consents; the enemy's assent is abstracted (the mediator brokered acceptable terms).
//
// It is consulted at BOTH surfaces around the LLM ("the prompt is advice, the bridge is law"): first whether
// mediate_peace is even taught to the model (EncounterContextBuilder), then again whether the action really
// executes (BannerlordGameStateBridge.ExecuteMediatePeace). Four gates, ordered most-absolute-first so the
// reason surfaced is the truest one:
//   1. FACTION LEADER: only a king/ruler can commit their whole realm to peace. A common lord or companion
//      speaks for no faction, so there is nothing to mediate whatever the player's standing.
//   2. A WAR TO END: the leader's faction must actually be at war with the target. No war, nothing to broker.
//   3. STATURE: one does not mediate between realms as a nobody. The player must carry genuine noble weight
//      (clan tier), gauged by EncounterContext.PlayerClanTier's own doc: "tier 3+ represents genuine nobility
//      with a fief". A landless upstart is not heeded when they presume to broker a peace between kingdoms.
//   4. REGARD: the leader must genuinely TRUST the player to heed their mediation. Gauged on the mod's own
//      NpcProfile.ReputationWithPlayer ledger (fall back to vanilla relation when no profile), floored at the
//      "deep trust" tier (>= 30) that ContextualNameResolver/DescribeRelation already treat as deep trust,
//      NOT the mere "genuine regard" (>= 10) a passing friend earns: brokering a realm's peace is a weighty
//      ask, so a leader heeds only someone they hold in real, tested esteem.
// NO gold cost: mediation is prestige and trust, not a purchase.

#region

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>Why the player cannot (yet) mediate peace with this leader, or <see cref="None" /> when they can.</summary>
   public enum PeaceMediationBlock
   {
      /// <summary>All gates pass: the player may broker peace between the leader's faction and the target.</summary>
      None,

      /// <summary>The conversation NPC is not a faction leader, so they speak for no realm that could make peace.</summary>
      NpcNotFactionLeader,

      /// <summary>The leader's faction is not at war with the target, so there is no war to mediate.</summary>
      NoWarToMediate,

      /// <summary>The player's clan standing is below the stature floor: a nobody is not heeded as a mediator between realms.</summary>
      StatureTooLow,

      /// <summary>The leader does not hold the player in deep enough regard to heed their mediation.</summary>
      RegardTooLow
   }

   /// <summary>
   ///   The pure gate behind mediate_peace. Pure and unit-tested so the teaching gate and the executor share one
   ///   decision and cannot drift. See the file header for the design and the thresholds' rationale.
   /// </summary>
   public static class PeaceMediationPolicy
   {
      /// <summary>
      ///   TUNE: the player's minimum clan tier (0-6) before a faction leader will heed them as a mediator between
      ///   realms. Set at 3, the mark EncounterContext.PlayerClanTier's own doc names as "genuine nobility with a
      ///   fief": below it the player is a landless upstart with no standing to broker a peace between kingdoms.
      /// </summary>
      public const int StatureTierFloor = Stature.LandedNobleTier;

      /// <summary>
      ///   TUNE: the personal regard (<c>NpcProfile.ReputationWithPlayer</c>, clamped [-100,100]) a faction leader
      ///   must hold for the player before heeding their mediation. Set at the "deep trust" tier (30) that
      ///   ContextualNameResolver/DescribeRelation already treat as deep trust, well above the mere "genuine
      ///   regard" (10) a passing friend earns: committing a whole realm to peace on a third party's word is a
      ///   weighty ask, granted only to someone the leader esteems in real, tested trust.
      /// </summary>
      public const int RegardFloor = 30;

      /// <summary>The blocking reason, or <see cref="PeaceMediationBlock.None" /> when the player may mediate.</summary>
      public static PeaceMediationBlock Evaluate(bool npcIsFactionLeader, bool factionAtWarWithTarget, int playerRegardWithLeader, int playerClanTier)
      {
         // Order runs from the most absolute bar to the most earnable, so the reason surfaced is the truest one:
         // a non-leader speaks for no realm whatever the player's standing; with no war there is nothing to
         // mediate however trusted the player is; stature (slow to earn) sits above regard (earnable in chat).
         if (!npcIsFactionLeader) return PeaceMediationBlock.NpcNotFactionLeader;
         if (!factionAtWarWithTarget) return PeaceMediationBlock.NoWarToMediate;
         if (playerClanTier < StatureTierFloor) return PeaceMediationBlock.StatureTooLow;
         if (playerRegardWithLeader < RegardFloor) return PeaceMediationBlock.RegardTooLow;

         return PeaceMediationBlock.None;
      }

      /// <summary>True when every gate passes, the convenience both live call sites use over <see cref="Evaluate" />.</summary>
      public static bool CanMediate(bool npcIsFactionLeader, bool factionAtWarWithTarget, int playerRegardWithLeader, int playerClanTier)
         => Evaluate(npcIsFactionLeader, factionAtWarWithTarget, playerRegardWithLeader, playerClanTier) == PeaceMediationBlock.None;
   }
}
