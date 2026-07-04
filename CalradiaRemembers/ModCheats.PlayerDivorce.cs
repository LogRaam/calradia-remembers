// Code written by Gabriel Mailhot, 04/07/2026.
// Divorce, Phase 2a: console diagnostic for the PLAYER themselves ending their OWN marriage.
// cr.end_own_marriage dispatches through the SAME bridge.ExecuteAction path a conversation uses for
// end_own_marriage, so eligibility (npcHero == player.Spouse) and the mutual-vs-repudiated consent
// decision are genuinely re-validated live, not reimplemented for the console.

#region

using System.Collections.Generic;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.GameState;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
    public static partial class ModCheats
    {
        /// <summary>
        ///   cr.end_own_marriage — dispatches end_own_marriage through the REAL bridge.ExecuteAction path
        ///   (the same one a conversation uses) against the player's own current spouse. Says so plainly
        ///   when the player is unmarried, rather than mis-reporting the action as broken.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("end_own_marriage", "cr")]
        public static string EndOwnMarriage(List<string> args)
        {
            if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

            Hero spouse = Hero.MainHero?.Spouse;
            if (spouse == null) return "You are not currently married.";

            ActionOutcome outcome = behavior.GameStateBridge.ExecuteAction(
                new GameAction {Type = "end_own_marriage", Parameters = new Dictionary<string, string>()},
                spouse, new ActionContext(GetOrCreateProfile(behavior, spouse), null));

            return outcome == null
                ? $"{spouse.Name}: end_own_marriage not eligible right now (the dissolution guard refused it)."
                : outcome.Message;
        }
    }
}
