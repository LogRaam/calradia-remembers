// Code written by Gabriel Mailhot, 17/11/2026.
// Step 2 of the Bellum Civile / AI Rebel Relay request (17/11/2026): "Full event record -> persistent storage -> relevance
// filtering -> compact NPC-facing summary -> full details when genuinely needed". A fed event's full text (Details) is kept
// outside the save; a character who already knows the event hears, when the player's words bear on it, the few passages of
// that text that answer them, never the whole, and never for an event the character does not know. Gabriel: "continu".
// Pure: the host hands the events the character knows (already through the awareness gate) and the player's line.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.StoryArchive;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>One event a character knows that has a full text.</summary>
   public sealed class EventDetailSource
   {
      /// <summary>The event's short line, as the character knows it.</summary>
      public string Summary { get; init; }

      /// <summary>The full text kept for it.</summary>
      public string Details { get; init; }

      /// <summary>How the character holds it: "" (well known) or a hedge such as "(heard secondhand) ".</summary>
      public string Hedge { get; init; }
   }

   /// <summary>What the host knows for one turn.</summary>
   public sealed class EventDetailRecallFacts
   {
      /// <summary>The events the character knows that have a full text.</summary>
      public IReadOnlyList<EventDetailSource> Events { get; init; }

      /// <summary>What the player just said.</summary>
      public string Query { get; init; }

      /// <summary>How many characters of passages at most; zero or less recalls nothing.</summary>
      public int BudgetChars { get; init; } = EventDetailRecall.DefaultBudgetChars;
   }

   /// <summary>The passages of one event's full text that bear on the turn.</summary>
   public sealed class EventDetailPassage
   {
      /// <summary>The event's short line.</summary>
      public string Summary { get; init; }

      /// <summary>How the character holds it.</summary>
      public string Hedge { get; init; }

      /// <summary>The passages, in the order the text gives them.</summary>
      public IReadOnlyList<string> Passages { get; init; }
   }

   /// <summary>Picks, from the full texts of events a character knows, the passages that answer the player's words. Pure.</summary>
   public static class EventDetailRecall
   {
      /// <summary>TUNE: about four hundred tokens a turn at most, spent only when the player's words bear on a known event.</summary>
      public const int DefaultBudgetChars = 1600;

      /// <summary>TUNE: a passage is a paragraph, or a run of its sentences up to this length.</summary>
      public const int PassageChars = 450;

      /// <summary>The passages to tell, best first by event; empty when the player's words bear on no full text.</summary>
      public static IReadOnlyList<EventDetailPassage> Find(EventDetailRecallFacts facts)
      {
         var none = new List<EventDetailPassage>();
         if (facts?.Events == null || facts.BudgetChars <= 0) return none;

         var query = new HashSet<string>(LongMemoryRecall.Tokens(facts.Query));
         if (query.Count == 0) return none;

         var chunks = new List<(int Event, int Order, string Text, List<string> Tokens)>();
         for (var e = 0; e < facts.Events.Count; e++)
         {
            EventDetailSource source = facts.Events[e];
            if (string.IsNullOrWhiteSpace(source?.Details)) continue;

            List<string> pieces = Split(source.Details);
            for (var i = 0; i < pieces.Count; i++)
               chunks.Add((e, i, pieces[i], LongMemoryRecall.Tokens(pieces[i]).ToList()));
         }

         if (chunks.Count == 0) return none;

         double avgLen = chunks.Average(c => (double) Math.Max(1, c.Tokens.Count));
         Dictionary<string, int> docFreq = query.ToDictionary(t => t, t => chunks.Count(c => c.Tokens.Contains(t)));
         var chosen = new List<(int Event, int Order, string Text)>();
         var spent = 0;
         foreach (var c in chunks.Select(c => (c.Event, c.Order, c.Text, Score: LongMemoryRecall.Bm25(c.Tokens, query, docFreq, chunks.Count, avgLen)))
                                 .Where(c => c.Score > 0).OrderByDescending(c => c.Score))
         {
            if (spent + c.Text.Length > facts.BudgetChars) continue;

            spent += c.Text.Length;
            chosen.Add((c.Event, c.Order, c.Text));
         }

         return chosen.GroupBy(c => c.Event)
                      .Select(g => new EventDetailPassage {
                         Summary = facts.Events[g.Key].Summary, Hedge = facts.Events[g.Key].Hedge ?? "",
                         Passages = g.OrderBy(c => c.Order).Select(c => c.Text).ToList()
                      })
                      .ToList();
      }

      /// <summary>
      ///   The block lines for the "what you know that touches on this" section: the event, then its passages, quoted as
      ///   what word brought. Null when there is nothing.
      /// </summary>
      public static string Render(IReadOnlyList<EventDetailPassage> passages)
      {
         if (passages == null || passages.Count == 0) return null;

         return string.Join("\n", passages.Select(p =>
            $"- {p.Hedge}Of {(p.Summary ?? "").TrimEnd('.', ' ')}, word has brought you more: {string.Join(" ... ", p.Passages)}"));
      }

      #region private

      private static List<string> Split(string text)
      {
         var pieces = new List<string>();
         foreach (string paragraph in text.Replace("\r\n", "\n").Split(new[] {'\n'}, StringSplitOptions.RemoveEmptyEntries))
         {
            string p = paragraph.Trim();
            if (p.Length == 0) continue;
            if (p.Length <= PassageChars)
            {
               pieces.Add(p);

               continue;
            }

            var current = new System.Text.StringBuilder();
            foreach (string sentence in Sentences(p))
            {
               if (current.Length > 0 && current.Length + 1 + sentence.Length > PassageChars)
               {
                  pieces.Add(current.ToString());
                  current.Clear();
               }

               if (current.Length > 0) current.Append(' ');
               current.Append(sentence.Length > PassageChars ? sentence.Substring(0, PassageChars) : sentence);
            }

            if (current.Length > 0) pieces.Add(current.ToString());
         }

         return pieces;
      }

      private static IEnumerable<string> Sentences(string paragraph)
      {
         var start = 0;
         for (var i = 0; i < paragraph.Length; i++)
            if ((paragraph[i] == '.' || paragraph[i] == '!' || paragraph[i] == '?') && (i + 1 == paragraph.Length || paragraph[i + 1] == ' '))
            {
               yield return paragraph.Substring(start, i + 1 - start).Trim();
               start = i + 1;
            }

         if (start < paragraph.Length && paragraph.Substring(start).Trim().Length > 0) yield return paragraph.Substring(start).Trim();
      }

      #endregion
   }
}
