// Code written by Gabriel Mailhot, 28/09/2026.
// Regard earned by letter (Lordfadooboo, Nexus, 28/09/2026; ruled by Gabriel the same day). Letters never moved the
// mod's regard at all: a letter may emit no [ACTION], so change_relation never ran, and a dozen warm letters left a
// correspondent exactly where they were. The character now judges each letter in the call that already decides the
// reply (a control line, like the correspondence preference), and this decides what the judgement is worth:
//
//   - warm: +1, at most once every CooldownDaysFor(warmth) days per correspondent;
//   - letters alone stop at Regard.FriendlyTier: what lies beyond is won in person;
//   - cold or insulting: -1, on no timer, but letters alone never drive a correspondent past Regard.WaryTier.

#region

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NpcMemoryService.Core.Standing;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>How a letter left its reader feeling toward the player, as the reader judged it.</summary>
    public enum LetterTone
    {
        /// <summary>No judgement was given (no line, or one that could not be read): nothing changes.</summary>
        Unjudged,

        /// <summary>Neither warmer nor cooler.</summary>
        Same,

        /// <summary>Kind, thoughtful or welcome.</summary>
        Warmer,

        /// <summary>Rude, cold, threatening or unwelcome.</summary>
        Cooler
    }

    /// <summary>What a letter's worth is weighed against.</summary>
    public sealed class LetterRegardFacts
    {
        /// <summary>The reader's judgement of the letter.</summary>
        public LetterTone Tone { get; set; }

        /// <summary>The reader's regard for the player now, on the mod's ledger.</summary>
        public int CurrentRegard { get; set; }

        /// <summary>The day a letter last earned regard with this reader; null when none ever has.</summary>
        public int? LastGainDay { get; set; }

        /// <summary>Today's campaign day.</summary>
        public int Today { get; set; }

        /// <summary>The MCM warmth setting, which also speeds regard in conversation.</summary>
        public RelationWarmth Warmth { get; set; } = RelationWarmth.Normal;
    }

    /// <summary>The worth of a letter to the reader's regard. Pure.</summary>
    public static class LetterRegardPolicy
    {
        /// <summary>The control line a reply carries its judgement on. Matched case-insensitively; stripped before delivery.</summary>
        public const string Marker = "[LETTER_TONE]";

        /// <summary>TUNE (Gabriel, 28/09/2026): days between two letter gains with one correspondent, at Normal warmth.</summary>
        public const int NormalCooldownDays = 5;

        /// <summary>Days between two letter gains at the given warmth: Normal 5, Warmer 3, Very Easy 1.</summary>
        public static int CooldownDaysFor(RelationWarmth warmth)
        {
            switch (warmth)
            {
                case RelationWarmth.VeryEasy: return 1;
                case RelationWarmth.Easier: return 3;
                default: return NormalCooldownDays;
            }
        }

        /// <summary>The regard a letter moves: +1, -1 or 0, per the rules in this file's header.</summary>
        public static int Shift(LetterRegardFacts facts)
        {
            if (facts == null) return 0;

            switch (facts.Tone)
            {
                case LetterTone.Warmer:
                    if (facts.CurrentRegard >= Regard.FriendlyTier) return 0;
                    if (facts.LastGainDay.HasValue && facts.Today - facts.LastGainDay.Value < CooldownDaysFor(facts.Warmth)) return 0;

                    return 1;
                case LetterTone.Cooler:
                    return facts.CurrentRegard <= Regard.WaryTier ? 0 : -1;
                default:
                    return 0;
            }
        }

        /// <summary>The judgement a reply carries on its <see cref="Marker" /> line; Unjudged when there is none or it cannot be read.</summary>
        public static LetterTone ParseTone(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return LetterTone.Unjudged;

            Match match = Regex.Match(raw, Regex.Escape(Marker) + @"\s*:?\s*(\w+)", RegexOptions.IgnoreCase);
            if (!match.Success) return LetterTone.Unjudged;

            switch (match.Groups[1].Value.ToLowerInvariant())
            {
                case "warmer": return LetterTone.Warmer;
                case "cooler": return LetterTone.Cooler;
                case "same": return LetterTone.Same;
                default: return LetterTone.Unjudged;
            }
        }

        /// <summary>The letter without its <see cref="Marker" /> line, so the player never reads the mod's metadata.</summary>
        public static string StripToneLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            var kept = new List<string>();
            foreach (string line in text.Split('\n'))
                if (line.IndexOf(Marker, StringComparison.OrdinalIgnoreCase) < 0)
                    kept.Add(line);

            return string.Join("\n", kept).TrimEnd();
        }
    }
}
