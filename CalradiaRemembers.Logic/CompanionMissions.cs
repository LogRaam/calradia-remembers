// Code written by Gabriel Mailhot, 22/06/2026.
// Companion missions (ROADMAP "Also planned") — pure foundation, built test-first ahead of the engine
// glue. The player dispatches a companion on an errand that resolves off-screen and returns as a report;
// the OUTCOME is computed deterministically here (a skill roll), the NARRATION is the LLM's job. The host
// supplies the companion's governing-skill level and a fresh random roll, so this stays engine-agnostic
// and unit-tested; the courier/dispatch wiring and the LLM framing live in the mod.

namespace CalradiaRemembers.Logic
{
    /// <summary>The errand a companion can be sent on. Each maps to one governing skill and a risk level.</summary>
    public enum CompanionMissionType
    {
        /// <summary>"Bring me the latest news" — returns world-events the player would not otherwise pull.</summary>
        GatherNews,

        /// <summary>Scout a lord or fief — returns strength, whereabouts, a secret.</summary>
        Spy,

        /// <summary>Steal and bring back loot — gold or an item, at the risk of capture.</summary>
        Steal,

        /// <summary>Sell goods at a better price than the player would get.</summary>
        Barter,

        /// <summary>Carry a message to / probe the mood of a faction.</summary>
        Envoy
    }

    /// <summary>
    ///   The CR-local skill a mission is judged by, kept independent of the engine's SkillObject. The mod
    ///   maps these to the vanilla skills (Scouting / Roguery / Trade / Charm) when reading a hero.
    /// </summary>
    public enum MissionSkill
    {
        Scouting,
        Roguery,
        Trade,
        Charm
    }

    /// <summary>The KIND of thing an errand brings back — drives how the host gathers it (flavour/voice is per-type).</summary>
    public enum CompanionMissionPayload
    {
        /// <summary>World-events the destination town has heard (GatherNews).</summary>
        News,

        /// <summary>A read on a place — strength, holdings, whereabouts (Spy).</summary>
        Intel,

        /// <summary>Denars: stolen (Steal) or earned by trade (Barter).</summary>
        Gold,

        /// <summary>A read on a faction's disposition — wars, leaning (Envoy).</summary>
        Mood
    }

    /// <summary>How well a dispatched companion's errand turned out.</summary>
    public enum MissionResult
    {
        /// <summary>The errand failed; nothing useful comes back (and, if risky, see <see cref="CompanionMissionOutcome.CompanionCaptured"/>).</summary>
        Failure,

        /// <summary>A partial return — some value, less than hoped, or with a complication.</summary>
        Partial,

        /// <summary>The errand succeeded in full.</summary>
        Success
    }

    /// <summary>The resolved result of a dispatched mission. The mod turns this into loot/intel + a report.</summary>
    public sealed class CompanionMissionOutcome
    {
        public MissionResult Result { get; }

        /// <summary>
        ///   True only when a RISKY errand (spy/steal) failed badly enough that the companion was taken
        ///   prisoner — the loop-closer into the prisoner-rescue feature. Never set for safe errands.
        /// </summary>
        public bool CompanionCaptured { get; }

        public CompanionMissionOutcome(MissionResult result, bool companionCaptured)
        {
            Result = result;
            CompanionCaptured = companionCaptured;
        }
    }

    /// <summary>
    ///   Pure decision logic for companion missions. The governing skill and risk per errand are fixed
    ///   here; the success bands are tunable constants Gabriel can adjust. Deterministic: the host passes
    ///   the companion's skill level and a fresh roll, so the same inputs always yield the same outcome
    ///   and every band is unit-testable.
    ///   TODO(Gabriel): ratify the tuning (skill divisor, the three thresholds, the attempt floor) and
    ///   confirm the risk set — spy/steal are risky here, per the ROADMAP loop-closer to prisoner rescue.
    /// </summary>
    public static class CompanionMissions
    {
        // ── Tuning (flagged for ratification) ─────────────────────────────────
        // Bannerlord skills run roughly 0..330; dividing by 3 turns a skill into a ~0..110 contribution
        // that sits on the same scale as the 0..99 roll, so both matter without either dominating.
        private const int SkillDivisor      = 3;
        private const int SuccessThreshold  = 100; // score at/above which the errand fully succeeds
        private const int PartialThreshold  = 50;  // score at/above which it partly succeeds
        private const int CaptureThreshold  = 0;   // a risky errand scoring at/below this gets the agent caught
        private const int MinSkillToAttempt = 25;  // below this the companion is too green to send at all

        /// <summary>The vanilla skill this errand is judged by.</summary>
        public static MissionSkill GoverningSkill(CompanionMissionType type)
        {
            switch (type)
            {
                case CompanionMissionType.GatherNews: return MissionSkill.Scouting;
                case CompanionMissionType.Spy:        return MissionSkill.Roguery;
                case CompanionMissionType.Steal:      return MissionSkill.Roguery;
                case CompanionMissionType.Barter:     return MissionSkill.Trade;
                case CompanionMissionType.Envoy:      return MissionSkill.Charm;
                default:                              return MissionSkill.Scouting;
            }
        }

