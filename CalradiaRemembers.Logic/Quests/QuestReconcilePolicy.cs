// Code written by Gabriel Mailhot, 07/07/2026.
// Quest-mirror anti-stuck reconciler, pure decision. The reconciler runs at every session launch and may close
// a native quest-journal entry (the mirror). The hard rule this policy encodes: a launch pass must NEVER cancel
// a quest it cannot POSITIVELY prove is finished, because a false cancel destroys a valid, often already-completed
// task (a repeatedly-reported "my quest auto-failed on load" bug). A mirror is closed ONLY when the giver is
// confirmed dead, or a matching quest is found in a terminal state. Every ambiguous case (giver reference not yet
// restored, profile not loaded in time, id drift after a version upgrade) KEEPS the mirror: a lingering entry is
// a far smaller harm than a destroyed completed quest.

namespace CalradiaRemembers.Logic.Quests
{
    /// <summary>What to do with one quest-journal mirror at session launch.</summary>
    public enum MirrorReconcileDecision
    {
        /// <summary>Leave the mirror open: a matching live quest, OR an ambiguous case we refuse to nuke.</summary>
        Keep,

        /// <summary>Close it: a matching quest exists but is already terminal (completed / cancelled / expired).</summary>
        CloseTerminal,

        /// <summary>Close it: the giver is confirmed dead, so the task can never be reported and paid.</summary>
        CloseGiverGone
    }

    /// <summary>Pure decision for the session-launch quest-mirror reconciler. See the file header for the guiding rule.</summary>
    public static class QuestReconcilePolicy
    {
        /// <summary>
        ///   What can be PROVEN about an orphaned journal mirror. Three booleans in a row read in a strict
        ///   order, and each unproven case must fall through to Keep: this pass can destroy a player's journal
        ///   entry, so a swap here is not a wrong answer, it is a lost quest.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class MirrorFacts
        {
           /// <summary>A POSITIVE death: the giver resolved and is not alive. Never a merely-null reference.</summary>
           public bool GiverConfirmedDead { get; set; }

           /// <summary>
           ///   A matching quest is outstanding or awaiting its reward, which is the whole point of the mirror.
           /// </summary>
           public bool HasLiveMatch { get; set; }

           /// <summary>A matching quest has genuinely terminated, which is a safe and proven close.</summary>
           public bool HasTerminalMatch { get; set; }
        }

        /// <summary>
        ///   Decides a mirror's fate. <c>GiverConfirmedDead</c> must be a POSITIVE death (the giver
        ///   resolved and is not alive), never a merely-null reference. <c>HasLiveMatch</c> is a
        ///   matching quest that is outstanding or awaiting its reward; <c>HasTerminalMatch</c> a
        ///   matching quest in a terminal status. When nothing is confirmed, the answer is always Keep.
        /// </summary>
        public static MirrorReconcileDecision Decide(MirrorFacts facts)
        {
           if (facts == null) return MirrorReconcileDecision.Keep;

           bool giverConfirmedDead = facts.GiverConfirmedDead;
           bool hasLiveMatch = facts.HasLiveMatch;
           bool hasTerminalMatch = facts.HasTerminalMatch;

            // A dead giver makes the task un-turn-in-able regardless of the quest's own state.
            if (giverConfirmedDead) return MirrorReconcileDecision.CloseGiverGone;

            // A living, matching quest is the whole point of keeping the mirror.
            if (hasLiveMatch) return MirrorReconcileDecision.Keep;

            // A matching quest that has genuinely terminated is a safe, proven close.
            if (hasTerminalMatch) return MirrorReconcileDecision.CloseTerminal;

            // Nothing proven: giver alive but no confident match (id drift, store not loaded, migration). Never nuke.
            return MirrorReconcileDecision.Keep;
        }

        /// <summary>
        ///   The other direction of the same reconciliation: re-creating the missing mirror of a live quest,
        ///   which heals saves damaged by the old false-cancel bug. Three booleans in a row, the last of which
        ///   inverts and is what makes the pass idempotent.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class MirrorRestoreFacts
        {
           /// <summary>The quest is outstanding or awaiting its reward.</summary>
           public bool QuestIsLive { get; set; }

           /// <summary>Its giver is a resolved, living hero.</summary>
           public bool GiverAliveAndResolved { get; set; }

           /// <summary>An open mirror already shows it. Running the pass twice must create nothing new.</summary>
           public bool OpenMirrorExists { get; set; }
        }

        /// <summary>
        ///   The journal is a PROJECTION of the authoritative quest store, so reconciliation runs both ways:
        ///   closing orphaned mirrors (<see cref="Decide" />) AND re-creating the missing mirror of a live quest.
        ///   This heals saves damaged by the old false-cancel bug (the quest survived in the store; only its
        ///   journal entry was destroyed) and any future mirror loss. Restore only when the quest is live
        ///   (outstanding or awaiting its reward), its giver is a resolved living hero, and no open mirror
        ///   already shows it (idempotence: running the pass twice creates nothing new).
        /// </summary>
        public static bool ShouldRestoreMirror(MirrorRestoreFacts facts)
        {
           if (facts == null) return false;

           bool questIsLive = facts.QuestIsLive;
           bool giverAliveAndResolved = facts.GiverAliveAndResolved;
           bool openMirrorExists = facts.OpenMirrorExists;

           return questIsLive && giverAliveAndResolved && !openMirrorExists;
        }
    }
}
