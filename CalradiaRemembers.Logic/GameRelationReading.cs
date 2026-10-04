// Code written by Gabriel Mailhot, 04/10/2026.
// What the game's relation with the player means for one character (tashmetu, Nexus, 04/10/2026, Personal Relations).
// In the base game a clan member shares the leader's relation, so Hero.GetRelationWithPlayer() was the clan's standing.
// A mod may give each noble a relation of their own; then the clan's standing is the leader's, and the character's own
// is a separate fact, said apart. Neither is the mod's personal regard, which only moves through what passes in person.

namespace CalradiaRemembers.Logic
{
   /// <summary>The game's relations with the player that one character's standing is read from.</summary>
   public sealed class GameRelationFacts
   {
      /// <summary>This character's relation with the player, as the game reads it (their own, under a per-hero mod).</summary>
      public int OwnRelation { get; init; }

      /// <summary>
      ///   Their clan leader's relation with the player, or null when there is no other leader to read: they lead the
      ///   clan, have none, or belong to the player's own clan.
      /// </summary>
      public int? LeaderRelation { get; init; }
   }

   /// <summary>Reads a character's clan standing and own relation with the player. Pure.</summary>
   public static class GameRelationReading
   {
      /// <summary>The clan's standing toward the player: the leader's relation, else the character's own.</summary>
      public static int ClanStanding(GameRelationFacts f) => f?.LeaderRelation ?? f?.OwnRelation ?? 0;

      /// <summary>
      ///   The character's own relation, when it differs from the clan's standing (only under a mod that gives each
      ///   noble a relation of their own); null otherwise, so nothing more is said.
      /// </summary>
      public static int? OwnRelationIfDistinct(GameRelationFacts f)
         => f?.LeaderRelation is int leader && leader != f.OwnRelation ? f.OwnRelation : (int?) null;
   }
}
