// Code written by Gabriel Mailhot, 30/07/2026.
// Bespoke council UI, Inc 1: the ENGAGEMENTS ledger the right-hand column mirrors. Unlike the AssemblyLedger
// (structured [RESOLUTION] motions driven by an explicit propose/agree/reverse lifecycle keyed on an id), an
// engagement EMERGES from the natural conversation: Gabriel ruled out a vote/propose button as an immersion
// break (2026-07-30), so the best roleplay comes from talk, not a UI mechanic. At the running-transcript
// compression cadence the mod runs an LLM extraction that reads the whole session and returns the CURRENT set of
// decisions actually pronounced and assented to in-fiction; this ledger RECONCILES to that snapshot. So a
// decision the table talks its way out of ("finalement, on ne raide pas la Sturgie") simply stops appearing in
// the next snapshot and is dropped here, with no explicit cancel gesture. The ledger is a MIRROR of the
// conversation, not a transaction log: what stands is what the latest extraction saw stand. Nothing here touches
// the world; only the AGREED engagements the player has therefore SEEN are applied when the council is lifted.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>Where an engagement stands in the conversation: still under discussion, or assented to by the party bound.</summary>
    public enum EngagementStatus
    {
        /// <summary>Raised and being weighed, but no one has yet agreed to be bound. Does not apply at the lift.</summary>
        Discussed,

        /// <summary>The concerned party assented in-fiction. This is what applies to the world when the council lifts.</summary>
        Agreed
    }

    /// <summary>
    ///   One item of an extraction snapshot: what the latest reading of the conversation found standing. The mod
    ///   builds these from the LLM extractor's output; the ledger reconciles to the whole list.
    /// </summary>
    public sealed class EngagementSnapshotItem
    {
        /// <summary>
        ///   A short stable identity for this decision, reused across snapshots while it stands, so the ledger
        ///   matches the same engagement turn to turn rather than treating a re-mention as a new one.
        /// </summary>
        public string Key { get; set; }

        /// <summary>Who is bound by the decision (a name or a group), as read from the conversation.</summary>
        public string Subject { get; set; }

        /// <summary>The decision itself, in plain words ("Aeldric leads the raid on Sturgia").</summary>
        public string Text { get; set; }

        /// <summary>Whether the conversation has actually settled this, or is still weighing it.</summary>
        public EngagementStatus Status { get; set; }
    }

    /// <summary>One engagement as the right column shows it: who, what, and whether it is settled.</summary>
    public sealed class CouncilEngagement
    {
        /// <summary>The stable identity the extraction reuses while this decision stands. Keys reconciliation.</summary>
        public string Key { get; set; }

        /// <summary>Who is bound by the decision (a name or a group).</summary>
        public string Subject { get; set; }

        /// <summary>The decision, in plain words.</summary>
        public string Text { get; set; }

        /// <summary>Discussed or Agreed. Only Agreed engagements apply when the council lifts.</summary>
        public EngagementStatus Status { get; set; }
    }

    /// <summary>The live mirror of the conversation's standing decisions, reconciled to each extraction snapshot.</summary>
    public sealed class EngagementLedger
    {
        /// <summary>Every engagement currently standing, in the order first seen. Settable so it can round-trip through a save.</summary>
        public List<CouncilEngagement> Engagements { get; set; } = new List<CouncilEngagement>();

        /// <summary>
        ///   Makes the ledger match <paramref name="snapshot" />, the latest reading of the whole conversation:
        ///   a decision the snapshot no longer carries is DROPPED (the conversation walked it back), a decision it
        ///   carries for the first time is ADDED, and one it still carries is UPDATED in place (its wording or its
        ///   Discussed/Agreed status may have moved). Keying on <see cref="EngagementSnapshotItem.Key" /> is what
        ///   keeps a decision that persists across turns a single row rather than one per mention. Items with no
        ///   key or no text are ignored: an extraction hiccup must never plant an empty row that would then apply
        ///   nothing at the lift. A null snapshot empties the ledger (the conversation now stands for nothing).
        /// </summary>
        public void Reconcile(IReadOnlyList<EngagementSnapshotItem> snapshot)
        {
            List<EngagementSnapshotItem> items = (snapshot ?? new List<EngagementSnapshotItem>())
               .Where(i => i != null && !string.IsNullOrWhiteSpace(i.Key) && !string.IsNullOrWhiteSpace(i.Text))
               .ToList();

            var standingKeys = new HashSet<string>(items.Select(i => i.Key.Trim()), StringComparer.OrdinalIgnoreCase);

            // Retraction: anything the latest extraction no longer sees standing leaves the mirror.
            Engagements.RemoveAll(e => e?.Key == null || !standingKeys.Contains(e.Key.Trim()));

            foreach (EngagementSnapshotItem item in items)
            {
                string key = item.Key.Trim();
                CouncilEngagement existing =
                    Engagements.FirstOrDefault(e => string.Equals(e.Key?.Trim(), key, StringComparison.OrdinalIgnoreCase));

                if (existing == null)
                {
                    Engagements.Add(new CouncilEngagement {
                        Key = key,
                        Subject = item.Subject?.Trim() ?? "",
                        Text = item.Text.Trim(),
                        Status = item.Status
                    });

                    continue;
                }

                existing.Subject = item.Subject?.Trim() ?? existing.Subject;
                existing.Text = item.Text.Trim();
                existing.Status = item.Status;
            }
        }

        /// <summary>
        ///   The engagements that commit when the council is lifted: the AGREED ones, and only those, because a
        ///   decision still being debated was never something the player saw the table settle on.
        /// </summary>
        public IEnumerable<CouncilEngagement> PendingCommit()
            => Engagements.Where(e => e != null && e.Status == EngagementStatus.Agreed);
    }
}
