// Code written by Gabriel Mailhot, 04/07/2026.
// The "Bearing" encyclopedia line (owner ruling, affect-coherence reform P2): the NPC's stance toward the
// player, in words only, numberless — the encyclopedia's counterpart to StanceDescriptor's prompt-facing
// posture block. Composed from Trust/Respect/Fear (NOT Affection — that is already the encyclopedia's
// "Personal regard:" line, so restating it here would be the same double-voicing StanceDescriptor.Summarize
// was fixed to avoid). Pure and numberless by design: the player earns a WORD, never a digit, from either
// surface.

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Composes the numberless "Bearing:" encyclopedia line from an NPC's Trust/Respect/Fear stance axes.</summary>
    public static class StanceBearingText
    {
        /// <summary>Tuning: trust/respect at/beyond this magnitude (either sign) is voiced.</summary>
        public const int Strong = 40;

        /// <summary>Tuning: fear at/above this reads as a faint unease — the lowest voiced fear band.</summary>
        public const int FearMild = 15;

        /// <summary>Tuning: fear at/above this reads as real wariness.</summary>
        public const int FearModerate = 40;

        /// <summary>Tuning: fear at/above this reads as outright fear.</summary>
        public const int FearStrong = 70;

        /// <summary>How far the NPC trusts the player's word, or null when neither trusting nor distrusting enough to voice.</summary>
        public static string TrustPhrase(int trust)
        {
            if (trust >= Strong) return "trusts your word";
            if (trust <= -Strong) return "does not trust your word";

            return null;
        }

        /// <summary>How much the NPC esteems the player's worth, or null when neither impressed nor contemptuous enough to voice.</summary>
        public static string RespectPhrase(int respect)
        {
            if (respect >= Strong) return "respects your strength";
            if (respect <= -Strong) return "holds you in contempt";

            return null;
        }

        /// <summary>How much the NPC fears the player, graduated, or null when unafraid. Fear is 0..100 (never negative), so it has no opposite pole to voice.</summary>
        public static string FearPhrase(int fear)
        {
            if (fear >= FearStrong) return "fears you";
            if (fear >= FearModerate) return "is wary of you";
            if (fear >= FearMild) return "is uneasy around you";

            return null;
        }

        /// <summary>
        ///   The full "Bearing:" value: every non-neutral phrase joined by commas, in a stable order
        ///   (trust, respect, fear). Empty string (never null) when every axis is neutral — the caller's
        ///   AutoHideRichTextWidget collapses on an empty string, not on null.
        /// </summary>
        public static string Compose(int trust, int respect, int fear)
        {
            var parts = new List<string>();
            Add(parts, TrustPhrase(trust));
            Add(parts, RespectPhrase(respect));
            Add(parts, FearPhrase(fear));

            return parts.Count == 0 ? "" : string.Join(", ", parts);
        }

        private static void Add(List<string> parts, string phrase)
        {
            if (phrase != null) parts.Add(phrase);
        }
    }
}
