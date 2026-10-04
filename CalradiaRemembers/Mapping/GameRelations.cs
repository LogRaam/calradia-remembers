// Code written by Gabriel Mailhot, 04/10/2026.
// The game's relations with the player that a character's standing is read from (GameRelationReading decides what
// they mean). In the base game Hero.GetRelationWithPlayer() swaps a clan member for the clan's leader, so it was the
// clan's standing; under a per-hero relation mod (tashmetu's Personal Relations, 04/10/2026) it is the character's own.
// Read both, so the clan's standing is always the leader's.

#region

using CalradiaRemembers.Logic;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Mapping
{
   /// <summary>Reads a character's own relation with the player and, when there is another to read, their clan leader's.</summary>
   public static class GameRelations
   {
      public static GameRelationFacts Of(Hero hero)
      {
         if (hero == null) return new GameRelationFacts();

         Hero leader = hero.Clan?.Leader;
         bool anotherLeader = leader != null && leader != hero && leader != Hero.MainHero && hero.Clan != Clan.PlayerClan;

         return new GameRelationFacts {
            OwnRelation = (int) hero.GetRelationWithPlayer(),
            LeaderRelation = anotherLeader ? (int) leader.GetRelationWithPlayer() : (int?) null
         };
      }
   }
}
