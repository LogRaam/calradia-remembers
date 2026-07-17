// Code written by Gabriel Mailhot, 16/07/2026.
// Duels pillar, Inc A (pure): a duel fought over a courted hero moves her heart, on top of (never instead of)
// DuelOutcomePolicy's generic renown and stance. The shape mirrors the outcome policy's own lesson but on the
// romance axis: fighting for her, win or lose, is what a suitor is FOR, so both PlayerWon and NpcWon move
// attraction upward or only mildly down; refusing to fight for her at all is judged worse than losing, because
// a man who will not risk the field for a woman he claims to want has shown his true measure. This file only
// ever touches CourtedAttractionDelta; the generic consequence stays DuelOutcomePolicy's job untouched.

namespace CalradiaRemembers.Logic.Duels
{
   /// <summary>The courtship-specific extra a duel over a hero produces, applied by the host to the courted
   /// hero's RomanticProfile.AttractionToPlayer (clamped [-100,100] there).</summary>
   public sealed class CourtshipDuelConsequence
   {
      /// <summary>Change to the courted hero's attraction toward the player.</summary>
      public int CourtedAttractionDelta { get; set; }
   }

   /// <summary>
   ///   Resolves a courtship-rivalry duel into how the courted hero's attraction moves. Every constant is a
   ///   tuning knob, kept modest since attraction is a [-100,100] axis moved in small steps. The ordering that
   ///   matters: winning beats losing beats refusing, because fighting at all (win or lose) is proof enough of
   ///   real want, while refusing is proof of its absence.
   /// </summary>
   public static class CourtshipDuelPolicy
   {
      /// <summary>Tuning: attraction gained when the player fought for her and won. She admires the victor.</summary>
      public const int AdmirationOnWin = 12;

      // STAKE: a refusal must cost MORE attraction than a loss, or a suitor could always play it safe by
      // declining any duel he might lose, and the design would reward cowardice over courage. A man who fought
      // and lost dared the field for her; a man who would not fight for her at all never risked anything, and
      // that is judged the worse of the two.

      /// <summary>Tuning: attraction lost when the player fought for her and lost. Modest: he dared, and daring
      /// counts for something even in defeat.</summary>
      public const int DoubtOnLoss = -6;

      /// <summary>Tuning: attraction lost when the player refused to fight for her. Worse than losing: a man
      /// who will not risk the field for her has shown he does not want her enough.</summary>
      public const int CowardiceOnRefuse = -10;

      /// <summary>Tuning: attraction gained when the rival backed out of the duel. A modest default gain: the
      /// field was never actually contested, so the player's own worth was not truly proven, only the rival's
      /// lack of nerve.</summary>
      public const int AdmirationOnRivalBalked = 4;

      /// <summary>
      ///   The courtship-specific consequence of a duel resolution. Maps <see cref="DuelResolution.PlayerWon" />
      ///   to <see cref="AdmirationOnWin" />, <see cref="DuelResolution.NpcWon" /> to <see cref="DoubtOnLoss" />,
      ///   <see cref="DuelResolution.PlayerForfeited" /> to <see cref="CowardiceOnRefuse" />, and
      ///   <see cref="DuelResolution.NpcForfeited" /> to <see cref="AdmirationOnRivalBalked" />. Any other value
      ///   (defensive: the enum should never carry one) yields a no-op zero delta.
      /// </summary>
      public static CourtshipDuelConsequence Resolve(DuelResolution resolution)
      {
         int delta;

         switch (resolution)
         {
            case DuelResolution.PlayerWon:
               delta = AdmirationOnWin;
               break;

            case DuelResolution.NpcWon:
               delta = DoubtOnLoss;
               break;

            case DuelResolution.PlayerForfeited:
               delta = CowardiceOnRefuse;
               break;

            case DuelResolution.NpcForfeited:
               delta = AdmirationOnRivalBalked;
               break;

            default:
               delta = 0;
               break;
         }

         return new CourtshipDuelConsequence {CourtedAttractionDelta = delta};
      }
   }
}
