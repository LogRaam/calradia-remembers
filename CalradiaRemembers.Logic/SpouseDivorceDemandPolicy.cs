// Code written by Gabriel Mailhot, 04/07/2026.
// Divorce, Phase 2b: the PLAYER's own unhappy spouse DEMANDS a divorce (owner ruling: entirely through
// conversation, never a UI button). This pure policy decides only WHEN a deeply unhappy spouse raises the
// demand in the first place; the conversational accept/refuse resolution and the actual dissolution are
// engine concerns (SpouseDivorceDemandBehavior / MarriageDissolutionService).

namespace CalradiaRemembers.Logic
{
   /// <summary>
   ///   Every already-resolved fact <see cref="SpouseDivorceDemandPolicy.ShouldDemand" /> needs. The engine
   ///   gathers these live from the player's own spouse each evaluation period.
   /// </summary>
   public sealed class SpouseDivorceDemandFacts
   {
      /// <summary>True when the spouse already has a demand pending — never re-raise while one is live.</summary>
      public bool AlreadyDemanding { get; set; }

      /// <summary>
      ///   True when the spouse has just discovered a bastard scandal touching the player (a hidden child
      ///   come to light) — a fresh wound distinct from ordinary marital misery.
      /// </summary>
      public bool DiscoveredBastardScandal { get; set; }

      /// <summary>True when either the spouse or the player is currently pregnant — never raised then.</summary>
      public bool EitherPregnant { get; set; }

      /// <summary>True when either the spouse or the player is currently held prisoner — never raised then.</summary>
      public bool EitherPrisoner { get; set; }

      /// <summary>
      ///   The strongest LIVE grudge the spouse nurses against the player, today's effective magnitude
      ///   (0 when they hold none). A grudge heavy enough on its own tips the demand even without a fresh
      ///   wound flag or a miserable relation reading.
      /// </summary>
      public int LiveGrudgeMagnitudeAgainstPlayer { get; set; }

      /// <summary>
      ///   True when the spouse has recently taken a jealousy wound over the player (a romantic act that
      ///   struck at their own marriage) — a fresh wound distinct from ordinary marital misery.
      /// </summary>
      public bool RecentJealousyWound { get; set; }

      /// <summary>The spouse's own personal relation toward the player.</summary>
      public int SpouseRelationWithPlayer { get; set; }
   }

   /// <summary>
   ///   Pure gate deciding whether the player's own spouse is unhappy enough to raise a divorce demand this
   ///   evaluation period. Every constant here is a tuning knob: changing one is a one-line, test-covered
   ///   decision, never a hunt through engine code.
   /// </summary>
   public static class SpouseDivorceDemandPolicy
   {
      /// <summary>
      ///   Tuning: at/above this LIVE grudge magnitude against the player, the grudge alone tips the
      ///   demand, whatever the blended relation reads (a single deep wound can outweigh an otherwise
      ///   civil marriage).
      /// </summary>
      public const int GrudgeMagnitudeBar = 40;

      /// <summary>
      ///   Tuning: at/below this relation with the player, the marriage is miserable enough on its own to
      ///   raise the demand, with no fresh wound needed at all.
      /// </summary>
      public const int MiserableRelationThreshold = -20;

      /// <summary>
      ///   True when the player's spouse should raise a divorce demand this evaluation period: never while
      ///   a demand is already pending, and never while either party is pregnant or held prisoner. Subject
      ///   to those hard gates, any ONE of the following tips it: the marriage reads as flatly miserable
      ///   (<see cref="MiserableRelationThreshold" />), a fresh wound just struck (jealousy or a discovered
      ///   bastard scandal, even at a merely moderate relation), or a live grudge against the player already
      ///   clears <see cref="GrudgeMagnitudeBar" />. A genuinely happy marriage, with none of these, never
      ///   demands.
      /// </summary>
      public static bool ShouldDemand(SpouseDivorceDemandFacts facts)
      {
         if (facts == null) return false;
         if (facts.AlreadyDemanding) return false;
         if (facts.EitherPregnant || facts.EitherPrisoner) return false;

         bool miserable = facts.SpouseRelationWithPlayer <= MiserableRelationThreshold;
         bool freshWound = facts.RecentJealousyWound || facts.DiscoveredBastardScandal;
         bool grudgeTipped = facts.LiveGrudgeMagnitudeAgainstPlayer >= GrudgeMagnitudeBar;

         return miserable || freshWound || grudgeTipped;
      }
   }
}
