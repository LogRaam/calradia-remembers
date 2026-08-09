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
      public static JealousyCulture FromRaw(
         string exclusivityOutrage,
         int polygamyEliteTier,
         string hierarchySensitivity,
         bool equityExpectation,
         string promptHint,
         bool patriarchalHousehold = false)
         => new(
            ParseOutrage(exclusivityOutrage),
            ParseHierarchy(hierarchySensitivity),
            equityExpectation,
            polygamyEliteTier,
            promptHint,
            patriarchalHousehold);

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