// Code written by Gabriel Mailhot, 12/06/2026.

#region

using System;
using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Chat
{
   /// <summary>
   ///   Heuristic detection of WHICH conversation participant the player is
   ///   addressing in a typed message. A bare mention is NOT an address — only a
   ///   stage direction (*turning to Olaf*) or a vocative ("Olaf, can you…") counts,
   ///   so the sticky addressee survives ordinary references to absent thirds.
   /// </summary>
   internal static class AddressedSpeakerDetector
   {
      // Bannerlord name connectors and address filler — never distinctive enough to
      // address by. Mirrors the stop-word set noted for ContextualNameResolver so an
      // epithet like "Hophtalamos of the Hand" is not matched on "the"/"of"/"hand".
      private static readonly HashSet<string> AddressStopWords =
         new(StringComparer.OrdinalIgnoreCase) {
            "the", "of", "and", "al", "de", "van", "von", "ibn", "bint", "fen", "hand"
         };

      /// <summary>
      ///   Returns the id of the participant the player is addressing, or null when
      ///   none is explicitly addressed (caller keeps the sticky addressee).
      /// </summary>
      public static string Detect(string text, List<Participant> participants)
      {
         if (string.IsNullOrWhiteSpace(text)) return null;
         if (participants == null || participants.Count == 0) return null;

         // 1. Stage directions (between asterisks) — strongest address signal.
         int star = text.IndexOf('*');
         while (star >= 0)
         {
            int end = text.IndexOf('*', star + 1);

            if (end < 0) break;
            string seg = text.Substring(star + 1, end - star - 1);
            string id = MatchMentioned(seg, participants);

            if (id != null) return id;
            star = text.IndexOf('*', end + 1);
         }

         // 2. Vocative — a participant name adjacent to an address cue.
         return MatchVocative(text, participants);
      }

      /// <summary>
      ///   The tokens of a name worth matching on: length ≥ 3 and not a connector
      ///   ("Hophtalamos of the Hand" → {hophtalamos}).
      /// </summary>
      public static HashSet<string> DistinctiveNameTokens(string name)
      {
         var set = new HashSet<string>();
         foreach (string t in Tokenize(name))
            if (t.Length >= 3 && !AddressStopWords.Contains(t))
               set.Add(t);

         return set;
      }

      /// <summary>One addressable participant: an id and their distinctive name tokens.</summary>
      internal readonly struct Participant
      {
         public Participant(string id, HashSet<string> tokens)
         {
            Id = id;
            Tokens = tokens;
         }

         public string Id { get; }
         public HashSet<string> Tokens { get; }
      }

      #region private

      private static string BareWord(string w)
         => w.Trim(' ', '\t', '\n', '\r', ',', '.', '?', '!', '"', '\'', ':', ';', '(', ')', '*')
             .ToLowerInvariant();

      private static string MatchMentioned(string segment, List<Participant> participants)
      {
         foreach (string w in SplitWords(segment))
         {
            string bare = BareWord(w);

            if (bare.Length < 3) continue;

            foreach (Participant p in participants)
               if (p.Tokens.Contains(bare))
                  return p.Id;
         }

         return null;
      }

      private static string MatchVocative(string text, List<Participant> participants)
      {
         // Single tokenization: one raw-word pass. Compare on the punctuation-stripped
         // word, but read the address cue from the SAME word's trailing punctuation —
         // so the two never desync (the bug a separate token array would reintroduce).
         string[] words = SplitWords(text);
         for (var i = 0; i < words.Length; i++)
         {
            string bare = BareWord(words[i]);

            if (bare.Length < 3) continue;

            bool cue = i == 0 || words[i].EndsWith(",") || words[i].EndsWith("?") || words[i].EndsWith(".") || words[i].EndsWith("!") || i + 1 < words.Length && BareWord(words[i + 1]) == "you";

            if (!cue) continue;

            foreach (Participant p in participants)
               if (p.Tokens.Contains(bare))
                  return p.Id;
         }

         return null;
      }

      private static string[] SplitWords(string s)
         => (s ?? "").Split(new[] {' ', '\t', '\n', '\r'}, StringSplitOptions.RemoveEmptyEntries);

      private static string[] Tokenize(string s)
         => (s ?? "")
            .ToLowerInvariant()
            .Split(new[] {' ', '\t', '\n', '\r', ',', '.', '?', '!', '"', '\'', ':', ';', '(', ')', '*'},
               StringSplitOptions.RemoveEmptyEntries);

      #endregion
   }
}