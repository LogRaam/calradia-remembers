// Code written by Gabriel Mailhot, 04/07/2026.
// Grudges pillar, resolution half: make_amends is the conversational Amends gesture (GrudgeGesture.Amends)
// from the ROADMAP's resolvable-grudge loop. Only ever taught for a grudge the player has actually LEARNED
// of (GrudgeBehavior.KnownGrudgesFor) — the player can only try to mend what they know is wrong. The gift
// and deed gestures resolve through their own channels (take_gold/give_item, and a future deed hook); this
// verb is words alone.

#region

using System;
using System.Linq;
using System.Text;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Logic.Grudges;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Verbs
{
    /// <summary>
    ///   make_amends: the player sincerely apologises or offers, in words, to set right a grievance this
    ///   NPC KNOWINGLY holds against them. Eligibility and teaching both read the dominant grudge from
    ///   <see cref="GrudgeBehavior.KnownGrudgesFor" /> (known + resolvable + targeting the player);
    ///   execution re-validates and delegates the evaluate-apply-persist sequence to
    ///   <see cref="GrudgeResolutionService.TryResolveDominant" />, the same seam the gift-routing hooks use.
    /// </summary>
    public sealed class MakeAmendsVerb : IConversationVerb
    {
        public string Name => "make_amends";

        public bool IsEligible(VerbContext ctx) => DominantKnownGrudge(ctx?.Npc) != null;

        public string TeachingText(VerbContext ctx)
        {
            Hero npc = ctx?.Npc;
            Grudge grudge = DominantKnownGrudge(npc);
            if (npc == null || grudge == null) return null;

            string word = GrudgeNarrator.RigidityWord(grudge.Rigidity);
            string cause = string.IsNullOrWhiteSpace(grudge.CauseSummary) ? "something between you" : grudge.CauseSummary;

            var sb = new StringBuilder();
            sb.AppendLine("MAKE_AMENDS, THE PLAYER TRYING TO MEND A GRIEVANCE AGAINST THEM:");
            sb.AppendLine($"You nurse {word} toward the player over {cause}. If they sincerely apologise, or earnestly");
            sb.AppendLine("offer in words to set things right, you may accept their amends, but only when it truly feels");
            sb.AppendLine("genuine to you, never merely because they asked.");

            string hint = grudge.ResolutionHintDiscovered ? HintFor(grudge.ResolutionKind) : null;
            if (!string.IsNullOrEmpty(hint)) sb.AppendLine(hint);

            sb.AppendLine("When you have genuinely accepted their amends, and only then, emit:");
            sb.AppendLine("[ACTION]");
            sb.AppendLine("type: make_amends");
            sb.AppendLine("[/ACTION]");
            sb.AppendLine();

            return sb.ToString();
        }

        public ActionOutcome Execute(GameAction action, VerbContext ctx)
        {
            try
            {
                Hero npc = ctx?.Npc;
                if (npc == null || Hero.MainHero == null) return null;

                // C5: resolve the SAME grudge the player was shown (the dominant KNOWN one), not whatever the
                // dominant LIVE grudge happens to be, so an apology can never silently mend a heavier hidden grudge.
                Grudge grudge = DominantKnownGrudge(npc);
                if (grudge == null)
                    return ActionOutcome.Refused($"({npc.Name} has no such grievance to mend right now.)");

                var today = (int) CampaignTime.Now.ToDays;
                GrudgeResolutionAttemptResult result = GrudgeResolutionService.TryResolve(npc, grudge, GrudgeGesture.Amends, 0, today);

                if (!result.HadResolvableGrudge) return null; // eligibility guaranteed one a moment ago — defensive only

                switch (result.Outcome)
                {
                    case GrudgeResolutionOutcome.Resolved:
                        ModLog.Info($"[ACTION] make_amends: {npc.Name}'s grievance resolved.");

                        return new ActionOutcome($"{npc.Name}'s grievance is mended.", true);
                    case GrudgeResolutionOutcome.Softened:
                        ModLog.Info($"[ACTION] make_amends: {npc.Name}'s grievance softened.");

                        return new ActionOutcome($"{npc.Name} softens, but the wound is not fully healed.", true);
                    default:
                        ModLog.Info($"[ACTION] make_amends: {npc.Name} was not moved.");

                        return new ActionOutcome($"Words are not enough for {npc.Name}.", false);
                }
            }
            catch (Exception ex)
            {
                ModLog.Error($"[ACTION] make_amends failed: {ex.GetType().Name}: {ex.Message}");

                return null;
            }
        }

        /// <summary>The holder's dominant grudge the player already knows of, resolvable, and held against the player. Null when none qualifies.</summary>
        private static Grudge DominantKnownGrudge(Hero npc)
        {
            if (npc == null || Hero.MainHero == null || GrudgeBehavior.Instance == null) return null;

            var today = (int) CampaignTime.Now.ToDays;

            return GrudgeBehavior.Instance.KnownGrudgesFor(npc.StringId, today)
                .FirstOrDefault(g => GrudgePolicy.IsResolvable(g) && g.TargetId == Hero.MainHero.StringId);
        }

        /// <summary>A directive naming what would truly satisfy the grudge, once its resolution hint has been discovered — an apology may not be enough for a wound that wants a gift or a deed.</summary>
        private static string HintFor(GrudgeResolutionKind kind)
        {
            switch (kind)
            {
                case GrudgeResolutionKind.Gift: return "Words alone will not fully mend this; only a worthy gift would truly satisfy you.";
                case GrudgeResolutionKind.Deed: return "Words alone cannot mend this; only a real deed in your favour would answer for it.";
                case GrudgeResolutionKind.Time: return "No apology hastens this much; only time truly wears it down.";
                default: return null; // Amends: an apology already suffices, nothing extra to hint
            }
        }
    }
}
