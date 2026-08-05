// Code written by Gabriel Mailhot, 05/08/2026.
// Player report: a companion got pregnant while the campaign's Life/Death Cycle option is disabled (no
// aging, no birth) - decompiling PregnancyCampaignBehavior.DailyTickHero shows vanilla's ENTIRE offspring
// delivery check is gated behind `!CampaignOptions.IsLifeDeathCycleDisabled`, so a pregnancy created under
// that setting is NEVER delivered: it sits in the engine's own pregnancy list forever, IsPregnant stays
// true forever, and the NPC's prompt keeps announcing it every conversation. Two decisions live here:
// whether a NEW conception may even be created (MayConceive), and whether an EXISTING tracked pregnancy
// has gone stuck long enough past its due date to end it childless rather than let it linger
// (ShouldEndStuckPregnancy). Ending it childless is a deliberate, documented compromise: it respects the
// player's own no-birth choice rather than silently overriding it with a birth they explicitly turned off.

namespace CalradiaRemembers.Logic
{
   /// <summary>
   ///   Pure gates around a pregnancy's whole lifecycle when the campaign's Life/Death Cycle option is
   ///   disabled (Bannerlord's own "no aging / no death" creation option, which also silently disables
   ///   vanilla's offspring delivery - see this file's header). Consulted by PregnancyService (conception)
   ///   and PregnancyBirthBehavior (the daily stuck-pregnancy cleanup).
   /// </summary>
   public static class PregnancyPolicy
   {
      /// <summary>
      ///   A conception must never be created while births are disabled: vanilla would never deliver it,
      ///   leaving the mother "pregnant" forever and the NPC endlessly mentioning it in chat. The intimate
      ///   act itself is unaffected - only the lingering pregnancy consequence is skipped.
      /// </summary>
      public static bool MayConceive(bool birthsDisabled) => !birthsDisabled;

      /// <summary>
      ///   An EXISTING tracked pregnancy should be ended (childless, never delivered) only when births are
      ///   disabled AND her due date has passed by more than <paramref name="overdueMarginDays" />. The
      ///   margin gives vanilla's own daily check a window to deliver normally first (should the player
      ///   re-enable births before the sweep runs) and absorbs ordinary day-boundary jitter right around
      ///   the due date itself. When births are NOT disabled, this always returns false: vanilla delivers
      ///   on its own and CR must never step on that.
      /// </summary>
      public static bool ShouldEndStuckPregnancy(bool birthsDisabled, int daysOverdue, int overdueMarginDays)
         => birthsDisabled && daysOverdue > overdueMarginDays;
   }
}
