// Code written by Gabriel Mailhot, 21/07/2026.
// Marriage-memory audit (player report, Nexus, v1.31.4): a player courted an NPC (Mesui) for ~40 in-game
// days; the chat and letters narrated a complete wedding and two [EVENT] entries were recorded ("Mesui and
// Huan Yi married under Khuzait law...", "...were married before the Khan's envoy..."), yet no marriage ever
// happened in the engine (cr.marriage_status reported the NPC free, no spouse in the Encyclopedia). Root
// cause: ProfileMutator.Apply stores ANY [EVENT] the model emits with no check that a matching
// [ACTION] type: marry was even emitted, let alone that BannerlordGameStateBridge.ExecuteMarry accepted it
// (in this case it plausibly refused: Mesui has grandchildren and is past the age-gap window
// MarriageEligibility allows). This policy names the one thing the mod must check on the summary TEXT before
// trusting it: does it claim this NPC and the player are now wed. The mod gates the event on that answer,
// then confirms or drops it against real engine state (Hero.Spouse) once the action loop has had its chance
// to run; see ChatViewModel.Exchanges.cs around the ProfileMutator.Apply call.

#region

using System;
using System.Text.RegularExpressions;

#endregion

namespace CalradiaRemembers.Logic.Marriage
{
    /// <summary>
    ///   Detects whether an [EVENT] summary CLAIMS that the conversation NPC and the player are now
    ///   married, so the mod can withhold the memory (and the romantic-arc advancement riding on it)
    ///   until <c>Hero.Spouse</c> confirms the wedding actually happened. A chat claim is not a
    ///   marriage; <c>BannerlordGameStateBridge.ExecuteMarry</c> is the only path that seals a real one.
    ///   Named and shaped so a second falsifiable-claim type could reuse the same "gate the memory on
    ///   real engine state" pattern later without a redesign.
    /// </summary>
    public static class MarriageClaimPolicy
    {
        /// <summary>Marriage vocabulary tight enough to avoid firing on unrelated prose.</summary>
        private static readonly string[] MarriageWords = {
            "married", "marry", "marries", "wed", "wedded", "weds", "wedding", "husband", "wife", "matrimony"
        };

        /// <summary>
        ///   A future or merely-intended marriage ("agreed to marry", "set a wedding date") is not the
        ///   falsifiable claim this policy guards against: only a CONSUMMATED wedding is worth gating on
        ///   real engine state. Courtship talk of an eventual marriage is exactly what the courtship arc
        ///   is for, and never itself needs correcting against Hero.Spouse.
        /// </summary>
        private static readonly string[] PendingOrFutureMarkers = {
            "will marry", "will wed", "plan to marry", "planned to marry", "plans to marry",
            "agree to marry", "agreed to marry", "agrees to marry", "intend to marry", "intends to marry",
            "intended to marry", "promise to marry", "promised to marry", "engaged", "engagement",
            "betrothed", "betrothal", "wedding date", "someday"
        };

        /// <summary>A pronoun-anchored phrase that pins the claim to the player's own union without either name being spelled out.</summary>
        private static readonly string[] FirstPersonPluralMarkers = {
            "we were wed", "we are wed", "we wed", "we married", "our wedding", "our marriage",
            "made us man and wife"
        };

        /// <summary>
        ///   One [EVENT] summary and the two people it might be about. THREE STRINGS in a row where the first
        ///   is PROSE and the other two are NAMES searched for inside it: swap the summary with a name and the
        ///   rule looks for a whole sentence inside a name, and no wedding is ever recognised again.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class MarriageClaimFacts
        {
           /// <summary>The [EVENT] summary text the model emitted.</summary>
           public string Summary { get; set; }

           /// <summary>The conversation NPC's display name, possibly a full titled name.</summary>
           public string NpcName { get; set; }

           /// <summary>The player's display name.</summary>
           public string PlayerName { get; set; }
        }

        /// <summary>
        ///   True when <c>Summary</c> claims the NPC and the player are now wed: it uses
        ///   marriage vocabulary, is not merely a future/pending intention, and carries evidence the
        ///   claim is about THIS pair (both names present, or a first-person-plural phrasing). False for
        ///   null/blank input, a third party's wedding, or a mere engagement.
        /// </summary>
        /// <param name="summary">The [EVENT] summary text emitted by the model.</param>
        /// <param name="npcName">The conversation NPC's display name (may be a full titled name).</param>
        /// <param name="playerName">The player's display name.</param>
        public static bool ClaimsMarriageWithPlayer(MarriageClaimFacts facts)
        {
           if (facts == null) return false;

           string summary = facts.Summary;
           string npcName = facts.NpcName;
           string playerName = facts.PlayerName;

            if (string.IsNullOrWhiteSpace(summary)) return false;

            if (!ContainsAny(summary, MarriageWords)) return false;
            if (ContainsAny(summary, PendingOrFutureMarkers)) return false;

            if (ContainsAny(summary, FirstPersonPluralMarkers)) return true;

            return ContainsName(summary, npcName) && ContainsName(summary, playerName);
        }

        /// <summary>Whole-word/whole-phrase, case-insensitive containment (so "wed" never matches inside "wedding" or "steward").</summary>
        private static bool ContainsAny(string text, string[] candidates)
        {
            foreach (string candidate in candidates)
                if (Regex.IsMatch(text, $@"\b{Regex.Escape(candidate)}\b", RegexOptions.IgnoreCase))
                    return true;

            return false;
        }

        /// <summary>
        ///   Whether any token (length >= 2) of <paramref name="fullName" /> appears in
        ///   <paramref name="text" /> as a whole word, so a short form ("Mesui" out of a fuller titled
        ///   name) or a two-word given name ("Huan Yi") still matches without requiring the full string.
        /// </summary>
        private static bool ContainsName(string text, string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return false;

            foreach (string token in fullName.Split((char[]) null, StringSplitOptions.RemoveEmptyEntries))
                if (token.Length >= 2 && Regex.IsMatch(text, $@"\b{Regex.Escape(token)}\b", RegexOptions.IgnoreCase))
                    return true;

            return false;
        }
    }
}
