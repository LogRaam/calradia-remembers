// Code written by Gabriel Mailhot, 19/06/2026.
// Sprint 24 (jealousy): per-culture jealousy norm, loaded from ModuleData/jealousy_cultures.json.

#region

using Newtonsoft.Json;

#endregion

namespace CalradiaRemembers.Jealousy
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
    ///   A culture's stance on romantic exclusivity, deserialized from
    ///   <c>jealousy_cultures.json</c>. Drives whether a staked partner reacts with
    ///   <b>exclusivity</b> jealousy (betrayal) or <b>competition</b> jealousy (rivalry over
    ///   rank/attention among shared partners). Every field is optional in the JSON; the
    ///   defaults here describe a strictly monogamous culture, so any culture not listed —
    ///   including ones added by faction mods — degrades safely to monogamous behaviour.
    /// </summary>
    public sealed class JealousyCultureProfile
    {
        /// <summary>Raw JSON value; parsed via <see cref="Outrage" />. Defaults to monogamous.</summary>
        [JsonProperty("exclusivityOutrage")]
        public string ExclusivityOutrage { get; set; } = "High";

        /// <summary>
        ///   Minimum clan tier (0..6) at/above which sharing partners is culturally tolerated.
        ///   Below this tier the culture behaves monogamously — capturing that historical
        ///   polygamy was concentrated among the elite. The default (99) is never reachable,
        ///   so an unlisted culture stays monogamous at every tier.
        /// </summary>
        [JsonProperty("polygamyEliteTier")]
        public int PolygamyEliteTier { get; set; } = 99;

        /// <summary>Raw JSON value; parsed via <see cref="Hierarchy" />.</summary>
        [JsonProperty("hierarchySensitivity")]
        public string HierarchySensitivity { get; set; } = "Low";

        /// <summary>True when the offence is UNEQUAL treatment of partners rather than rank itself (Aserai).</summary>
        [JsonProperty("equityExpectation")]
        public bool EquityExpectation { get; set; }

        /// <summary>One-sentence cultural framing injected into the jealous party's reaction prompt.</summary>
        [JsonProperty("promptHint")]
        public string PromptHint { get; set; } = "";

        /// <summary>The strictly-monogamous fallback used for any culture not present in the file.</summary>
        public static JealousyCultureProfile Monogamous { get; } = new JealousyCultureProfile();

        [JsonIgnore]
        public JealousyOutrage Outrage => ParseOutrage(ExclusivityOutrage);

        [JsonIgnore]
        public JealousyHierarchy Hierarchy => ParseHierarchy(HierarchySensitivity);

        /// <summary>
        ///   True when a partner of this culture, at <paramref name="clanTier" />, tolerates the
        ///   player taking another partner — i.e. reacts with competition jealousy rather than
        ///   betrayal. Requires both a non-high exclusivity stance AND meeting the elite tier gate.
        /// </summary>
        public bool ToleratesSharedPartners(int clanTier)
            => Outrage != JealousyOutrage.High && clanTier >= PolygamyEliteTier;

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
