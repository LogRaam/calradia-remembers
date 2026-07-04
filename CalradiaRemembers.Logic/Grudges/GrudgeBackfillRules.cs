// Code written by Gabriel Mailhot, 03/07/2026.
// Grudges pillar, pure foundation: the ENGINE-FREE mapping from the REAL persisted markers a save-migration
// inventory confirmed (jealousy NotableEvents, terminal InformalQuest status, the divorce set-aside
// Betrayal event, the bastard-claim WorldEvent) to GrudgeMigration.MigrationCandidate. Conservative by
// construction: every method here recognizes ONLY the exact marker documented below and returns null for
// anything else — including a half-match — rather than guess (the ROADMAP's hard save-migration rule).
// One primary marker per mechanic: a failed/abandoned quest is read from the STRUCTURED InformalQuest
// status only, never ALSO from the mirrored Conflict-text NotableEvent QuestService writes for the same
// fact, so a single quest failure never yields two grudges.

#region

using System;
using System.Collections.Generic;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Grudges
{
    /// <summary>A persisted <c>NotableEvent</c> on some NPC's profile, as read by the engine-side scan.</summary>
    public sealed class ProfileEventFacts
    {
        /// <summary>Hero StringId of the profile this event sits on.</summary>
        public string HolderId { get; set; }

        /// <summary>The event's <c>NotableEventType</c> ordinal (ordinals are frozen — see the SDK enum).</summary>
        public int EventTypeOrdinal { get; set; }

        /// <summary>The event's own summary text.</summary>
        public string Summary { get; set; }

        /// <summary>In-game day the event was recorded.</summary>
        public int GameDay { get; set; }
    }

    /// <summary>A terminated <c>InformalQuest</c> (Expired/Abandoned), as read by the engine-side scan.</summary>
    public sealed class QuestOutcomeFacts
    {
        /// <summary>Hero StringId of the quest giver (the profile the quest lives on).</summary>
        public string GiverId { get; set; }

        /// <summary>The quest's terminal <c>QuestStatus</c> name ("Expired" or "Abandoned" recognized; anything else is skipped).</summary>
        public string StatusName { get; set; }

        /// <summary>Game day the quest was issued — the fallback anchor day when no deadline was set.</summary>
        public int IssuedOnDay { get; set; }

        /// <summary>Game day the quest's deadline fell on, when one was set.</summary>
        public int? DeadlineDay { get; set; }

        /// <summary>The giver's own description of the task, reused to build the grudge's cause text.</summary>
        public string Description { get; set; }
    }

    /// <summary>A persisted <c>WorldEvent</c>, as read by the engine-side scan.</summary>
    public sealed class WorldEventFacts
    {
        /// <summary>Hero StringIds the event involves (actor [0] is the claimant for a bastard-claim event).</summary>
        public List<string> ActorIds { get; set; }

        /// <summary>In-game day the event occurred.</summary>
        public int Day { get; set; }

        /// <summary>The event's own <c>WorldEventType</c> name.</summary>
        public string TypeName { get; set; }

        /// <summary>True when the player is one of the actors.</summary>
        public bool InvolvesPlayer { get; set; }

        /// <summary>The event's own summary text.</summary>
        public string Summary { get; set; }
    }

    /// <summary>
    ///   Maps the real Tier-1 markers already sitting in existing saves to <see cref="MigrationCandidate" />,
    ///   so <see cref="GrudgeMigration" /> never has to know about NotableEvent/InformalQuest/WorldEvent
    ///   shapes. Scheme-warned and scheme-abetted are deliberately ABSENT here: their only trace in a save
    ///   is a relation delta or a 42-day-expiring rumour, neither of which is recoverable as an unambiguous
    ///   marker, so they become live-planted-only from this build forward (graceful degradation, not a bug).
    /// </summary>
    public static class GrudgeBackfillRules
    {
        // Suggested backfill magnitudes. GrudgePolicy.EffectiveMagnitude decays these from the ORIGINAL
        // EventDay on every read, so a migrated grudge arrives already softened by however long ago the
        // triggering event happened — intentional: an old wound should not read as fresh as one formed today.
        public const int JealousyMagnitude = 40;
        public const int FailedQuestMagnitude = 30;
        public const int DivorceMagnitude = 60;
        public const int BastardClaimMagnitude = 50;

        /// <summary>The truncation length for a cause carried onto a synthesized grudge (chronicler prose, not a data dump).</summary>
        public const int MaxCauseLength = 160;

        // NotableEventType ordinals are frozen (see the SDK enum); Jealousy = 11 is EXCLUSIVE to the
        // jealousy system, so the ordinal alone is an unambiguous marker — no text sniffing needed.
        private const int JealousyEventOrdinal = (int) NotableEventType.Jealousy;

        // Betrayal = 7 is shared by several mechanics, so the divorce set-aside marker additionally
        // requires BOTH exact substrings DivorceProcessBehavior.ApplySuccessConsequences writes on the
        // set-aside spouse's profile: "Was set aside by {npc.Name}, the marriage dissolved at the player's
        // urging."
        private const int BetrayalEventOrdinal = (int) NotableEventType.Betrayal;
        private const string DivorceSetAsidePrefix = "Was set aside by ";
        private const string DivorceSetAsideSuffix = "the marriage dissolved at the player's urging.";

        private const string ExpiredStatus = "Expired";
        private const string AbandonedStatus = "Abandoned";

        // BastardBehavior.RecordClaim's exact summary shape: "{bastard.Name}, natural-born child of
        // {parent}, has come of age and presses a claim against their house."
        private const string BastardClaimTypeName = "Scandal";
        private const string BastardClaimSubstring = "presses a claim against their house";

        /// <summary>
        ///   Rule 1 (Jealousy, ordinal 11) and rule 3 (the divorce set-aside Betrayal marker): both read a
        ///   persisted <c>NotableEvent</c>. Returns null when the event matches neither exact marker.
        ///   <paramref name="playerId" /> becomes the candidate's target — both grudges are against the
        ///   player, per the ROADMAP's framing (the staked party grudges the player's own act; the set-aside
        ///   spouse's grudge is signed by their prior regard for the player, "at the player's urging").
        ///   <paramref name="regardForTarget" />, <paramref name="traits" /> and
        ///   <paramref name="possessiveAmplifier" /> are the LIVE facts the engine-side caller already
        ///   resolved for this holder at migration time.
        /// </summary>
        public static MigrationCandidate FromProfileEvent(
            ProfileEventFacts facts, string playerId, int regardForTarget, TraitFacts traits, bool possessiveAmplifier)
        {
            if (facts == null || string.IsNullOrEmpty(facts.HolderId)) return null;

            if (facts.EventTypeOrdinal == JealousyEventOrdinal)
                return new MigrationCandidate {
                    HolderId = facts.HolderId,
                    TargetId = playerId,
                    Marker = "jealousy",
                    EventDay = facts.GameDay,
                    Summary = Truncate(facts.Summary),
                    RegardForTarget = regardForTarget,
                    Traits = traits,
                    PossessiveAmplifier = possessiveAmplifier,
                    Magnitude = JealousyMagnitude
                };

            if (facts.EventTypeOrdinal == BetrayalEventOrdinal && IsDivorceSetAsideMarker(facts.Summary))
                return new MigrationCandidate {
                    HolderId = facts.HolderId,
                    TargetId = playerId,
                    Marker = "divorce",
                    EventDay = facts.GameDay,
                    Summary = Truncate(facts.Summary),
                    RegardForTarget = regardForTarget,
                    Traits = traits,
                    Magnitude = DivorceMagnitude
                };

            return null;
        }

        /// <summary>
        ///   Rule 2 (FailedQuest): a terminal <c>InformalQuest</c> whose status is "Expired" or "Abandoned".
        ///   This is the ONLY marker converted for a broken word — the mirrored Conflict-text NotableEvent
        ///   QuestService also writes for the very same fact ("let the deadline lapse on the task to…" /
        ///   "gave up the task to…") is deliberately NOT also scanned, so one quest failure never yields two
        ///   grudges. Day anchors on the deadline when one was set, else the day the quest was issued.
        /// </summary>
        public static MigrationCandidate FromQuestOutcome(
            QuestOutcomeFacts facts, string playerId, int regardForTarget, TraitFacts traits)
        {
            if (facts == null || string.IsNullOrEmpty(facts.GiverId)) return null;

            bool expired = string.Equals(facts.StatusName, ExpiredStatus, StringComparison.OrdinalIgnoreCase);
            bool abandoned = string.Equals(facts.StatusName, AbandonedStatus, StringComparison.OrdinalIgnoreCase);
            if (!expired && !abandoned) return null;

            int day = facts.DeadlineDay ?? facts.IssuedOnDay;

            return new MigrationCandidate {
                HolderId = facts.GiverId,
                TargetId = playerId,
                Marker = "failed_quest",
                EventDay = day,
                Summary = Truncate(BuildQuestCause(facts, expired)),
                RegardForTarget = regardForTarget,
                Traits = traits,
                Magnitude = FailedQuestMagnitude
            };
        }

        /// <summary>
        ///   Rule 4 (BastardClaim): a Scandal <c>WorldEvent</c> involving the player whose summary contains
        ///   the exact claim-pressed substring <c>BastardBehavior.RecordClaim</c> writes. Holder = the
        ///   claimant (<see cref="WorldEventFacts.ActorIds" />[0], the bastard who came of age), target =
        ///   the player (whose house the claim is pressed against).
        /// </summary>
        public static MigrationCandidate FromWorldEvent(
            WorldEventFacts facts, string playerId, int regardForTarget, TraitFacts traits)
        {
            if (facts == null || !facts.InvolvesPlayer) return null;
            if (!string.Equals(facts.TypeName, BastardClaimTypeName, StringComparison.OrdinalIgnoreCase)) return null;
            if (facts.ActorIds == null || facts.ActorIds.Count == 0) return null;
            if (facts.Summary == null || facts.Summary.IndexOf(BastardClaimSubstring, StringComparison.OrdinalIgnoreCase) < 0) return null;

            string claimantId = facts.ActorIds[0];
            if (string.IsNullOrEmpty(claimantId)) return null;

            return new MigrationCandidate {
                HolderId = claimantId,
                TargetId = playerId,
                Marker = "bastard_claim",
                EventDay = facts.Day,
                Summary = Truncate(facts.Summary),
                RegardForTarget = regardForTarget,
                Traits = traits,
                Magnitude = BastardClaimMagnitude
            };
        }

        #region private

        private static bool IsDivorceSetAsideMarker(string summary)
            => !string.IsNullOrEmpty(summary)
               && summary.IndexOf(DivorceSetAsidePrefix, StringComparison.OrdinalIgnoreCase) >= 0
               && summary.IndexOf(DivorceSetAsideSuffix, StringComparison.OrdinalIgnoreCase) >= 0;

        private static string BuildQuestCause(QuestOutcomeFacts facts, bool expired)
        {
            string task = string.IsNullOrWhiteSpace(facts.Description)
                ? "a task asked of the player"
                : facts.Description.TrimEnd('.');

            return expired
                ? $"The player let the deadline lapse on {task}."
                : $"The player gave up on {task}, a broken word however honest.";
        }

        private static string Truncate(string summary)
        {
            if (string.IsNullOrWhiteSpace(summary)) return null;
            string trimmed = summary.Trim();

            return trimmed.Length <= MaxCauseLength
                ? trimmed
                : trimmed.Substring(0, MaxCauseLength).TrimEnd() + "...";
        }

        #endregion
    }
}
