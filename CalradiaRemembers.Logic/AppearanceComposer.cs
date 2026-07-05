// Code written by Gabriel Mailhot, 05/07/2026.
// Appearance pillar, composer increment (ratified): a hero's appearance block is (1) DERIVED from
// reliably-available body facts (AppearanceFacts) plus (2) an authored free-text prose descriptor,
// rendered together as natural-language sentences. Serves TWO framings via AppearanceVoice: the PLAYER
// (and any other described hero) in the THIRD person, read BY the speaking NPC ("A tall man..."); and the
// speaking NPC's OWN body in the SECOND person, read by the character the LLM plays ("You are a tall
// man..."). PURE: plain facts + authored prose + voice in, a string out. Reading the engine's
// BodyProperties sliders and any editable prose field are a separate, later step. Any numeric axis that
// lands in its own "average" middle band is left unvoiced (short/tall, slight/solid/powerful only speak up
// when they stand out), the same discipline as StanceBearingText. Age and hair are either known or omitted
// outright, never rendered as "unknown". When NOTHING derivable stands out and no prose was authored,
// Compose returns "" so the caller renders nothing at all.

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Whose voice the appearance is written in.</summary>
    public enum AppearanceVoice
    {
        /// <summary>Third person, describing the hero TO someone else (the player, a witness): "A tall man in his prime, with dark hair."</summary>
        Subject,

        /// <summary>Second person, the hero's OWN body read by the character the LLM plays: "You are a tall man in your prime, with dark hair."</summary>
        Self
    }

    /// <summary>
    ///   Composes a hero's natural-language appearance description from derived body facts and an authored
    ///   free-text prose fragment, in either the third-person <see cref="AppearanceVoice.Subject"/> voice
    ///   (the player or another hero described TO the NPC) or the second-person <see cref="AppearanceVoice.Self"/>
    ///   voice (the speaking NPC's own body). See this file's header for the design.
    /// </summary>
    public static class AppearanceComposer
    {
        /// <summary>Tuning: height strictly below this reads as "short". Above <see cref="HeightTallAbove"/> reads as "tall". Between: unvoiced ("average").</summary>
        public const double HeightShortBelow = 0.35;

        /// <summary>Tuning: height strictly above this reads as "tall".</summary>
        public const double HeightTallAbove = 0.65;

        /// <summary>Tuning: the build/weight average strictly below this reads as "slightly built".</summary>
        public const double BuildSlightBelow = 0.3;

        /// <summary>Tuning: the build/weight average strictly above this (but not past <see cref="BuildPowerfulAbove"/>) reads as "solidly built".</summary>
        public const double BuildSolidAbove = 0.6;

        /// <summary>Tuning: the build/weight average strictly above this reads as "powerfully built".</summary>
        public const double BuildPowerfulAbove = 0.85;

        /// <summary>Tuning: age strictly below this reads as "youth".</summary>
        public const int AgeYouthBelow = 25;

        /// <summary>Tuning: age at/above <see cref="AgeYouthBelow"/> and strictly below this reads as "prime".</summary>
        public const int AgePrimeBelow = 40;

        /// <summary>Tuning: age at/above <see cref="AgePrimeBelow"/> and strictly below this reads as "middle years"; at/above this reads as "old age".</summary>
        public const int AgeMiddleBelow = 60;

        /// <summary>Height band, or null when average (neither short nor tall enough to voice).</summary>
        public static string HeightPhrase(double heightNormalized)
        {
            if (heightNormalized < HeightShortBelow) return "short";
            if (heightNormalized > HeightTallAbove) return "tall";

            return null;
        }

        /// <summary>
        ///   Build/weight band from the average of both sliders, or null when average (neither slight,
        ///   solid, nor powerful enough to voice). Build and weight are folded into ONE phrase rather than
        ///   two, since an appearance description does not separately narrate "muscular" and "heavy".
        /// </summary>
        public static string BuildPhrase(double buildNormalized, double weightNormalized)
        {
            double combined = (buildNormalized + weightNormalized) / 2.0;

            if (combined < BuildSlightBelow) return "slightly built";
            if (combined > BuildPowerfulAbove) return "powerfully built";
            if (combined > BuildSolidAbove) return "solidly built";

            return null;
        }

        /// <summary>
        ///   Age band phrase (paired with a possessive by <see cref="Compose"/>: "in his/her/your
        ///   youth/prime/middle years/old age"), or null when <paramref name="ageYears"/> is zero or
        ///   negative (not known/derivable).
        /// </summary>
        public static string AgePhrase(int ageYears)
        {
            if (ageYears <= 0) return null;
            if (ageYears < AgeYouthBelow) return "youth";
            if (ageYears < AgePrimeBelow) return "prime";
            if (ageYears < AgeMiddleBelow) return "middle years";

            return "old age";
        }

        /// <summary>
        ///   Composes the full appearance description: derived body facts as one natural-language sentence
        ///   in the requested <paramref name="voice"/>, followed by the trimmed authored prose as its own
        ///   sentence. Gracefully omits any unknown/average fact rather than ever emit "unknown" or an empty
        ///   fragment. Returns "" (never null) when there is nothing derived worth saying AND no authored
        ///   prose, so the caller renders nothing.
        /// </summary>
        public static string Compose(AppearanceFacts facts, string authoredProse, AppearanceVoice voice = AppearanceVoice.Subject)
        {
            string derived = ComposeDerived(facts, voice);
            string authored = (authoredProse ?? "").Trim();

            if (derived.Length == 0) return authored;
            if (authored.Length == 0) return derived;

            return derived + " " + authored;
        }

        #region private

        private static string ComposeDerived(AppearanceFacts facts, AppearanceVoice voice)
        {
            string heightWord = HeightPhrase(facts.HeightNormalized);
            string buildWord = BuildPhrase(facts.BuildNormalized, facts.WeightNormalized);
            string agePhrase = AgePhrase(facts.AgeYears);

            var withClauses = new List<string>();
            string hairColor = (facts.HairColorDescriptor ?? "").Trim();
            if (hairColor.Length > 0) withClauses.Add(hairColor + " hair");
            if (!facts.IsFemale && facts.HasBeard) withClauses.Add("a beard");

            var adjectives = new List<string>();
            if (heightWord != null) adjectives.Add(heightWord);
            if (buildWord != null) adjectives.Add(buildWord);

            bool hasNothingToSay = adjectives.Count == 0 && agePhrase == null && withClauses.Count == 0;
            if (hasNothingToSay) return "";

            string genderNoun = facts.IsFemale ? "woman" : "man";
            string subject = adjectives.Count == 0 ? "a " + genderNoun : "a " + string.Join(", ", adjectives) + " " + genderNoun;

            // Voice decides the subject framing and the age possessive. Subject = third person, describing
            // the hero TO the NPC (a bare noun phrase the host renders under a naming heading, never "you").
            // Self = second person, the speaking NPC's own body, read by the character the LLM plays.
            string sentence = voice == AppearanceVoice.Self ? "You are " + subject : Capitalize(subject);

            if (agePhrase != null)
                sentence += " in " + AgePossessive(facts.IsFemale, voice) + " " + agePhrase;

            if (withClauses.Count > 0)
                sentence += ", with " + string.Join(" and ", withClauses);

            return sentence + ".";
        }

        private static string AgePossessive(bool isFemale, AppearanceVoice voice)
            => voice == AppearanceVoice.Self ? "your" : (isFemale ? "her" : "his");

        private static string Capitalize(string s)
            => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        #endregion
    }
}
