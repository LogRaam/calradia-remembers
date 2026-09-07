// Code written by Gabriel Mailhot, 06/09/2026.
// Tournament memory pillar Inc 4 (rivalries). Repeated arena meetings between a lord and the player build into a
// standing SPORTING RIVALRY once they have faced each other enough. This pure policy holds that numeric contract:
// how many head-to-heads crystallize a rivalry, and whether it is good-natured or bitter. Gabriel: BOTH flavors, by
// character, so a low-honour competitor turns bitter (a light grudge) while everyone else becomes a worthy rival (an
// appreciation). The engine keeps the tally and plants the aftermath; this only decides what the numbers mean, so
// the same repeated rivalry can read as friendly or sour depending purely on who the lord is.

namespace CalradiaRemembers.Logic.Tournaments
{
   /// <summary>What a standing tournament rivalry has become between a lord and the player.</summary>
   public enum TournamentRivalryFlavor
   {
      /// <summary>Not yet a rivalry: too few head-to-heads to count.</summary>
      None,

      /// <summary>A worthy, good-natured rivalry: mutual respect that warms into an appreciation.</summary>
      Friendly,

      /// <summary>A sour rivalry: a low-honour competitor who nurses the contest as a light grudge.</summary>
      Bitter
   }

   /// <summary>Decides when repeated arena meetings become a rivalry, and of which flavor.</summary>
   public static class TournamentRivalryPolicy
   {
      /// <summary>Head-to-head meetings (wins + losses vs the player) before a rivalry crystallizes. Tuning.</summary>
      public const int RivalryThreshold = 3;

      /// <summary>The warmth a worthy-opponent rivalry plants (a light appreciation). Tuning.</summary>
      public const int FriendlyMagnitude = 20;

      /// <summary>
      ///   The cold edge a bitter rivalry plants: a LIGHT grudge, a sporting sourness, never a blood feud. Tuning.
      ///   <para>
      ///     Read this against <c>GrudgePolicy.MagnitudeFloor</c> (15) before changing it. That floor is
      ///     trait-modulated (+5 per Mercy level, -5 per Calculating level), so 18 forms a grudge on an unforgiving
      ///     or scheming lord and is deliberately forgiven by a merciful one. That is the intended reading: the
      ///     rivalry itself still crystallizes and colours what they say, mercy only declines to nurse it as a
      ///     grievance. Lower this and a bitter rivalry stops leaving any mark at all.
      ///   </para>
      /// </summary>
      public const int BitterMagnitude = 18;

      /// <summary>
      ///   The rivalry's flavor from the running tally and the lord's character. Below the threshold there is no
      ///   rivalry yet. At or above it, a low-honour competitor turns bitter; everyone else becomes a worthy rival.
      ///   Win/loss balance does not decide the flavor: character does (the same as the per-match Tone ruling).
      /// </summary>
      public static TournamentRivalryFlavor Evaluate(int playerWins, int heroWins, TournamentTraitFacts traits)
      {
         if (playerWins + heroWins < RivalryThreshold) return TournamentRivalryFlavor.None;

         return (traits?.Honor ?? 0) <= -1 ? TournamentRivalryFlavor.Bitter : TournamentRivalryFlavor.Friendly;
      }
   }
}
