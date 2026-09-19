// Code written by Gabriel Mailhot, 19/09/2026.
// A door for other modules into the prompt's background prose, so a bridge mod stops having to overwrite files.
//
// Asked for by tashmetu (Nexus, 19/09/2026), who is building a Calradia Remembers bridge for Bellum Civile:
//
//   "Right now I have to tell users to copy a file into your ModuleData\Custom\, which means overwriting
//    whatever is there and if a second mod ever wants to contribute prompt text, it destroys the first one's
//    work. Neither is your fault, there is just no other way in at the moment."
//
// He proposed the shape, and argued it from something already proven rather than invented: Bellum Civile uses
// the same pattern for its own policy agendas, feudal titles, realm names and mercenary name pools. Any loaded
// module ships a file at a known path, the host applies them after its own, and launcher load order decides
// precedence. Nothing is overwritten: the mod's files stay the mod's, an update does not clobber a
// contribution, and a contribution does not clobber the player's own edits.
//
// His third argument is the one that settled the format. A small bridge only appends a few lines, while a
// TOTAL CONVERSION needs to genuinely replace the geography, and one mechanism has to serve both without the
// small case being able to cause the large one by accident.
//
// THE DIRECTIVE MARKER IS "!" AND THAT IS NOT A DETAIL. The obvious design gives subtraction to a leading "-",
// and prose written for an LLM is full of lines that begin with a dash, because that is how anyone writes a
// list. A patch that quietly deleted a lord's behaviour because a contributor wrote a bullet would be a
// miserable thing to debug from the outside, so every directive is spelled out and anything else is text.

#region

using System;
using System.Collections.Generic;
using System.Text;

#endregion

namespace CalradiaRemembers.Logic.Narrative
{
   /// <summary>One module's contribution to one narrative file: where it came from, and what it says.</summary>
   public sealed class NarrativePatch
   {
      public NarrativePatch(string source, string text)
      {
         Source = source ?? "";
         Text = text ?? "";
      }

      /// <summary>The module that shipped it, for the log line: a modder debugging a bridge needs to see whose patch won.</summary>
      public string Source { get; }

      public string Text { get; }
   }

   /// <summary>
   ///   Lays other modules' contributions over one of the mod's own narrative files, in load order.
   /// </summary>
   public static class NarrativePatchComposer
   {
      /// <summary>Discards everything composed so far. The total-conversion case: this module's geography replaces Calradia's.</summary>
      public const string ReplaceDirective = "!replace";

      /// <summary>Drops every line composed so far that CONTAINS the rest of this line. The subtractive half.</summary>
      public const string RemoveDirective = "!remove ";

      /// <summary>
      ///   The mod's own <paramref name="baseText" /> with each patch applied after it, in the order given,
      ///   which the caller takes from the launcher. A patch that says nothing changes nothing.
      /// </summary>
      public static string Compose(string baseText, IEnumerable<NarrativePatch> patches)
      {
         var lines = new List<string>(SplitLines(baseText ?? ""));
         var touched = false;

         foreach (NarrativePatch patch in patches ?? new List<NarrativePatch>())
         {
            if (patch == null || string.IsNullOrWhiteSpace(patch.Text)) continue;

            touched = true;

            foreach (string raw in SplitLines(patch.Text))
            {
               string line = raw ?? "";
               string trimmed = line.Trim();

               if (trimmed.Equals(ReplaceDirective, StringComparison.OrdinalIgnoreCase))
               {
                  lines.Clear();

                  continue;
               }

               if (trimmed.StartsWith(RemoveDirective, StringComparison.OrdinalIgnoreCase))
               {
                  string needle = trimmed.Substring(RemoveDirective.Length).Trim();

                  // An empty !remove would take the whole file with it, which is what !replace is FOR and is
                  // never what a typo meant.
                  if (needle.Length > 0)
                     lines.RemoveAll(l => l != null && l.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0);

                  continue;
               }

               lines.Add(line);
            }
         }

         // NOBODY CONTRIBUTED, SO NOTHING IS TOUCHED, not even the line endings. Composing rewrites CRLF to LF
         // and collapses the blank runs a removal leaves behind, which is right when a patch has been applied
         // and wrong for the overwhelming majority of players who have no bridge mod at all: they must get back
         // exactly the bytes the mod shipped. My first draft returned a joined copy here and the test caught it
         // on the line endings alone, which was the small visible end of a real difference.
         return touched ? Trimmed(lines) : baseText ?? "";
      }

      #region private

      private static string[] SplitLines(string text)
         => text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

      /// <summary>
      ///   Joined back up without the blank runs that removing lines out of the middle of a document leaves
      ///   behind. Blank lines separate blocks in these files, so two in a row is a paragraph break and three
      ///   is a hole where something used to be.
      /// </summary>
      private static string Trimmed(List<string> lines)
      {
         var sb = new StringBuilder();
         var blanks = 0;

         foreach (string line in lines)
         {
            if (string.IsNullOrWhiteSpace(line))
            {
               blanks++;

               if (blanks > 1) continue;
            }
            else { blanks = 0; }

            sb.Append(line).Append('\n');
         }

         return sb.ToString().Trim('\n');
      }

      #endregion
   }
}
