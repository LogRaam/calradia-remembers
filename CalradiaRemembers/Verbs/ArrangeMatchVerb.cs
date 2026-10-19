// Code written by Gabriel Mailhot, 01/10/2026.

#region

using System;
using System.Text;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.GameState;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Verbs
{
    /// <summary>
    ///   arrange_match (fkasad, Nexus, 01/10/2026: "Playing wingman for a guy trying hard to get a girl married?"; Gabriel:
    ///   a real marriage): the head of a lady's house (or the lady herself, when she answers for herself) gives her hand
    ///   to the suitor who sent the player. Taught only to someone with authority over a lady some lord has sent the
    ///   player to win for him. The bridge re-checks everything: the engine's own legality, no captivity, no war between
    ///   the houses, her heart (she must hold him a friend), her family's consent; then the game weds them.
    /// </summary>
    public sealed class ArrangeMatchVerb : IConversationVerb
    {
        public string Name => "arrange_match";

        public bool IsEligible(VerbContext ctx) => Pending(ctx) != null;

        public string TeachingText(VerbContext ctx)
        {
            PendingMatch match = Pending(ctx);
            if (match == null) return null;

            var sb = new StringBuilder();
            sb.AppendLine("ARRANGE_MATCH, GIVING A HAND IN MARRIAGE:");
            sb.AppendLine($"The player comes on behalf of {match.SuitorName}, who would wed {match.BrideName}, and you have the say over her");
            sb.AppendLine("hand. Weigh the match as the head of a house would. If you consent to the marriage, emit:");
            sb.AppendLine("[ACTION]");
            sb.AppendLine("type: arrange_match");
            sb.AppendLine("[/ACTION]");
            sb.AppendLine("The game weds them only if she herself holds him dear enough and the match is lawful; otherwise it refuses,");
            sb.AppendLine("and you may say so.");
            sb.AppendLine();

            return sb.ToString();
        }

        public ActionOutcome Execute(GameAction action, VerbContext ctx)
        {
            try
            {
                if (Pending(ctx) == null) return ActionOutcome.Refused("(No match has been asked of you; nothing is promised.)");

                return ctx.Bridge.SettleMatch(ctx.Npc);
            }
            catch (Exception ex)
            {
                ModLog.Error($"[ACTION] arrange_match failed: {ex.GetType().Name}: {ex.Message}");

                return null;
            }
        }

        private static PendingMatch Pending(VerbContext ctx) => ctx?.Npc == null || ctx.Bridge == null ? null : ctx.Bridge.MatchToArrange(ctx.Npc);
    }
}
