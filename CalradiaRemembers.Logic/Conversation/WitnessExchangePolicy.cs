// Code written by Gabriel Mailhot, 20/10/2026.
//
// WHY THIS EXISTS. Lordfadooboo (Nexus, 20/10/2026), meeting Apolanea on the road with a companion invited in: "when an
// ai sends a message that ends in a question, then a bystander will say something. then the first ai will respond to
// that bystander and then ask me the same question it asked in the last message, then in rare occasion [...] a third time
// ask me that same question again, all before ive had an opportunity to answer it in the first place."
//
// The 15C exchange (RunNpcWitnessExchangeAsync) re-calls the main character after a bystander speaks, up to the MCM's
// cross-talk rounds, and nothing looked at the character's own line first. A line that ends on a question to the player
// has handed the floor to the player: the beat is the player's to answer, and every further round only re-asks it. So
// the exchange stops there and the bystander's line stands. A question put to a bystander by name keeps the exchange
// going, since that bystander is the one who can answer it.
//
// Failure modes, both mild: a question to a bystander who is not named stops the exchange (the player answers or waits,
// which is what the game did before cross-talk existed); a statement never stops it.

#region

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

#endregion

namespace CalradiaRemembers.Logic.Conversation
{
   /// <summary>Decides whether the bystander exchange must stop and leave the floor to the player.</summary>
   public static class WitnessExchangePolicy
   {
      private static readonly Regex StageDirection = new Regex(@"\*[^*]*\*", RegexOptions.Compiled);

      /// <summary>
      ///   True when <paramref name="mainLine" />, the main character's last line, ends on a question whose closing sentence
      ///   names none of <paramref name="witnessNames" />: a question to the player, who must now be let to answer it.
      /// </summary>
      public static bool WaitsForThePlayer(string mainLine, IEnumerable<string> witnessNames)
      {
         if (!EnsembleFloorPolicy.EndsOnAQuestion(mainLine)) return false;

         string question = ClosingSentence(mainLine);
         foreach (string name in witnessNames ?? Array.Empty<string>())
         {
            if (string.IsNullOrWhiteSpace(name)) continue;

            // The FIRST word of the name, as EnsembleFloorPolicy reads it: "Ira", rarely "Ira of Pethros".
            string first = name.Trim().Split(' ')[0];
            if (first.Length < 2) continue;

            if (Regex.IsMatch(question, $@"\b{Regex.Escape(first)}\b", RegexOptions.IgnoreCase)) return false;
         }

         return true;
      }

      // The last sentence of the spoken words: a bystander named earlier in the line ("Ira is right. But what will you
      // do?") is not the one being asked.
      private static string ClosingSentence(string line)
      {
         string spoken = StageDirection.Replace(line, " ").Trim().TrimEnd('"', '\'', '”', '’', ')', ']', ' ', '?');
         int cut = spoken.LastIndexOfAny(new[] {'.', '!', '?', '\n'});

         return cut < 0 ? spoken : spoken.Substring(cut + 1);
      }
   }
}
