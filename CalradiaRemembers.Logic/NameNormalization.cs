// Code written by Gabriel Mailhot, 05/07/2026.
// Pure: player feedback — typing "Heloise" for a hero named "Héloïse the Finger" silently failed to match.
// Most players cannot (or will not) type accented characters, so name matching must fold them away first.

#region

using System;
using System.Globalization;
using System.Text;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Folds a name (or any free-text token) down to a plain, lowercase, diacritic-free form so that
    ///   "Héloïse" and "heloise" compare equal. Used at every name-matching boundary that takes free-typed
    ///   player text: the console name resolver and the in-conversation third-party name scanner.
    /// </summary>
    public static class NameNormalization
    {
        /// <summary>
        ///   Lowercases <paramref name="s" /> (invariant culture) and strips diacritics via Unicode NFD
        ///   decomposition, dropping every <see cref="UnicodeCategory.NonSpacingMark" /> combining character,
        ///   then recomposes (NFC) what remains. "Héloïse" folds to "heloise"; "François" to "francois"; an
        ///   already-plain name is only lowercased. Null or empty input returns "" (never null), so callers
        ///   can safely feed the result straight into further string operations.
        /// </summary>
        public static string Fold(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";

            string decomposed = s.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposed.Length);

            foreach (char c in decomposed)
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
