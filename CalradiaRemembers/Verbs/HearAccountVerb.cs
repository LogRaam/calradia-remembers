// Code written by Gabriel Mailhot, 19/09/2026.
// Grudges pillar: hear_account records that the player has ANSWERED a grievance, and what they said.
//
// Zivkyn (Nexus, 18/09/2026): a lord kept "asking why i killed their Kinsmen, that i did months ago, even though
// ive explained it 10 times now". Every explanation was spoken into a conversation and lost with it; the grudge
// itself never learned it had been answered, so the next conversation met it fresh. Gabriel's ruling (19/09/2026):
// an answer mends nothing (a vendetta stays unforgivable, a mendable grudge is still mended only by amends, gifts or
// deeds), but the holder must KNOW it was answered, stop asking, and stop riding out to confront the player.
//
// Unlike make_amends this is taught for vendettas too: those are exactly the grievances nothing else can touch, and
// the ones Zivkyn met.

#region

using System;
using System.Linq;
using System.Text;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Logic.Grudges;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Verbs
{
    /// <summary>
    ///   hear_account: the player explains or justifies a grievance this NPC knowingly holds against them. Records
    ///   the answer on the grudge (<see cref="GrudgePolicy.RecordAnswer" />); the weight of the grudge is untouched.
    /// </summary>
    public sealed class HearAccountVerb : IConversationVerb
    {
        public string Name => "hear_account";

        public bool IsEligible(VerbContext ctx) => UnansweredKnownGrudge(ctx?.Npc) != null;

        public string TeachingText(VerbContext ctx)
        {
            Grudge grudge = UnansweredKnownGrudge(ctx?.Npc);
            if (grudge == null) return null;

            string cause = string.IsNullOrWhiteSpace(grudge.CauseSummary) ? "what lies between you" : grudge.CauseSummary;

            var sb = new StringBuilder();
            sb.AppendLine("HEAR_ACCOUNT, THE PLAYER ANSWERING YOUR GRIEVANCE:");
            sb.AppendLine($"When the player gives you their account of {cause} (why they did it, what they say happened,");
            sb.AppendLine("or their justification), record that you have heard it, whether or not you believe a word:");
            sb.AppendLine("[ACTION]");
            sb.AppendLine("type: hear_account");
            sb.AppendLine("account: <their account, in one short line of your own words>");
            sb.AppendLine("[/ACTION]");
            sb.AppendLine("Hearing it is not forgiving it: your grievance stays exactly as heavy. It only means you will not ask again.");
            sb.AppendLine();

            return sb.ToString();
        }

        public ActionOutcome Execute(GameAction action, VerbContext ctx)
        {
            try
            {
                Hero npc = ctx?.Npc;
                if (npc == null || Hero.MainHero == null) return null;

                Grudge grudge = UnansweredKnownGrudge(npc);
                if (grudge == null) return null; // nothing to answer, or already answered: silently nothing to do

                string account = null;
                action?.Parameters?.TryGetValue("account", out account);
                if (string.IsNullOrWhiteSpace(account)) account = action?.Context;

                if (!GrudgePolicy.RecordAnswer(grudge, account, (int) CampaignTime.Now.ToDays))
                {
                    ModLog.Info($"[ACTION] hear_account: {npc.Name} emitted it with no account; nothing recorded.");

                    return null;
                }

                ModLog.Info($"[ACTION] hear_account: {npc.Name}'s {grudge.Source} grudge answered: \"{grudge.PlayerAccount}\".");

                // Not a success toast: nothing was mended, and a line saying so would read like a reward.
                return null;
            }
            catch (Exception ex)
            {
                ModLog.Error($"[ACTION] hear_account failed: {ex.GetType().Name}: {ex.Message}");

                return null;
            }
        }

        /// <summary>
        ///   The holder's dominant KNOWN grudge against the player that has not been answered yet. Vendettas included,
        ///   unlike make_amends: answering is not mending.
        /// </summary>
        internal static Grudge UnansweredKnownGrudge(Hero npc)
        {
            if (npc == null || Hero.MainHero == null || GrudgeBehavior.Instance == null) return null;

            return GrudgeBehavior.Instance.KnownGrudgesFor(npc.StringId, (int) CampaignTime.Now.ToDays)
                .FirstOrDefault(g => !g.IsResolved && !GrudgePolicy.IsAnswered(g) && g.TargetId == Hero.MainHero.StringId);
        }
    }
}
