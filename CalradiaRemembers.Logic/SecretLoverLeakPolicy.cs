// Code written by Gabriel Mailhot, 05/09/2026.
// Mystery-lover pillar (design [[project_npc_recent_matter]] phase 3): a secret lover is kept GENUINELY secret for
// a while, then whispers of the bond's EXISTENCE (never the player's identity) may begin to circulate as gossip.
// This pure policy decides ONLY when a bond is eligible for a leak roll (a real delay has passed, an onset is
// known, it has not already leaked); the engine does the random roll and records the anonymous world event.

namespace CalradiaRemembers.Logic
{
   /// <summary>Gates when the EXISTENCE of a secret lover may begin to leak: after a delay, once, never before.</summary>
   public static class SecretLoverLeakPolicy
   {
      /// <summary>How long a secret bond stays genuinely secret before any whisper of its existence may start.</summary>
      public const int LeakDelayDays = 14;

      /// <summary>Once past the delay, the per-day chance that whispers actually begin (so a leak feels organic, not clockwork, and some secrets simply keep longer).</summary>
      public const int DailyLeakChancePercent = 12;

      /// <summary>
      ///   True when this secret bond may be ROLLED for a leak today: its onset is known, the delay has fully
      ///   elapsed, and it has not already leaked. The caller then rolls <see cref="DailyLeakChancePercent" />; a
      ///   bond with no onset, still inside its delay, or already leaked is never eligible.
      /// </summary>
      public static bool IsEligibleToLeak(int? secretSinceDay, int today, bool alreadyLeaked)
      {
         if (alreadyLeaked || !secretSinceDay.HasValue) return false;

         return today - secretSinceDay.Value >= LeakDelayDays;
      }
   }
}
