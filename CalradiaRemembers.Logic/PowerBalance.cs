// Code written by Gabriel Mailhot, 20/06/2026.

#region

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>
   ///   The raw, engine-gathered facts of the power differential between the player's realm and a
   ///   lord NPC — pure to reason over. The host fills these from the live game (fief counts, war
   ///   state, captivity, a recent battlefield defeat); <see cref="PowerBalance" /> turns them into
   ///   the prose note. Recency of the defeat is decided host-side; this only carries the verdict.
   /// </summary>
   public sealed class PowerBalanceFacts
   {
      /// <summary>True only when BOTH belong to a kingdom AND those kingdoms are at war.</summary>
      public bool AtWar { get; set; }

      /// <summary>True when the NPC's clan holds at least one town or castle.</summary>
      public bool NpcClanHasFiefs { get; set; }

      /// <summary>True when the NPC's clan belongs to a kingdom.</summary>
      public bool NpcInKingdom { get; set; }

      /// <summary>Only lords carry kingdom/clan standing worth weighing; others get no note.</summary>
      public bool NpcIsLord { get; set; }

      /// <summary>True when the NPC is currently a prisoner of the player's faction.</summary>
      public bool NpcIsPlayerPrisoner { get; set; }

      /// <summary>Towns and castles held by the NPC's kingdom (0 when the NPC has none).</summary>
      public int NpcKingdomFiefs { get; set; }

      /// <summary>True when the player's forces beat this NPC in a battle recently (recency judged host-side).</summary>
      public bool NpcRecentlyLostBattleToPlayer { get; set; }

      /// <summary>True when the player's clan holds at least one town or castle.</summary>
      public bool PlayerClanHasFiefs { get; set; }

      /// <summary>True when the player's clan belongs to a kingdom.</summary>
      public bool PlayerInKingdom { get; set; }

      /// <summary>Towns and castles held by the player's kingdom (0 when the player has none).</summary>
      public int PlayerKingdomFiefs { get; set; }
   }

   /// <summary>
   ///   Produces the POWER BALANCE prompt note for a lord: how the military/political imbalance
   ///   should colour their candor — calibrated realism, never servility. Captivity is the most
   ///   acute reality and overrides the territorial calculus; otherwise a territorial/clan note is
   ///   built, with a fresh battlefield defeat layered on top. Null when the balance is roughly
   ///   even, the NPC is not a lord, or nothing weighs on the exchange.
   /// </summary>
   public static class PowerBalance
   {
      public static string DescribeFor(PowerBalanceFacts f)
      {
         if (f is not {NpcIsLord: true}) return null;

         // Captivity dominates every other consideration — rank and territory cease to matter
         // while a lord's life and freedom rest in the player's hands.
         if (f.NpcIsPlayerPrisoner)
            return "You are currently a prisoner of the player's faction. Whatever your rank, your freedom " +
                   "and your safety rest in their hands now — and a captive measures every word.";

         string standing = DescribeStanding(f);
         string defeat = f.NpcRecentlyLostBattleToPlayer
            ? "The player's own forces bested you on the field not long ago; that defeat is still fresh, " +
              "and it tempers any defiance."
            : null;

         return Combine(standing, defeat);
      }

      #region private

      /// <summary>Joins the standing note and the defeat clause, dropping whichever is absent.</summary>
      private static string Combine(string standing, string defeat)
      {
         if (string.IsNullOrEmpty(standing))
            return string.IsNullOrEmpty(defeat)
               ? null
               : defeat;
         if (string.IsNullOrEmpty(defeat)) return standing;

         return standing + " " + defeat;
      }

      /// <summary>
      ///   The territorial/clan standing note, faithful to the original thresholds: a war read by
      ///   fief ratio, a peacetime remark only on a marked asymmetry, else a clan-level fief gap.
      ///   Null when neither party is in a kingdom or the balance is unremarkable.
      /// </summary>
      private static string DescribeStanding(PowerBalanceFacts f)
      {
         // A territorial comparison means nothing unless at least one side belongs to a kingdom.
         if (!f.PlayerInKingdom && !f.NpcInKingdom) return null;

         // Realm against realm only when BOTH have one. VonLemberg (Discord, 16/11/2026, source audit of 2.8.12): with one
         // side sworn to no realm, its missing kingdom counted as a kingdom of 0 fiefs, and a lord told an independent player
         // "Your kingdom outmatches the player's realm, 5 fiefs to their 0" (even one whose own clan holds a town). A house
         // with no realm is not a realm of nothing: the clan-level gap below still speaks.
         if (!f.PlayerInKingdom || !f.NpcInKingdom) return ClanGap(f);

         int playerFiefs = f.PlayerKingdomFiefs;
         int npcFiefs = f.NpcKingdomFiefs;

         // Guard the divide; a fief-less kingdom facing a fief-holding player reads as a rout.
         float ratio = npcFiefs > 0
            ? (float) playerFiefs / npcFiefs
            : playerFiefs > 0
               ? 10f
               : 1f;

         if (f.AtWar)
         {
            if (ratio >= 2f)
               return "Your kingdom is at war with the player's realm, and the balance of power is heavily " +
                      "against you — they hold far more territory. Your position is precarious.";
            if (ratio <= 0.5f)
               return "Your kingdom is at war with the player's realm, and you hold a clear advantage — " +
                      "your forces and territory outmatch theirs.";

            return "Your kingdom is at war with the player's realm. The outcome remains uncertain; " +
                   "neither side commands a decisive advantage.";
         }

         // At peace — only remark on a significant asymmetry (>= 2x ratio, >= 3 fiefs).
         if (ratio >= 2f && playerFiefs >= 3)
            return $"The player's realm is considerably more powerful than yours — they hold {playerFiefs} " +
                   $"fiefs to your kingdom's {npcFiefs}. You are aware of this disparity.";
         if (ratio <= 0.5f && npcFiefs >= 3)
            return $"Your kingdom outmatches the player's realm — {npcFiefs} fiefs to their {playerFiefs}. " +
                   "You hold the stronger position.";

         return ClanGap(f);
      }

      /// <summary>Clan-level: the NPC holds nothing while the player's clan does.</summary>
      private static string ClanGap(PowerBalanceFacts f)
         => !f.NpcClanHasFiefs && f.PlayerClanHasFiefs
            ? "Your clan currently holds no fiefs, while the player's clan does. That gap in standing is not lost on you."
            : null;

      #endregion
   }
}