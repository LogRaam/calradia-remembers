// Code written by Gabriel Mailhot, 26/10/2026.
// When the player leaves the house for a ruler through the heir, the old character lives on as one of the world's lords,
// wed to her, and the player now plays the heir. Gabriel, 26/10/2026: "oui transferer les souvenirs au 'nouveau' npc".
// Without them, the character the player played for a whole campaign meets the heir as a stranger with no past, and the
// empress forgets the courtship that brought him to her (a succession drops private history for good).
// So the old character is given his own life as memories (his greatest deeds, in order, and why he left the house), and
// the ruler keeps her romance with him as part of her own life, no longer as history with the player.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Dynasty
{
    /// <summary>One deed of the player's chronicle, as the old character will remember it.</summary>
    public sealed class PastDeed
    {
        /// <summary>The day it happened.</summary>
        public int Day { get; init; }

        /// <summary>How much it mattered to the world (the chronicle's own magnitude).</summary>
        public int Magnitude { get; init; }

        /// <summary>What happened, in words.</summary>
        public string Summary { get; init; } = "";
    }

    /// <summary>What the old character is, and to whom, when he leaves the house for a ruler.</summary>
    public sealed class PredecessorFacts
    {
        /// <summary>The old character's name.</summary>
        public string Name { get; init; } = "";

        /// <summary>The heir, now the player.</summary>
        public string HeirName { get; init; } = "";

        /// <summary>What the old character is to the heir ("father", "sister", "kin").</summary>
        public string HeirIsTheir { get; init; } = "kin";

        /// <summary>The ruler he weds.</summary>
        public string RulerName { get; init; } = "";

        /// <summary>The house he left.</summary>
        public string HouseName { get; init; } = "";

        /// <summary>The ruler is a woman (for the pronouns of his memory).</summary>
        public bool RulerIsFemale { get; init; }
    }

    /// <summary>The memories the old character keeps, and the ruler's romance kept as her own life.</summary>
    public static class PredecessorMemoryPolicy
    {
        /// <summary>The most deeds the old character keeps: enough for a life, few enough to stay a summary.</summary>
        public const int MostDeedsKept = 12;

        /// <summary>
        ///   His greatest deeds, the weightiest first chosen, then told in the order they happened, as his own life.
        /// </summary>
        public static List<NotableEvent> OwnLife(IEnumerable<PastDeed> deeds)
            => (deeds ?? Enumerable.Empty<PastDeed>())
               .Where(d => d != null && !string.IsNullOrWhiteSpace(d.Summary))
               .OrderByDescending(d => d.Magnitude).ThenBy(d => d.Day)
               .Take(MostDeedsKept)
               .OrderBy(d => d.Day)
               .Select(d => new NotableEvent(d.Day, NotableEventType.OwnLife, d.Summary.Trim()))
               .ToList();

        /// <summary>
        ///   Why he left: his own life (he won her and gave up his house), and his history with the heir he gave it to.
        /// </summary>
        public static List<NotableEvent> TheLeaving(PredecessorFacts f, int today)
        {
            if (f == null) return new List<NotableEvent>();

            string her = f.RulerIsFemale ? "her" : "him";
            string hers = f.RulerIsFemale ? "her" : "his";

            return new List<NotableEvent> {
                new(today, NotableEventType.OwnLife,
                    $"Won {f.RulerName}, who would never give up {hers} house; gave up the head of {f.HouseName} to wed {her}, and joined {hers}."),
                new(today, NotableEventType.Agreement,
                    $"Handed the head of {f.HouseName} to {f.HeirName}, my {f.HeirIsTheir}, to wed {f.RulerName}.")
            };
        }

        /// <summary>
        ///   The ruler's romance with him, kept as her own life instead of history with the player, who is now the heir:
        ///   a flirt or an intimacy becomes a memory of him by name. Anything else is returned unchanged.
        /// </summary>
        public static NotableEvent KeptAsHerOwnLife(NotableEvent e, string formerPlayerName)
        {
            if (e == null || (e.type != NotableEventType.Flirt && e.type != NotableEventType.Intimacy)) return e;

            string text = ReplaceIgnoringCase(e.summary ?? "", "the player", formerPlayerName);

            return new NotableEvent(e.gameDay, NotableEventType.OwnLife, $"With {formerPlayerName}, before our wedding: {text}");
        }

        private static string ReplaceIgnoringCase(string text, string find, string with)
        {
            if (string.IsNullOrEmpty(with)) return text;

            int at = text.IndexOf(find, StringComparison.OrdinalIgnoreCase);
            while (at >= 0)
            {
                text = text.Substring(0, at) + with + text.Substring(at + find.Length);
                at = text.IndexOf(find, at + with.Length, StringComparison.OrdinalIgnoreCase);
            }

            return text;
        }
    }
}
