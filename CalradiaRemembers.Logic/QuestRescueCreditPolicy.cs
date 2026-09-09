// Code written by Gabriel Mailhot, 08/09/2026.
// Player report (v2.5.3, 2026-09-07): the player got a quest target out of a settlement dungeon, then a few
// days later read "so and so is lost, they are no longer on the map" and the rescue quest was cancelled. His
// success cancelled his quest. A rescue used to be credited ONLY from a player-won field battle
// (QuestDeedTracker.OnMapEventEnded reading BattleFacts.FreedPrisonerHeroIds); freeing someone from a dungeon
// produces no such battle, so the deed went uncredited and the daily void sweep later read the freed target
// as proof the task was impossible (CalradiaRemembers.Logic.QuestTargetVoidPolicy.ShouldVoid). Gabriel's
// ruling reverses the old "rescue is only detected from a battle" decision: credit the release however it
// happened, EXCEPT a ransom or a release that came bundled with a peace treaty (benefit of the doubt goes to
// the player everywhere else, having just seen what the opposite costs). A death frees nobody, so it never
// credits either. Kept engine-free: the host maps TaleWorlds.CampaignSystem.Actions.EndCaptivityDetail onto
// CaptivityEndReason and passes the plain fact in, exactly as QuestTargetVoidPolicy already does for the
// target hero's live state.
//
// The two routes that actually free an imprisoned hero, traced against the engine: (1) the vanilla PRISON
// BREAK, where the player enters a town or castle's keep, fights or sneaks past the guards, and reaches the
// exit, on success calling EndCaptivityAction.ApplyByEscape via SandBox.CampaignBehaviors.PrisonBreakCampaignBehavior,
// which dispatches ReleasedAfterEscape (this is the reporter's exact case); and (2) CONQUERING the settlement
// holding them, which frees every prisoner in its dungeon via EndCaptivityAction.ApplyByReleasedAfterBattle
// (ReleasedAfterBattle), not only a field battle won as before. Ransom and Peace map onto the engine's own
// EndCaptivityAction.ApplyByRansom and ApplyByPeace, so the ruling's two exclusions already line up with the
// engine's vocabulary with nothing to force.
//
// One honest consequence, not an oversight: ApplyByEscape's own "facilitator" parameter (who staged the
// break) is never dispatched on the CampaignEvent (see QuestDeedTracker's comment on this), so a prisoner who
// slips free on their own, unaided, reports the exact same ReleasedAfterEscape reason as a staged breakout and
// credits identically. Gabriel chose that deliberately, having just seen what the strict "only a proven
// player deed" reading cost a real player.

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Engine-free mirror of TaleWorlds.CampaignSystem.Actions.EndCaptivityDetail. The host
    ///   (CalradiaRemembers.Quests.QuestDeedTracker) resolves the engine's own enum onto this one; a value the
    ///   engine adds in some future patch, unrecognised by that mapping, resolves to <see cref="Other" /> so it
    ///   still lands on a defined, deliberate answer in <see cref="QuestRescueCreditPolicy" /> rather than being
    ///   silently dropped.
    /// </summary>
    public enum CaptivityEndReason
    {
        /// <summary>The captive was bought free with coin. Not the player's own deed.</summary>
        Ransom,

        /// <summary>The captive walked free as a term of a peace treaty. Not the player's own deed.</summary>
        Peace,

        /// <summary>
        ///   A battle ended the captivity (the captor's side lost a field fight, or the settlement holding the
        ///   captive was conquered, freeing every prisoner in its dungeon).
        /// </summary>
        Battle,

        /// <summary>
        ///   The captive got free of the captor's hold. Covers BOTH a player-staged prison break (the vanilla
        ///   mission: enter the keep's Prison menu, fight or sneak past the guards, reach the exit; on success
        ///   SandBox's PrisonBreakCampaignBehavior calls EndCaptivityAction.ApplyByEscape) and a captive who
        ///   slips free entirely unaided, since the engine reports both the same way and never names who (if
        ///   anyone) staged it.
        /// </summary>
        Escape,

        /// <summary>The captor chose to let the captive go (mercy, a bargain, or the player's own free_prisoner action).</summary>
        ByChoice,

        /// <summary>The captive died in captivity. Frees nobody; there is no one left to rescue.</summary>
        Death,

        /// <summary>The captivity ended through some other compensation to the captor.</summary>
        Compensation,

        /// <summary>
        ///   A reason this mod's host mapping does not recognise (an engine value added after this was written).
        ///   Falls into the ruling's "everything else" bucket: benefit of the doubt goes to the player.
        /// </summary>
        Other
    }

    /// <summary>
    ///   Decides whether the end of a captivity should be credited to the player as a rescue, for the purpose
    ///   of satisfying an outstanding <see cref="NpcMemoryService.Core.Models.QuestType.RescuePrisoner" /> quest.
    /// </summary>
    public static class QuestRescueCreditPolicy
    {
        /// <summary>
        ///   True when this way of ending a captivity should credit an outstanding rescue quest on the freed
        ///   hero. Total over <see cref="CaptivityEndReason" />: a ransom, a peace release, and a death are the
        ///   only exclusions named by the ruling; every other reason, including <see cref="CaptivityEndReason.Other" />
        ///   (a future engine reason this mod does not yet know), credits.
        /// </summary>
        public static bool CreditsPlayerRescue(CaptivityEndReason reason)
        {
            switch (reason)
            {
                case CaptivityEndReason.Ransom:
                case CaptivityEndReason.Peace:
                case CaptivityEndReason.Death:
                    return false;
                default:
                    return true;
            }
        }
    }
}
