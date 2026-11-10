// Code written by Gabriel Mailhot, 10/11/2026.
// Plausible text for test data, learned from real text: a word chain of order two.
//
// Gabriel (10/11/2026): "peux-tu faire une commande console qui ajoute beaucoup d'historique a la partie en cours pour
// gonfler la sauvegarde?", to compare codecs and measure a large save without a long campaign (he does not play one).
// Copying real memories again and again would make the data far more repetitive than a campaign's, which flatters the
// codecs with a long window; a word chain keeps the local statistics of the real text (which words follow which) without
// repeating it verbatim. Still not the real thing: say so wherever its numbers are read.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#endregion

namespace CalradiaRemembers.Logic.Saving
{
   /// <summary>A word chain of order two learned from sample sentences; writes new sentences in their manner.</summary>
   public sealed class MarkovText
   {
      private const string Start = "\u0002";

      private readonly Dictionary<string, List<string>> _next = new Dictionary<string, List<string>>();

      /// <summary>Learns from <paramref name="samples" /> (each a sentence or a few); empty or blank samples are skipped.</summary>
      public MarkovText(IEnumerable<string> samples)
      {
         foreach (string sample in samples ?? Enumerable.Empty<string>())
         {
            string[] words = (sample ?? "").Split(new[] {' ', '\t', '\r', '\n'}, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) continue;

            string a = Start, b = Start;
            foreach (string w in words)
            {
               Add(a + " " + b, w);
               a = b;
               b = w;
            }

            Add(a + " " + b, null);
         }
      }

      /// <summary>Whether anything was learned.</summary>
      public bool IsEmpty => _next.Count == 0;

      /// <summary>
      ///   One new sentence of at most <paramref name="maxWords" /> words, drawn with <paramref name="random" /> (seed it for
      ///   the same text every time); empty when nothing was learned.
      /// </summary>
      public string Sentence(Random random, int maxWords)
      {
         if (IsEmpty || random == null || maxWords <= 0) return "";

         var sb = new StringBuilder();
         string a = Start, b = Start;
         for (var i = 0; i < maxWords; i++)
         {
            if (!_next.TryGetValue(a + " " + b, out List<string> options)) break;

            string w = options[random.Next(options.Count)];
            if (w == null) break;

            if (sb.Length > 0) sb.Append(' ');
            sb.Append(w);
            a = b;
            b = w;
         }

         return sb.ToString();
      }

      private void Add(string key, string word)
      {
         if (!_next.TryGetValue(key, out List<string> list)) _next[key] = list = new List<string>();
         list.Add(word);
      }
   }
}
