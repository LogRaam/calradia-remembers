// Code written by Gabriel Mailhot, 04/07/2026.
// Grudges pillar, discovery half: grudge_terms closes the reconcile loop. Before this verb, the player
// could see a known grievance (encyclopedia + prompt) but never LEARN what would actually mend it —
// Grudge.ResolutionHintDiscovered had no writer anywhere in the game. This verb is that writer: when the
// player asks what it would take to make things right, the NPC voices the price in character and the
// resolution hint lights up for good (encyclopedia's Grievances hint sentence, MakeAmendsVerb's own hint
// line) via the same DominantKnownGrudge shape MakeAmendsVerb already uses, narrowed to grudges whose
// hint is still undiscovered.

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
    ///   grudge_terms: the player asks what it would take to mend a grievance this NPC KNOWINGLY holds
    ///   against them, whose resolution hint they have not yet learned. Eligibility and teaching both read
    ///   the dominant such grudge from <see cref="GrudgeBehavior.KnownGrudgesFor" /> (known + resolvable +
    ///   targeting the player + <see cref="Grudge.ResolutionHintDiscovered" /> still false); execution
    ///   re-validates and flips the hint flag on the very grudge object the ledger holds by reference, so
    ///   the mutation persists at the next save exactly like <see cref="GrudgeResolutionService" />'s own
    ///   Resolve/Soften writes.
    /// </summary>
    public sealed class GrudgeTermsVerb : IConversationVerb
    {
        public string Name => "grudge_terms";

        public bool IsEligible(VerbContext ctx) => DominantUndiscoveredGrudge(ctx?.Npc) != null;

        public string TeachingText(VerbContext ctx)
        {
            Hero npc = ctx?.Npc;
            Grudge grudge = DominantUndiscoveredGrudge(npc);
            if (npc == null || grudge == null) return null;

            string word = GrudgeNarrator.RigidityWord(grudge.Rigidity);
            string cause = string.IsNullOrWhiteSpace(grudge.CauseSummary) ? "something between you" : grudge.CauseSummary;
            string price = PriceFor(grudge.ResolutionKind);

            var sb = new StringBuilder();
            sb.AppendLine("GRUDGE_TERMS, THE PLAYER ASKING WHAT WOULD MEND A GRIEVANCE AGAINST THEM:");
            sb.AppendLine($"You nurse {word} toward the player over {cause}. If they ask what it would take to make");
            sb.AppendLine("things right, how to mend it, or what you want in exchange for your forgiveness, voice the");
            sb.AppendLine($"true price in your own words: {price}");
            sb.AppendLine("When you have genuinely voiced it, and only then, emit:");
            sb.AppendLine("[ACTION]");
            sb.AppendLine("type: grudge_terms");
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

                Grudge grudge = DominantUndiscoveredGrudge(npc);
                if (grudge == null)
                    return ActionOutcome.Refused($"(There is nothing new to learn from {npc.Name} about this right now.)");

                grudge.ResolutionHintDiscovered = true;

                string sentence = HintSentenceFor(grudge.ResolutionKind);
                ModLog.Info($"[ACTION] grudge_terms: {npc.Name}'s resolution hint revealed ({grudge.ResolutionKind}).");

                return new ActionOutcome($"You now know what it would take to mend this with {npc.Name}: {sentence}.", true);
            }
            catch (Exception ex)
            {
                ModLog.Error($"[ACTION] grudge_terms failed: {ex.GetType().Name}: {ex.Message}");

                return null;
            }
        }

        /// <summary>The holder's dominant grudge that is known to the player, resolvable, held against the player, and whose resolution hint has not yet been discovered. Null when none qualifies.</summary>
        private static Grudge DominantUndiscoveredGrudge(Hero npc)
        {
            if (npc == null || Hero.MainHero == null || GrudgeBehavior.Instance == null) return null;

            var today = (int) CampaignTime.Now.ToDays;

            return GrudgeBehavior.Instance.KnownGrudgesFor(npc.StringId, today)
                .FirstOrDefault(g => !g.ResolutionHintDiscovered && GrudgePolicy.IsResolvable(g) && g.TargetId == Hero.MainHero.StringId);
        }

        /// <summary>The in-character flavour of the true price, keyed to the resolution kind, for the NPC to voice when asked.</summary>
        private static string PriceFor(GrudgeResolutionKind kind)
        {
            switch (kind)
            {
                case GrudgeResolutionKind.Amends: return "a sincere apology, owned in front of them, would go a long way.";
                case GrudgeResolutionKind.Gift: return "only a worthy gift would truly soften you.";
                case GrudgeResolutionKind.Deed: return "only a real deed in their favour would answer for it.";
                case GrudgeResolutionKind.Time: return "nothing they do will hasten it; only time will wear it down, and you say so plainly.";
                case GrudgeResolutionKind.Unforgivable: return "there is nothing that would mend this, and you say so coldly, with no room left for hope.";
                default: return "a sincere apology would go some way toward it.";
            }
        }

        /// <summary>The player-facing summary of what was just learned, mirroring GrudgeNarrator's own hint phrasing by resolution kind.</summary>
        private static string HintSentenceFor(GrudgeResolutionKind kind)
        {
            switch (kind)
            {
                case GrudgeResolutionKind.Amends: return "a sincere amend might yet mend it";
                case GrudgeResolutionKind.Gift: return "a worthy gift might soften it";
                case GrudgeResolutionKind.Deed: return "only a deed in their favour would answer for it";
                case GrudgeResolutionKind.Time: return "time may be the only balm";
                case GrudgeResolutionKind.Unforgivable: return "there is no mending this";
                default: return "words alone may help";
            }
        }
    }
}
