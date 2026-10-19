// Code written by Gabriel Mailhot, 07/07/2026.
// Companion-victim captive scenes, Phase 3: the mechanical AFTERMATH of a captor tormenting or assaulting a
// held companion before the player. Pure and unit-tested so the magnitudes stay protected; the host reads
// them to crater the companion's bond with the player (grief), plant the companion's revenge grudge against a
// named captor, and record the atrocity as a world-event (the player's chronicled revenge driver).

namespace CalradiaRemembers.Logic.Companions
{
   /// <summary>The computed aftermath of one companion-victim scene: how the bond scars, how deep the revenge, how loud the news.</summary>
   public readonly struct CompanionAbuseConsequence
   {
      /// <summary>False at severity 0 (nothing abusive happened): no consequence at all.</summary>
      public bool Forms { get; init; }

      /// <summary>Delta (negative) to apply to the companion's TRUST toward the player: the ordeal scars the bond with the one who could not save them.</summary>
      public int BondTrustCrater { get; init; }

      /// <summary>Delta (negative) to apply to the companion's AFFECTION toward the player.</summary>
      public int BondAffectionCrater { get; init; }

      /// <summary>Magnitude of the REVENGE grudge the companion holds against a NAMED captor (0 for a faceless bandit who earns no lasting vendetta).</summary>
      public int CaptorRevengeMagnitude { get; init; }

      /// <summary>Magnitude of the atrocity world-event (its news reach and chronicle weight).</summary>
      public int WorldEventMagnitude { get; init; }
   }

   /// <summary>
   ///   Pure policy for the companion-victim aftermath. Severity is the scene's abuse severity (0..3, from
   ///   <c>CaptiveIntentRules.CaptiveAbuseSeverity</c>): 0 is a non-abusive intent (no consequence). Being the
   ///   FORCED perpetrator (the captor made the player do it) cuts the bond deeper than being made to watch. A
   ///   NAMED captor (a persistent nemesis or lord) earns a lasting revenge grudge; a faceless bandit does not.
   /// </summary>
   public static class CompanionAbuseConsequencePolicy
   {
      /// <summary>TUNE: trust the ordeal costs the player, per severity level.</summary>
      public const int TrustCraterPerSeverity = 6;

      /// <summary>TUNE: affection the ordeal costs the player, per severity level.</summary>
      public const int AffectionCraterPerSeverity = 4;

      /// <summary>TUNE: extra bond damage when the player's OWN hands were forced to the deed (a deeper betrayal than watching).</summary>
      public const int CoercedTrustBonus = 8;

      /// <summary>TUNE: extra affection damage when the player was the forced perpetrator (half the trust bonus).</summary>
      public const int CoercedAffectionBonus = 4;

      /// <summary>TUNE: base revenge grudge against a named captor.</summary>
      public const int CaptorRevengeBase = 30;

      /// <summary>TUNE: added revenge per severity level (how far the captor took it).</summary>
      public const int CaptorRevengePerSeverity = 15;

      /// <summary>TUNE: base news reach of the atrocity.</summary>
      public const int WorldEventBase = 30;

      /// <summary>TUNE: added news reach per severity level.</summary>
      public const int WorldEventPerSeverity = 12;

      /// <summary>Computes the aftermath. Severity 0 (or less) forms nothing.</summary>
      public static CompanionAbuseConsequence Resolve(int severity, bool playerCoerced, bool captorIsNamed)
      {
         if (severity <= 0) return new CompanionAbuseConsequence {Forms = false, BondTrustCrater = 0, BondAffectionCrater = 0, CaptorRevengeMagnitude = 0, WorldEventMagnitude = 0};

         int trust = severity * TrustCraterPerSeverity + (playerCoerced ? CoercedTrustBonus : 0);
         int affection = severity * AffectionCraterPerSeverity + (playerCoerced ? CoercedAffectionBonus : 0);
         int revenge = captorIsNamed ? CaptorRevengeBase + severity * CaptorRevengePerSeverity : 0;
         int news = WorldEventBase + severity * WorldEventPerSeverity;

         return new CompanionAbuseConsequence {Forms = true, BondTrustCrater = -trust, BondAffectionCrater = -affection, CaptorRevengeMagnitude = revenge, WorldEventMagnitude = news};
      }
   }
}
