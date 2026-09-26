// Code written by Gabriel Mailhot, 25/09/2026.
// The memory a new spouse keeps of the wedding itself. zhenja1, 25/09/2026: he accepted a vanilla marriage offer, went
// to fetch his bride, and at their first word she spoke "almost like she'd been part of my house the whole time". CR
// wrote nothing on her when the marriage happened, and a spouse with no memories is told "you are already married and
// share a life", a line written for campaigns that START with a spouse. fkasad, the same day: the bride should be
// prepared for that first encounter. This is that preparation, as a fact she remembers rather than a script.

namespace CalradiaRemembers.Logic
{
   /// <summary>What is known about a match at the moment of the wedding.</summary>
   public sealed class WeddingFacts
   {
      /// <summary>The player's name, as every memory line names him (the character's "you" is the character).</summary>
      public string PlayerName { get; set; } = "";

      /// <summary>
      ///   The spouse already has memories of the player: they have talked, so whatever brought them together is in
      ///   those memories and the wedding needs no story of its own.
      /// </summary>
      public bool HaveSharedHistory { get; set; }

      /// <summary>The engine says the two have met, even if nothing was said that CR recorded.</summary>
      public bool HaveMet { get; set; }

      /// <summary>The spouse comes from another clan, so the match was one between two houses.</summary>
      public bool FromAnotherHouse { get; set; }
   }

   /// <summary>Words the new spouse's memory of the wedding.</summary>
   public static class WeddingMemoryText
   {
      /// <summary>
      ///   The line for the spouse's history. When they have no history, it says how little they know each other, so
      ///   the model speaks as a newlywed instead of inventing years of married life.
      /// </summary>
      public static string For(WeddingFacts facts)
      {
         string player = string.IsNullOrWhiteSpace(facts?.PlayerName) ? "the player" : facts.PlayerName;

         if (facts == null || facts.HaveSharedHistory) return $"Were wed to {player}.";

         string match = facts.FromAnotherHouse ? "in a match agreed between your two houses" : "in a match made for you";

         return facts.HaveMet
            ? $"Were wed to {player} {match}. You had met, but you hardly know each other yet: you are newlyweds."
            : $"Were wed to {player} {match}, before the two of you had ever spoken. You are newlyweds and still strangers to each other.";
      }
   }
}
