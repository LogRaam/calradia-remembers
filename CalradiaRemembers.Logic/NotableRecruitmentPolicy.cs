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

using NpcMemoryService.Core.Standing;

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
      public const int RegardFloor = Regard.DeepBondTier;

      /// <summary>
      ///   TUNE: the one-off relation bonus applied between the new companion and the player when a notable is
      ///   recruited, on top of whatever regard was already earned. Represents the history and local standing
      ///   the notable carries over from their gang: they arrive already an ally, not a stranger. Kept modest
      ///   (well below the deep-bond mark) so it colours the start of the companionship without erasing the
      ///   room the relationship still has to grow.
      /// </summary>
      public const int StartingRelationBonus = 20;

      /// <summary>The blocking reason, or <see cref="NotableRecruitmentBlock.None" /> when the notable can be recruited.</summary>
      /// <summary>
      ///   What is known about a notable the player would bring into their clan, read off the campaign by the
      ///   caller. Named rather than positional (2026-09-10): four booleans in a row around a regard, where
      ///   "already of the player's clan" and "has an heir to leave the post to" are both just true.
      /// </summary>
      public sealed class NotableRecruitmentFacts
      {
         /// <summary>They are a settlement notable, which is who this is about.</summary>
         public bool IsNotable { get; set; }

         /// <summary>They are alive.</summary>
         public bool IsAlive { get; set; }

         /// <summary>They are somebody's prisoner.</summary>
         public bool IsPrisoner { get; set; }

         /// <summary>They already belong to the player's clan, so there is nothing to ask.</summary>
         public bool IsPlayerClanAlready { get; set; }

         /// <summary>The mod's own personal regard ledger, never the vanilla clan relation.</summary>
         public int Regard { get; set; }

         /// <summary>
         ///   Somebody can inherit their post. A notable who leaves an empty chair behind takes their
         ///   settlement's whole standing with them, so the succession is the price of the offer.
         /// </summary>
         public bool HasEligibleSuccessor { get; set; }
      }

      /// <summary>Which door is shut on recruiting this notable, or None. Unknown facts refuse.</summary>
      public static NotableRecruitmentBlock Evaluate(NotableRecruitmentFacts facts)
      {
         if (facts == null) return NotableRecruitmentBlock.NotANotable;

         bool isNotable = facts.IsNotable;
         bool isAlive = facts.IsAlive;
         bool isPrisoner = facts.IsPrisoner;
         bool isPlayerClanAlready = facts.IsPlayerClanAlready;
         int regard = facts.Regard;
         bool hasEligibleSuccessor = facts.HasEligibleSuccessor;

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
      public static bool CanRecruit(NotableRecruitmentFacts facts)
         => Evaluate(facts) == NotableRecruitmentBlock.None;
   }
}
