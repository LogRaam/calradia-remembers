// Code written by Gabriel Mailhot, 14/08/2026.

#region

using System;
using CalradiaRemembers.Logic.Letters;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Letters
{
   /// <summary>
   ///   Reads the active game language and picks the decorative (Galahad) or plain (Cyrillic-safe) brush
   ///   name for the parchment letter view, per <see cref="LetterFontPolicy" />. A single shared resolver
   ///   so <see cref="LetterViewModel" /> does not duplicate the language lookup, and so
   ///   <see cref="LanguageIdProvider" /> is one place to override for a self-test.
   /// </summary>
   internal static class LetterFontResolver
   {
      /// <summary>
      ///   The active game language id, TaleWorlds.Localization.MBTextManager.ActiveTextLanguage by default
      ///   (verified against this engine build's Modules/Native/ModuleData/Languages/RU/language_data.xml,
      ///   which declares Russian's own id as "Русский", the exact string <see cref="LetterFontPolicy" />
      ///   matches). Overridable so CrSelfTest's letter_font test can force a Cyrillic language without
      ///   touching the real campaign's language setting.
      /// </summary>
      internal static Func<string> LanguageIdProvider = () => MBTextManager.ActiveTextLanguage;

      /// <summary>Picks <paramref name="plainBrush" /> over <paramref name="decorativeBrush" /> for the current game language.</summary>
      internal static string Resolve(string decorativeBrush, string plainBrush)
         => LetterFontPolicy.PreferPlainFont(LanguageIdProvider?.Invoke())
            ? plainBrush
            : decorativeBrush;
   }
}
