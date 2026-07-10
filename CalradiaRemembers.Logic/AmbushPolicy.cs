// Code written by Gabriel Mailhot, 30/06/2026.

namespace CalradiaRemembers.Logic
{
   /// <summary>Why a player's town entry did or did not spring an armed ambush (host-logged for diagnosis).</summary>
   public enum AmbushEntryOutcome
   {
      /// <summary>No lord has an attempt armed against the player — the ordinary case.</summary>
      NoArmedAssassin,

      /// <summary>An attempt is armed, but the experimental feature toggle is off — nothing happens.</summary>
      Disabled,

      /// <summary>Armed and enabled, but the per-visit roll missed — the knives stay sheathed this visit.</summary>
      Fizzled,

      /// <summary>Armed, enabled, and the roll landed — the ambush springs.</summary>
      Spring
   }

   /// <summary>
   ///   The make-up of an ambush party: one elite chief (a real soldier of the paymaster's faction, the tell
   ///   that a lord is behind it) and a body of cutthroats in disguise.
   /// </summary>
   public readonly struct AmbushForce
   {
      public AmbushForce(int chiefCount, int banditCount)
      {
         ChiefCount = chiefCount;
         BanditCount = banditCount;
      }

      /// <summary>The elite leader(s) — always one for a street ambush.</summary>
      public int ChiefCount { get; }

      /// <summary>The disguised cutthroats under the chief.</summary>
      public int BanditCount { get; }

      /// <summary>Everyone on the attacking side.</summary>
      public int TotalAttackers => ChiefCount + BanditCount;
   }

   /// <summary>When an armed assassination springs in town, and how heavy it is.</summary>
   public static class AmbushPolicy
   {
      /// <summary>The highest troop tier the chief can climb to (the cap of the noble line).</summary>
      public const int MaxChiefTier = 6;

      /// <summary>The most companions who can stand with the player in a street ambush.</summary>
      public const int MaxCompanionsAtSide = 3;

      /// <summary>The lowest troop tier the chief is allowed to be — still a credible soldier, not a recruit.</summary>
      public const int MinChiefTier = 2;

      /// <summary>The chance, per eligible town visit, that an armed assassination actually springs. Tuning.</summary>
      public const int SpringChancePercent = 35;

      /// <summary>
      ///   How many of the player's party companions stand with them when the knives come out: every companion
      ///   in the party, capped at <see cref="MaxCompanionsAtSide" />. A negative count floors to none.
      /// </summary>
      public static int CompanionsAtSide(int companionsInParty)
         => companionsInParty < 0
            ? 0
            : companionsInParty > MaxCompanionsAtSide
               ? MaxCompanionsAtSide
               : companionsInParty;

      /// <summary>
      ///   The attacking force for a given defending side (the player plus the companions at their side):
      ///   one elite chief plus cutthroats, the TOTAL scaling as (defenders + 1) squared so larger retinues face
      ///   sharply heavier odds, nudged by ±1 so two ambushes never field the exact same number.
      ///   <paramref name="jitterRoll" /> is any 0..99 value (its remainder mod 3 picks the nudge), kept as an
      ///   argument so the size stays pure and unit-tested. A side below one floors to one.
      /// </summary>
      public static AmbushForce ComputeForce(int defendersAtPlayerSide, int jitterRoll)
      {
         int defenders = defendersAtPlayerSide < 1
            ? 1
            : defendersAtPlayerSide;
         int total = (defenders + 1) * (defenders + 1); // (N+1)^2 attackers, the chief among them
         int bandits = total - 1 + JitterOffset(jitterRoll);
         if (bandits < 1) bandits = 1;

         return new AmbushForce(1, bandits);
      }

      /// <summary>
      ///   The outcome of the player walking into a town, once the host has confirmed it IS the player
      ///   entering a town (the "every party enters every settlement" firehose filter stays in the host).
      ///   Pure so the gating ORDER is protected by tests, and so the host can log exactly why a visit did or
      ///   did not spring — the trigger path used to be silent, which made "no ambush ever fires" impossible to
      ///   diagnose. The arming is NOT spent on a miss: an armed lord re-rolls every visit until it springs.
      /// </summary>
      public static AmbushEntryOutcome DecidePlayerTownEntry(bool featureEnabled, bool hasArmedAssassin, int rollPercent)
      {
         if (!hasArmedAssassin) return AmbushEntryOutcome.NoArmedAssassin;
         if (!featureEnabled) return AmbushEntryOutcome.Disabled;

         return ShouldSpring(rollPercent)
            ? AmbushEntryOutcome.Spring
            : AmbushEntryOutcome.Fizzled;
      }

      /// <summary>
      ///   The troop tier the ambush chief should match: roughly the player's own clan tier, so the fight scales
      ///   to the player rather than fielding a top-tier veteran against a fledgling clan. Bounded so an early
      ///   clan still faces a real soldier and a great house never gets a super-human chief.
      /// </summary>
      public static int EliteChiefTier(int playerClanTier)
         => playerClanTier < MinChiefTier
            ? MinChiefTier
            : playerClanTier > MaxChiefTier
               ? MaxChiefTier
               : playerClanTier;

      // The attacking force scales with the SQUARE of the defending side: hired killers do not take on an
      // important target at a disadvantage, and a bigger retinue draws a disproportionately bigger ambush so two
      // or three companions can't trivially cut their way out. No upper bound — the fight is outside the walls,
      // so crowding is no issue. Totals (chief included): 1->4, 2->9, 3->16, 4->25.

      /// <summary>True when a 0..99 roll lands inside the spring chance — the ambush triggers this visit.</summary>
      public static bool ShouldSpring(int rollPercent)
         => rollPercent is >= 0 and < SpringChancePercent;

      #region private

      /// <summary>Maps a 0..99 roll to a -1 / 0 / +1 nudge (evenly, by its remainder mod 3).</summary>
      private static int JitterOffset(int roll) => (roll < 0
                                                      ? -roll
                                                      : roll) %
                                                   3 -
                                                   1;

      #endregion
   }
}