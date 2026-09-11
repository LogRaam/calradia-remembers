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
    /// <summary>
    ///   Why <see cref="QuestTargetVoidPolicy.ReasonToVoid" /> would cancel a hero-bound quest, or
    ///   <see cref="None" /> when it stands. Split out from a plain bool (v2.5.3, player report) because a
    ///   rescue's target being free is NOT the same fact as the target having left the world: the wording a
    ///   player reads must tell those apart, or a rescue the player just completed themselves reads as their
    ///   target having vanished.
    /// </summary>
    public enum QuestVoidReason
    {
        /// <summary>The target has not left the board (or is not held-captive-relevant); the quest stands.</summary>
        None,

        /// <summary>The target hero's StringId no longer resolves to anyone in the world.</summary>
        TargetVanished,

        /// <summary>The target hero resolves, but is dead.</summary>
        TargetDead,

        /// <summary>
        ///   A rescue's target is alive and present but no longer held captive. This is the safety-net path
        ///   only: QuestDeedTracker.OnHeroPrisonerReleased now credits a qualifying release BEFORE the daily
        ///   sweep ever runs (see QuestRescueCreditPolicy), so a quest reaching here has already survived that
        ///   credit and is either a release the hook never observed (an old save, a missed event) or one of
        ///   the ruling's own exclusions (ransom, peace, death is TargetDead above). Distinct from
        ///   TargetVanished/TargetDead because the target is NOT gone from the world; the closure wording
        ///   must say so honestly, never that the target vanished.
        /// </summary>
        RescueTargetFreed
    }

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
        ///   because its target hero left the board. <c>HasTargetHero</c> is false when the quest
        ///   names no specific hero (e.g. a DeliverPrisoner keyed on a faction rather than a named lord): those are
        ///   never voided here. <c>TargetResolved</c> is false when the target's StringId no longer
        ///   resolves to any hero in the world (vanished), <c>TargetAlive</c> false when it resolves
        ///   but is dead, and <c>TargetIsPrisoner</c> reports whether it is currently held captive
        ///   (only meaningful for a rescue). A target that is alive, present, and (for a rescue) still captive
        ///   keeps the quest standing.
        /// </summary>
        public static bool ShouldVoid(QuestTargetFacts facts)
        {
           if (facts == null) return false;

           QuestType type = facts.Type;
           bool hasTargetHero = facts.HasTargetHero;
           bool targetResolved = facts.TargetResolved;
           bool targetAlive = facts.TargetAlive;
           bool targetIsPrisoner = facts.TargetIsPrisoner;

           return QuestTargetVoidPolicy.ReasonToVoid(new QuestTargetVoidPolicy.QuestTargetFacts {Type = type, HasTargetHero = hasTargetHero, TargetResolved = targetResolved, TargetAlive = targetAlive, TargetIsPrisoner = targetIsPrisoner}) != QuestVoidReason.None;
        }

        /// <summary>
        ///   What is left of a quest's target hero. A quest type then FOUR booleans, three of which are read
        ///   in a strict order that IS the rule: vanished, then dead, then, for a rescue only, freed. Shared
        ///   with <see cref="ShouldVoid" /> so the yes/no and the why can never answer differently.
        ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
        ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
        ///   that names only what it is about reads as the rule it protects.
        /// </summary>
        public sealed class QuestTargetFacts
        {
           /// <summary>The quest's kind. Only some kinds are bound to a target hero at all.</summary>
           public QuestType Type { get; set; }

           /// <summary>The quest names a target hero.</summary>
           public bool HasTargetHero { get; set; }

           /// <summary>That hero can still be found in the world. Unresolved means vanished entirely.</summary>
           public bool TargetResolved { get; set; }

           /// <summary>They are alive. Capture, attack, execute, deliver and rescue are all moot otherwise.</summary>
           public bool TargetAlive { get; set; }

           /// <summary>
           ///   They are still held. A rescue needs that; freed or escaped there is nobody left to free, but they
           ///   are not GONE, which is why that case gets its own reason.
           /// </summary>
           public bool TargetIsPrisoner { get; set; }
        }

        /// <summary>
        ///   The same decision as <see cref="ShouldVoid" />, but naming WHY, so the host can give the player an
        ///   honest closure line instead of one blanket "gone from the world" wording for every case. See
        ///   <see cref="QuestVoidReason" /> for what each answer means and why <see cref="QuestVoidReason.RescueTargetFreed" />
        ///   is kept apart from the other two.
        /// </summary>
        public static QuestVoidReason ReasonToVoid(QuestTargetFacts facts)
        {
           if (facts == null) return QuestVoidReason.None;

           QuestType type = facts.Type;
           bool hasTargetHero = facts.HasTargetHero;
           bool targetResolved = facts.TargetResolved;
           bool targetAlive = facts.TargetAlive;
           bool targetIsPrisoner = facts.TargetIsPrisoner;

            if (!hasTargetHero) return QuestVoidReason.None;
            if (!IsTargetHeroBound(type)) return QuestVoidReason.None;

            if (!targetResolved) return QuestVoidReason.TargetVanished; // the hero has vanished from the world entirely
            if (!targetAlive) return QuestVoidReason.TargetDead;         // capture/attack/execute/deliver/rescue are all moot

            // A rescue needs the captive still held; freed or escaped, there is no longer anyone to free, but
            // they are not GONE, so this gets its own reason (see QuestVoidReason.RescueTargetFreed).
            if (type == QuestType.RescuePrisoner && !targetIsPrisoner) return QuestVoidReason.RescueTargetFreed;

            return QuestVoidReason.None;
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
