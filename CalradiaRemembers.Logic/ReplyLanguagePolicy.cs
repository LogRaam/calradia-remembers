// Code written by Gabriel Mailhot, 17/10/2026.
// RaveN017Mod (Nexus, 17/10/2026), playing in French with "Reply language" set to Français: "ca se produit uniquement
// quand quelqu'un vient me parler sur la carte de campagne: il commence toujours par me parler en anglais jusqu'a ce que
// je lui demande de parler en francais ... Egalement pareil pour le systeme de conseil". And: "Is there a way for your
// mod to detect the base language that the user has selected for Bannerlord?"
//
// Two rules live here. Which language the replies are in when the player named none: the game's own (an English game
// keeps the old mirror of whatever the player writes). And how an opening the character speaks first carries it: an
// opener has no player message to set the language, only an English stage direction, so the direction itself names
// the language, the last thing the model reads.

#region

using System;
using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Which language the characters speak, and how an opening line is told so.</summary>
    public static class ReplyLanguagePolicy
    {
        // The names Bannerlord 1.4.8 stores in BannerlordConfig.Language (InitialMenuAnnouncementVM.GetLanguageString),
        // as a model knows them. A name not listed (a mod's language) is used as it is.
        private static readonly IReadOnlyDictionary<string, string> GameLanguageNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Français"] = "French",
            ["Deutsch"] = "German",
            ["Italiano"] = "Italian",
            ["Polski"] = "Polish",
            ["Русский"] = "Russian",
            ["Türkçe"] = "Turkish",
            ["日本語"] = "Japanese",
            ["한국어"] = "Korean",
            ["简体中文"] = "Simplified Chinese",
            ["繁體中文"] = "Traditional Chinese",
            ["Español (LA)"] = "Latin American Spanish",
            ["Português (BR)"] = "Brazilian Portuguese"
        };

        /// <summary>
        ///   The language every reply is written in: the player's own setting when they named one; else the game's
        ///   language; else none, and the model mirrors whatever the player writes (the behaviour before this rule). An
        ///   English game is none: an English-speaking player keeps the mirror, so a line written in another tongue is
        ///   still answered in it.
        /// </summary>
        public static string Resolve(string playerSetting, string gameLanguage)
        {
            if (!string.IsNullOrWhiteSpace(playerSetting)) return playerSetting.Trim();
            if (string.IsNullOrWhiteSpace(gameLanguage)) return "";

            string game = gameLanguage.Trim();
            if (game.Equals("English", StringComparison.OrdinalIgnoreCase)) return "";

            return GameLanguageNames.TryGetValue(game, out string name) ? name : game;
        }

        /// <summary>
        ///   An opening the character speaks first, with the language named at its end. Without it, the only user turn
        ///   is an English stage direction and the first line came back in English.
        /// </summary>
        public static string WithLanguage(string openingCue, string language)
        {
            if (string.IsNullOrWhiteSpace(language) || string.IsNullOrEmpty(openingCue)) return openingCue;

            return openingCue.TrimEnd() + " (Speak and narrate in " + language.Trim() + ".)";
        }
    }
}
