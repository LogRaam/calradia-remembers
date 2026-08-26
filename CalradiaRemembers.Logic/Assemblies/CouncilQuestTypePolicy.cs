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
    /// <summary>Maps a council "quest" resolution's quest_type token to the deed type the lift grounds, with the legacy bandit-clear fallback.</summary>
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
        ///   The deed type to ground. A null/blank token (the model named none, or a ledger entry recorded
        ///   before quest_type existed) keeps the legacy <see cref="QuestType.BanditClear" /> grounding, exactly
        ///   as every council quest behaved before this field shipped; the same fallback serves a token that is
        ///   unrecognized, or recognized but outside <see cref="Supported" /> (e.g. a hero-bound deed the block
        ///   cannot ground): the pledge then grounds exactly as it would have before, never refused for naming
        ///   an ambitious type. Synonym tolerance ("besiege", "scout") comes free from the SDK's single
        ///   vocabulary (<see cref="SectionResponseParser.ParseQuestType" />).
        /// </summary>
        public static QuestType Resolve(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return QuestType.BanditClear;

            QuestType? parsed = SectionResponseParser.ParseQuestType(token);

            return parsed.HasValue && Supported.Contains(parsed.Value)
                ? parsed.Value
                : QuestType.BanditClear;
        }
    }
}
