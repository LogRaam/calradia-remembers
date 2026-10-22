// Code written by Gabriel Mailhot, 22/10/2026.
// Gabriel (21/10/2026), after reading another LLM mod that tracks the player's notoriety per culture: "2 et 3
// m'interesse". Today a character is told what deeds of the player reached them, and a lord's opening tone reads the
// player's renown, but nobody is told whether they have even heard the NAME: a famous king met by a village notable and
// an unknown upstart both get "you have never met this person". This is the pure rule that says how far the player's
// name has travelled to this one character, and what it carries once it has. Nothing is saved: it is read when the
// character speaks (player experience over simulation). Decided overnight on Gabriel's leave; every number is a TUNE.

namespace CalradiaRemembers.Logic.Social
{
   /// <summary>How far the player's name has reached one character who does not know them in person.</summary>
   public enum FameBand
   {
      /// <summary>They have never heard the name.</summary>
      Unknown,

      /// <summary>The name is familiar, little more.</summary>
      NameHeard,

      /// <summary>They know who the player is by reputation.</summary>
      ByReputation,

      /// <summary>Everyone in their world knows the name; they would know the banner.</summary>
      Famous
   }

   /// <summary>Who is hearing of the player: how widely word reaches someone of their station.</summary>
   public enum FameObserver
   {
      /// <summary>A lord or lady: the halls hear of every rising house.</summary>
      Noble,

      /// <summary>A town or village notable: hears what passes through the market.</summary>
      Notable,

      /// <summary>A wanderer or anyone else: hears only what is loud.</summary>
      Commoner
   }

   /// <summary>What the player is said to be, once known by reputation (the game's own traits for the player).</summary>
   public enum FameRepute
   {
      /// <summary>Nothing in particular is said.</summary>
      None,

      /// <summary>Merciless (the game's Mercy trait below zero).</summary>
      Merciless,

      /// <summary>Merciful (Mercy above zero).</summary>
      Merciful,

      /// <summary>Keeps their word (Honor above zero).</summary>
      Honorable,

      /// <summary>Not to be trusted (Honor below zero).</summary>
      Faithless
   }

   /// <summary>What the rule reads: the player's standing and who is hearing of it.</summary>
   public sealed class PlayerFameFacts
   {
      /// <summary>The player's clan renown, as the game keeps it.</summary>
      public float PlayerRenown { get; init; }

      /// <summary>The player rules a realm: a crown is known well past its borders.</summary>
      public bool PlayerRulesARealm { get; init; }

      /// <summary>The station of the one hearing of the player.</summary>
      public FameObserver Observer { get; init; }

      /// <summary>The observer serves the same realm as the player.</summary>
      public bool SameRealm { get; init; }

      /// <summary>The observer is of the player's own culture: their own folk talk of their own.</summary>
      public bool SameCulture { get; init; }

      /// <summary>The player's recorded deeds that took place in the observer's realm (WorldEvents involving the player).</summary>
      public int DeedsInObserversRealm { get; init; }

      /// <summary>A deed of the player has already reached this character by word (the rumour path).</summary>
      public bool HeardADeed { get; init; }

      /// <summary>The game's Mercy trait of the player (-2 to 2).</summary>
      public int PlayerMercy { get; init; }

      /// <summary>The game's Honor trait of the player (-2 to 2).</summary>
      public int PlayerHonor { get; init; }
   }

   /// <summary>How far the player's name has reached this character, and what it carries (pure).</summary>
   public sealed class PlayerFameReading
   {
      /// <summary>How far the name has reached.</summary>
      public FameBand Band { get; init; }

      /// <summary>What the name is said to carry; None below <see cref="FameBand.ByReputation" />.</summary>
      public FameRepute Repute { get; init; }
   }

   /// <summary>The rule: renown and a crown carry the name; station, realm, culture and deeds done nearby carry it further.</summary>
   public static class PlayerFamePolicy
   {
      /// <summary>TUNE: renown at which the name starts to travel (the game's clan tier 1 sits at 50).</summary>
      public const float RenownKnownInPlaces = 50f;

