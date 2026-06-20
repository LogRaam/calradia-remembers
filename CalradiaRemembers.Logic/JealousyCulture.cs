// Code written by Gabriel Mailhot, 20/06/2026.
// Extracted from the mod (Sprint 24 jealousy) so the monogamous/polygamous tolerance rule and the
// raw-value parsing are unit-testable: these barèmes break silently and govern the whole mechanic.

namespace CalradiaRemembers.Logic
{
    /// <summary>How strongly a romantic act elsewhere reads as a betrayal for this culture.</summary>
    public enum JealousyOutrage
    {
        Low,
        Medium,
        High
    }

    /// <summary>How strongly senior-vs-secondary partner rank drives competition jealousy.</summary>
    public enum JealousyHierarchy
    {
        Low,
        Medium,
        High
    }

    /// <summary>
    ///   A culture's parsed stance on romantic exclusivity. Drives whether a staked partner reacts
    ///   with EXCLUSIVITY jealousy (betrayal) or COMPETITION jealousy (rivalry over rank/attention
    ///   among shared partners). The defaults describe a strictly monogamous culture, so any culture
    ///   not configured — including ones added by faction mods — degrades safely to monogamy. The
    ///   engine-coupled DTO (<c>JealousyCultureProfile</c>) deserializes the raw JSON and hands the
    ///   strings to <see cref="FromRaw" />, keeping the parsing and the tolerance rule testable here.
    /// </summary>
    public sealed class JealousyCulture
    {
        private JealousyCulture(JealousyOutrage outrage, JealousyHierarchy hierarchy,
            bool equityExpectation, int polygamyEliteTier, string promptHint)
        {
            Outrage           = outrage;
            Hierarchy         = hierarchy;
            EquityExpectation = equityExpectation;
            PolygamyEliteTier = polygamyEliteTier;
            PromptHint        = promptHint ?? "";
        }

        /// <summary>How strongly a romantic act elsewhere reads as betrayal.</summary>
        public JealousyOutrage Outrage { get; }

        /// <summary>How strongly senior-vs-secondary rank drives competition jealousy.</summary>
        public JealousyHierarchy Hierarchy { get; }

        /// <summary>True when the offence is UNEQUAL treatment of partners rather than rank itself (Aserai).</summary>
        public bool EquityExpectation { get; }

        /// <summary>Minimum clan tier (0..6) at/above which sharing partners is culturally tolerated.</summary>
        public int PolygamyEliteTier { get; }

        /// <summary>One-sentence cultural framing injected into the jealous party's reaction prompt.</summary>
        public string PromptHint { get; }

        /// <summary>The strictly-monogamous fallback used for any culture not present in the file.</summary>
        public static JealousyCulture Monogamous { get; } =
            new JealousyCulture(JealousyOutrage.High, JealousyHierarchy.Low, false, 99, "");

        /// <summary>
        ///   True when a partner of this culture, at <paramref name="clanTier" />, tolerates the
        ///   player taking another partner — i.e. reacts with competition jealousy rather than
        ///   betrayal. Requires BOTH a non-high exclusivity stance AND meeting the elite tier gate
        ///   (historical polygamy was concentrated among the elite).
        /// </summary>
        public bool ToleratesSharedPartners(int clanTier)
            => Outrage != JealousyOutrage.High && clanTier >= PolygamyEliteTier;

        /// <summary>
        ///   Builds a culture from the raw JSON values. Unrecognised or missing strings degrade to
        ///   the strictly-monogamous defaults, so an unlisted or mistyped culture stays safe.
        /// </summary>
        public static JealousyCulture FromRaw(
            string exclusivityOutrage, int polygamyEliteTier, string hierarchySensitivity,
            bool equityExpectation, string promptHint)
            => new JealousyCulture(
                ParseOutrage(exclusivityOutrage),
                ParseHierarchy(hierarchySensitivity),
                equityExpectation,
                polygamyEliteTier,
                promptHint);

        private static JealousyOutrage ParseOutrage(string s)
        {
            if (string.Equals(s, "Low", System.StringComparison.OrdinalIgnoreCase)) return JealousyOutrage.Low;
            if (string.Equals(s, "Medium", System.StringComparison.OrdinalIgnoreCase)) return JealousyOutrage.Medium;
            return JealousyOutrage.High; // default / unrecognized → monogamous
        }

        private static JealousyHierarchy ParseHierarchy(string s)
        {
            if (string.Equals(s, "High", System.StringComparison.OrdinalIgnoreCase)) return JealousyHierarchy.High;
            if (string.Equals(s, "Medium", System.StringComparison.OrdinalIgnoreCase)) return JealousyHierarchy.Medium;
            return JealousyHierarchy.Low;
        }
    }
}
