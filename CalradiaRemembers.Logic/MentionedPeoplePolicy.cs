// Code written by Gabriel Mailhot, 16/11/2026.
// VonLemberg and his tester Goeth (Discord, 16/11/2026, two source audits of 2.8.12). First: a hero is admitted to "NAMES
// MENTIONED IN THE PLAYER'S MESSAGE" on any one shared word of "Name of House (Culture)", and every match is written into
// the prompt with no cap; Goeth saw a block of about 6,000 characters. A culture word ("Empire", "Battanian") matches every
// hero of that culture. Second: the line names a hero's house and culture but never the realm the house serves, so "Harry
// of Strickland (Volantene)" reads as loyal to his birth, and a model can take culture for allegiance.
// Gabriel (16/11/2026): "oui vas-y pour 1, 2 et 3".
//
// This decides which of the people found are written into the prompt, in what order, and what is said of those left out;
// and how a mentioned person's present allegiance is worded. Prompt only: the verbs' own entity resolution
// (ExternalVerbAdapter) reads the full match list, unchanged.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>One person found in the player's message, and how they were found.</summary>
    public sealed class MentionCandidate
    {
        /// <summary>Who they are (the hero id), handed back in the chosen order.</summary>
        public string Key { get; init; }

        /// <summary>The speaker's spouse, parent or child: the one meant first when a name is shared.</summary>
        public bool IsKin { get; init; }

        /// <summary>The speaker knows them: same house, or something lies between them (regard, a reason).</summary>
        public bool IsKnown { get; init; }

        /// <summary>A word of their own name was used: the player named this person.</summary>
        public bool MatchedPersonalName { get; init; }

        /// <summary>A word of their house's name was used: how a player asks about a house.</summary>
        public bool MatchedHouse { get; init; }

        /// <summary>A word of their culture was used. Alone, it names a people, not this person.</summary>
        public bool MatchedCulture { get; init; }

        /// <summary>How many words of their label the player used (a full name beats one shared word).</summary>
        public int MatchedWords { get; init; }
    }

    /// <summary>The people written into the prompt, in order, and how many were found but left out.</summary>
    public sealed class MentionSelection
    {
        /// <summary>The keys to describe, most likely meant first.</summary>
        public IReadOnlyList<string> Shown { get; init; } = Array.Empty<string>();

        /// <summary>Found by their name or house but past the cap: said in one line, so the speaker can ask which.</summary>
        public int Omitted { get; init; }
    }

    /// <summary>A mentioned person's house and the realm it serves now, as the game holds it.</summary>
    public sealed class AllegianceFacts
    {
        /// <summary>Their house's name. Blank: no house (a notable, a wanderer), and nothing is said.</summary>
        public string HouseName { get; init; }

        /// <summary>The realm the house serves now. Blank: sworn to none.</summary>
        public string RealmName { get; init; }

        /// <summary>The house serves that realm for pay, not by oath.</summary>
        public bool IsMercenary { get; init; }

        /// <summary>This person rules that realm.</summary>
        public bool RulesTheRealm { get; init; }

        /// <summary>A free company (a minor faction) rather than a noble house.</summary>
        public bool IsFreeCompany { get; init; }

        /// <summary>Bandits, outlaws: no allegiance to word.</summary>
        public bool IsOutlawBand { get; init; }
    }

    /// <summary>Which mentioned people reach the prompt, and how their allegiance is said.</summary>
    public static class MentionedPeoplePolicy
    {
        /// <summary>
        ///   Most people a single message is taken to mean. A line runs about 100 to 350 characters, so this holds the
        ///   block near 2,000 at worst (Goeth measured about 6,000); a player naming more than this many at once is rare,
        ///   and the rest are counted, not dropped silently.
        /// </summary>
        public const int MaxShown = 8;

        /// <summary>
        ///   Drops whoever was found only through a culture word (it names a people), orders the rest (kin, then those
        ///   named by their own name, then those the speaker knows, then the most words matched, then as found) and keeps
        ///   the first <see cref="MaxShown" />.
        /// </summary>
        public static MentionSelection Select(IReadOnlyList<MentionCandidate> candidates)
        {
            if (candidates == null || candidates.Count == 0) return new MentionSelection();

            List<MentionCandidate> meant = candidates
                .Select((c, i) => (c, i))
                .Where(x => x.c != null && !(x.c.MatchedCulture && !x.c.MatchedPersonalName && !x.c.MatchedHouse))
                .OrderByDescending(x => x.c.IsKin)
                .ThenByDescending(x => x.c.MatchedPersonalName)
                .ThenByDescending(x => x.c.IsKnown)
                .ThenByDescending(x => x.c.MatchedWords)
                .ThenBy(x => x.i)
                .Select(x => x.c)
                .ToList();

            return new MentionSelection {
                Shown = meant.Take(MaxShown).Select(c => c.Key).ToList(),
                Omitted = Math.Max(0, meant.Count - MaxShown)
            };
        }

        /// <summary>The honest line for those left out, or null when nobody was.</summary>
        public static string OmittedLine(int omitted)
            => omitted <= 0
                ? null
                : $"{omitted} {(omitted == 1 ? "other person also bears" : "others also bear")} a word of that name; if the player may mean one of them, ask which in character rather than guess.";

        /// <summary>
        ///   "their house, Meroc, serves Vlandia", "rules Vlandia", "their house, Lochana, fights for Vlandia for pay",
        ///   "a free company sworn to no realm", "their house, Meroc, is sworn to no realm"; null when there is no house
        ///   or the band is outlaw. The realm the house serves NOW, which a culture never tells.
        /// </summary>
        public static string AllegianceClause(AllegianceFacts f)
        {
            if (f == null || f.IsOutlawBand || string.IsNullOrWhiteSpace(f.HouseName)) return null;

            string house = f.HouseName.Trim();
            string realm = string.IsNullOrWhiteSpace(f.RealmName) ? null : f.RealmName.Trim();

            if (realm == null) return f.IsFreeCompany ? $"{house}, a free company sworn to no realm" : $"their house, {house}, is sworn to no realm";
            if (f.RulesTheRealm) return $"rules {realm}";
            if (f.IsMercenary) return $"their {(f.IsFreeCompany ? "company" : "house")}, {house}, fights for {realm} for pay";

            return $"their house, {house}, serves {realm}";
        }

        /// <summary>Said once under the list: a culture is where someone comes from, the allegiance is whom they serve now.</summary>
        public const string CultureIsNotAllegiance =
            "A culture in parentheses is where someone comes from, not whom they serve: the allegiance given is the realm their house serves now.";
    }
}
