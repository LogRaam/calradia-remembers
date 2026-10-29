// Code written by Gabriel Mailhot, 28/10/2026.
// Who may stand in a duel of honour with the player. avalanche24 (Nexus, 28/10/2026): "A landowner with zero stats,
// challenging my level 40 polearm [...] master to duel", and "A companion who literally joined a few hours ago,
// challenging me to a duel, just because I told her [...] she should act like one and follow orders". Since v1.31.9 a
// companion and any notable counted as a peer. Gabriel (28/10/2026): the duel is a noble's affair, apart from the street
// brawl, which is a settling of scores ("je ferais la difference entre le duel qui est noble et la bagarre de rue qui est
// un reglement de compte"), and a companion only brawls ("bagarre seulement pour le compagnon").

namespace CalradiaRemembers.Logic.Duels
{
   /// <summary>What the game says of the one the player would duel.</summary>
   public sealed class DuelPeerFacts
   {
      /// <summary>A lord or a lady of a noble house (the game's Lord occupation), landed or not.</summary>
      public bool IsNoble { get; init; }

      /// <summary>A companion hired into the player's company.</summary>
      public bool IsPlayerCompanion { get; init; }

      /// <summary>A notable of a town or village: a merchant, an artisan, a landowner, a headman, a preacher, a gang leader.</summary>
      public bool IsNotable { get; init; }
   }

   /// <summary>Whether someone is of the standing a duel of honour asks.</summary>
   public static class DuelPeerPolicy
   {
      /// <summary>
      ///   Only a noble crosses blades with the player in a duel of honour. A companion settles a quarrel with their
      ///   fists, and a notable or a commoner has no honour of that kind to stake.
      /// </summary>
      public static bool IsHonourPeer(DuelPeerFacts facts) => facts != null && facts.IsNoble;
   }
}
