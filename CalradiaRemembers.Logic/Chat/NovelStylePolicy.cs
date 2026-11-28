// Code written by Gabriel Mailhot, 28/11/2026.
// Stage 5 of the group-scene plan, the novel's presentation. Gabriel (28/11/2026), on "la meilleure forme qui se
// rapproche de la qualité d'un roman": "oui ok j'aime ça": speech in quotation marks, gestures as plain third-person
// prose, no asterisks, the speaker's name kept as a label, the narrator's purple for narration only. An MCM choice,
// "Narrative style", Roleplay by default. The prompt teaches the style (PromptBuilder.EnableNovelStyle); this decides
// the three things the chat does with a reply in that style:
//   - whether the style applies to this conversation at all (never a captive scene, which keeps its own contract);
//   - what is shown when a model still writes *asterisks*: "*She sets down the cup.*" becomes plain prose, but a
//     first-person span stays starred, since bare first-person prose outside quotes reads as the player narrating;
//   - what part of a line is SPOKEN, for the turn-taking tests (does the line end on a question, did a bystander
//     speak): they find the speech by stripping *asterisks*, and a novel line has none, so "\"Where were you?\" She sets
//     down the cup." would end on "." and the question would be lost.

#region

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

#endregion

namespace CalradiaRemembers.Logic.Chat
{
   /// <summary>What the chat knows when it decides whether the novel style applies.</summary>
   public sealed class NovelStyleFacts
   {
      /// <summary>The MCM "Narrative style" is set to Novel.</summary>
      public bool NovelChosen { get; init; }

      /// <summary>The conversation is a captive scene (the player held, the player as captor), which keeps its own voice contract.</summary>
      public bool CaptiveScene { get; init; }
   }

   /// <summary>Decides how a reply in the novel style is shown and read.</summary>
   public static class NovelStylePolicy
   {
      // One or two asterisks either side (a model sometimes writes markdown bold), a span on one line.
      private static readonly Regex StarSpan = new Regex(@"\*{1,2}([^*\r\n]{1,400}?)\*{1,2}", RegexOptions.Compiled);

      // A span in the first person: "I", "I'm", "I'd", "I'll", "I've", "My", "Me" as its first word.
      private static readonly Regex FirstPerson = new Regex(@"^(I|I'm|I'd|I'll|I've|My|Me|Mine)\b", RegexOptions.Compiled);

      private static readonly char[] Quotes = {'"', '“', '”'};

      /// <summary>
      ///   What the action interpreter is told about a reply in the novel style. Its own prompt reads "I" as the NPC; here
      ///   the NPC's deeds are told by its name or "he"/"she", and those are its acts as surely.
      /// </summary>
      public const string InterpreterNote =
         "STYLE OF THE REPLY: it is written as a novel. The NPC's own gestures and deeds are plain third-person prose, by the "
       + "NPC's name or 'he'/'she' (not 'I'), and its speech is inside double quotation marks. A deed told that way of the NPC "
       + "is the NPC's own act, exactly as 'I' would be; 'you' is still the PLAYER.";

      /// <summary>True when the novel style applies: chosen, and not a captive scene.</summary>
      public static bool Applies(NovelStyleFacts facts)
         => facts != null && facts.NovelChosen && !facts.CaptiveScene;

      /// <summary>
      ///   The reply as the chat shows it in the novel style: each third-person *span* unwrapped into plain prose, its first
      ///   letter raised when it opens a sentence and a full stop given when it has none. A first-person span is left as
      ///   written. Text without asterisks comes back unchanged.
      /// </summary>
      public static string ForDisplay(string reply)
      {
         if (string.IsNullOrEmpty(reply) || reply.IndexOf('*') < 0) return reply;

         string shown = StarSpan.Replace(reply, m =>
         {
            string inner = m.Groups[1].Value.Trim();
            if (inner.Length == 0 || FirstPerson.IsMatch(inner)) return m.Value;

            if (OpensASentence(reply, m.Index)) inner = char.ToUpperInvariant(inner[0]) + inner.Substring(1);
            char last = inner[inner.Length - 1];
            if (last != '.' && last != '!' && last != '?' && last != ',' && last != ';' && last != ':' && last != '…' && Array.IndexOf(Quotes, last) < 0)
               inner += ".";

            return inner;
         });

         return Regex.Replace(shown, @"[ \t]{2,}", " ");
      }

      /// <summary>
      ///   The spoken part of a line for the turn-taking tests: the quoted speech, joined, when the line holds any; the line
      ///   unchanged when it holds none (a model that ignored the style, or a line all speech). Asterisk spans outside the
      ///   quotes go with the rest of the narration.
      /// </summary>
      public static string SpokenPart(string line)
      {
         if (string.IsNullOrWhiteSpace(line)) return line;

         List<string> spoken = QuotedSpans(line);

         return spoken.Count == 0 ? line : string.Join(" ", spoken);
      }

      #region private

      // The text between each opening and closing double quote (straight or curly), quotes kept. An unclosed quote at the
      // end is not speech we can bound, so it is ignored.
      private static List<string> QuotedSpans(string line)
      {
         var spans = new List<string>();
         int i = 0;
         while (i < line.Length)
         {
            int open = line.IndexOfAny(Quotes, i);
            if (open < 0) break;
            int close = line.IndexOfAny(Quotes, open + 1);
            if (close < 0) break;

            string span = line.Substring(open, close - open + 1);
            if (span.Trim(Quotes).Trim().Length > 0) spans.Add(span);
            i = close + 1;
         }

         return spans;
      }

      // A span opens a sentence when only whitespace lies between it and the start of the reply or the end of the previous
      // sentence; after a closing quote, when the speech inside ended its sentence ("Sit." she... yes; "Sit," she... no).
      private static bool OpensASentence(string text, int index)
      {
         for (int i = index - 1; i >= 0; i--)
         {
            char c = text[i];
            if (char.IsWhiteSpace(c)) continue;
            if (Array.IndexOf(Quotes, c) >= 0) return i > 0 && EndsASentence(text[i - 1]);

            return EndsASentence(c);
         }

         return true;
      }

      private static bool EndsASentence(char c) => c == '.' || c == '!' || c == '?' || c == '…';

      #endregion
   }
}
