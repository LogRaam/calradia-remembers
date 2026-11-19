// Code written by Gabriel Mailhot, 18/11/2026.
// Past bonds written into a character override. Lufaine (Discord, 18/11/2026): "An additional section for the character
// overrides txt for past interpersonal relationships with other characters/player. For example, if a companion/character's
// lore involves another character, but it hasn't been physically played out on screen. Past romances, rivalries, etc. I've
// been getting around it by manually adding day 0 memories to the characters, but would be neat if there was a way to
// instantly load it into the game via cr.reload_lore without having to bloat the "backstory" entry."
// Gabriel (18/11/2026) took the recommendation: a "past" list in the override, recalled when the other person is named or
// present rather than always in the prompt; a bond with the player only from the campaign's own overlay, since a shared
// pack cannot know who the player will be; no starting relations (the History Editor does that), the bond stays a memory.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>One past bond as the author writes it in character_overrides.json ("past": [ ... ]).</summary>
    public sealed class PastBond
    {
        /// <summary>The other person: a hero id, a name only one hero bears, or "player" (campaign overlay only).</summary>
        [JsonProperty("with")]
        public string With { get; set; }

        /// <summary>One of <see cref="PastBondPolicy.Kinds" />; an unknown kind is read as "other".</summary>
        [JsonProperty("kind")]
        public string Kind { get; set; }

        /// <summary>What lay between them, in the author's words: the line the character remembers.</summary>
        [JsonProperty("summary")]
        public string Summary { get; set; }

        /// <summary>Read from this campaign's own overlay file, not the shared one: only then may it name the player.</summary>
        [JsonIgnore]
        public bool FromCampaign { get; set; }
    }

    /// <summary>A bond with both people resolved to hero ids, as the recall reads it.</summary>
    public sealed class PastBondEntry
    {
        /// <summary>The hero whose override wrote the bond.</summary>
        public string OwnerId { get; init; }

        /// <summary>The other person's hero id (the player's own id for a bond with the player).</summary>
        public string OtherId { get; init; }

        /// <summary>The normalised kind (<see cref="PastBondPolicy.KindOf" />).</summary>
        public string Kind { get; init; }

        public string Summary { get; init; }

        /// <summary>Written in this campaign's overlay: a bond with the player is kept only then.</summary>
        public bool FromCampaign { get; init; }
    }

    /// <summary>What the recall needs to know about one turn of conversation.</summary>
    public sealed class PastBondRecallFacts
    {
        /// <summary>The character speaking.</summary>
        public string SpeakerId { get; init; }

        /// <summary>The player's hero id: always in the scene.</summary>
        public string PlayerId { get; init; }

        /// <summary>Every resolved bond of every override (either side of a bond remembers it).</summary>
        public IReadOnlyCollection<PastBondEntry> Bonds { get; init; } = Array.Empty<PastBondEntry>();

        /// <summary>Hero ids the player named in this message.</summary>
        public ICollection<string> NamedIds { get; init; } = Array.Empty<string>();

        /// <summary>Hero ids of the others in the scene (witnesses), besides the player.</summary>
        public ICollection<string> PresentIds { get; init; } = Array.Empty<string>();
    }

    /// <summary>A bond the speaker brings to mind this turn, seen from the speaker's side.</summary>
    public sealed class RecalledBond
    {
        /// <summary>The other person's hero id.</summary>
        public string OtherId { get; init; }

        public string Kind { get; init; }

        public string Summary { get; init; }

        /// <summary>The other person is the player.</summary>
        public bool WithPlayer { get; init; }
    }

    /// <summary>Which past bonds a character brings to mind, and how each reads in the prompt. Pure.</summary>
    public static class PastBondPolicy
    {
        /// <summary>The kinds a bond may be.</summary>
        public static readonly IReadOnlyList<string> Kinds = new[] {"romance", "rivalry", "friendship", "feud", "other"};

        /// <summary>The word that names the player in the "with" field.</summary>
        public const string PlayerToken = "player";

        /// <summary>The most bonds brought to mind in one turn.</summary>
        public const int MaxLines = 4;

        /// <summary>The kind as written, lower-cased; anything unknown or missing is "other".</summary>
        public static string KindOf(string written)
        {
            string k = written?.Trim().ToLowerInvariant();

            return k != null && Kinds.Contains(k) ? k : "other";
        }

        /// <summary>True when the "with" field names the player rather than a hero.</summary>
        public static bool NamesThePlayer(string with) => string.Equals(with?.Trim(), PlayerToken, StringComparison.OrdinalIgnoreCase);

        /// <summary>What is wrong with one written bond, in plain words for cr.lore_status; empty when it is clean.</summary>
        public static IEnumerable<string> Problems(PastBond bond)
        {
            if (bond == null) yield break;

            string who = string.IsNullOrWhiteSpace(bond.With) ? "a past bond" : $"the past bond with \"{bond.With.Trim()}\"";
            if (string.IsNullOrWhiteSpace(bond.With)) yield return "a past bond names no one in \"with\" and is ignored";
            if (string.IsNullOrWhiteSpace(bond.Summary)) yield return $"{who} has no summary and is ignored";
            if (!string.IsNullOrWhiteSpace(bond.Kind) && !Kinds.Contains(bond.Kind.Trim().ToLowerInvariant()))
                yield return $"{who}: kind \"{bond.Kind.Trim()}\" is not one of {string.Join(", ", Kinds)}, read as other";
            if (NamesThePlayer(bond.With) && !bond.FromCampaign)
                yield return $"{who} is ignored here: a bond with the player belongs in this campaign's own character_overrides.json (Custom/campaigns/<id>/), since a shared file cannot know who the player will be";
        }

        /// <summary>
        ///   The bonds the speaker brings to mind this turn: those the speaker's override wrote and those another override
        ///   wrote with the speaker (both people remember), whose other person is the player, is in the scene, or was named.
        ///   The player first, then those present, then those named; at most <see cref="MaxLines" />.
        /// </summary>
        public static IReadOnlyList<RecalledBond> Recall(PastBondRecallFacts facts)
        {
            if (facts?.Bonds == null || string.IsNullOrEmpty(facts.SpeakerId)) return Array.Empty<RecalledBond>();

            var found = new List<(RecalledBond Bond, int Rank)>();
            var seen = new HashSet<string>();

            foreach (PastBondEntry b in facts.Bonds)
            {
                if (b == null || string.IsNullOrWhiteSpace(b.Summary) || string.IsNullOrEmpty(b.OwnerId) || string.IsNullOrEmpty(b.OtherId)) continue;

                string other = b.OwnerId == facts.SpeakerId ? b.OtherId : b.OtherId == facts.SpeakerId ? b.OwnerId : null;
                if (other == null || other == facts.SpeakerId) continue;

                bool withPlayer = facts.PlayerId != null && (b.OtherId == facts.PlayerId || b.OwnerId == facts.PlayerId);
                if (withPlayer && !b.FromCampaign) continue;

                int rank = withPlayer ? 0
                         : facts.PresentIds?.Contains(other) == true ? 1
                         : facts.NamedIds?.Contains(other) == true ? 2
                         : -1;
                if (rank < 0) continue;

                string summary = b.Summary.Trim();
                if (!seen.Add(other + "\n" + summary)) continue;

                found.Add((new RecalledBond {OtherId = other, Kind = KindOf(b.Kind), Summary = summary, WithPlayer = withPlayer}, rank));
            }

            return found.OrderBy(f => f.Rank).Take(MaxLines).Select(f => f.Bond).ToList();
        }

        /// <summary>
        ///   One line of the remembered-past block: the speaker lived it, it is older than the campaign, and it names the other
        ///   person (the player as "the one before you", whose name the speaker may not know).
        /// </summary>
        public static string Line(RecalledBond bond, string otherName)
        {
            if (bond == null || string.IsNullOrWhiteSpace(bond.Summary)) return null;

            string who = bond.WithPlayer ? "the one before you" : string.IsNullOrWhiteSpace(otherName) ? "someone" : otherName.Trim();
            string opening = bond.Kind switch {
                "romance" => $"An old romance with {who}",
                "rivalry" => $"An old rivalry with {who}",
                "friendship" => $"An old friendship with {who}",
                "feud" => $"An old feud with {who}",
                _ => $"Between you and {who}, long ago"
            };

            return $"- {PastOnMentionPolicy.LivedItTag}{opening}: {bond.Summary.Trim().TrimEnd('.', ' ')} (before the present days).";
        }
    }
}
