// Code written by Gabriel Mailhot, 12/11/2026.
// EvilPoptart1987 (Nexus, 11/11/2026, 2.8.8): "Characters need to stop forcing quests on you that you cannot conceivably
// complete. For instance, capturing a noble you are not at war with. ... you serve a realm not at war with them and even
// if you can attack them, why would you start a war over one person. It forces you to abandon and incur a massive grudge
// for no reason." Traced: a capture is judged by the GIVER's war (CaptureQuestPolicy), so a lord at war with someone hands
// the player that capture even when the player's own realm is at peace with them; and abandoning it costs the full broken
// word. Gabriel (12/11/2026, "A + C"): the offer says so before the player accepts, and giving such a task up is a
// disappointment, not a grudge. This decides when a task means attacking a realm the player is at peace with.

#region

using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Quests
{
    /// <summary>What decides whether carrying out a task means attacking a realm the player is at peace with.</summary>
    public sealed class PeaceAttackFacts
    {
        /// <summary>The task's kind.</summary>
        public QuestType Type { get; init; }

        /// <summary>True when the realm the task strikes at could be told (a named lord's, a village's, a faction's).</summary>
        public bool TargetRealmKnown { get; init; }

        /// <summary>True when the player's own realm (or house, with no realm) is at war with that realm.</summary>
        public bool PlayerAtWarWithTarget { get; init; }

        /// <summary>True when the lord the task names is already the player's captive, so no attack is needed to take him.</summary>
        public bool TargetHeroHeldByPlayer { get; init; }
    }

    /// <summary>Decides whether a task means attacking a realm the player is at peace with. Pure.</summary>
    public static class PeaceAttackPolicy
    {
        /// <summary>
        ///   True when carrying out the task means an attack on a realm the player is not at war with: a lord to take,
        ///   beat or put to death who is not yet in the player's hands, or a realm's party, caravan, village or walls to
        ///   strike. A war declared on purpose (declare_war) is the deed itself, never a hidden cost.
        /// </summary>
        public static bool MeansAttackingARealmAtPeace(PeaceAttackFacts facts)
        {
            if (facts == null || !facts.TargetRealmKnown || facts.PlayerAtWarWithTarget) return false;

            switch (facts.Type)
            {
                case QuestType.CapturePrisoner:
                case QuestType.DeliverPrisoner:
                case QuestType.ExecuteEnemy:
                    return !facts.TargetHeroHeldByPlayer;
                case QuestType.AttackLord:
                case QuestType.AttackFaction:
                case QuestType.AttackCaravan:
                case QuestType.RaidVillage:
                case QuestType.Siege:
                    return true;
                default:
                    return false;
            }
        }
    }
}
