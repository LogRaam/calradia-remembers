// Code written by Gabriel Mailhot, 10/10/2026.
// Where the player may hire a courier to send a letter. raphareish (Nexus, 10/10/2026): "sending letters from castles
// and villages?" Until then the only door was the town tavern keeper, so castles and villages, which have no tavern,
// had none, though the courier itself can ride from any gate. A town keeps its tavern keeper; a castle and a village
// get the same service in their own menu (fkasad: a castle often has no one to speak to, so a menu, not a person).

namespace CalradiaRemembers.Logic
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
      /// <summary>A castle or a village offers it in its menu; a town through its tavern keeper; nowhere else.</summary>
      public static bool OfferedInTheMenu(CourierHirePlace place) => place == CourierHirePlace.Castle || place == CourierHirePlace.Village;
   }
}
