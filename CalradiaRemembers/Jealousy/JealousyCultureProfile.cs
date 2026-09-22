// Code written by Gabriel Mailhot, 20/06/2026.

#region

using CalradiaRemembers.Logic;
using Newtonsoft.Json;

#endregion

namespace CalradiaRemembers.Jealousy
{
   /// <summary>
   ///   The on-disk shape of a culture's stance on romantic exclusivity, deserialized from
   ///   <c>jealousy_cultures.json</c>. Every field is optional; the defaults here describe a
   ///   strictly monogamous culture, so any culture not listed — including ones added by faction
   ///   mods — degrades safely to monogamous behaviour. The parsing and the tolerance rule live in
   ///   the testable <see cref="JealousyCulture" />; call <see cref="ToCulture" /> to get it.
   /// </summary>
   public sealed class JealousyCultureProfile
   {
      /// <summary>The strictly-monogamous fallback used for any culture not present in the file.</summary>
      public static JealousyCultureProfile Monogamous { get; } = new();

      /// <summary>True when the offence is UNEQUAL treatment of partners rather than rank itself (Aserai).</summary>
      [JsonProperty("equityExpectation")]
      public bool EquityExpectation { get; set; }

      /// <summary>Raw JSON value ("Low"/"Medium"/"High"); parsed in <see cref="ToCulture" />.</summary>
      [JsonProperty("exclusivityOutrage")]
      public string ExclusivityOutrage { get; set; } = "High";

      /// <summary>Raw JSON value ("Low"/"Medium"/"High"); parsed in <see cref="ToCulture" />.</summary>
      [JsonProperty("hierarchySensitivity")]
      public string HierarchySensitivity { get; set; } = "Low";

      /// <summary>
      ///   Minimum clan tier (0..6) at/above which sharing partners is culturally tolerated.
      ///   Below this tier the culture behaves monogamously — capturing that historical polygamy
      ///   was concentrated among the elite. The default (99) is never reachable.
      /// </summary>
      [JsonProperty("polygamyEliteTier")]
      public int PolygamyEliteTier { get; set; } = 99;

      /// <summary>One-sentence cultural framing injected into the jealous party's reaction prompt.</summary>
      [JsonProperty("promptHint")]
      public string PromptHint { get; set; } = "";

      /// <summary>
      ///   True for a PATRIARCHAL, harem-accepting culture whose wives are culturally submissive to their
      ///   husbands (Aserai is the archetype). Combined with a permissive exclusivity stance it marks a
      ///   "harem culture": a neglected wife may take a lover, but she keeps it SECRET rather than declaring
      ///   an open one. Optional; defaults false (an egalitarian culture), so old configs and any culture
      ///   absent from the file stay save-safe and behave egalitarian.
      /// </summary>
      [JsonProperty("patriarchalHousehold")]
      public bool PatriarchalHousehold { get; set; }

      /// <summary>
      ///   Whether a partner of this people may take someone OUTSIDE the household. Absent means true, which
      ///   is what every culture did before this field existed. Set false for a CLOSED plural house: several
      ///   spouses, each faithful inward (yozakura12, 22/09/2026).
      /// </summary>
      [JsonProperty("outsidePartnersAllowed")]
      public bool OutsidePartnersAllowed { get; set; } = true;

      /// <summary>Parses this DTO into the pure, testable culture value object.</summary>
      public JealousyCulture ToCulture()
         => JealousyCulture.FromRaw(new JealousyCulture.JealousyCultureRaw {ExclusivityOutrage = ExclusivityOutrage, PolygamyEliteTier = PolygamyEliteTier, HierarchySensitivity = HierarchySensitivity, EquityExpectation = EquityExpectation, PromptHint = PromptHint, PatriarchalHousehold = PatriarchalHousehold, OutsidePartnersAllowed = OutsidePartnersAllowed});
   }
}