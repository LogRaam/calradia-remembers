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
using System.Linq;
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
      ///   A line the AUTHOR reads and the model never does. tashmetu's first contribution opened both files with a
      ///   paragraph explaining cr_patch to a human reader ("appended after the mod's own text and after whatever
      ///   you keep in your Custom folder"), and every word of it went to the model on every call, because the only
      ///   lines kept out were directives. A contributor should be able to document a file without the lords of
      ///   Calradia hearing about Custom folders.
      /// </summary>
      public const string CommentPrefix = "!#";

      /// <summary>
      ///   The heading that separates a module's contribution from whatever came before it, so it is its own
      ///   section rather than a continuation of the last one.
      ///   <para>
      ///     tashmetu, 19/09/2026 again, and this one would have been invisible from outside. behavior_guidelines
      ///     ends with the per-culture blocks, "If you are from the Nords:" last, and each character is shown only
      ///     their own culture's block. A contribution appended straight after it, with no heading of its own,
      ///     BECAME part of the Nords block: every lord who was not a Nord lost it, and nothing said so. The
      ///     narrowing ends a section at the next "=== " heading, so one is placed here unless the contributor
      ///     already opened with one.
      ///   </para>
      ///   Worded as plain guidance rather than as the module's name, because the model reads it and has no use
      ///   for knowing which mod wrote the lines below it.
      /// </summary>
      public const string ContributionHeading = "=== FURTHER GUIDANCE ===";

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

            // The boundary is owed once per contribution, before its first line of text, and only when there is
            // something above it to be separated from.
            var boundaryPending = true;

            foreach (string raw in SplitLines(patch.Text))
            {
               string line = raw ?? "";
               string trimmed = line.Trim();

               if (trimmed.StartsWith(CommentPrefix, StringComparison.Ordinal)) continue;

               if (trimmed.Equals(ReplaceDirective, StringComparison.OrdinalIgnoreCase))
               {
                  lines.Clear();
                  touched = true;

                  continue;
               }

               if (trimmed.StartsWith(RemoveDirective, StringComparison.OrdinalIgnoreCase))
               {
                  string needle = trimmed.Substring(RemoveDirective.Length).Trim();

                  // An empty !remove would take the whole file with it, which is what !replace is FOR and is
                  // never what a typo meant.
                  if (needle.Length > 0)
                  {
                     lines.RemoveAll(l => l != null && l.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0);
                     touched = true;
                  }

                  continue;
               }

               if (boundaryPending && trimmed.Length > 0)
               {
                  boundaryPending = false;

                  // A contributor who opened with a heading of their own has already drawn the line; a second
                  // one straight above it would only be an empty section.
                  if (!trimmed.StartsWith("=== ", StringComparison.Ordinal) && lines.Any(l => !string.IsNullOrWhiteSpace(l)))
                  {
                     lines.Add("");
                     lines.Add(ContributionHeading);
                  }
               }

               lines.Add(line);

               // A blank line alone changes nothing a reader would notice; only real text counts as a change,
               // so a file of nothing but comments and spacing leaves the shipped bytes untouched.
               if (trimmed.Length > 0) touched = true;
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
