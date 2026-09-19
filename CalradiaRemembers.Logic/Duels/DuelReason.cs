// Code written by Gabriel Mailhot, 16/07/2026.
// Duels pillar, Inc A (pure): the REASON a duel is fought shapes its consequence. Two reasons, both "soft"
// (they move perception, never the strategic board, never a war). HonourAffront is the generic case
// DuelOutcomePolicy already handles in full (renown and stance). CourtshipRivalry is new: a rival suitor
// forces the matter with steel, and the consequence that matters is not renown but how the COURTED hero's
// heart leans, which is CourtshipDuelPolicy's job, layered on top of the generic outcome, never replacing it.

namespace CalradiaRemembers.Logic.Duels
{
   /// <summary>Why a duel was fought, so the right consequence policy applies on top of the generic outcome.</summary>
   public enum DuelReason
   {
      /// <summary>An insulted lord demands satisfaction. The generic renown and stance consequence
      /// (<see cref="DuelOutcomePolicy" />) is the whole of it; nothing extra applies.</summary>
      HonourAffront,

      /// <summary>A rival suitor for a hero the player courts forces the matter with steel. On top of the
      /// generic outcome, the consequence that matters moves the courted hero's attraction toward the player
      /// (<see cref="CourtshipDuelPolicy" />).</summary>
      CourtshipRivalry,

      /// <summary>
      ///   A BOUT BOTH SIDES WANTED: a wager, a test of skill, a bored companion who fancies a match. Nobody
      ///   was insulted, so nobody's pride is wounded by losing and no house is shamed by it.
      ///   <para>
      ///     Zinced121212 (Nexus, 18/09/2026): "Currently all duels remove personal regard even if it's a
      ///     friendly battle that both sides agree to and the companion seems to be in a good mood after."
      ///     He was right, and the cause was one hard-coded value: every duel armed from a chat was filed as
      ///     HonourAffront, whose whole premise is that somebody was insulted. The fiction had been producing
      ///     friendly bouts for a while (yozakura12's log a day earlier records a private sparring match over
      ///     a wager) and the ledger had no word for one.
      ///   </para>
      /// </summary>
      Friendly
   }
}
