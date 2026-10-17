// Code written by Gabriel Mailhot, 17/10/2026.
// The past brought up when the player names it. Gabriel's ruling on the world's long-term memory (17/10/2026): it
// "pourrait parfois etre utilise pour des conversations entre joueur et NPC sur le passe"; and on the authored history
// file the same day, he wants to "mettre en scene des evenements du passe qui ne se sont pas produits dans le temps de
// jeu". Both are only worth something if a lord asked about a person, a place or a realm can speak of what he knows of
// it, years later. docs/design/AUTHORED_HISTORY.md section 8: "when the player names a person, a place or a realm, the
// one to three most relevant events the NPC would know go below the CURRENT ENCOUNTER: marker".
//
// Unlike the recent-news block (WorldFactRelevancePolicy), a keyword alone never brings up the past: only a name the
// player said. "How went the battle yesterday?" must not raise a battle of ten years ago.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>How close the speaker stands to a past event, from farthest to nearest. A nearer tie ranks first.</summary>
    public enum PastTie
    {
        /// <summary>Knows it some other way (told in confidence, or the event was great enough for every realm).</summary>
        None,

        /// <summary>Of a realm the event involved.</summary>
        Realm,

        /// <summary>Of the place it happened (his home or his house's fief).</summary>
        Place,

        /// <summary>Of the same clan as someone in it.</summary>
        Clan,

        /// <summary>Kin of someone in it.</summary>
        Kin,

        /// <summary>He was in it himself.</summary>
        Actor
    }

    /// <summary>One long-term event the speaker knows, as the past-on-mention block weighs it.</summary>
    public sealed class PastCandidateFacts
    {
        /// <summary>The event's id, for a stable order and to skip what another block already shows.</summary>
        public string Id { get; init; }

        /// <summary>Everyone and everything the event names: actors, those harmed, realms and the place.</summary>
        public IReadOnlyCollection<string> EntityIds { get; init; } = Array.Empty<string>();

        /// <summary>The ready-to-inject summary line.</summary>
        public string Summary { get; init; }

        /// <summary>The absolute day it happened (an authored event before the campaign has an earlier day).</summary>
        public int Day { get; init; }

        /// <summary>How great it was.</summary>
        public int Magnitude { get; init; }

        /// <summary>How close the speaker stands to it.</summary>
        public PastTie Tie { get; init; }

        /// <summary>A secret few share: the line carries the discretion tag.</summary>
        public bool Secret { get; init; }
    }

    /// <summary>The turn the past is weighed for: what the player named, and when the speaker stands.</summary>
    public sealed class PastMentionRequest
    {
        /// <summary>The ids of the people, places and realms the player named in this message.</summary>
        public IReadOnlyCollection<string> NamedIds { get; init; } = Array.Empty<string>();

        /// <summary>Today, as an absolute day.</summary>
        public int Today { get; init; }

        /// <summary>Days in a game year (the calendar model's).</summary>
        public int DaysInYear { get; init; }

        /// <summary>The speaker's birth, as an absolute day; null when not known.</summary>
        public int? SpeakerBirthDay { get; init; }

        /// <summary>Ids already shown by another block this turn.</summary>
        public IReadOnlyCollection<string> AlreadyShownIds { get; init; } = Array.Empty<string>();
    }

    /// <summary>Picks and words the past events a speaker brings to a turn in which the player named their people.</summary>
    public static class PastOnMentionPolicy
    {
        /// <summary>At most this many past events in one turn (the design's "one to three").</summary>
        public const int MaxLines = 3;

        /// <summary>The note on an event the speaker was in himself: he lived it, he does not repeat hearsay.</summary>
        public const string LivedItTag = "(you were part of it) ";

        /// <summary>The note on an event older than the speaker: he knows it only as it was told to him.</summary>
        public const string BeforeBirthTag = "(before you were born: you know it as it was told) ";

        /// <summary>
        ///   True when the event names someone or something the player named. The cheap test, run before the knowledge
        ///   gate: the past a speaker brings up is the intersection of both, so the order does not change what he knows.
        /// </summary>
        public static bool IsNamed(PastCandidateFacts c, PastMentionRequest r)
            => c != null && r?.NamedIds != null && c.EntityIds != null && c.EntityIds.Any(id => id != null && r.NamedIds.Contains(id));

        /// <summary>
        ///   The events to bring up, best first: named in the message, with a summary, not already shown; the greatest
        ///   first, then the nearest to the speaker, then the most recent. At most <see cref="MaxLines" />.
        /// </summary>
        public static IReadOnlyList<PastCandidateFacts> Pick(IEnumerable<PastCandidateFacts> known, PastMentionRequest r)
        {
            if (known == null || r == null) return Array.Empty<PastCandidateFacts>();

            return known.Where(c => IsNamed(c, r) && !string.IsNullOrWhiteSpace(c.Summary)
                                    && (c.Id == null || r.AlreadyShownIds == null || !r.AlreadyShownIds.Contains(c.Id)))
                        .OrderByDescending(c => c.Magnitude)
                        .ThenByDescending(c => c.Tie)
                        .ThenByDescending(c => c.Day)
                        .ThenBy(c => c.Id, StringComparer.Ordinal)
                        .Take(MaxLines)
                        .ToList();
        }

        /// <summary>How long ago, in words: "within the past year", "a year ago", "nine years ago".</summary>
        public static string AgeOf(int day, PastMentionRequest r)
        {
            int daysInYear = r == null || r.DaysInYear <= 0 ? 1 : r.DaysInYear;
            int years = Math.Max(0, ((r?.Today ?? day) - day) / daysInYear);

            return years == 0 ? "within the past year"
                 : years == 1 ? "a year ago"
                 : years + " years ago";
        }

        /// <summary>
        ///   One line of the block: the discretion tag on a secret, whether the speaker lived it or knows it only as told,
        ///   the summary, and how long ago.
        /// </summary>
        public static string Line(PastCandidateFacts c, PastMentionRequest r)
        {
            if (c == null || string.IsNullOrWhiteSpace(c.Summary)) return null;

            bool beforeBirth = r?.SpeakerBirthDay != null && c.Day < r.SpeakerBirthDay.Value;
            string tie = c.Tie == PastTie.Actor ? LivedItTag : beforeBirth ? BeforeBirthTag : "";

            return $"- {RumorDiscretion.TagFor(c.Secret)}{tie}{c.Summary.Trim().TrimEnd('.', ' ')} ({AgeOf(c.Day, r)}).";
        }

        /// <summary>The whole block, or null when nothing the player named is in the speaker's past.</summary>
        public static string Block(IEnumerable<PastCandidateFacts> known, PastMentionRequest r)
        {
            List<string> lines = Pick(known, r).Select(c => Line(c, r)).Where(l => l != null).ToList();

            return lines.Count == 0 ? null : string.Join("\n", lines);
        }
    }
}
