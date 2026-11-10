// Code written by Gabriel Mailhot, 10/11/2026.
// What becomes of an item handed to someone who rides in the player's own company and draws on its stores.
//
// Aporo (Discord, 10/11/2026): "I tried to equip my companion through chat, but character took item and never equipped
// it, it happened 2 out of 3 times, even when i told companion to equip it, could it worked more like a gift?" Read in
// the code: give_item took the item out of the party's stores, and when the companion did not put it on it went nowhere
// at all; when they did, the piece it replaced was overwritten and lost too. For a lord a gift leaves the company, and
// that is right; a companion riding with the player shares its stores, so what they do not wear never left. Gabriel
// (10/11/2026): "go".
//
// So, for someone sharing the stores: the piece put on is kept, the rest goes back to the stores, and the piece it
// replaced goes back too. Only what is kept is a gift (trust, a remembered kindness): an item that comes straight back
// must not buy regard each time it is handed over.

namespace CalradiaRemembers.Logic
{
   /// <summary>One good handed over to someone who shares the player's stores. One fact per line.</summary>
   public sealed class CompanyGiftFacts
   {
      /// <summary>How many of the good were handed over.</summary>
      public int Count { get; init; }

      /// <summary>They put one of it on (a slot takes one piece).</summary>
      public bool PutOn { get; init; }

      /// <summary>Putting it on took the place of a piece they were wearing.</summary>
      public bool ReplacedAPiece { get; init; }
   }

   /// <summary>Where the good ends up.</summary>
   public sealed class CompanyGiftOutcome
   {
      /// <summary>How many go back to the company's stores.</summary>
      public int BackToStores { get; init; }

      /// <summary>The piece they took off goes back to the stores.</summary>
      public bool ReplacedPieceToStores { get; init; }

      /// <summary>How many count as a gift (kept by them).</summary>
      public int KeptAsGift { get; init; }
   }

   /// <summary>Settles a good handed to someone who shares the player's stores. Pure.</summary>
   public static class CompanyGiftPolicy
   {
      /// <summary>The piece put on is kept; every other one, and the piece it replaced, goes back to the stores.</summary>
      public static CompanyGiftOutcome Settle(CompanyGiftFacts facts)
      {
         if (facts == null || facts.Count <= 0) return new CompanyGiftOutcome();

         int kept = facts.PutOn ? 1 : 0;

         return new CompanyGiftOutcome {
            KeptAsGift = kept,
            BackToStores = facts.Count - kept,
            ReplacedPieceToStores = facts.PutOn && facts.ReplacedAPiece
         };
      }
   }
}
