// Code written by Gabriel Mailhot, 10/11/2026.
// Whose army a scout_army task sends the player to observe.
//
// bdionysis80 (Nexus, 10/11/2026), at war with Sturgia but not with the Nords: a council pledged to scout Hvalvik, a
// Nord port, and the game set "Scout an army of Sturgia". "Hvalvik belongs to the Nords, while the Sturgian army
// belongs to a completely different faction." The council named the place (target_settlement), but the scout_army
// binding never read it: with no hero or faction named it fell back to the giver's first enemy kingdom, so the task the
// player accepted was not the one the table spoke of. Gabriel (10/11/2026): "oui vas-y".
//
// So a named place decides the realm: an army of the place's owner, when the giver is at war with it; and when the giver
// is not (or the place cannot be found), no task at all, never another realm's army in its stead. The first-enemy
// fallback stays only for a task that named no target whatever.

namespace CalradiaRemembers.Logic.Quests
{
   /// <summary>What a scout_army proposal names, and how the named place stands. One fact per line.</summary>
   public sealed class ScoutArmyTargetFacts
   {
      /// <summary>The proposal names an army's leader (target_hero).</summary>
      public bool NamesHero { get; init; }

      /// <summary>The proposal names a realm (target_faction).</summary>
      public bool NamesFaction { get; init; }

      /// <summary>The proposal names a place (target_settlement).</summary>
      public bool NamesPlace { get; init; }

      /// <summary>The named place is a real settlement of the world.</summary>
      public bool PlaceFound { get; init; }

      /// <summary>The giver's realm is at war with the realm that holds the named place.</summary>
      public bool PlaceOwnerAtWarWithGiver { get; init; }
   }

   /// <summary>The target a scout_army task is bound to.</summary>
   public enum ScoutArmyTarget
   {
      /// <summary>The army led by the named hero.</summary>
      NamedHero,

      /// <summary>Any army of the named realm.</summary>
      NamedFaction,

      /// <summary>Any army of the realm holding the named place.</summary>
      PlaceOwner,

      /// <summary>Any army of the giver's first enemy realm (nothing was named).</summary>
      FirstEnemy,

      /// <summary>No task: the named place is not held by an enemy, or cannot be found.</summary>
      Refuse
   }

   /// <summary>Decides whose army a scout_army task concerns. Pure.</summary>
   public static class ScoutArmyTargetPolicy
   {
      /// <summary>A named hero first, then a named realm, then the named place's owner if at war; nothing named: the first enemy.</summary>
      public static ScoutArmyTarget Decide(ScoutArmyTargetFacts facts)
      {
         if (facts == null) return ScoutArmyTarget.Refuse;
         if (facts.NamesHero) return ScoutArmyTarget.NamedHero;
         if (facts.NamesFaction) return ScoutArmyTarget.NamedFaction;
         if (facts.NamesPlace) return facts.PlaceFound && facts.PlaceOwnerAtWarWithGiver ? ScoutArmyTarget.PlaceOwner : ScoutArmyTarget.Refuse;

         return ScoutArmyTarget.FirstEnemy;
      }
   }
}
