// Code written by Gabriel Mailhot, 20/07/2026.
// ASSEMBLY pillar, Council module: the per-session record of every motion and its current status. This is the
// single source of truth the roster panel reads ("what did this member decide or receive?") and the executor
// commits at the lift. Plain settable data so the mod can round-trip it through JSON SyncData; every status move
// is guarded by ResolutionLifecycle so an illegal transition (e.g. committing a reversed motion) cannot happen.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>One motion: what it is, who it lands on, its lifecycle status, and, if it failed, why.</summary>
    public sealed class AssemblyResolutionEntry
    {
        /// <summary>Stable id assigned when the motion is proposed; used to agree, reverse, or commit it.</summary>
        public string Id { get; set; }

        /// <summary>The resolution type key (e.g. an assign-party-role kind), so the executor knows what to run.</summary>
        public string Kind { get; set; }

        /// <summary>StringId of the hero the motion lands on (shown under his roster row).</summary>
        public string TargetHeroId { get; set; }

        /// <summary>Short label for the panel (e.g. "Scout role").</summary>
        public string Label { get; set; }

        /// <summary>Fuller description of what was decided.</summary>
        public string Detail { get; set; }

        /// <summary>
        ///   Kind-specific grounding data the lift executor needs beyond <see cref="Detail" /> (e.g. a named
        ///   settlement for a "quest" pledge, so a bandit-clear task can be grounded the same way an ordinary
        ///   [QUEST] block would be). Null when the kind needs none, or none was given.
        /// </summary>
        public string TargetHint { get; set; }

        public ResolutionStatus Status { get; set; }

        /// <summary>The player turn on which the motion was raised.</summary>
        public int Turn { get; set; }

        /// <summary>Set only when the commit failed at the lift, so the panel can show the honest reason.</summary>
        public string FailureReason { get; set; }
    }

    /// <summary>The mutable ledger of one assembly's motions, guarded by the resolution lifecycle.</summary>
    public sealed class AssemblyLedger
    {
        /// <summary>Every motion raised this session, in the order raised.</summary>
        public List<AssemblyResolutionEntry> Entries { get; set; } = new List<AssemblyResolutionEntry>();

        /// <summary>Puts a new motion on the table as Proposed and returns it.</summary>
        public AssemblyResolutionEntry Propose(string id, string kind, string targetHeroId, string label, string detail, int turn)
        {
            var entry = new AssemblyResolutionEntry
            {
                Id = id,
                Kind = kind,
                TargetHeroId = targetHeroId,
                Label = label,
                Detail = detail,
                Status = ResolutionStatus.Proposed,
                Turn = turn
            };
            Entries.Add(entry);
            return entry;
        }

        /// <summary>Consents to a proposed motion. No-op returning false if the id is unknown or the move illegal.</summary>
        public bool Agree(string id) => TryTransition(id, ResolutionStatus.Agreed);

        /// <summary>
        ///   Withdraws a provisional motion (the cancel path). Legal only while the motion is still Proposed or
        ///   Agreed; once committed at the lift it cannot be taken back, because a real effect would then exist.
        /// </summary>
        public bool Reverse(string id)
        {
            AssemblyResolutionEntry e = Find(id);
            if (e == null || !ResolutionLifecycle.CanReverse(e.Status)) return false;
            e.Status = ResolutionStatus.Reversed;
            return true;
        }

        /// <summary>At the lift: marks an agreed motion as applied after the bridge ran its effect.</summary>
        public bool MarkApplied(string id) => TryTransition(id, ResolutionStatus.Applied);

        /// <summary>At the lift: marks an agreed motion as failed and records the honest reason for the panel.</summary>
        public bool MarkFailed(string id, string reason)
        {
            AssemblyResolutionEntry e = Find(id);
            if (e == null || !ResolutionLifecycle.CanTransition(e.Status, ResolutionStatus.Failed)) return false;
            e.Status = ResolutionStatus.Failed;
            e.FailureReason = reason;
            return true;
        }

        /// <summary>Every motion landing on one hero, for that hero's roster panel.</summary>
        public IEnumerable<AssemblyResolutionEntry> EntriesFor(string heroId)
            => Entries.Where(e => string.Equals(e.TargetHeroId, heroId, StringComparison.Ordinal));

        /// <summary>The motions that commit when the assembly is lifted: the agreed ones, and only those.</summary>
        public IEnumerable<AssemblyResolutionEntry> PendingCommit()
            => Entries.Where(e => ResolutionLifecycle.CommitsAtLift(e.Status));

        private AssemblyResolutionEntry Find(string id)
            => Entries.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.Ordinal));

        private bool TryTransition(string id, ResolutionStatus to)
        {
            AssemblyResolutionEntry e = Find(id);
            if (e == null || !ResolutionLifecycle.CanTransition(e.Status, to)) return false;
            e.Status = to;
            return true;
        }
    }
}
