// Code written by Gabriel Mailhot, 10/10/2026.
// Where the player may hire a courier to send a letter. raphareish (Nexus, 10/10/2026): "sending letters from castles
// and villages?" Until then the only door was the town tavern keeper, so castles and villages, which have no tavern,
// had none, though the courier itself can ride from any gate. A town keeps its tavern keeper; a castle and a village
// get the same service in their own menu (fkasad: a castle often has no one to speak to, so a menu, not a person).
// Kamen (Discord, 23/11/2026): "am i crazy or i cant send letters from cities, only from castles?" Having found the
// option in a castle's menu, the player looked for it in the town's menu and it was not there. Gabriel (23/11/2026)
// ruled the town menu offers it too; the tavern keeper keeps his line.

namespace CalradiaRemembers.Logic.Letters
{
   /// <summary>The kind of place the player stands in.</summary>
   public enum CourierHirePlace
   {
      Town,
      Castle,
      Village,
      Elsewhere
   }

   /// <summary>Where the courier is offered from the settlement's own menu. Pure.</summary>
   public static class CourierHirePlacePolicy
   {
      /// <summary>A town, a castle or a village offers it in its menu (a town through its tavern keeper as well); nowhere else.</summary>
      public static bool OfferedInTheMenu(CourierHirePlace place) => place != CourierHirePlace.Elsewhere;
   }
}
