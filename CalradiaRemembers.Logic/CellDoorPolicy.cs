// Code written by Gabriel Mailhot, 16/11/2026.
// News stops at the cell door.
//
// Gabriel's rule of 12/09/2026, "No courier reaches a cell", was built as "a prisoner knows nothing": the realm's news,
// his house's wars, the underworld, all cut, while the prompt told him "What you knew BEFORE is still yours" and nothing
// supplied it. Schmidt (Discord, 16/11/2026): "NPC's are way too oblivious in prison, even if they were locked up the
// same day, they have no idea what happened outside or what other lords of their faction were doing when they were
// captured." And the other way: three channels were never cut (the facts the player's message names, the player's deeds
// heard, the shared past), so a man in a cell could be handed today's events. Gabriel (16/11/2026): "oui vas-y" -
// knowledge stops on the day he was taken: everything dated up to it is still his, nothing after it reaches him.
//
// If this is wrong one way, a captive talks of a war declared while he sat in the dungeon; the other, a lord taken this
// morning has never heard of the war he was taken in.

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>What decides the last day of news a character can know.</summary>
    public sealed class CellDoorFacts
    {
        /// <summary>The character is held prisoner now.</summary>
        public bool IsPrisoner { get; init; }

        /// <summary>The campaign day he was taken, when the game knows it; null when it does not.</summary>
        public int? CaptureDay { get; init; }

        /// <summary>Today's campaign day.</summary>
        public int Today { get; init; }
    }

    /// <summary>A change of war or peace between the character's realm and another, after he was taken.</summary>
    public sealed class RealmChangeSinceCapture
    {
        /// <summary>The day it happened.</summary>
        public int Day { get; init; }

        /// <summary>The other realm (an id or a name, the same kind as the list of realms at war now).</summary>
        public string OtherRealm { get; init; }

        /// <summary>True for a war declared, false for a peace struck.</summary>
        public bool WarBegan { get; init; }
    }

    /// <summary>The cell door: the last day of news a character knows, and the wars his realm fought that day.</summary>
    public static class CellDoorPolicy
    {
        /// <summary>
        ///   The last day whose news this character can know: today for a free one, the day he was taken for a prisoner.
        ///   Null for a prisoner whose capture day is unknown: he knows no dated news at all (the rule as it stood).
        /// </summary>
        public static int? LastNewsDay(CellDoorFacts f)
        {
            if (f == null) return null;
            if (!f.IsPrisoner) return f.Today;
            if (f.CaptureDay == null) return null;

            return f.CaptureDay.Value < f.Today ? f.CaptureDay.Value : f.Today;
        }

        /// <summary>True when news of an event on <paramref name="eventDay" /> can be known by one whose news stops on <paramref name="lastNewsDay" />.</summary>
        public static bool Admits(int eventDay, int? lastNewsDay) => lastNewsDay != null && eventDay <= lastNewsDay.Value;

        /// <summary>
        ///   The realms his realm was at war with on the day he was taken, from the wars of today undone by what changed
        ///   since: for each realm, the FIRST change after his capture tells what held before it (a war declared since means
        ///   peace then; a peace struck since means war then); a realm with no change since stands as it stands today.
        ///   Reading only the changes since the capture keeps it right for wars older than any record.
        /// </summary>
        public static IReadOnlyList<string> AtWarWhenTaken(IEnumerable<string> atWarNow, IEnumerable<RealmChangeSinceCapture> changesSinceCapture)
        {
            var result = new List<string>((atWarNow ?? Enumerable.Empty<string>()).Where(r => !string.IsNullOrWhiteSpace(r)).Distinct());

            IEnumerable<RealmChangeSinceCapture> firsts = (changesSinceCapture ?? Enumerable.Empty<RealmChangeSinceCapture>())
                                                          .Where(c => c != null && !string.IsNullOrWhiteSpace(c.OtherRealm))
                                                          .GroupBy(c => c.OtherRealm)
                                                          .Select(g => g.OrderBy(c => c.Day).First());

            foreach (RealmChangeSinceCapture first in firsts)
            {
                bool atWarThen = !first.WarBegan;
                if (atWarThen && !result.Contains(first.OtherRealm)) result.Add(first.OtherRealm);
                if (!atWarThen) result.Remove(first.OtherRealm);
            }

            return result;
        }
    }
}
