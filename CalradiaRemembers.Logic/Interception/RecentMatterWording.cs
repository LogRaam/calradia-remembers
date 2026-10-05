// Code written by Gabriel Mailhot, 05/10/2026.
// How the matter a character comes to raise is quoted in the meeting block. fkasad (Nexus, 05/10/2026): the block
// speaks to the character as "you", and a lived memory is written to the player ("You freed me..."): pasted as it was,
// the model read its "you" as the character and Dorin claimed to have freed the man who freed him. A lived memory is
// quoted as the character's own words, with whose "you" is whose said beside it.

namespace CalradiaRemembers.Logic.Interception
{
   /// <summary>What the matter's clause is written from.</summary>
   public sealed class RecentMatterWordingFacts
   {
      public string Summary { get; init; }

      public RecentMatterRegister Register { get; init; }

      /// <summary>The player's name, for "you" in a lived memory; empty when the host has none.</summary>
      public string PlayerName { get; init; }

      /// <summary>The character raising the matter, for "I" in a lived memory.</summary>
      public string CharacterName { get; init; }
   }

   /// <summary>Words the clause naming the matter a character raises. Pure.</summary>
   public static class RecentMatterWording
   {
      public static string MatterClause(RecentMatterWordingFacts f)
      {
         if (f == null || string.IsNullOrWhiteSpace(f.Summary)) return "";
         string summary = f.Summary.Trim().TrimEnd('.');
         if (f.Register != RecentMatterRegister.LivedMemory) return $" The matter: {summary}.";

         string player = string.IsNullOrWhiteSpace(f.PlayerName) ? "the player" : f.PlayerName.Trim();
         string self = string.IsNullOrWhiteSpace(f.CharacterName) ? "you" : $"you, {f.CharacterName.Trim()}";

         return $" The matter, in your own memory's words (in them, \"you\" is {player} and \"I\" is {self}): \"{f.Summary.Trim()}\"";
      }
   }
}
