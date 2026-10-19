// Code written by Gabriel Mailhot, 03/07/2026.
// RP with teeth, increment 2, Phase 2: bridges TrackedCommitment (the persisted record) and
// CommitmentQuest (the vanilla quest-journal mirror).

#region

using System;
using System.Linq;
using CalradiaRemembers.Foundation;
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

        /// <summary>
        ///   Closes the mirror neutrally (v1.34.1 hotfix): a keep-peace oath whose peace was broken by the
        ///   OTHER house, so the promisor is the victim, not an oathbreaker. Cancels the journal entry with a
        ///   neutral banner, never the fail banner <see cref="CloseBreached" /> raises.
        /// </summary>
        public static void CloseNeutral(TrackedCommitment commitment, string reason)
        {
            if (commitment == null) return;

            try
            {
                CommitmentQuest mirror = FindMirror(commitment.Id);
                if (mirror == null) return;

                mirror.UpdateStatus(reason);
                mirror.CloseNeutral(reason);
            }
            catch (Exception ex)
            {
                ModLog.Error($"[COMMITMENT-JOURNAL] CloseNeutral failed for {commitment.Id}: {ex.GetType().Name}: {ex.Message}");
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
        ///   2026-08-22: an NPC's vow is no longer a player quest (no journal entry, no deadline, no
        ///   relation effect). Runs at every session launch (wired by
        ///   <see cref="CalradiaRemembers.Behaviors.NpcMemoryCampaignBehavior.OnSessionLaunched" />) and
        ///   closes EVERY <see cref="CommitmentQuest" /> mirror carried over from an older save
        ///   NEUTRALLY: a cancel banner, never the fail banner the old breach path raised, so a legacy
        ///   save never greets the player with "quest failed" for a vow that was never theirs. Then it
        ///   hands off to <see cref="CommitmentBehavior.RetireAllSilently" /> to drain the tracked list
        ///   into plain memories. A fresh campaign has no mirrors and nothing to drain.
        /// </summary>
        public static void ReconcileOnSessionLaunch()
        {
            if (Campaign.Current?.QuestManager != null)
            {
                var closed = 0;
                foreach (CommitmentQuest q in Campaign.Current.QuestManager.Quests
                                                       .OfType<CommitmentQuest>()
                                                       .ToList()) // snapshot, we mutate during iteration
                {
                    q.CloseNeutral("This vow now lives in memory, not the quest log.");
                    closed++;
                }

                if (closed > 0)
                    ModLog.Info($"[COMMITMENT-JOURNAL] closed {closed} legacy pledge mirror(s) neutrally at session launch (vows are memories now).");
            }

            CommitmentBehavior.Instance?.RetireAllSilently();
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
