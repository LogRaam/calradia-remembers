// Code written by Gabriel Mailhot, 06/08/2026.
// v1.34.2 fault/no-fault hardening (audit Pattern 2): decides when an OUTSTANDING informal quest bound to a
// specific target hero has become impossible through no fault of the player, because that hero left the board
// (died, vanished, or, for a rescue, is no longer a prisoner) between the promise and its execution. The daily
// quest sweep (CalradiaRemembers.Quests.QuestService.RunDailyMaintenance) only ever handled the GIVER dying and
// the war premise ending; a capture/attack/execute/rescue/deliver quest whose TARGET died went untouched and
// later expired with an unjust relation penalty plus a FailedQuest grudge, punishing the player for an act of
// the world. This pure rule tells the sweep to cancel such a quest as a clean NEUTRAL VOID instead. Kept
// engine-free so it stays tested; the host resolves the live hero state and passes plain facts in.

#region

using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Decides whether a hero-bound quest should be voided because its target hero has left the world.</summary>
    public static class QuestTargetVoidPolicy
    {
        /// <summary>
        ///   True for the quest kinds bound to ONE specific target hero whose loss makes the task impossible
        ///   through no fault of the player: capture, attack, execute, rescue, and deliver-to-a-named-hero.
        ///   Faction- or settlement-bound kinds (BanditClear, AttackFaction, RaidVillage, Siege, ...) are NOT
        ///   here: their premise is handled separately (QuestService.PremiseGone). NemesisBounty is deliberately
        ///   excluded too: it has its own dedicated resolver (NemesisResolutionBehavior /
        ///   QuestService.CancelOutstandingBountiesOn) that must not be second-guessed here.
        /// </summary>
        public static bool IsTargetHeroBound(QuestType type)
        {
            switch (type)
            {
                case QuestType.CapturePrisoner:
                case QuestType.AttackLord:
                case QuestType.ExecuteEnemy:
                case QuestType.RescuePrisoner:
                case QuestType.DeliverLetter:
                case QuestType.DeliverPrisoner:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        ///   Whether an outstanding quest should be VOIDED (cancelled with no penalty, no grudge, no fail banner)
        ///   because its target hero left the board. <paramref name="hasTargetHero" /> is false when the quest
        ///   names no specific hero (e.g. a DeliverPrisoner keyed on a faction rather than a named lord): those are
        ///   never voided here. <paramref name="targetResolved" /> is false when the target's StringId no longer
        ///   resolves to any hero in the world (vanished), <paramref name="targetAlive" /> false when it resolves
        ///   but is dead, and <paramref name="targetIsPrisoner" /> reports whether it is currently held captive
        ///   (only meaningful for a rescue). A target that is alive, present, and (for a rescue) still captive
        ///   keeps the quest standing.
        /// </summary>
        public static bool ShouldVoid(QuestType type, bool hasTargetHero, bool targetResolved, bool targetAlive, bool targetIsPrisoner)
        {
            if (!hasTargetHero) return false;
            if (!IsTargetHeroBound(type)) return false;

            if (!targetResolved) return true; // the hero has vanished from the world entirely
            if (!targetAlive) return true;     // the hero is dead: capture/attack/execute/deliver/rescue are all moot

            // A rescue needs the captive still held; freed or escaped, there is no longer anyone to free.
            if (type == QuestType.RescuePrisoner && !targetIsPrisoner) return true;

            return false;
        }

        /// <summary>
        ///   The narrower set of hero-bound quests whose whole point is to STRIKE a specific enemy lord (capture,
        ///   attack, or execute). Their premise is the war between the giver's realm and the target's: once peace
        ///   is made, carrying the task out would be a fresh act of war against a realm the giver just signed a
        ///   treaty with (player report: a capture-a-lord task survived the peace and could not be dropped).
        ///   Rescue and deliver kinds are deliberately NOT here: peace does not make rescuing your own man or
        ///   delivering a letter impossible, so a treaty must never silently void them.
        /// </summary>
        public static bool IsWarHostileHeroQuest(QuestType type)
        {
            switch (type)
            {
                case QuestType.CapturePrisoner:
                case QuestType.AttackLord:
                case QuestType.ExecuteEnemy:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        ///   Whether a war-hostile hero quest should be VOIDED (neutral: no penalty, no grudge, no fail banner)
        ///   because the giver's realm is no longer at war with the target hero's realm. Only a hero-bound war
        ///   quest (<see cref="IsWarHostileHeroQuest" />) qualifies, and only once peace holds; while the war is
        ///   still on, the task stands. Faction-bound war quests are voided separately by QuestService.PremiseGone;
        ///   this fills the gap for the ones keyed on a specific enemy lord (which carry a target hero, not a
        ///   target faction, so PremiseGone never saw them).
        /// </summary>
        public static bool ShouldVoidOnPeace(QuestType type, bool hasTargetHero, bool giverAtWarWithTargetFaction)
        {
            return hasTargetHero && IsWarHostileHeroQuest(type) && !giverAtWarWithTargetFaction;
        }
    }
}