        /// <summary>True when a botched errand can get the companion captured (spy / steal).</summary>
        public static bool IsRisky(CompanionMissionType type)
            => type == CompanionMissionType.Spy || type == CompanionMissionType.Steal;

        /// <summary>What an errand brings back, so the host gathers the right payload (the voice stays per-type).</summary>
        public static CompanionMissionPayload PayloadOf(CompanionMissionType type)
        {
            switch (type)
            {
                case CompanionMissionType.Spy:    return CompanionMissionPayload.Intel;
                case CompanionMissionType.Steal:  return CompanionMissionPayload.Gold;
                case CompanionMissionType.Barter: return CompanionMissionPayload.Gold;
                case CompanionMissionType.Envoy:  return CompanionMissionPayload.Mood;
                default:                          return CompanionMissionPayload.News;
            }
        }

        /// <summary>
        ///   True when the companion is skilled enough in the governing skill to be worth sending at all.
        ///   Below the floor the errand is refused before any roll (the mod surfaces the refusal in dialog).
        /// </summary>
        public static bool CanAttempt(int governingSkillLevel)
            => governingSkillLevel >= MinSkillToAttempt;

        /// <summary>
        ///   Resolves an errand from the companion's governing-skill level, a fresh roll (0..99 from the
        ///   host's RNG), and the errand's difficulty (0..100). Inputs are clamped defensively. A risky
        ///   errand that scores at/below the capture threshold returns the companion captured.
        /// </summary>
        public static CompanionMissionOutcome Resolve(CompanionMissionType type, int governingSkillLevel, int roll, int difficulty)
        {
            int skill = governingSkillLevel < 0 ? 0 : governingSkillLevel;
            int r     = roll < 0 ? 0 : roll > 99 ? 99 : roll;
            int diff  = difficulty < 0 ? 0 : difficulty > 100 ? 100 : difficulty;

            int score = skill / SkillDivisor + r - diff;

            if (score >= SuccessThreshold) return new CompanionMissionOutcome(MissionResult.Success, false);
            if (score >= PartialThreshold) return new CompanionMissionOutcome(MissionResult.Partial, false);

            bool captured = IsRisky(type) && score <= CaptureThreshold;
            return new CompanionMissionOutcome(MissionResult.Failure, captured);
        }

        // ── Duration & report sizing (tunable — flagged for ratification) ─────
        private const int BaseDurationDays      = 2;  // a quick errand (news / barter)
        private const int DifficultyDurationStep = 25; // every 25 difficulty adds a day on the road
        private const int FullNewsItems         = 4;  // a clean news briefing
        private const int PartialNewsItems      = 2;  // a thinner one when the errand only half-worked
        private const int PartialLootPercent    = 40; // a botched-but-not-failed steal brings back a fraction

        /// <summary>
        ///   How many days a dispatched errand takes before the companion returns: a base by errand
        ///   (the farther/riskier ones run a day longer) plus a step per difficulty band. Difficulty is
        ///   clamped 0..100. TODO(Gabriel): tune by in-game feel.
        /// </summary>
        public static int DurationDays(CompanionMissionType type, int difficulty)
        {
            int diff = difficulty < 0 ? 0 : difficulty > 100 ? 100 : difficulty;
            int baseDays = (type == CompanionMissionType.Envoy
                            || type == CompanionMissionType.Spy
                            || type == CompanionMissionType.Steal)
                ? BaseDurationDays + 1
                : BaseDurationDays;
            return baseDays + diff / DifficultyDurationStep;
        }

        /// <summary>
        ///   How many news items a "bring me the news" report carries, given how the errand turned out and
        ///   how many are available: a full briefing on success, a thinner one on a partial, nothing on a
        ///   failure. Never more than are available.
        /// </summary>
        public static int NewsItemsForResult(MissionResult result, int available)
        {
            if (available <= 0) return 0;
            int cap;
            switch (result)
            {
                case MissionResult.Success: cap = FullNewsItems; break;
                case MissionResult.Partial: cap = PartialNewsItems; break;
                default:                    return 0;
            }
            return available < cap ? available : cap;
        }

        /// <summary>
        ///   The loot a STEAL errand brings back from a base haul, scaled by how it went: the full haul on
        ///   a clean success, a fraction on a partial, nothing on a failure. Never negative.
        /// </summary>
        public static int LootForResult(MissionResult result, int baseAmount)
        {
            if (baseAmount <= 0) return 0;
            switch (result)
            {
                case MissionResult.Success: return baseAmount;
                case MissionResult.Partial: return baseAmount * PartialLootPercent / 100;
                default:                    return 0;
            }
        }
    }
}
