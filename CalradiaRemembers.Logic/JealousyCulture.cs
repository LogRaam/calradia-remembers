// Code written by Gabriel Mailhot, 20/06/2026.

#region

using System;

#endregion

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
      private JealousyCulture(JealousyOutrage outrage,
                              JealousyHierarchy hierarchy,
                              bool equityExpectation,
                              int polygamyEliteTier,
                              string promptHint,
                              bool patriarchalHousehold)
      {
         Outrage = outrage;
         Hierarchy = hierarchy;
         EquityExpectation = equityExpectation;
         PolygamyEliteTier = polygamyEliteTier;
         PromptHint = promptHint ?? "";
         PatriarchalHousehold = patriarchalHousehold;
      }

      /// <summary>The strictly-monogamous fallback used for any culture not present in the file.</summary>
      public static JealousyCulture Monogamous { get; } =
         new(JealousyOutrage.High, JealousyHierarchy.Low, false, 99, "", false);

      /// <summary>True when the offence is UNEQUAL treatment of partners rather than rank itself (Aserai).</summary>
      public bool EquityExpectation { get; }

      /// <summary>How strongly senior-vs-secondary rank drives competition jealousy.</summary>
      public JealousyHierarchy Hierarchy { get; }

      /// <summary>How strongly a romantic act elsewhere reads as betrayal.</summary>
      public JealousyOutrage Outrage { get; }

      /// <summary>Minimum clan tier (0..6) at/above which sharing partners is culturally tolerated.</summary>
      public int PolygamyEliteTier { get; }

      /// <summary>
      ///   True for a PATRIARCHAL, harem-accepting culture whose wives are culturally submissive to their
      ///   husbands (Aserai): a neglected wife may take a lover, but keeps it SECRET rather than open. Read
      ///   through <see cref="IsHaremCulture" />. Defaults false, so any unlisted culture is egalitarian.
      /// </summary>
      public bool PatriarchalHousehold { get; }

      /// <summary>One-sentence cultural framing injected into the jealous party's reaction prompt.</summary>
      public string PromptHint { get; }

      /// <summary>
      ///   True when this is a HAREM culture: it accepts shared partners (a non-High exclusivity stance) AND is
      ///   patriarchal with a submissive wife (<see cref="PatriarchalHousehold" />). A wife of such a culture
      ///   hides her lover (a SECRET affair) and may stray even without an open arrangement when neglected; an
      ///   egalitarian culture instead takes an OPEN lover, and only under a reciprocal open arrangement. This
      ///   is culture-level (no clan tier): the elite-tier gate governs WHO shares partners, not whether the
      ///   people are a harem culture at all.
      /// </summary>
      public bool IsHaremCulture => Outrage != JealousyOutrage.High && PatriarchalHousehold;

      /// <summary>
      ///   Builds a culture from the raw JSON values. Unrecognised or missing strings degrade to
      ///   the strictly-monogamous defaults, so an unlisted or mistyped culture stays safe.
      /// </summary>
      /// <summary>
      ///   One culture's jealousy rules exactly as they are WRITTEN in jealousy_cultures.json, before anything is
      ///   parsed. The host's own DTO fills it, so the shape of the file and the shape of this record stay one
      ///   thing rather than two that can disagree.
      ///   <para>
      ///     Named rather than positional (2026-09-10). Gabriel: the code should be an example of good practice,
      ///     and a repository with no exceptions has nothing to explain. Every policy in Logic now takes its
      ///     facts by name, so a caller cannot pass them in the wrong order, a test names only what it is about,
      ///     and adding a field costs nothing to a test that never used it.
      ///   </para>
      /// </summary>
      public sealed class JealousyCultureRaw
      {
         /// <summary>How badly this culture takes exclusivity being broken, as the file spells it ("Low", "High").</summary>
         public string ExclusivityOutrage { get; set; } = "";

         /// <summary>The clan tier from which polygamy is tolerated, historical polygamy being an elite affair.</summary>
         public int PolygamyEliteTier { get; set; }

         /// <summary>How much rank matters to them, as the file spells it.</summary>
         public string HierarchySensitivity { get; set; } = "";

         /// <summary>They expect partners to be treated as equals.</summary>
         public bool EquityExpectation { get; set; }

         /// <summary>The sentence the prompt carries for this culture, authored in the file.</summary>
         public string PromptHint { get; set; } = "";

         /// <summary>The household is patriarchal, which colours who may take another partner.</summary>
         public bool PatriarchalHousehold { get; set; }
      }

      /// <summary>Reads one culture's written rules into the rule the mod actually applies.</summary>
      public static JealousyCulture FromRaw(JealousyCultureRaw raw)
      {
         raw ??= new JealousyCultureRaw();

         return new JealousyCulture(
            ParseOutrage(raw.ExclusivityOutrage),
            ParseHierarchy(raw.HierarchySensitivity),
            raw.EquityExpectation,
            raw.PolygamyEliteTier,
            raw.PromptHint,
            raw.PatriarchalHousehold);
      }

      /// <summary>
      ///   True when a partner of this culture, at <paramref name="clanTier" />, tolerates the
      ///   player taking another partner — i.e. reacts with competition jealousy rather than
      ///   betrayal. Requires BOTH a non-high exclusivity stance AND meeting the elite tier gate
      ///   (historical polygamy was concentrated among the elite).
      /// </summary>
      public bool ToleratesSharedPartners(int clanTier)
         => Outrage != JealousyOutrage.High && clanTier >= PolygamyEliteTier;

      #region private

      private static JealousyHierarchy ParseHierarchy(string s)
      {
         if (string.Equals(s, "High", StringComparison.OrdinalIgnoreCase)) return JealousyHierarchy.High;
         if (string.Equals(s, "Medium", StringComparison.OrdinalIgnoreCase)) return JealousyHierarchy.Medium;

         return JealousyHierarchy.Low;
      }

      private static JealousyOutrage ParseOutrage(string s)
      {
         if (string.Equals(s, "Low", StringComparison.OrdinalIgnoreCase)) return JealousyOutrage.Low;
         if (string.Equals(s, "Medium", StringComparison.OrdinalIgnoreCase)) return JealousyOutrage.Medium;

         return JealousyOutrage.High; // default / unrecognized → monogamous
      }

      #endregion
   }
}