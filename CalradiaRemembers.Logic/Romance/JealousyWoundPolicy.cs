// Code written by Gabriel Mailhot, 25/10/2026.
// What a jealous reaction does beyond the regard it costs: wound the bond (Committed falls to Estranged) and plant a
// grudge the partner nurses. Zivkyn (Nexus, 25/10/2026): "The Rhodok Twins Aela and Elae, I took them both as
// Consorts, And they both agree no jealousy, Yet one always ends up with a Greivence." Both happened on every
// intimacy, whatever she had accepted and whoever the other was. Gabriel's ruling the same day: "si elles acceptent,
// elles devraient etre plus tolerantes a la jalousie. Mais si le joueur passe beaucoup plus de temps avec une que
// l'autre, la jalousie s'applique quand meme... accepter de partager ne veut pas dire accepter etre la deuxieme."
// So a partner on a sharing track (her people's custom, or her own word) bears the player's love for ANOTHER PARTNER
// OF THE HOUSEHOLD without a wound or a grudge: the regard still pays its lighter price, and neglect is the
// competition path's business. A liaison outside the household stays a betrayal of the bond whatever she accepted
// (the 22/09/2026 boundary: what she accepted is a household, not a blank cheque).

namespace CalradiaRemembers.Logic.Romance
{
   /// <summary>What is known about one jealous reaction when deciding whether it wounds the bond and leaves a grudge.</summary>
   public sealed class JealousyWoundFacts
   {
      /// <summary>Who the jealous party is to the player and the courted (the player's partner, a secret lover, a rival...).</summary>
      public JealousStake Stake { get; init; }

      /// <summary>How far the act went: a flirt breeds doubt, only an intimacy or a marriage can wound or be nursed.</summary>
      public RomanticActTier Tier { get; init; }

      /// <summary>Her price, from <see cref="JealousyTrackPolicy" />: Betrayal, or a sharing track (custom or her word).</summary>
      public JealousyTrack Track { get; init; }

      /// <summary>
      ///   The courted is another partner of the player's household (the native spouse or a committed consort). False
      ///   for anyone else: a liaison outside the household is not what she agreed to share the player with.
      /// </summary>
      public bool CourtedIsHouseholdPartner { get; init; }
   }

   /// <summary>Whether a jealous reaction wounds the bond and whether it plants a grudge.</summary>
   public static class JealousyWoundPolicy
   {
      /// <summary>
      ///   The player's own partner on a sharing track, over another partner of the household: she bears it (the
      ///   regard still falls, more lightly). Anything else keeps its wound and its grudge.
      /// </summary>
      public static bool IsBorneWithinTheHousehold(JealousyWoundFacts f)
         => f != null && f.Stake == JealousStake.PlayerPartner && f.Track != JealousyTrack.Betrayal && f.CourtedIsHouseholdPartner;

      /// <summary>
      ///   The bond falls (Committed to Estranged): only an intimacy or a marriage, only for the player's own partner or
      ///   secret lover, and never when she bears it within the household. A flirt makes her doubt, it does not break it.
      /// </summary>
      public static bool WoundsTheBond(JealousyWoundFacts f)
         => f != null && f.Tier >= RomanticActTier.Intimacy
                      && (f.Stake == JealousStake.PlayerPartner || f.Stake == JealousStake.SecretLover)
                      && !IsBorneWithinTheHousehold(f);

      /// <summary>
      ///   A grudge she nurses: only an intimacy or a marriage, never from the secret lover (her hurt stays private),
      ///   and never when she bears it within the household.
      /// </summary>
      public static bool PlantsAGrudge(JealousyWoundFacts f)
         => f != null && f.Tier >= RomanticActTier.Intimacy && f.Stake != JealousStake.SecretLover && !IsBorneWithinTheHousehold(f);
   }
}
