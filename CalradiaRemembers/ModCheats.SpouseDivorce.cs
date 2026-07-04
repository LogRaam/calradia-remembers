// Code written by Gabriel Mailhot, 04/07/2026.
// Divorce, Phase 2b: console diagnostics for the player's OWN spouse's divorce demand. Mirrors the
// cr.divorce_* pattern (cr.divorce_info -> cr.divorce -> cr.divorce_tick), and cr.divorce dispatches
// through the SAME bridge.ExecuteAction path a conversation uses for accept_divorce/decline_divorce.

#region

using System.Collections.Generic;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Marriage;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
    public static partial class ModCheats
    {
        /// <summary>
        ///   cr.spouse_divorce_accept — dispatches accept_divorce through the REAL bridge.ExecuteAction path
        ///   (the same one a conversation uses) against the player's own spouse. Use after
        ///   cr.spouse_divorce_demand to test the accept path without waiting on the LLM.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("spouse_divorce_accept", "cr")]
        public static string SpouseDivorceAccept(List<string> args)
        {
            if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

            Hero spouse = Hero.MainHero?.Spouse;
            if (spouse == null) return "You are not currently married.";

            ActionOutcome outcome = behavior.GameStateBridge.ExecuteAction(
                new GameAction {Type = "accept_divorce", Parameters = new Dictionary<string, string>()},
                spouse, new ActionContext(GetOrCreateProfile(behavior, spouse), null));

            return outcome == null
                ? $"{spouse.Name}: accept_divorce not eligible right now (is a demand pending? run cr.spouse_divorce_demand)."
                : outcome.Message;
        }

        /// <summary>
        ///   cr.spouse_divorce_decline — dispatches decline_divorce through the REAL bridge.ExecuteAction
        ///   path against the player's own spouse. Registers the refusal immediately (the grudge deepens)
        ///   instead of waiting out the grace-period timeout.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("spouse_divorce_decline", "cr")]
        public static string SpouseDivorceDecline(List<string> args)
        {
            if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;

            Hero spouse = Hero.MainHero?.Spouse;
            if (spouse == null) return "You are not currently married.";

            ActionOutcome outcome = behavior.GameStateBridge.ExecuteAction(
                new GameAction {Type = "decline_divorce", Parameters = new Dictionary<string, string>()},
                spouse, new ActionContext(GetOrCreateProfile(behavior, spouse), null));

            return outcome == null
                ? $"{spouse.Name}: decline_divorce not eligible right now (is a demand pending? run cr.spouse_divorce_demand)."
                : outcome.Message;
        }

        /// <summary>
        ///   cr.spouse_divorce_demand — forces the player's CURRENT spouse into the demanding state right
        ///   now (sets the persisted flag; the announcing letter is queued too, when the LLM is available).
        ///   Says so plainly when the player is unmarried.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("spouse_divorce_demand", "cr")]
        public static string SpouseDivorceDemand(List<string> args)
        {
            if (!TryContext(out _, out string error)) return error;
            if (SpouseDivorceDemandBehavior.Instance == null) return "Spouse-divorce-demand behavior not active. Start a campaign first.";

            (bool success, string message) = SpouseDivorceDemandBehavior.Instance.ForceDemand();

            return success
                ? message + " Talk to them, then cr.spouse_divorce_accept / cr.spouse_divorce_decline to resolve it without the LLM."
                : message;
        }

        /// <summary>
        ///   cr.spouse_divorce_info — reports whether the player's current spouse (if any) has a pending
        ///   divorce demand, and since which day. Read-only.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("spouse_divorce_info", "cr")]
        public static string SpouseDivorceInfo(List<string> args)
        {
            if (!TryContext(out _, out string error)) return error;

            Hero spouse = Hero.MainHero?.Spouse;
            var sb = new StringBuilder();

            if (spouse == null)
            {
                sb.AppendLine("You are not currently married.");

                return sb.ToString();
            }

            int? startDay = SpouseDivorceDemandBehavior.Instance?.DemandStartDay(spouse.StringId);
            sb.AppendLine(startDay == null
                ? $"{spouse.Name}: no divorce demand pending."
                : $"{spouse.Name}: DEMANDING since day {startDay} (today is day {(int) CampaignTime.Now.ToDays}).");

            return sb.ToString();
        }

        /// <summary>
        ///   cr.spouse_divorce_tick — runs one weekly evaluation right now (the demand-raising check plus
        ///   the grace-period refusal sweep), skipping the wait for the real weekly tick.
        /// </summary>
        [CommandLineFunctionality.CommandLineArgumentFunction("spouse_divorce_tick", "cr")]
        public static string SpouseDivorceTick(List<string> args)
        {
            if (!TryContext(out _, out string error)) return error;
            if (SpouseDivorceDemandBehavior.Instance == null) return "Spouse-divorce-demand behavior not active.";

            SpouseDivorceDemandBehavior.Instance.RunTick();

            return "Ran one weekly evaluation (demand-raising + grace-period refusal sweep). Use cr.spouse_divorce_info to see the result.";
        }
    }
}
