// Code written by Gabriel Mailhot, 14/11/2026.
// What other mods write of a character through CrCharacterText, and what of it reaches the prompt and the panel. Bhelogan
// (Discord, 14/11/2026), whose Detective's Notebook wrote 600 to 1,200 characters of description into
// character_overrides.json between markers: "Something like ... Section = Appearance | Backstory | Private, ShowInPanel,
// Lean ... Appearance: shown in the panel, given to the model; Private: model only, never in the panel; a budget around
// 1,200 characters (RegisterLineSource's 200 is too short for a description); the same for the player". This decides
// which pieces are kept, in what order, and for which audience.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.Character
{
    /// <summary>Where a piece of a mod's text about a character belongs.</summary>
    public enum CharacterTextSection
    {
        /// <summary>How they look: given to the model with their appearance, and shown in the panel when asked.</summary>
        Appearance = 0,

        /// <summary>More of who they are: given to the model, and shown in the panel's Character section when asked.</summary>
        Backstory = 1,

        /// <summary>What they keep to themself: given to the model only, never shown.</summary>
        Private = 2
    }

    /// <summary>One piece of text a mod wrote about a character.</summary>
    public sealed class CharacterTextPiece
    {
        /// <summary>The mod that wrote it; pieces are ordered by it, so the prompt stays the same from turn to turn.</summary>
        public string Source { get; init; }

        /// <summary>Where it belongs.</summary>
        public CharacterTextSection Section { get; init; }

        /// <summary>The text itself.</summary>
        public string Text { get; init; }

        /// <summary>Whether the mod wants it shown in the conversation panel (never for Private).</summary>
        public bool ShowInPanel { get; init; }

        /// <summary>Whether it is kept in the Compact prompt for small models (then at most <see cref="CharacterTextComposer.MaxLeanChars" />).</summary>
        public bool Lean { get; init; }
    }

    /// <summary>What of the mods' pieces about a character reaches the prompt and the panel.</summary>
    public static class CharacterTextComposer
    {
        /// <summary>The longest piece kept, in characters; a longer one is left out whole rather than cut mid-sentence.</summary>
        public const int MaxChars = 1200;

        /// <summary>The longest piece kept in the Compact prompt, in characters.</summary>
        public const int MaxLeanChars = 300;

        /// <summary>The most pieces kept in one section, across all mods.</summary>
        public const int MaxPiecesPerSection = 3;

        /// <summary>
        ///   The pieces of <paramref name="section" /> for the prompt, joined by line breaks, ordered by source (ordinal) then as
        ///   given: blank ones and ones over <see cref="MaxChars" /> left out, at most <see cref="MaxPiecesPerSection" />. In the
        ///   Compact prompt only pieces marked Lean and within <see cref="MaxLeanChars" />. Null when nothing is kept.
        /// </summary>
        public static string ForPrompt(IEnumerable<CharacterTextPiece> pieces, CharacterTextSection section, bool compactPrompt)
            => Join(Kept(pieces, section).Where(p => !compactPrompt || (p.Lean && p.Text.Trim().Length <= MaxLeanChars)));

        /// <summary>
        ///   The pieces of <paramref name="section" /> the mods asked to show in the panel, same order and bounds as the prompt.
        ///   Always null for <see cref="CharacterTextSection.Private" />.
        /// </summary>
        public static string ForPanel(IEnumerable<CharacterTextPiece> pieces, CharacterTextSection section)
            => section == CharacterTextSection.Private ? null : Join(Kept(pieces, section).Where(p => p.ShowInPanel));

        /// <summary>
        ///   <paramref name="first" /> followed by <paramref name="then" />, a space between, either may be empty; null when both
        ///   are.
        /// </summary>
        public static string Append(string first, string then)
        {
            bool hasFirst = !string.IsNullOrWhiteSpace(first), hasThen = !string.IsNullOrWhiteSpace(then);
            if (!hasFirst && !hasThen) return null;
            if (!hasThen) return first.Trim();
            if (!hasFirst) return then.Trim();

            return first.Trim() + " " + then.Trim();
        }

        #region private

        private static IEnumerable<CharacterTextPiece> Kept(IEnumerable<CharacterTextPiece> pieces, CharacterTextSection section)
            => (pieces ?? Enumerable.Empty<CharacterTextPiece>())
               .Select((p, i) => (p, i))
               .Where(x => x.p != null && x.p.Section == section && !string.IsNullOrWhiteSpace(x.p.Text) && x.p.Text.Trim().Length <= MaxChars)
               .OrderBy(x => x.p.Source ?? "", StringComparer.Ordinal)
               .ThenBy(x => x.i)
               .Select(x => x.p);

        private static string Join(IEnumerable<CharacterTextPiece> pieces)
        {
            List<string> texts = pieces.Take(MaxPiecesPerSection).Select(p => p.Text.Trim()).ToList();

            return texts.Count == 0 ? null : string.Join("\n", texts);
        }

        #endregion
    }
}
