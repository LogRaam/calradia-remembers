// Code written by Gabriel Mailhot, 03/07/2026.
// DIVORCE, Phase 1 (NPC case): end_marriage is the first verb built on the IConversationVerb contract
// from scratch (the four court verbs were migrated onto it; this one was designed for it). A marriage is
// an alliance: the NPC never dissolves theirs on the spot, only agrees to BEGIN an estrangement process
// that plays out over days (CalradiaRemembers.Marriage.DivorceProcessBehavior), which the spouse may
// resist, delay, or defeat outright.

#region

using System;
using System.Text;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Marriage;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Verbs
{
    /// <summary>
    ///   end_marriage: the player persuades the NPC to begin ending THEIR OWN marriage (never the
    ///   player's own spouse — that is Phase 2, out of scope here). Eligibility is entirely
    ///   <see cref="DivorceEligibilityResolver.IsEligible" /> (re-checked fresh both while teaching and at
    ///   execution). Taught only when eligible, and the teaching text is explicit that this BEGINS a slow
    ///   estrangement, not an instant act.
    /// </summary>
    public sealed class EndMarriageVerb : IConversationVerb
    {
        public string Name => "end_marriage";

        public bool IsEligible(VerbContext ctx)
            => ctx?.Npc != null && DivorceEligibilityResolver.IsEligible(ctx.Npc, out _);

        public string TeachingText(VerbContext ctx)
        {
            if (ctx?.Npc == null) return null;
            if (!DivorceEligibilityResolver.IsEligible(ctx.Npc, out _)) return null;

            Hero spouse = ctx.Npc.Spouse;
            if (spouse == null) return null;

            var sb = new StringBuilder();
            sb.AppendLine("END_MARRIAGE, ENDING YOUR OWN MARRIAGE:");
            sb.AppendLine($"Your marriage to {spouse.Name} has grown truly unhappy, and your trust in the player runs deep");
            sb.AppendLine("enough that you could confide this and let them counsel you toward ending it. This is a grave,");
            sb.AppendLine("scandalous undertaking, one you would only accept for someone you deeply trust, and only when the");
            sb.AppendLine("marriage is genuinely unhappy, never to please the player or on a whim.");
            sb.AppendLine("This does NOT end the marriage on the spot. It BEGINS a slow estrangement that plays out over");
            sb.AppendLine($"days, one {spouse.Name} may resist, delay, or even defeat outright — both your houses will feel it.");
            sb.AppendLine("When you have genuinely resolved to begin this, and only then, emit:");
            sb.AppendLine("[ACTION]");
            sb.AppendLine("type: end_marriage");
            sb.AppendLine("[/ACTION]");
            sb.AppendLine();

            return sb.ToString();
        }

        public ActionOutcome Execute(GameAction action, VerbContext ctx)
        {
            try
            {
                Hero npcHero = ctx?.Npc;
                NpcProfile profile = ctx?.Profile;

                if (npcHero == null || profile == null) return null;
                if (!DivorceEligibilityResolver.IsEligible(npcHero, out string reason))
                    return new ActionOutcome($"({npcHero.Name} cannot begin this: {reason}.)", false);

                Hero spouse = npcHero.Spouse;
                if (spouse == null) return null; // eligibility guaranteed a spouse a moment ago — defensive only

                DivorceProcessBehavior processes = DivorceProcessBehavior.Instance;
                if (processes == null) return null;

                var today = (int) CampaignTime.Now.ToDays;
                processes.Create(npcHero.StringId, spouse.StringId, today);

                profile.Events.Add(new NotableEvent(today, NotableEventType.Agreement,
                    $"Resolved to end the marriage to {spouse.Name}, with the player's counsel."));

                ModLog.Info($"[ACTION] end_marriage: {npcHero.Name} began an estrangement from {spouse.Name}.");

                return new ActionOutcome(
                    $"{npcHero.Name} resolves to end the marriage to {spouse.Name}. It will not happen overnight, {spouse.Name} may yet resist.",
                    true);
            }
            catch (Exception ex)
            {
                ModLog.Error($"[ACTION] end_marriage failed: {ex.GetType().Name}: {ex.Message}");

                return null;
            }
        }
    }
}
