// Code written by Gabriel Mailhot, 31/10/2026.
// RaveN017Mod (Nexus PM, 29/10/2026): threats from lords "appear in English even though my game is in French". Five kinds of
// letter are fixed English texts the mod writes itself, with no model call, so Reply Language never reached them. Gabriel
// approved (31/10/2026, "go"): they are translated when the player opens them, as an envelope is written then, so a letter
// thrown away costs nothing; an English player costs no call; and a failed call still gives the letter, in English.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Letters
{
    /// <summary>Which letters are translated when opened, and whether the translation that came back may stand. Pure.</summary>
    public static class FixedLetterTranslationPolicy
    {
        // A number, with the separators a language may write it with ("5000", "5 000", "5,000", "5.000").
        private static readonly Regex Number = new Regex(@"\d(?:[\s.,'  ]?\d)*", RegexOptions.Compiled);

        private static readonly HashSet<string> EnglishNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "English", "Anglais", "en", "eng", "en-us", "en-gb"
        };

        /// <summary>The letter is a text the mod wrote itself, not one the model wrote in the reply language.</summary>
        public static bool IsFixedText(LetterReason reason)
            => reason is LetterReason.StanceHostility or LetterReason.StanceFavor or LetterReason.Blackmail
                or LetterReason.BastardMotherNote or LetterReason.CompanionFarewell;

        /// <summary>The fixed text already reads in this language. Reply Language is free text, so the usual ways of writing it count.</summary>
        public static bool IsEnglish(string language) => language != null && EnglishNames.Contains(language.Trim());

        /// <summary>The letter asks for a translation before the player reads it. A blank language is an English game.</summary>
        public static bool NeedsTranslation(PendingLetter letter, string language)
            => letter != null && IsFixedText(letter.Reason) && !string.IsNullOrWhiteSpace(letter.Content)
               && !string.IsNullOrWhiteSpace(language) && !IsEnglish(language);

        /// <summary>
        ///   The text the player reads: the translation, or the original when none came back or when it lost a number. A sum
        ///   or a delay in a fixed letter is the real mechanic's, so English beats a wrong figure.
        /// </summary>
        public static string TextToShow(string original, string translated)
        {
            string text = Unquoted(translated);
            if (string.IsNullOrWhiteSpace(text)) return original;

            return KeepsEveryNumber(original, text) ? text : original;
        }

        private static string Unquoted(string text)
        {
            string trimmed = text?.Trim() ?? "";

            return trimmed.Length > 1 && trimmed[0] == '"' && trimmed[trimmed.Length - 1] == '"'
                ? trimmed.Substring(1, trimmed.Length - 2).Trim()
                : trimmed;
        }

        private static bool KeepsEveryNumber(string original, string text)
        {
            List<string> found = NumbersIn(text);

            return NumbersIn(original).All(found.Remove);
        }

        private static List<string> NumbersIn(string text)
            => Number.Matches(text ?? "").Cast<Match>()
                .Select(m => new string(m.Value.Where(char.IsDigit).ToArray()))
                .ToList();
    }
}
