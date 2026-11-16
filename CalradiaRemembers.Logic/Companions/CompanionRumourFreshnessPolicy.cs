// Code written by Gabriel Mailhot, 17/10/2026.
// LapitaPolata (Nexus, 17/10/2026): "They are often a few hours or maybe days late to bring the news. Once a companion
// came to me and told me that a castle was being sieged. But by that time, the castle was already taken. ... Another
// time a companion told me that a war broke out and told me to get prepared. But I was already on my way to siege with
// my legion. It's like they were not in my team witnessing everything with me, but were calling from the other side of
// the continent."
//
// A companion's "word of the world" was any event they would have heard of, up to the awareness window (about three
// weeks for a war), and nothing checked it against the world when it was spoken. This decides whether a piece of news
// is still news to bring to the player: fresh, not something the player lived or their own realm did, not overtaken by
// what came after, and still true. And it words how old it is, so it is told as past.
//
// Lordfadooboo (Nexus bug tracker, 16/11/2026, 2.8.12): "whether i play with the toggle set to on or set to off, my
// companions still ask to speak with me to tell me about a random castle that was taken by a random lord." The MCM
// switch "NPCs Raise World News" promises, when off, that "an NPC approaches you only about something between the two
// of you"; it held only the lords' gossip, never a companion's news. Gabriel (16/11/2026): every companion rumour obeys it.

namespace CalradiaRemembers.Logic.Companions
{
    /// <summary>One piece of news a companion travelling with the player might bring, against the world as it is now.</summary>
    public sealed class CompanionRumourFreshnessFacts
    {
        /// <summary>Days since it happened.</summary>
        public int AgeDays { get; init; }

        /// <summary>The player took part in it, or it is about the player: they were there, it is not news to them.</summary>
        public bool InvolvesThePlayer { get; init; }

        /// <summary>A war or a peace in which the player's own realm (or clan) is a side: they know it first.</summary>
        public bool IsThePlayersOwnWarOrPeace { get; init; }

        /// <summary>A later event has overtaken it: the place fell after the siege fight being told.</summary>
        public bool OvertakenSince { get; init; }

        /// <summary>It is no longer so: a war since made peace, a peace since broken.</summary>
        public bool NoLongerTrue { get; init; }

        /// <summary>
        ///   The player turned "NPCs Raise World News" off (the default): no one comes to bring distant news, a companion
        ///   included. Everything this topic brings is news the player had no part in, so off means none of it.
        /// </summary>
        public bool WorldNewsTurnedOff { get; init; }
    }

    /// <summary>Whether a companion still brings a piece of news to the player, and how its age is said.</summary>
    public static class CompanionRumourFreshnessPolicy
    {
        /// <summary>
        ///   A companion travels with the player: news older than this would have reached the player already, the same
        ///   way it reached the companion. The awareness window (40 days, RumorAwareness) still serves letters and what a
        ///   character has heard; only the news a companion comes to bring is held to this.
        /// </summary>
        public const int MaxAgeDays = 7;

        /// <summary>Null when the companion may bring it; else why not, for the log and cr.audience_topics.</summary>
        public static string WhyNotNews(CompanionRumourFreshnessFacts f)
        {
            if (f == null) return "no news";
            if (f.WorldNewsTurnedOff) return "NPCs Raise World News is off";
            if (f.InvolvesThePlayer) return "the player was part of it";
            if (f.IsThePlayersOwnWarOrPeace) return "a war or peace of the player's own realm";
            if (f.OvertakenSince) return "overtaken since (the place has fallen)";
            if (f.NoLongerTrue) return "no longer true";
            if (f.AgeDays > MaxAgeDays) return $"{f.AgeDays} days old, older than {MaxAgeDays}";

            return null;
        }

        /// <summary>How old it is, in words, to tell it as past: "today", "yesterday", "3 days ago".</summary>
        public static string AgeWords(int ageDays)
            => ageDays <= 0 ? "today"
             : ageDays == 1 ? "yesterday"
             : ageDays + " days ago";
    }
}
