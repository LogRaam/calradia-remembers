// Code written by Gabriel Mailhot, 12/08/2026.

#region

using System.Collections.Generic;
using System.Text.RegularExpressions;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>
   ///   Advisory, dependency-light structural signals for the roleplay model bench (cr.rp_bench). Each is a
   ///   soft "eyeball" metric dumped into the readable report so a user swapping the roleplay model in MCM can
   ///   rank models at a glance without re-reading every transcript. NONE of these is a hard gate: the user is
   ///   the judge, these only point the eye at the turns worth reading.
   /// </summary>
   public static class RoleplayBenchChecks
   {
      /// <summary>
      ///   Phrases that read as a model breaking character: an out-of-fiction refusal or moralizing aside. A
      ///   Hardcore-scene model that emits any of these must be FLAGGED, not silently mis-scored as merely poor
      ///   prose: this is the key ranking signal. Exposed so the tests can assert coverage without re-typing the
      ///   literals.
      /// </summary>
      internal static readonly string[] RefusalPhrases = {
         "as an ai",
         "as a language model",
         "i can't",
         "i cannot",
         "i can not",
         "i am not able",
         "i'm not able",
         "i won't",
         "content policy",
         "i must decline",
         "i'm sorry, but i",
         "not comfortable",
         "cannot continue",
         "unable to continue"
      };

      /// <summary>
      ///   Real-world religion/history tokens the setting forbids. Kept deliberately conservative to avoid false
      ///   positives (Calradia uses the plural "gods", so a bare "god" is NOT listed; only the phrases "god bless"
      ///   and "oh my god" are). Advisory only. Exposed so the tests can assert coverage without re-typing the
      ///   literals.
      /// </summary>
      internal static readonly string[] EarthReferenceTerms = {
         "jesus",
         "christ",
         "rome",
         "roman",
         "napoleon",
         "bible",
         "allah",
         "buddha",
         "god bless",
         "oh my god"
      };

      /// <summary>
      ///   Counts the NPC lines that VERBATIM-repeat (case- and whitespace-insensitive) an EARLIER NPC line in the
      ///   same scene. Verbatim repetition was the exact weak-model failure (glm-5.2); a good model scores 0.
      ///   Empty/whitespace lines never count as a repeat.
      /// </summary>
      public static int HasVerbatimRepeat(IReadOnlyList<string> npcLines)
      {
         if (npcLines == null) return 0;

         var seen = new HashSet<string>();
         var repeats = 0;
         foreach (string line in npcLines)
         {
            string norm = Normalize(line);

            if (norm.Length == 0) continue;
            if (!seen.Add(norm)) repeats++;
         }

         return repeats;
      }

      /// <summary>
      ///   True when the text reads as a model refusal / out-of-fiction moralizing / broken character: a
      ///   case-insensitive scan for any <see cref="RefusalPhrases" />. At Hardcore a censoring model must be
      ///   flagged rather than mis-read as bad prose, which is why this drives the report's headline signal.
      /// </summary>
      public static bool LooksLikeRefusal(string text)
      {
         if (string.IsNullOrWhiteSpace(text)) return false;

         string lower = text.ToLowerInvariant();
         foreach (string phrase in RefusalPhrases)
            if (lower.Contains(phrase))
               return true;

         return false;
      }

      /// <summary>
      ///   True when the text references real-world religion/history the world forbids (a word-boundary scan of
      ///   the conservative <see cref="EarthReferenceTerms" /> list). ADVISORY: kept narrow so Calradia's own
      ///   plural "gods" never trips it, and so "roman" does not fire on "romantic" (the word boundary guards it).
      /// </summary>
      public static bool MentionsEarthReference(string text)
      {
         if (string.IsNullOrWhiteSpace(text)) return false;

         string lower = text.ToLowerInvariant();
         foreach (string term in EarthReferenceTerms)
            if (Regex.IsMatch(lower, "\\b" + Regex.Escape(term) + "\\b"))
               return true;

         return false;
      }

      #region private

      /// <summary>Lower-cases, trims, and collapses every whitespace run to a single space, for a verbatim compare.</summary>
      private static string Normalize(string text)
      {
         if (string.IsNullOrWhiteSpace(text)) return "";

         return Regex.Replace(text.Trim().ToLowerInvariant(), "\\s+", " ");
      }

      #endregion
   }
}
