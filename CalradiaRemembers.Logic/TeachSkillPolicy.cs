// Code written by Gabriel Mailhot, 20/08/2026.
// The roadmap wish this closes: NPCs already OFFER to instruct the player (swordsmanship, riding, court
// politics), organically, in prose, but the offer never became real, the empty-promise class the mod closes
// everywhere else (join_party, marry, recruit_prisoner...). teach_skill is the executor; this is its law. A
// lesson must be BOUNDED (a nudge, never a level-up, mirroring RelationGate's "trust gained slowly" pillar),
// RE-VALIDATED against the teacher's own live skill (a novice cannot teach mastery they do not have), and
// NON-FARMABLE (a per-NPC cooldown, the same shape as RelationGate.CooldownHours/NpcProfile.LastRelationGainHour,
// so one companion cannot be re-asked for a fresh lesson every turn). Pure: ints/doubles only, no engine or SDK
// types, so the bridge (the law) stamps NpcProfile.LastTaughtHour itself after reading this policy's verdict.

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Single source of truth for whether an NPC may teach a skill at all, and how much real XP a lesson
    ///   actually grants once it does. Two pillars:
    ///
    ///   - A teacher must be genuinely COMPETENT: only a skill value at or above
    ///     <see cref="TeacherSkillFloor"/> is worth learning from (<see cref="CanTeach"/>).
    ///   - The XP granted is CLAMPED to a modest range whose ceiling scales with the teacher's own mastery (a
    ///     master grants more than a bare-floor novice), bounded overall by <see cref="MaxCeiling"/> so a
    ///     single lesson is always a nudge, never a level-up, and gated by <see cref="CooldownHours"/> so the
    ///     same NPC cannot be re-asked for a fresh lesson every turn.
    /// </summary>
    public static class TeachSkillPolicy
    {
        /// <summary>
        ///   A teacher's own skill value must reach this floor before they have anything worth teaching.
        ///   100 sits well above vanilla's early tiers (a companion picked up at 20-40 in their focus skill
        ///   is not yet a teacher), but short of the 300 cap, so a genuinely accomplished (not necessarily
        ///   maxed) companion or lord can already give real lessons.
        /// </summary>
        public const int TeacherSkillFloor = 100;

        /// <summary>
        ///   Minimum in-game hours between two lessons from the SAME NPC. Mirrors
        ///   <see cref="RelationGate.IntimacyCooldownHours"/>'s shape (a once-a-day-ish significant beat) rather
        ///   than the shorter routine <see cref="RelationGate.CooldownHours"/>: a lesson grants real, persisted
        ///   skill XP (unlike a routine +1 regard tick), so it earns the stricter window.
        /// </summary>
        public const double CooldownHours = 24.0;

        /// <summary>The smallest ceiling a lesson can carry, for a teacher barely past <see cref="TeacherSkillFloor"/>.</summary>
        private const int MinCeiling = 10;

        /// <summary>
        ///   The largest ceiling any single lesson can ever carry, however masterful the teacher: a lesson is
        ///   always a nudge, never a level-up. Vanilla's own skill-XP thresholds climb steeply with level, so
        ///   even this ceiling barely dents a high skill; it exists to bound the LOW end too, where XP-per-level
        ///   is cheap and an unclamped grant could feel like a shortcut.
        /// </summary>
        private const int MaxCeiling = 60;

        /// <summary>Vanilla's own skill cap, the top of the teacher-mastery scale <see cref="MinCeiling"/>-to-<see cref="MaxCeiling"/> is stretched across.</summary>
        private const int MaxSkillLevel = 300;

        /// <summary>True when a teacher's own skill value is high enough to genuinely teach it.</summary>
        public static bool CanTeach(int teacherSkillLevel) => teacherSkillLevel >= TeacherSkillFloor;

        /// <summary>
        ///   True when this teacher can teach THIS player: competent in the skill at all, and better at it than
        ///   the player already is. A tie is not enough, since two equals have nothing to show each other.
        ///   <para>
        ///     Found while answering a player's question (Zinced121212, Nexus, 19/09/2026), about whether
        ///     characters are aware of skills at all: the lesson never looked at the player's own level. A player
        ///     at 300 Medicine could be "taught" Medicine by a companion at 100, and paid in real experience for
        ///     it. The competence floor was right and was the only thing asked; nobody asked whether the student
        ///     already knew more than the master.
        ///   </para>
        ///   Kept apart from <see cref="GrantedXp" /> on purpose: that one answers "how much, from a teacher
        ///   entitled to teach", and adding a third integer to a signature that already runs two in a row would
        ///   make it one silent argument swap away from inverting who is teaching whom.
        /// </summary>
        public static bool CanTeachPlayer(int teacherSkillLevel, int playerSkillLevel)
            => CanTeach(teacherSkillLevel) && teacherSkillLevel > playerSkillLevel;

        /// <summary>
        ///   Resolves the real XP a lesson grants: 0 when the teacher is not competent
        ///   (<see cref="CanTeach"/> false), 0 when the last lesson from this NPC landed less than
        ///   <see cref="CooldownHours"/> ago, 0 for a non-positive request, and otherwise the LLM's requested
        ///   amount clamped to a ceiling that scales linearly with the teacher's own mastery between
        ///   <see cref="MinCeiling"/> (at the floor) and <see cref="MaxCeiling"/> (at the skill cap).
        /// </summary>
        /// <param name="teacherSkillLevel">The teaching NPC's own live value in the taught skill (0-300).</param>
        /// <param name="requestedAmount">The XP the LLM proposed; advice only, never trusted past the ceiling.</param>
        /// <param name="lastTaughtHour">Campaign hour this NPC last taught the player, or null if never.</param>
        /// <param name="nowHour"><c>CampaignTime.Now.ToHours</c> at this moment.</param>
        public static int GrantedXp(int teacherSkillLevel, int requestedAmount, double? lastTaughtHour, double nowHour)
        {
            if (!CanTeach(teacherSkillLevel)) return 0;
            if (requestedAmount <= 0) return 0;
            if (lastTaughtHour.HasValue && nowHour - lastTaughtHour.Value < CooldownHours) return 0;

            int ceiling = CeilingFor(teacherSkillLevel);

            return requestedAmount < ceiling ? requestedAmount : ceiling;
        }

        #region private

        private static int CeilingFor(int teacherSkillLevel)
        {
            int level = teacherSkillLevel > MaxSkillLevel ? MaxSkillLevel : teacherSkillLevel;
            double progress = (level - TeacherSkillFloor) / (double) (MaxSkillLevel - TeacherSkillFloor);
            if (progress < 0.0) progress = 0.0;
            if (progress > 1.0) progress = 1.0;

            return MinCeiling + (int) (progress * (MaxCeiling - MinCeiling));
        }

        #endregion
    }
}
