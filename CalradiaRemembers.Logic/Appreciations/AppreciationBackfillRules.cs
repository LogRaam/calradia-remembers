// Code written by Gabriel Mailhot, 04/07/2026.
// Appreciations pillar, pure foundation: the ENGINE-FREE mapping from persisted markers to
// AppreciationMigration.MigrationCandidate, mirroring GrudgeBackfillRules' shape. UNLIKE GrudgeBackfillRules
// (whose Tier-1 markers were confirmed by a real 2026-07-03 save inventory), Appreciations is a BRAND NEW
// pillar with no prior ad hoc system to consolidate — so almost nothing here has an existing, unambiguous
// marker in a save today. Only two rules are wired to something that genuinely exists:
//   - Rule 1 (KindnessInScene): NotableEventType.Intimacy, a real ordinal recorded for a consensual
//     intimate moment. PLACEHOLDER-GRADE: Intimacy is not EXCLUSIVE to "a kindness" the way Jealousy is
//     exclusive to grudges (it also feeds pregnancy/jealousy consequences downstream) — treating every
//     Intimacy event as a felt kindness is a reasonable but UNCONFIRMED heuristic, flagged for Gabriel's
//     review before this ships live.
//   - Rule 2 (QuestDoneForThem): a terminal InformalQuest.Status == Completed, a real, structured,
//     unambiguous marker (mirrors GrudgeBackfillRules' use of Expired/Abandoned for FailedQuest).
// Every other source (GiftReceived, GoldReceived, RansomedOrFreed, TroopsOrInfluenceSpentForThem,
// DivorceGratitude) has NO recoverable marker in an existing save — no dedicated NotableEvent is written
// for a gift/gold hand-off or a ransom today, and DivorceConsequencePolicy's Gratitude/Relief verdicts are
// explicitly transient (never persisted; see its own header comment). They are deliberately ABSENT from
// this file and become live-plant-only from this build forward, exactly mirroring how GrudgeBackfillRules
// omits SchemeWarned/SchemeAbetted (graceful degradation, not a bug).

#region

using System;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Appreciations
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

    /// <summary>A terminated <c>InformalQuest</c> (Completed), as read by the engine-side scan.</summary>
    public sealed class QuestOutcomeFacts
    {
        /// <summary>Hero StringId of the quest giver (the profile the quest lives on, and the one who would feel the appreciation).</summary>
        public string GiverId { get; set; }

        /// <summary>The quest's terminal <c>QuestStatus</c> name ("Completed" recognized; anything else is skipped).</summary>
        public string StatusName { get; set; }

        /// <summary>Game day the quest was issued — the fallback anchor day when no completion day is known.</summary>
        public int IssuedOnDay { get; set; }

        /// <summary>Game day the quest's deed was verified/satisfied, when known.</summary>
        public int? SatisfiedOnDay { get; set; }

        /// <summary>The giver's own description of the task, reused to build the appreciation's cause text.</summary>
        public string Description { get; set; }
    }

    /// <summary>
    ///   Maps the two Appreciations markers that already exist in a save to
    ///   <see cref="MigrationCandidate" />, so <see cref="AppreciationMigration" /> never has to know about
    ///   NotableEvent/InformalQuest shapes.
    /// </summary>
    public static class AppreciationBackfillRules
    {
        // Suggested backfill magnitudes. AppreciationPolicy.EffectiveMagnitude decays these from the
        // ORIGINAL EventDay on every read, so a migrated appreciation arrives already softened by however
        // long ago the triggering event happened.
        public const int KindnessInSceneMagnitude = 30;
        public const int QuestDoneForThemMagnitude = 25;

        /// <summary>The truncation length for a cause carried onto a synthesized appreciation (chronicler prose, not a data dump).</summary>
        public const int MaxCauseLength = 160;

        // NotableEventType.Intimacy — see this file's header for why this is a PLACEHOLDER-GRADE marker,
        // not a confirmed-exclusive one like GrudgeBackfillRules' use of Jealousy.
        private const int IntimacyEventOrdinal = (int) NotableEventType.Intimacy;

        private const string CompletedStatus = "Completed";

        /// <summary>
        ///   Rule 1 (KindnessInScene): a persisted Intimacy <c>NotableEvent</c>. Returns null for any other
        ///   ordinal. <paramref name="playerId" /> becomes the candidate's target, per the ROADMAP's
        ///   framing (the holder feels the appreciation toward the player).
        /// </summary>
        public static MigrationCandidate FromProfileEvent(ProfileEventFacts facts, string playerId, TraitFacts traits)
        {
            if (facts == null || string.IsNullOrEmpty(facts.HolderId)) return null;
            if (facts.EventTypeOrdinal != IntimacyEventOrdinal) return null;

            return new MigrationCandidate {
                HolderId = facts.HolderId,
                TargetId = playerId,
                Marker = "kindness_in_scene",
                EventDay = facts.GameDay,
                Summary = Truncate(facts.Summary),
                Traits = traits,
                Magnitude = KindnessInSceneMagnitude
            };
        }

        /// <summary>
        ///   Rule 2 (QuestDoneForThem): a terminal <c>InformalQuest</c> whose status is "Completed" — the
        ///   ONLY marker converted, mirroring how GrudgeBackfillRules reads FailedQuest from Expired/Abandoned
        ///   alone. Day anchors on when the deed was verified/satisfied, else the day the quest was issued.
        /// </summary>
        public static MigrationCandidate FromQuestOutcome(QuestOutcomeFacts facts, string playerId, TraitFacts traits)
        {
            if (facts == null || string.IsNullOrEmpty(facts.GiverId)) return null;
            if (!string.Equals(facts.StatusName, CompletedStatus, StringComparison.OrdinalIgnoreCase)) return null;

            int day = facts.SatisfiedOnDay ?? facts.IssuedOnDay;

            return new MigrationCandidate {
                HolderId = facts.GiverId,
                TargetId = playerId,
                Marker = "quest_done_for_them",
                EventDay = day,
                Summary = Truncate(BuildQuestCause(facts)),
                Traits = traits,
                Magnitude = QuestDoneForThemMagnitude
            };
        }

        #region private

        private static string BuildQuestCause(QuestOutcomeFacts facts)
        {
            string task = string.IsNullOrWhiteSpace(facts.Description)
                ? "a task asked of the player"
                : facts.Description.TrimEnd('.');

            return $"The player saw through {task}, as promised.";
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
