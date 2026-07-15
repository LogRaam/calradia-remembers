// Code written by Gabriel Mailhot, 14/07/2026.
// Duels pillar, Inc 0 (pure): the gate that decides whether a duel may even be OFFERED, before anyone weighs
// whether to accept it. This is the bridge's law in policy form: the LLM may have an NPC say "let us settle
// this with steel", but a challenge is only admissible between two able people of standing who are face to
// face and not already bound to each other by captivity. Get this wrong and a chat could call a commoner, a
// prisoner, a spouse, or someone on the far side of Calradia to a field they can never reach, and the whole
// feature reads as broken. The war-declaration line lives one layer up (a peace-time ATTACK is out; a duel is
// the honest channel): this policy only judges the duel itself.

namespace CalradiaRemembers.Logic.Duels
{
   /// <summary>Who issued the challenge. Eligibility is symmetric, but the direction is carried so the outcome and forfeit rules know who would be refusing.</summary>
   public enum DuelChallenger
   {
      /// <summary>The player called the NPC out.</summary>
      Player,

      /// <summary>The NPC called the player out.</summary>
      Npc
   }

   /// <summary>
   ///   Why a duel challenge is or is not admissible right now. <see cref="Eligible" /> is the only "yes";
   ///   every other value is a concrete reason the bridge can voice ("he is your prisoner", "he is not here").
   /// </summary>
   public enum DuelEligibility
   {
      /// <summary>The challenge may proceed to <see cref="DuelAcceptancePolicy" />.</summary>
      Eligible,

      /// <summary>One party cannot fight at all (dead, wounded past fighting, a child). Nothing else matters if so.</summary>
      Incapable,

      /// <summary>You do not duel yourself, your spouse, or your own child. Kin is not a field of honour.</summary>
      SelfOrFamily,

      /// <summary>One holds the other prisoner. Captivity has its own scenes; a captive cannot give real consent to a "fair" duel.</summary>
      InCaptivity,

      /// <summary>A duel is between people of standing. You do not call a commoner or a servant to the field.</summary>
      NotAPeer,

      /// <summary>They do not share a place. You cannot challenge someone you are not standing before.</summary>
      NotFaceToFace,

      /// <summary>They crossed blades too recently. The field is a grave matter, not a daily treadmill.</summary>
      TooSoon
   }

   /// <summary>Everything <see cref="DuelChallengePolicy.Evaluate" /> needs. The host resolves each fact from the live heroes; the policy only judges.</summary>
   public sealed class DuelChallengeContext
   {
      /// <summary>Who is calling whom out. Does not change eligibility, only travels to later rules.</summary>
      public DuelChallenger Challenger { get; set; }

      /// <summary>Both parties are alive and physically able to fight (not a corpse, not bedridden, not a child).</summary>
      public bool BothAbleToFight { get; set; }

      /// <summary>The two are the same person, married to each other, or parent and child.</summary>
      public bool IsSelfOrFamily { get; set; }

      /// <summary>Either party is holding the other prisoner right now.</summary>
      public bool EitherHoldsOtherPrisoner { get; set; }

      /// <summary>The challenged party is a lord / person of standing, not a commoner.</summary>
      public bool TargetIsAPeer { get; set; }

      /// <summary>The two share a place (a settlement, or the same spot on the map), so a duel could actually happen.</summary>
      public bool SharePlace { get; set; }

      /// <summary>Game days since these two last dueled, or null if never. Below the cooldown blocks a fresh one.</summary>
      public double? DaysSinceLastDuel { get; set; }
   }

   /// <summary>
   ///   Decides whether a duel challenge is admissible at all. The ORDER of the checks is deliberate and
   ///   tested: the most fundamental impossibility wins, so the reason the bridge voices is the truest one
   ///   ("he is dead" outranks "he is not here"). Nothing here weighs willingness; that is
   ///   <see cref="DuelAcceptancePolicy" />.
   /// </summary>
   public static class DuelChallengePolicy
   {
      /// <summary>Tuning: the fewest game days that must pass before the same two may duel again.</summary>
      public const int CooldownDays = 7;

      /// <summary>
      ///   The eligibility verdict. Checked from the most fundamental barrier to the most temporary: an
      ///   incapable or kin target can NEVER be dueled, captivity and standing are hard social bars, sharing a
      ///   place is situational, and the cooldown is the only one that simply passes with time. A null context
      ///   is treated as incapable (the safe refusal).
      /// </summary>
      public static DuelEligibility Evaluate(DuelChallengeContext ctx)
      {
         if (ctx == null || !ctx.BothAbleToFight) return DuelEligibility.Incapable;
         if (ctx.IsSelfOrFamily) return DuelEligibility.SelfOrFamily;
         if (ctx.EitherHoldsOtherPrisoner) return DuelEligibility.InCaptivity;
         if (!ctx.TargetIsAPeer) return DuelEligibility.NotAPeer;
         if (!ctx.SharePlace) return DuelEligibility.NotFaceToFace;
         if (IsOnCooldown(ctx.DaysSinceLastDuel)) return DuelEligibility.TooSoon;

         return DuelEligibility.Eligible;
      }

      /// <summary>True when a duel happened too recently for another. Never dueled (null) is never on cooldown.</summary>
      public static bool IsOnCooldown(double? daysSinceLastDuel)
         => daysSinceLastDuel.HasValue && daysSinceLastDuel.Value < CooldownDays;
   }
}
