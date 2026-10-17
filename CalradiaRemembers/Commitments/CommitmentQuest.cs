// Code written by Gabriel Mailhot, 03/07/2026.
// RP with teeth, increment 2, Phase 2: the vanilla quest-journal mirror of a TrackedCommitment.

#region

using System;
using System.Linq;
using CalradiaRemembers.Logic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

#endregion

namespace CalradiaRemembers.Commitments
{
    /// <summary>
    ///   A thin <see cref="QuestBase" /> wrapper around a <see cref="TrackedCommitment" /> (a lord's
    ///   pledge TO THE PLAYER) that makes it visible in Bannerlord's native quest journal, modeled
    ///   directly on <see cref="CalradiaRemembers.Missions.CompanionErrandQuest" />. Read-only: no
    ///   dialogs, no gold reward, it exists purely so "who owes me what" is legible without the console.
    ///   The journal entry is a MIRROR of the data owned by <see cref="CommitmentBehavior" />'s
    ///   <c>Active</c> list; it is never authoritative. All lifecycle decisions happen there;
    ///   <see cref="CommitmentJournalBridge" /> is the only place that opens or closes this mirror.
    ///   Anti-stuck guarantee: <see cref="CommitmentJournalBridge.ReconcileOnSessionLaunch" /> force-closes
    ///   any mirror whose commitment is no longer Active, on every session launch.
    ///   Saveable fields: only <see cref="_promisorId" /> and <see cref="_commitmentId" />. Everything
    ///   else (title, log lines) is pushed in by the bridge from the live <see cref="TrackedCommitment" />.
    ///   The commitment's own <see cref="TrackedCommitment.Id" /> (already a stable GUID minted at
    ///   <see cref="CommitmentBehavior.Create" />) is reused verbatim as the mirror's cross-reference key
    ///   rather than composing a new one — it survives even if the promisor Hero later becomes unavailable.
    /// </summary>
    public sealed class CommitmentQuest : QuestBase
    {
        // Saveable field IDs (do not reuse).
        [SaveableField(1)]
        private readonly string _promisorId;

        [SaveableField(2)]
        private readonly string _commitmentId;

        /// <summary>Creates a new mirror for a just-issued <paramref name="commitment" />.</summary>
        public CommitmentQuest(TrackedCommitment commitment, Hero promisor)
            : base(
                BuildQuestId(promisor, commitment.Id),
                promisor,
                // Stored directly as the engine due time: never times out. CommitmentBehavior owns
                // the real deadline (TrackedCommitment.DeadlineDay); this reflects it read-only.
                CampaignTime.Never,
                0) // no gold reward: fulfillment/breach consequences are relation + world-event, not gold
        {
            _promisorId = promisor.StringId;
            _commitmentId = commitment.Id;

            // R7-light: MoveAgainst keeps the ORIGINAL two-name opening line exactly as shipped; the three
            // swear_oath kinds route through CommitmentBehavior's own kind-aware builder instead, so the
            // player's vanilla quest log reads the actual oath sworn, never "moved against" for all four kinds.
            string openedLine = commitment.Kind == CommitmentKind.MoveAgainst
                ? CommitmentJournalText.BuildOpenedLine(promisor.Name?.ToString(), ResolveRivalName(commitment))
                : CommitmentJournalText.BuildOpenedLine(commitment.Kind, promisor.Name?.ToString(),
                    CommitmentBehavior.ResolveTargetDisplayName(commitment), commitment.PromisedAmount);
            AddLog(new TextObject(Mapping.ShownText.Of(openedLine)));
            SetDialogs();
            StartQuest();
        }

        // We have no engine countdown to show: the real timing lives in CommitmentBehavior.
        public override bool IsRemainingTimeHidden => true;

        /// <summary>
        ///   LOAD-BEARING: without a special type, QuestManager.OnGameLoaded cancels every non-finalized quest
        ///   that has no live Issue attached, so the mirror died (with a cancel banner) at every reload.
        ///   See <see cref="Quests.CalradiaRemembersQuest.SpecialQuestType" /> for the full engine analysis.
        /// </summary>
        public override string SpecialQuestType => "CalradiaRemembers";

        public override TextObject Title => new(CommitmentJournalText.BuildTitle(ResolvePromisorName()));

        /// <summary>The pledge's stable Id, used by <see cref="CommitmentJournalBridge" /> to find this mirror.</summary>
        internal string CommitmentId => _commitmentId;

        /// <summary>The promisor's StringId, exposed so a diagnostic can cross-check the mirror.</summary>
        internal string PromisorId => _promisorId;

        /// <summary>Closes the mirror: the deadline lapsed with nothing done, an oath broken.</summary>
        public void CloseBreached(string reason)
        {
            try { CompleteQuestWithFail(new TextObject(reason)); }
            catch (Exception ex) { ModLog.Error($"[COMMITMENT-JOURNAL] CloseBreached failed: {ex.Message}"); }
        }

        /// <summary>Closes the mirror: the pledge was honored before the deadline.</summary>
        public void CloseFulfilled()
        {
            try { CompleteQuestWithSuccess(); }
            catch (Exception ex) { ModLog.Error($"[COMMITMENT-JOURNAL] CloseFulfilled failed: {ex.Message}"); }
        }

        /// <summary>
        ///   Closes the mirror neutrally, neither honored nor broken (v1.34.1 hotfix): a keep-peace oath whose
        ///   peace was broken by the OTHER house (the promisor was invaded). Cancel, never fail, so the player
        ///   is not shown a broken-faith banner for a war their sworn lord did not start.
        /// </summary>
        public void CloseNeutral(string reason)
        {
            try { CompleteQuestWithCancel(new TextObject(reason)); }
            catch (Exception ex) { ModLog.Error($"[COMMITMENT-JOURNAL] CloseNeutral failed: {ex.Message}"); }
        }

        /// <summary>Pushes a new status line to the journal log; called just before either closing method.</summary>
        public void UpdateStatus(string logLine)
        {
            if (!string.IsNullOrWhiteSpace(logLine)) AddLog(new TextObject(Mapping.ShownText.Of(logLine)));
        }

        internal static string BuildQuestId(Hero promisor, string commitmentId)
            => $"cr_pledge_{promisor.StringId}_{commitmentId}";

        protected override void InitializeQuestOnGameLoad()
        {
            // Reconnect happens by commitment Id lookup (CommitmentJournalBridge), not here. If the
            // pledge is gone, the reconciler closes this mirror at the next session launch.
            SetDialogs();
        }

        // ── QuestBase contract ────────────────────────────────────────────────

        protected override void SetDialogs()
        {
            // No conversation flows: this is a read-only mirror, not an interactive quest.
        }

        #region private

        private static string ResolveRivalName(TrackedCommitment commitment)
        {
            Hero rival = Hero.FindFirst(h => h.StringId == commitment.TargetId);

            return rival?.Name?.ToString();
        }

        private string ResolvePromisorName()
        {
            TrackedCommitment live = CommitmentBehavior.Instance?.Active.FirstOrDefault(c => c.Id == _commitmentId);
            if (live == null) return QuestGiver?.Name?.ToString() ?? "A lord";

            Hero promisor = Hero.FindFirst(h => h.StringId == live.PromisorId);

            return promisor?.Name?.ToString() ?? QuestGiver?.Name?.ToString() ?? "A lord";
        }

        #endregion
    }
}
