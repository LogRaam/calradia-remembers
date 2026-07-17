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
      CourtshipRivalry
   }
}
