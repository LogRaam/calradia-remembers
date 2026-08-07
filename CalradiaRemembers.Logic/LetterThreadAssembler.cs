// Code written by Gabriel Mailhot, 07/08/2026.
// The letter archive shows a player's whole correspondence with one contact as a dated, threaded
// conversation. This pure assembler turns the two persisted letter lists (the NPC's letters to the
// player, and the player's letters to the NPC, including the player's inline replies) into a single
// chronological thread, oldest-first, each entry carrying a dated header. It is the testable core of
// the archive: the in-game viewer (LetterArchiveViewModel) only binds what this produces, so the
// ordering, the header wording, and the display cap are all pinned here rather than in engine code.

#region

using System.Collections.Generic;
using System.Linq;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Which side of the correspondence a thread entry sits on.</summary>
    public enum LetterDirection
    {
        /// <summary>A letter the NPC wrote to the player.</summary>
        Incoming,

        /// <summary>A letter (or reply) the player wrote to the NPC.</summary>
        Outgoing
    }

    /// <summary>One line of the correspondence thread: a dated header and the letter body.</summary>
    public sealed class LetterThreadEntry
    {
        public LetterThreadEntry(LetterDirection direction, int day, string header, string body)
        {
            Direction = direction;
            Day = day;
            Header = header;
            Body = body;
        }

        public LetterDirection Direction { get; }

        /// <summary>The in-game day the entry is dated to (when the letter was written / the reply sent).</summary>
        public int Day { get; }

        /// <summary>"Letter from X, autumn of the 3rd year, day 12" / "Your letter to X, ...".</summary>
        public string Header { get; }

        /// <summary>The letter (or reply) text.</summary>
        public string Body { get; }

        public bool IsIncoming => Direction == LetterDirection.Incoming;
    }

    /// <summary>Builds the dated, chronological correspondence thread for one contact.</summary>
    public static class LetterThreadAssembler
    {
        /// <summary>
        ///   The dated header for a thread entry, in the same Calradian calendar the chronicle uses
        ///   ("Letter from X, autumn of the 3rd year, day 12"). When the day is unknown (0 on letters
        ///   from a save written before dates were stored) the date clause is omitted rather than
        ///   printing a confidently wrong "spring of the 1st year".
        /// </summary>
        public static string Header(LetterDirection direction, string contactName, int day, bool isReply = false)
        {
            string who = string.IsNullOrEmpty(contactName) ? "?" : contactName;
            string opener = direction == LetterDirection.Incoming
                ? $"Letter from {who}"
                : isReply
                    ? $"Your reply to {who}"
                    : $"Your letter to {who}";

            if (day <= 0) return opener;

            string date = $"{ChronicleCalendar.SeasonNameOf(day)} of the "
                          + $"{ChronicleCalendar.Ordinal(ChronicleCalendar.YearOf(day))} year, "
                          + $"day {ChronicleCalendar.DayOfYear(day)}";

            return $"{opener}, {date}";
        }

        /// <summary>
        ///   Assembles the correspondence thread, oldest-first. Includes only letters that were actually
        ///   part of the exchange (delivered, not lost, with a body); in-flight and waylaid letters are not
        ///   correspondence yet and never appear. A player reply that rode inline on a received letter is
        ///   surfaced as its own outgoing entry, dated to the day the player read the letter, so the archive
        ///   reads as the full back-and-forth the player asked to see. Capped to the newest <paramref name="cap" />
        ///   entries so a decades-long correspondence never floods the view.
        /// </summary>
        public static IReadOnlyList<LetterThreadEntry> Build(string contactName,
            IEnumerable<PendingLetter> incoming,
            IEnumerable<PlayerLetter> outgoing,
            int cap)
        {
            var entries = new List<(int Day, int Tie, LetterThreadEntry Entry)>();

            if (incoming != null)
                foreach (PendingLetter l in incoming)
                {
                    if (!LetterArchivePolicy.IsArchived(l)) continue;

                    int day = ArchiveDay(l.SentOnDay, l.DeliveryDay);
                    entries.Add((day, 0, new LetterThreadEntry(
                        LetterDirection.Incoming, day,
                        Header(LetterDirection.Incoming, contactName, day), l.Content)));

                    // The player's inline reply to this very letter, if they wrote one, dated to the day
                    // they received it (EffectiveDeliveredDay), so it threads directly after the letter.
                    if (!string.IsNullOrWhiteSpace(l.PlayerReply))
                    {
                        int replyDay = l.EffectiveDeliveredDay;
                        entries.Add((replyDay, 1, new LetterThreadEntry(
                            LetterDirection.Outgoing, replyDay,
                            Header(LetterDirection.Outgoing, contactName, replyDay, isReply: true), l.PlayerReply)));
                    }
                }

            if (outgoing != null)
                foreach (PlayerLetter l in outgoing)
                {
                    if (!LetterArchivePolicy.IsArchived(l)) continue;

                    int day = ArchiveDay(l.SentOnDay, l.DeliveryDay);
                    entries.Add((day, 1, new LetterThreadEntry(
                        LetterDirection.Outgoing, day,
                        Header(LetterDirection.Outgoing, contactName, day), l.Content)));
                }

            // Oldest-first. The tie-break keeps a received letter above the player's own letter/reply of the
            // same day, so a same-day exchange reads letter-then-answer rather than the reverse. OrderBy is a
            // stable sort, so letters sharing both keys keep their list order.
            List<LetterThreadEntry> ordered = entries
                .OrderBy(e => e.Day)
                .ThenBy(e => e.Tie)
                .Select(e => e.Entry)
                .ToList();

            if (cap > 0 && ordered.Count > cap)
                ordered = ordered.Skip(ordered.Count - cap).ToList();

            return ordered;
        }

        /// <summary>The day to file a letter under: when it was written, falling back to its due day on old data.</summary>
        private static int ArchiveDay(int sentOnDay, int deliveryDay)
            => sentOnDay > 0 ? sentOnDay : deliveryDay;
    }
}