      /// <summary>TUNE: renown of a house of some standing (tier 2 to 3).</summary>
      public const float RenownOfStanding = 150f;

      /// <summary>TUNE: renown of a great house (tier 4).</summary>
      public const float RenownOfAGreatHouse = 400f;

      /// <summary>TUNE: renown known across Calradia (tier 5 and beyond).</summary>
      public const float RenownAcrossCalradia = 900f;

      /// <summary>TUNE: at most this many deeds done in the observer's realm add to the reach.</summary>
      public const int MaxDeedsCounted = 2;

      /// <summary>TUNE: points from which the name is heard, known by reputation, famous.</summary>
      public const int NameHeardFrom = 1, KnownOfFrom = 3, FamousFrom = 5;

      /// <summary>The reading for this character.</summary>
      public static PlayerFameReading Read(PlayerFameFacts f)
      {
         if (f == null) return new PlayerFameReading {Band = FameBand.Unknown};

         int points = RenownPoints(f.PlayerRenown)
                      + (f.PlayerRulesARealm ? 2 : 0)
                      + (f.Observer == FameObserver.Noble ? 1 : f.Observer == FameObserver.Commoner ? -1 : 0)
                      + (f.SameRealm ? 1 : 0)
                      + (f.SameCulture ? 1 : 0)
                      + System.Math.Min(System.Math.Max(f.DeedsInObserversRealm, 0), MaxDeedsCounted);

         FameBand band = points >= FamousFrom ? FameBand.Famous
            : points >= KnownOfFrom ? FameBand.ByReputation
            : points >= NameHeardFrom ? FameBand.NameHeard
            : FameBand.Unknown;
         // Whoever heard a deed of the player knows them by that much at least: fame never hides a deed.
         if (f.HeardADeed && band < FameBand.ByReputation) band = FameBand.ByReputation;

         return new PlayerFameReading {Band = band, Repute = band >= FameBand.ByReputation ? Repute(f.PlayerMercy, f.PlayerHonor) : FameRepute.None};
      }

      /// <summary>
      ///   The one line the character is given, naming the player's character; null when no line is due. The line
      ///   tells them what they know of the name, never that they met.
      /// </summary>
      public static string Line(PlayerFameReading r, string playerName)
      {
         if (r == null) return null;
         string name = string.IsNullOrWhiteSpace(playerName) ? "this person" : playerName.Trim();
         string said = Said(r.Repute);

         switch (r.Band)
         {
            case FameBand.Unknown:
               return $"You have never heard the name {name}. Whatever they claim to be, you know nothing of them but what you see.";
            case FameBand.NameHeard:
               return $"The name {name} is faintly familiar, a name passed along with others; you know nothing more of them.";
            case FameBand.ByReputation:
               return $"You know of {name} by reputation, though you have never met{said}.";
            default:
               return $"Everyone in your world has heard of {name}; you would know the banner anywhere{said}.";
         }
      }

      private static int RenownPoints(float renown)
         => renown >= RenownAcrossCalradia ? 4
            : renown >= RenownOfAGreatHouse ? 3
            : renown >= RenownOfStanding ? 2
            : renown >= RenownKnownInPlaces ? 1
            : 0;

      // The loudest of the two traits is what is said; a tie goes to mercy, the one people talk of most.
      private static FameRepute Repute(int mercy, int honor)
      {
         if (mercy == 0 && honor == 0) return FameRepute.None;
         if (System.Math.Abs(mercy) >= System.Math.Abs(honor)) return mercy < 0 ? FameRepute.Merciless : FameRepute.Merciful;

         return honor < 0 ? FameRepute.Faithless : FameRepute.Honorable;
      }

      private static string Said(FameRepute r)
      {
         switch (r)
         {
            case FameRepute.Merciless: return "; they are said to be merciless";
            case FameRepute.Merciful: return "; they are said to be merciful";
            case FameRepute.Honorable: return "; they are said to keep their word";
            case FameRepute.Faithless: return "; they are said not to keep their word";
            default: return "";
         }
      }
   }
}
