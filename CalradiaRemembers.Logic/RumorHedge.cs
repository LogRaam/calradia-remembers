// Code written by Gabriel Mailhot, 15/11/2026.
// VonLemberg (Discord, 15/11/2026, source audit of 2.8.10): "StatedAsFact influences magnitude but is not preserved as
// explicit metadata in WorldEvent. Later recipients see confidence derived from awareness, not necessarily the original
// speaker's uncertainty ... Hearsay remains hearsay, not established truth." Gabriel: "oui". Once a character's news
// carries its realm, a thing only wondered aloud reached a neighbour as "heard secondhand", word for word like a thing
// stated flatly. The speaker's own doubt now travels with the news, on every line that tells it.

namespace CalradiaRemembers.Logic
{
   /// <summary>How a piece of news that was only a guess is told. Pure.</summary>
   public static class RumorHedge
   {
      /// <summary>
      ///   The tag a guessed piece of news carries in a prompt line. It says whose doubt it is, so the character passes it
      ///   on as a guess and never as a thing done.
      /// </summary>
      public const string GuessTag = "(only a guess, as the one who first said it had it) ";

      /// <summary>The tag to put before a known event's line: <see cref="GuessTag" /> for a guess, empty otherwise.</summary>
      public static string TagFor(bool onlyAGuess) => onlyAGuess ? GuessTag : "";
   }
}
