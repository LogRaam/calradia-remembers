// Code written by Gabriel Mailhot, 14/08/2026.

#region

using System;
using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic.Letters
{
   /// <summary>
   ///   Decides whether the parchment letter view should swap its decorative Latin font (Galahad) for a
   ///   plain, glyph-complete one. Galahad has no proper Cyrillic glyphs, so a Cyrillic-script game
   ///   language renders it through a per-glyph fallback that looks broken and miscentered (player
   ///   report). Latin-script languages keep the decorative parchment look CR ships by default.
   /// </summary>
   public static class LetterFontPolicy
   {
      /// <summary>
      ///   The game-language ids this policy recognises as Cyrillic-script, matched case-insensitively. Verified
      ///   against this engine build's own data: <c>Русский</c> is the id
      ///   <c>TaleWorlds.Localization.MBTextManager.ActiveTextLanguage</c> returns for Russian (from
      ///   <c>Modules/Native/ModuleData/Languages/RU/language_data.xml</c>, <c>id="Русский"</c>) and the same id
      ///   <c>TaleWorlds.GauntletUI</c>'s own <c>NativeLanguages.xml</c> keys its Galahad-&gt;FiraSansExtraCondensed
      ///   font-substitution table on. <c>Russian</c> (the English word) is kept as a defensive alternate in case
      ///   a future caller ever hands the id in that form instead. Only Russian ships as a Cyrillic language in
      ///   this Bannerlord build; add further native ids here (e.g. a future Ukrainian/Bulgarian localisation) as
      ///   the mod picks them up.
      /// </summary>
      private static readonly HashSet<string> CyrillicLanguageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
      {
         "Русский",
         "Russian"
      };

      /// <summary>
      ///   True when <paramref name="languageId" /> names a Cyrillic-script game language. Matched
      ///   case-insensitively after trimming; null, empty, or any id this policy does not recognise all fall
      ///   through to false, so the decorative Galahad look stays the safe default for every language.
      /// </summary>
      public static bool PreferPlainFont(string languageId)
      {
         if (string.IsNullOrWhiteSpace(languageId)) return false;

         return CyrillicLanguageIds.Contains(languageId.Trim());
      }
   }
}
