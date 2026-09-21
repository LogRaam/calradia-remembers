// Code written by Gabriel Mailhot, 05/09/2026.
// Mystery-lover pillar (design phase 3b-3): an enemy lord who holds the player's secret-lover identity can extract
// gold for his silence. This pure policy holds the two numeric rules of that blackmail: the payment is clamped to a
// sane band (the LLM negotiates an amount, but it can never be nonsense), and once paid the lord backs off for a
// cooldown (so a paid-off player is not shaken down every single visit). The engine owns the gold transfer and the
// day-stamped ledger; this decides what the numbers mean.

namespace CalradiaRemembers.Logic
{
   /// <summary>The numeric contract for hush money paid to a lord who holds the player's secret-lover identity.</summary>
   public static class HushMoneyPolicy
   {
      /// <summary>The least a hush payment can be, so a lord never settles for a pittance the LLM lowballed. Tuning.</summary>
      public const int MinPayment = 250;

      /// <summary>
      ///   The most a single hush payment can be, so a hallucinated demand cannot bankrupt the player. It may be tuned
      ///   LOWER than the bridge's own ceiling, never higher: take_gold would clamp the payment and the silence would
      ///   be paid for and not bought (the same trap BastardBlackmailPolicy documents).
      /// </summary>
      public const int MaxPayment = GoldTransferLimit.MaxSingleTransfer;

      /// <summary>How long, in days, a paid lord holds his peace before the leverage framing may press for coin again. Tuning.</summary>
      public const int CooldownDays = 14;

      /// <summary>Clamps a negotiated amount into the sane band.</summary>
      public static int ClampPayment(int requested)
         => requested < MinPayment ? MinPayment : requested > MaxPayment ? MaxPayment : requested;

      /// <summary>True when a hush payment on <paramref name="paidDay" /> is still within the cooldown as of <paramref name="today" /> (the lord should not extort again yet). No payment recorded is never "recently paid".</summary>
      public static bool IsRecentlyPaid(int? paidDay, int today)
         => paidDay.HasValue && today - paidDay.Value < CooldownDays;
   }
}
