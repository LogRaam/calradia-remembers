// Code written by Gabriel Mailhot, 11/08/2026.
// recruit_notable: the pure gate deciding whether a settlement NOTABLE (a town gang leader, headman,
// merchant, artisan, rural notable or preacher) can be persuaded, in conversation, to leave their post and
// swear into the player's clan as a COMPANION, once the player has earned enough standing with them.
//
// Design (author-approved): recruiting a notable is a weighty turn, so it is gated on HIGH personal regard,
// and it must never leave a settlement's role vacant: the departing notable BEQUEATHS their gang/role to one
// of their own people (a supporter or another eligible commoner in the SAME settlement) who replaces them.
// The gate therefore refuses when no such successor exists, so the executor never strips a settlement of its
// only notable of that kind. A starting relation bonus is applied to the new companion to represent the
// history and standing the notable brings from their gang.
//
// Like PrisonerRecruitmentPolicy, this decision is consulted at BOTH surfaces around the LLM ("the prompt is
// advice, the bridge is law"): whether recruit_notable is even taught to the model (EncounterContextBuilder),
// then again whether it really executes (BannerlordGameStateBridge). Pure and unit-tested so the two live
// call sites share one decision and can never drift.

#region

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>Why a notable cannot (yet) be recruited as a companion, or <see cref="None" /> when they can.</summary>
   public enum NotableRecruitmentBlock
   {
      /// <summary>All gates pass: the notable can leave their post and swear into the player's clan.</summary>
      None,

      /// <summary>The hero is not a settlement notable, so recruit_notable does not apply (a lord, wanderer or stranger).</summary>
      NotANotable,

      /// <summary>The hero is dead: nothing to recruit.</summary>
      NotAlive,

      /// <summary>The notable is a prisoner (their own recruitment path is recruit_prisoner, not this verb).</summary>
      IsPrisoner,

      /// <summary>The notable already belongs to the player's clan: there is nothing to recruit.</summary>
      AlreadyPlayerClan,

      /// <summary>No supporter or eligible commoner in the settlement can inherit the role, so the post would be left vacant.</summary>
      NoEligibleSuccessor,

      /// <summary>Regard is below the floor: a notable will not abandon their standing for a player they do not deeply esteem.</summary>
      RegardTooLow
   }

   /// <summary>
   ///   The pure gate behind recruit_notable. Pure and unit-tested so the teaching gate and the executor
   ///   share one decision and cannot drift. See the file header for the design and the author calls it honours.
   /// </summary>
   public static class NotableRecruitmentPolicy
   {
      /// <summary>
      ///   TUNE: the personal regard (<c>NpcProfile.ReputationWithPlayer</c>, clamped [-100,100]) a notable
      ///   must hold for the player before they will forsake their post and take service. Set at the "deep
      ///   bond" mark, matching <see cref="LordRecruitment.MinRelation" /> and
      ///   <see cref="PrisonerRecruitmentPolicy.RegardFloor" />: uprooting a gang leader or headman from the
      ///   standing they hold in their town is a sustained investment, never a quick exploit.
      /// </summary>
      public const int RegardFloor = 60;

      /// <summary>
      ///   TUNE: the one-off relation bonus applied between the new companion and the player when a notable is
      ///   recruited, on top of whatever regard was already earned. Represents the history and local standing
      ///   the notable carries over from their gang: they arrive already an ally, not a stranger. Kept modest
      ///   (well below the deep-bond mark) so it colours the start of the companionship without erasing the
      ///   room the relationship still has to grow.
      /// </summary>
      public const int StartingRelationBonus = 20;

      /// <summary>The blocking reason, or <see cref="NotableRecruitmentBlock.None" /> when the notable can be recruited.</summary>
      public static NotableRecruitmentBlock Evaluate(
         bool isNotable, bool isAlive, bool isPrisoner, bool isPlayerClanAlready, int regard, bool hasEligibleSuccessor)
      {
         // Order runs from the most absolute bar (who they are) to the most temporary and earnable (regard), so
         // the reason surfaced is the truest one: someone who is not a notable at all is never told their regard
         // is the problem, and the "earn their deep trust" refusal is the last word only once every structural
         // gate has already passed.
         if (!isNotable) return NotableRecruitmentBlock.NotANotable;
         if (!isAlive) return NotableRecruitmentBlock.NotAlive;
         if (isPrisoner) return NotableRecruitmentBlock.IsPrisoner;
         if (isPlayerClanAlready) return NotableRecruitmentBlock.AlreadyPlayerClan;
         if (!hasEligibleSuccessor) return NotableRecruitmentBlock.NoEligibleSuccessor;
         if (regard < RegardFloor) return NotableRecruitmentBlock.RegardTooLow;

         return NotableRecruitmentBlock.None;
      }

      /// <summary>True when every gate passes, the convenience both live call sites use over <see cref="Evaluate" />.</summary>
      public static bool CanRecruit(
         bool isNotable, bool isAlive, bool isPrisoner, bool isPlayerClanAlready, int regard, bool hasEligibleSuccessor)
         => Evaluate(isNotable, isAlive, isPrisoner, isPlayerClanAlready, regard, hasEligibleSuccessor)
            == NotableRecruitmentBlock.None;
   }
}
