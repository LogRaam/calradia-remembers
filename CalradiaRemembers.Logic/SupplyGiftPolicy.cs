// Code written by Gabriel Mailhot, 26/10/2026.
// froggyluv (Nexus, 26/10/2026): in a famine, lords ask him for 30-50 bags of grain; he delivers, "sometimes the flavor
// text will say i delivered but the NPC still complain of my broken promises". Only the chat window's bag button settled
// a supply task; grain handed over in words (give_item) or through the manual Give menu left the party and the task stayed
// open, to expire as a broken promise. This decides what such a gift comes to, the way a tale's typed hand-over already
// does (TaleItemPolicy.ReadGift): the goods the task asks for, carried in full, settle it; short of the count, nothing
// leaves, as with the bag.

namespace CalradiaRemembers.Logic
{
   /// <summary>What a gift of goods comes to when the receiver has a supply task outstanding with the player.</summary>
   public enum SupplyGiftReading
   {
      /// <summary>No supply task asks for these goods: a plain gift.</summary>
      NotTheTask,

      /// <summary>The party carries the count the task asks: exactly that leaves, and the task is done.</summary>
      Settles,

      /// <summary>The party carries less than the task asks: nothing leaves, and the task waits.</summary>
      Short
   }

   /// <summary>The facts behind a gift of goods to someone with a supply task outstanding.</summary>
   public sealed class SupplyGiftFacts
   {
      /// <summary>The receiver has a supply task outstanding whose category these goods answer.</summary>
      public bool TaskAsksTheseGoods { get; init; }

      /// <summary>How many of these goods the player's party carries.</summary>
      public int Held { get; init; }

      /// <summary>How many the task asks for.</summary>
      public int Asked { get; init; }
   }

   /// <summary>Reads a gift of goods against the receiver's outstanding supply task.</summary>
   public static class SupplyGiftPolicy
   {
      /// <summary>Settles when the task asks for these goods and the party carries the count; short, nothing leaves.</summary>
      public static SupplyGiftReading Read(SupplyGiftFacts f)
      {
         if (f == null || !f.TaskAsksTheseGoods || f.Asked <= 0) return SupplyGiftReading.NotTheTask;

         return f.Held >= f.Asked ? SupplyGiftReading.Settles : SupplyGiftReading.Short;
      }
   }
}
