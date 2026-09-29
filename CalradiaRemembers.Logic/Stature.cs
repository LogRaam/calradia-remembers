// Code written by Gabriel Mailhot, 28/09/2026.
// Rank as a type. Gabriel (28/09/2026): "oui tu peux le faire, ca va rendre l'architecture meilleure." The engine-facts
// map showed Clan.Tier read by 28 rules under five names (a prestige score, a stature, a station, a rank, plain tiers),
// each reading it from the game itself and each keeping its own marks. This is the one home for both halves: the one
// place the mod reads rank from the game is StatureReader, and the marks the rules already used are named here.
//
// The rungs are the marks the code ALREADY decided on. Nothing was re-tuned in the move:
//
//   Established    1   a lord of an established house (the duel's station 1; below it, "no standing" for a letter)
//   LandedNoble    3   genuine nobility with a fief (EncounterContext.PlayerClanTier's own word; the peace mediator)
//   GreatHouse     4   a great house (a famous foe in battle, the duel's station 2, and whom a ruler writes to herself)
//
// A ruler stands above every house, whatever its tier: the duel's station 3, and a letter from the throne.

namespace CalradiaRemembers.Logic
{
   /// <summary>A person's (or a house's) standing in the realm: their clan's tier, and whether they rule.</summary>
   public readonly struct Stature
   {
      /// <summary>A lord of an established house: the duel's first station, and the least standing a letter assumes.</summary>
      public const int EstablishedTier = 1;

      /// <summary>Genuine nobility with a fief: enough to be heard between realms (peace mediation).</summary>
      public const int LandedNobleTier = 3;

      /// <summary>A great house: a famous foe, the duel's second station, and one a ruler writes to herself.</summary>
      public const int GreatHouseTier = 4;

      public Stature(int clanTier, bool isRuler)
      {
         ClanTier = clanTier < 0 ? 0 : clanTier;
         IsRuler = isRuler;
      }

      /// <summary>The tier of the clan (0 for none).</summary>
      public int ClanTier { get; }

      /// <summary>Rules a realm (or, for a house, is its ruling clan).</summary>
      public bool IsRuler { get; }

      /// <summary>At or above <see cref="EstablishedTier" />.</summary>
      public bool IsEstablished => ClanTier >= EstablishedTier;

      /// <summary>At or above <see cref="LandedNobleTier" />.</summary>
      public bool IsLandedNoble => ClanTier >= LandedNobleTier;

      /// <summary>At or above <see cref="GreatHouseTier" />.</summary>
      public bool IsGreatHouse => ClanTier >= GreatHouseTier;

      /// <summary>
      ///   The coarse station of the duel (moved here from ChatViewModel on 28/09/2026, unchanged): a ruling monarch 3, a
      ///   great house 2, an established lord 1, a minor noble, companion or landless figure 0.
      /// </summary>
      public int Station => IsRuler ? 3 : IsGreatHouse ? 2 : IsEstablished ? 1 : 0;

      /// <summary>How many coarse stations <paramref name="lower" /> stands beneath <paramref name="higher" /> (0 for a peer or above).</summary>
      public static int StationsBelow(Stature higher, Stature lower) => higher.Station - lower.Station > 0 ? higher.Station - lower.Station : 0;

      /// <summary>How many clan tiers this stands above <paramref name="other" /> (negative when below).</summary>
      public int TiersAbove(Stature other) => ClanTier - other.ClanTier;

      public override string ToString() => IsRuler ? $"ruler, tier {ClanTier}" : $"tier {ClanTier}";
   }
}
