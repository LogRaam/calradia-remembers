// Code written by Gabriel Mailhot, 03/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Text;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

#endregion

namespace CalradiaRemembers.Verbs
{
    /// <summary>
    ///   release_prisoner (RP with teeth, increment 1): the player intercedes for a THIRD-PARTY hero
    ///   prisoner the NPC's own clan holds (not the player's own captive, that is <c>free_prisoner</c>).
    ///   Taught whenever <see cref="CourtActionResolver.HeldPrisonersOf" /> lists at least one captive;
    ///   re-resolves the named captive against the LIVE holdings and re-checks custody
    ///   (<see cref="CourtActionResolver.CanRelease" />) before freeing them at execution.
    /// </summary>
    public sealed class ReleasePrisonerVerb : IConversationVerb
    {
        public string Name => "release_prisoner";

        public bool IsEligible(VerbContext ctx)
            => ctx?.Npc != null && CourtActionResolver.HeldPrisonersOf(ctx.Npc).Count > 0;

        public string TeachingText(VerbContext ctx)
        {
            if (ctx?.Npc == null) return null;

            List<Hero> heldPrisoners = CourtActionResolver.HeldPrisonersOf(ctx.Npc);
            if (heldPrisoners.Count == 0) return null;

            var sb = new StringBuilder();
            sb.AppendLine("RELEASE_PRISONER, INTERCEDING FOR SOMEONE YOU HOLD CAPTIVE:");
            sb.AppendLine("You currently hold these captives, and the player might persuade you to set one free:");
            foreach (Hero captive in heldPrisoners)
                sb.AppendLine($"- {captive.Name}");
            sb.AppendLine("When you have genuinely agreed to free one of them, and only then, emit:");
            sb.AppendLine("[ACTION]");
            sb.AppendLine("type: release_prisoner");
            sb.AppendLine("target: the captive's name, exactly as listed above");
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

                if (npcHero == null || profile == null || Hero.MainHero == null) return null;

                action.Parameters.TryGetValue("target", out string targetName);
                Hero target = CourtActionResolver.ResolveHeldPrisonerByName(targetName, npcHero);

                if (target == null || !CourtActionResolver.CanRelease(target, npcHero))
                    return new ActionOutcome($"({npcHero.Name} holds no such captive to free.)", false);

                EndCaptivityAction.ApplyByReleasedByChoice(target, npcHero);
                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(
                    target, Hero.MainHero, CourtActionPolicy.ReleasePrisonerRelationBump, false);

                var today = (int) CampaignTime.Now.ToDays;
                profile.Events.Add(new NotableEvent(today, NotableEventType.Collaboration,
                    $"Freed {target.Name} from captivity at the player's urging."));

                ModLog.Info($"[ACTION] release_prisoner: {npcHero.Name} freed {target.Name} at the player's word.");

                return new ActionOutcome($"{npcHero.Name} sets {target.Name} free, at your urging.", true);
            }
            catch (Exception ex)
            {
                ModLog.Error($"[ACTION] release_prisoner failed: {ex.GetType().Name}: {ex.Message}");

                return null;
            }
        }
    }
}
