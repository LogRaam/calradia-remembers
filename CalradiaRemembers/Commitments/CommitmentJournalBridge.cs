// Code written by Gabriel Mailhot, 03/07/2026.
// RP with teeth, increment 2, Phase 2: bridges TrackedCommitment (the persisted record) and
// CommitmentQuest (the vanilla quest-journal mirror).

#region

using System;
using System.Linq;
using CalradiaRemembers.Logic;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Commitments
{
    /// <summary>
    ///   Bridges <see cref="TrackedCommitment" /> (the persisted record, authority, owned by
    ///   <see cref="CommitmentBehavior" />'s <c>Active</c> list) and <see cref="CommitmentQuest" /> (the
    ///   <c>QuestBase</c> mirror in the vanilla quest journal). The bridge is the ONLY place that opens
    ///   or closes journal mirrors; <see cref="CommitmentBehavior" /> calls these methods right after its
    ///   own lifecycle transitions. Modeled directly on
    ///   <see cref="CalradiaRemembers.Missions.CompanionErrandJournalBridge" />.
    ///   Every entry point is try/catch-guarded: a journal failure must never break the underlying pledge
    ///   pipeline (relation, world-events, the scheme it launched).
    ///   Mirrors are found by the commitment's stable <see cref="TrackedCommitment.Id" />, so closing
    ///   still works even if a hero reference is unavailable (e.g. the promisor died before their
    ///   deadline).
    ///   Anti-stuck guarantee: <see cref="ReconcileOnSessionLaunch" /> is called at every session launch
    ///   and force-closes any mirror whose commitment Id is no longer Active in
    ///   <see cref="CommitmentBehavior" />.
    /// </summary>
    public static class CommitmentJournalBridge
    {
        /// <summary>Closes the mirror: the deadline lapsed with nothing done, an oath broken.</summary>
        public static void CloseBreached(TrackedCommitment commitment)
        {
            if (commitment == null) return;

            try
            {
                CommitmentQuest mirror = FindMirror(commitment.Id);
                if (mirror == null) return;

                // R7-light: MoveAgainst keeps the ORIGINAL two-name line exactly as shipped; the three
                // swear_oath kinds route through CommitmentBehavior's own kind-aware builder instead (the SAME
                // one CommitmentBehavior.Breach itself uses for the profile-event/world-event text), so the
                // player's vanilla quest log never says "moved against" for a pay_gold/keep_peace/protect oath.
                string line = commitment.Kind == CommitmentKind.MoveAgainst
                    ? CommitmentJournalText.BuildBreachedLine(ResolveName(commitment.PromisorId), ResolveName(commitment.TargetId))
                    : CommitmentJournalText.BuildBreachedLine(commitment.Kind, ResolveName(commitment.PromisorId),
                        CommitmentBehavior.ResolveTargetDisplayName(commitment), commitment.PromisedAmount);
                mirror.UpdateStatus(line);
                mirror.CloseBreached(line);
            }
            catch (Exception ex)
            {
                ModLog.Error($"[COMMITMENT-JOURNAL] CloseBreached failed for {commitment.Id}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        /// <summary>Closes the mirror: the pledge was honored before the deadline.</summary>
        public static void CloseFulfilled(TrackedCommitment commitment)
        {
            if (commitment == null) return;

            try
            {
                CommitmentQuest mirror = FindMirror(commitment.Id);
                if (mirror == null) return;

                // R7-light: same kind-aware split as CloseBreached above.
                string line = commitment.Kind == CommitmentKind.MoveAgainst
                    ? CommitmentJournalText.BuildFulfilledLine(ResolveName(commitment.PromisorId), ResolveName(commitment.TargetId))
                    : CommitmentJournalText.BuildFulfilledLine(commitment.Kind, ResolveName(commitment.PromisorId),
                        CommitmentBehavior.ResolveTargetDisplayName(commitment), commitment.PromisedAmount);
                mirror.UpdateStatus(line);
                mirror.CloseFulfilled();
            }
            catch (Exception ex)
            {
                ModLog.Error($"[COMMITMENT-JOURNAL] CloseFulfilled failed for {commitment.Id}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        /// <summary>Creates and starts a mirror for a just-issued pledge. Requires a live promisor Hero.</summary>
        public static void OpenMirror(TrackedCommitment commitment, Hero promisor)
        {
            if (commitment == null || promisor is not {IsAlive: true}) return;

            try
            {
                var mirror = new CommitmentQuest(commitment, promisor);
                ModLog.Info($"[COMMITMENT-JOURNAL] opened mirror: {mirror.StringId}");
            }
            catch (Exception ex)
            {
                ModLog.Error($"[COMMITMENT-JOURNAL] OpenMirror failed for {promisor.Name}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        /// <summary>
        ///   Anti-stuck pass. Runs at every session launch (wired by
        ///   <see cref="CalradiaRemembers.Behaviors.NpcMemoryCampaignBehavior.OnSessionLaunched" />). Closes
        ///   any live <see cref="CommitmentQuest" /> whose commitment Id is no longer Active in
        ///   <see cref="CommitmentBehavior" /> (already resolved on an earlier session where the mirror
        ///   failed to close for some other reason, or the promisor vanished before it could).
        /// </summary>
        public static void ReconcileOnSessionLaunch()
        {
            if (Campaign.Current?.QuestManager == null) return;
            CommitmentBehavior behavior = CommitmentBehavior.Instance;
            if (behavior == null) return;

            var closed = 0;
            foreach (CommitmentQuest q in Campaign.Current.QuestManager.Quests
                                                   .OfType<CommitmentQuest>()
                                                   .ToList()) // snapshot, we may mutate during iteration
            {
                if (behavior.Active.Any(c => c.Id == q.CommitmentId)) continue;

                ModLog.Info($"[COMMITMENT-JOURNAL] closing stale mirror '{q.StringId}' at session launch, pledge no longer active.");
                q.CloseBreached("The pledge is no longer tracked.");
                closed++;
            }

            if (closed > 0)
                ModLog.Info($"[COMMITMENT-JOURNAL] reconciled {closed} stale mirror(s) at session launch.");
        }

        #region private

        private static CommitmentQuest FindMirror(string commitmentId)
        {
            if (string.IsNullOrEmpty(commitmentId) || Campaign.Current?.QuestManager == null) return null;

            return Campaign.Current.QuestManager.Quests
                           .OfType<CommitmentQuest>()
                           .FirstOrDefault(q => q.CommitmentId == commitmentId);
        }

        private static string ResolveName(string heroId)
        {
            if (string.IsNullOrEmpty(heroId)) return null;

            Hero hero = Hero.FindFirst(h => h.StringId == heroId);

            return hero?.Name?.ToString();
        }

        #endregion
    }
}
