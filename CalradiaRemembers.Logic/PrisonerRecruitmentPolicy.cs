// Code written by Gabriel Mailhot, 06/08/2026.
// v1.34.3 recruit_prisoner: the pure gate deciding whether a captured HERO the player holds prisoner can be
// persuaded, in conversation, to switch sides and swear into the player's clan. Player report (Nexus): a
// player talked a captured noble into joining them, the LLM narrated the switch, and nothing happened because
// no executor existed for recruiting a held prisoner (join_clan excludes prisoners; turn_nemesis is reserved
// for a tracked bandit nemesis in a captor scene). This is that missing verb's decision logic.
//
// It is consulted at BOTH surfaces around the LLM ("the prompt is advice, the bridge is law"): first whether
// recruit_prisoner is even taught to the model (EncounterContextBuilder), then again whether the action really
// executes (BannerlordGameStateBridge). Three gates, all required:
//   1. HELD BY PLAYER: the hero must genuinely be a prisoner the player (their party or a player-clan
//      settlement) currently holds. Nobody swears into a captor's clan who is not that captor's captive.
//   2. NOT A CLAN LEADER: the same anti-orphan guard join_clan enforces. Reassigning a clan leader's Clan
//      would leave Clan.Leader / Kingdom.Leader dangling on the abandoned house (save corruption).
//   3. STRONG REGARD: persuading a captured ENEMY lord to abandon their own side is the weightiest turn there
//      is, so it requires HIGH personal regard (the mod's own ReputationWithPlayer ledger, clamped [-100,100]).
//      The floor sits at the "deep bond" mark (60), matching LordRecruitment.MinRelation, so a captured enemy
//      who typically starts hostile must be won over through sustained investment, never trivially exploited.

#region

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>Why a held prisoner cannot (yet) be recruited, or <see cref="None" /> when they can.</summary>
   public enum PrisonerRecruitmentBlock
   {
      /// <summary>All gates pass: the held prisoner can be sworn into the player's clan.</summary>
      None,

      /// <summary>The hero is not genuinely a prisoner the player currently holds.</summary>
      NotHeldByPlayer,

      /// <summary>The prisoner leads their own clan: recruiting them would orphan that house (save corruption).</summary>
      IsClanLeader,

      /// <summary>Regard is below the floor: a captured enemy will not switch sides for a captor they do not deeply esteem.</summary>
      RegardTooLow
   }

   /// <summary>
   ///   The pure gate behind recruit_prisoner. Pure and unit-tested so the teaching gate and the executor
   ///   share one decision and cannot drift. See the file header for the design and the player report it closes.
   /// </summary>
   public static class PrisonerRecruitmentPolicy
   {
      /// <summary>
      ///   TUNE: the personal regard (<c>NpcProfile.ReputationWithPlayer</c>, clamped [-100,100]) a held
      ///   prisoner must hold for the player before they will switch sides. Set at the "deep bond" mark,
      ///   matching <see cref="LordRecruitment.MinRelation" />: a captured enemy lord starts hostile, so
      ///   reaching this is a sustained investment, never a quick exploit.
      /// </summary>
      public const int RegardFloor = 60;

      /// <summary>The blocking reason, or <see cref="PrisonerRecruitmentBlock.None" /> when the prisoner can be recruited.</summary>
      public static PrisonerRecruitmentBlock Evaluate(bool heldByPlayer, bool isClanLeader, int regard)
      {
         // Order runs from the most absolute bar to the most temporary, so the reason surfaced is the truest one:
         // a prisoner not even in the player's hands cannot be recruited whatever their regard, and a clan leader
         // never can however deep the bond.
         if (!heldByPlayer) return PrisonerRecruitmentBlock.NotHeldByPlayer;
         if (isClanLeader) return PrisonerRecruitmentBlock.IsClanLeader;
         if (regard < RegardFloor) return PrisonerRecruitmentBlock.RegardTooLow;

         return PrisonerRecruitmentBlock.None;
      }

      /// <summary>True when every gate passes, the convenience both live call sites use over <see cref="Evaluate" />.</summary>
      public static bool CanRecruit(bool heldByPlayer, bool isClanLeader, int regard)
         => Evaluate(heldByPlayer, isClanLeader, regard) == PrisonerRecruitmentBlock.None;
   }
}
