// Code written by Gabriel Mailhot, 30/10/2026.
// The letter box's console doors (Gabriel, 30/10/2026): hear the courier's sound once to learn which of the engine's two
// roads plays it, and see, open or throw what waits in the box without the map bar.

#region

using System.Collections.Generic;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Letters;
using CalradiaRemembers.Logic.Letters;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>cr.letter_sound: plays the letter box's courier sound and says which road played it (the log has the detail).</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("letter_sound", "cr")]
      public static string PlayLetterSound(List<string> args)
      {
         LetterBox.ForgetSoundSpacing(); // never ResetForSession: it would drop an envelope being written
         bool played = LetterBox.PlayArrivalSound();

         return played
            ? "The courier's sound was sent to the engine. Did you hear it? The [SOUND] line in CalradiaRemembers.log says which road played it."
            : "The courier's sound did not play. The [SOUND] line in CalradiaRemembers.log says why.";
      }

      /// <summary>cr.letterbox [show|open N|throw N]: lists the letters waiting in the box, or opens or throws the Nth.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("letterbox", "cr")]
      public static string LetterBoxCommand(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;

         IReadOnlyList<BoxedLetter> waiting = LetterBox.Waiting();
         string verb = args != null && args.Count > 0 ? args[0].ToLowerInvariant() : "list";

         if (verb == "show")
         {
            LetterBoxWindow.Open();

            return LetterBoxWindow.IsOpen ? "The letter box is open." : "The letter box could not open (a letter window is open?).";
         }

         if ((verb == "open" || verb == "throw") && args.Count > 1 && int.TryParse(args[1], out int n))
         {
            if (n < 1 || n > waiting.Count) return $"There is no letter {n}; the box holds {waiting.Count}.";

            BoxedLetter boxed = waiting[n - 1];
            if (verb == "throw")
            {
               LetterBox.Throw(boxed);

               return $"Threw {boxed.Sender.Name}'s letter away. Silenced: {(LetterBoxPolicy.ThrowSilences(boxed.Letter.Reason) ? LetterBoxPolicy.SubjectOf(boxed.Letter.Reason) : "nothing")}.";
            }

            return LetterBox.Open(boxed) ? $"Opened {boxed.Sender.Name}'s letter." : "A letter window is already open; close it first.";
         }

         if (waiting.Count == 0) return "The letter box is empty.";

         var sb = new StringBuilder($"{waiting.Count} letter(s) waiting, oldest first:\n");
         for (var i = 0; i < waiting.Count; i++)
         {
            BoxedLetter b = waiting[i];
            sb.Append($"  {i + 1}. {b.Sender.Name} ({b.Letter.Reason}), day {b.Letter.EffectiveDeliveredDay}{(b.Urgent ? ", URGENT" : "")}{(b.Awaited ? ", ribbon" : "")}\n");
         }

         return sb.Append("cr.letterbox open N reads one; cr.letterbox throw N throws it away.").ToString();
      }
   }
}
