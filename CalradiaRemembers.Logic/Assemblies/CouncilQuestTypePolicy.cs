// Code written by Gabriel Mailhot, 26/08/2026.
// ASSEMBLY pillar, Council module: a council "quest" resolution now names WHICH verifiable deed it pledges
// (quest_type, see NpcMemoryService.Core.Models.ResolutionProposal.QuestType), because grounding every pledge
// as a bandit-clear made one bandit fight near the target complete the WHOLE table's tasks at once (player
// report 2026-08-25: eight siege-council pledges all "Done" from clearing one bandit camp). This policy is the
// ONE place that decides which deed types a council quest may ground as; the deed-verification half needs no
// change at all (QuestDeedTracker already matches each type to its own real game event, so differently-typed
// pledges complete independently by construction).

#region

using System.Collections.Generic;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Parsing;

#endregion

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>Maps a council "quest" resolution's quest_type token to the deed type the lift grounds; an unsupported named deed fails.</summary>
    public static class CouncilQuestTypePolicy
    {
        /// <summary>
        ///   The deed types a council task pledge may ground as, in the order the prompt vocabulary lists them.
        ///   Every one grounds on the settlement the pledge already names (target_settlement): siege and
        ///   raid_village bind it directly, bandit_clear/bandit_hideout hunt around it, and scout_army with no
        ///   named hero or faction falls back to the giver's first enemy kingdom at binding time (QuestFactory's
        ///   own ScoutArmy fallback). Hero-bound deeds (capture, execute, rescue, deliver) are deliberately NOT
        ///   here: the [RESOLUTION] block teaches no target_hero field, so they could never ground.
        /// </summary>
        public static IReadOnlyList<string> TeachableTokens { get; } = new[] {
            "siege",
            "scout_army",
            "raid_village",
            "bandit_hideout",
            "bandit_clear"
        };

        private static readonly HashSet<QuestType> Supported = new() {
            QuestType.Siege,
            QuestType.ScoutArmy,
            QuestType.RaidVillage,
            QuestType.BanditHideout,
            QuestType.BanditClear
        };

        /// <summary>
        ///   The deed type to ground, or false when the token NAMES a deed the council cannot ground. jaserms1
        ///   (Nexus, 16/10/2026): a pledge to "patrol the villages" grounded as a bandit-clear task; a named but
        ///   unsupported deed now fails the pledge honestly instead of becoming a different one. A null/blank token
        ///   (the model named none, or an entry recorded before quest_type existed) still grounds as the legacy
        ///   bandit-clear.
        /// </summary>
        public static bool TryResolve(string token, out QuestType type)
        {
            type = QuestType.BanditClear;
            if (string.IsNullOrWhiteSpace(token)) return true;

            QuestType? parsed = SectionResponseParser.ParseQuestType(token);
            if (!parsed.HasValue || !Supported.Contains(parsed.Value)) return false;

            type = parsed.Value;

            return true;
        }
    }
}
