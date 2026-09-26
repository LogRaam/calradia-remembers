// Code written by Gabriel Mailhot, 25/09/2026.
// How the world records the player's clan entering a realm's service. tashmetu, 25/09/2026: hiring on as a
// mercenary went down the vassalage path, so the world recorded "Brennoc swore fealty to Regnum Valandorum", the
// chronicle said he "bent the knee", and Derthert told the hired sellsword "I've heard word you swore fealty". A
// mercenary sells his sword for a season; he swears nothing. The leaving side already had its own wording
// (RecordMercenaryContractEnd); this is the matching start.

namespace CalradiaRemembers.Logic
{
   /// <summary>The news line, the chronicle line and the reach of the player's clan entering a realm's service.</summary>
   public sealed class RealmServiceLine
   {
      /// <summary>The neutral line characters hear as news.</summary>
      public string Summary { get; set; }

      /// <summary>The chronicler's line for the player's own chronicle (no date; the writer prefixes it).</summary>
      public string ChronicleLine { get; set; }

      /// <summary>How far the news carries.</summary>
      public int Magnitude { get; set; }
   }

   /// <summary>Words the player's entry into a realm as what it was: an oath of fealty, or a hired contract.</summary>
   public static class RealmServiceWording
   {
      /// <summary>An oath of fealty: the realm's own business, heard widely.</summary>
      public const int VassalageMagnitude = 55;

      /// <summary>
      ///   A mercenary contract: news, but a smaller matter than an oath, and just above the contract's END (30),
      ///   which is the same service running out.
      /// </summary>
      public const int MercenaryMagnitude = 35;

      /// <summary>
      ///   The lines for the player's clan joining <paramref name="kingdomName" /> under <paramref name="rulerName" />,
      ///   as a hired company when <paramref name="asMercenary" />, else as a sworn vassal.
      /// </summary>
      public static RealmServiceLine Joined(bool asMercenary, string playerName, string kingdomName, string rulerName)
      {
         string ruler = string.IsNullOrWhiteSpace(rulerName) ? "the crown" : rulerName;

         return asMercenary
            ? new RealmServiceLine {
               Summary = $"{playerName} took service with {kingdomName} as a mercenary captain.",
               ChronicleLine = $"{playerName} hired out sword and company to {ruler} of {kingdomName}, for coin and not for oath",
               Magnitude = MercenaryMagnitude
            }
            : new RealmServiceLine {
               Summary = $"{playerName} swore fealty to {kingdomName}.",
               ChronicleLine = $"{playerName} bent the knee and swore fealty to {ruler} of {kingdomName}",
               Magnitude = VassalageMagnitude
            };
      }
   }
}
