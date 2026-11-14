// Code written by Gabriel Mailhot, 14/11/2026.
// Bhelogan (Discord, 14/11/2026), with a screenshot of the conversation panel: "is there a better way to write the entries,
// so they show up looking formatted for a human instead of an LLM?" The Character section showed the backstory of
// character_overrides.json as written, and other mods write their own blocks there between markers they find again later
// ("[DNB_PHYSICAL_DESCRIPTION]...[/DNB_PHYSICAL_DESCRIPTION]", "[HSAI_SECRET:hs_secret_...]"), so the player read the
// markers, and a secret before discovering it. This decides what of such a text the panel shows; the model still reads it
// whole.

#region

using System.Text.RegularExpressions;

#endregion

namespace CalradiaRemembers.Logic.Character
{
    /// <summary>What of an authored text the conversation panel shows a human.</summary>
    public static class PanelTextCleaner
    {
        // A marker name: capitals, digits and underscores, three or more, as mods write them ("DNB_PHYSICAL_DESCRIPTION").
        private const string Name = "[A-Z][A-Z0-9_]{2,}";

        private static readonly Regex SecretBlock = new Regex(
            @"\[(?<n>[A-Z][A-Z0-9_]*(?:SECRET|PRIVATE|HIDDEN)[A-Z0-9_]*)\].*?(?:\[/\k<n>\]|$)",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);

        private static readonly Regex Token = new Regex(@"\[" + Name + @":[^\]\r\n]*\]", RegexOptions.CultureInvariant);

        private static readonly Regex Marker = new Regex(@"\[/?" + Name + @"\]", RegexOptions.CultureInvariant);

        private static readonly Regex Spaces = new Regex(@"[ \t]{2,}", RegexOptions.CultureInvariant);

        private static readonly Regex BlankLines = new Regex(@"\n\s*\n\s*\n+", RegexOptions.CultureInvariant);

        /// <summary>
        ///   The text as the panel shows it: a block whose marker names a secret (SECRET, PRIVATE or HIDDEN in its name) is left
        ///   out whole, to its closing marker or, unclosed, to the end; a reference token ("[NAME:value]") is left out; any other
        ///   marker ("[NAME]", "[/NAME]") is removed and the words between are kept. Ordinary brackets ("[sic]") stay. Null
        ///   stays null.
        /// </summary>
        public static string ForPanel(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            string cleaned = SecretBlock.Replace(text, "");
            cleaned = Token.Replace(cleaned, "");
            cleaned = Marker.Replace(cleaned, "");
            cleaned = cleaned.Replace("\r\n", "\n");
            cleaned = Spaces.Replace(cleaned, " ");
            cleaned = BlankLines.Replace(cleaned, "\n\n");

            return cleaned.Trim();
        }
    }
}
