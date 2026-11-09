// Code written by Gabriel Mailhot, 04/07/2026.

#region

using System;
using System.Text;
using CalradiaRemembers.Appreciations;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic.Actions;
using CalradiaRemembers.Logic.Appreciations;
using CalradiaRemembers.Logic.WarDuty;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

#endregion

namespace CalradiaRemembers.Verbs
{
    /// <summary>
    ///   give_troops: the reciprocal of <see cref="LendTroopsVerb" />, 04/07/2026 — the PLAYER reinforces a
    ///   needy NPC's own (genuinely under-strength) party with some of their own soldiers. The LLM plays the
    ///   NPC, so this action is emitted BY THE NPC when they ACCEPT the player's offer, never by the player.
    ///   Eligibility and the count offered are entirely <see cref="CourtActionResolver.TryGiveTroops" /> /
    ///   <see cref="CourtActionResolver.MoveLowestTierTroops" /> (re-checked and re-computed against the LIVE
    ///   rosters at execution: "the bridge is law"); this class only orchestrates. Closes the last open
    ///   Appreciations source, <see cref="AppreciationSource.TroopsOrInfluenceSpentForThem" />.
    /// </summary>
    public sealed class GiveTroopsVerb : IConversationVerb
    {
        public string Name => "give_troops";

        public bool IsEligible(VerbContext ctx)
            => ctx?.Npc != null && ctx.Profile != null && (LevyTroops(ctx.Npc).Men > 0 || CourtActionResolver.TryGiveTroops(ctx.Npc, ctx.Profile, out _));

        public string TeachingText(VerbContext ctx)
        {
            if (ctx?.Npc == null || ctx.Profile == null) return null;

            LevyTroopTake levy = LevyTroops(ctx.Npc);
            if (levy.Men > 0)
            {
                var lb = new StringBuilder();
                lb.AppendLine("GIVE_TROOPS, SOLDIERS TOWARD THE LEVY THE PLAYER OWES YOU:");
                lb.AppendLine("If the player offers soldiers from their ranks toward the levy, and you accept them, emit:");
                lb.AppendLine("[ACTION]");
                lb.AppendLine("type: give_troops");
                lb.AppendLine("[/ACTION]");
                lb.AppendLine($"The game moves {levy.Men} of their soldiers, worth {levy.Value} denars, into your ranks and counts");
                lb.AppendLine("them toward the levy. Only emit this once you have truly accepted the men in this conversation.");
                lb.AppendLine();

                return lb.ToString();
            }

            if (!CourtActionResolver.TryGiveTroops(ctx.Npc, ctx.Profile, out int count)) return null;

            var sb = new StringBuilder();
            sb.AppendLine("GIVE_TROOPS, ACCEPTING SOLDIERS FROM THE PLAYER FOR YOUR OWN RANKS:");
            sb.AppendLine("Your own force runs thin these days, worn down and under-strength. If the player offers");
            sb.AppendLine("to lend or give you soldiers to reinforce your ranks, and you have genuinely resolved to");
            sb.AppendLine("accept, emit:");
            sb.AppendLine("[ACTION]");
            sb.AppendLine("type: give_troops");
            sb.AppendLine("[/ACTION]");
            sb.AppendLine($"The game moves up to {count} of the player's own soldiers into your ranks. Only emit");
            sb.AppendLine("this once you have truly decided to accept in this conversation, never as an empty thanks.");
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

                // Men a lord asked for are his errand (cost bench, 09/11/2026): the reader called "fresh men, as I
                // asked" give_troops, which moves them and leaves his task open. The game knows the task; it closes it.
                if (TroopHandOverReadingPolicy.IsTheRecruitTask(DeliverRecruitsVerb.HandOverFacts(ctx)))
                {
                    ModLog.Info($"[ACTION] give_troops toward {npcHero.Name} carried out as deliver_recruits: his recruit task is open and the player holds the men.");

                    return new DeliverRecruitsVerb().Execute(action, ctx);
                }

                // The vassal war duty (Gabriel, 15/10/2026): a liege the player owes a levy takes soldiers toward it,
                // whatever his own party's need. A levy, not a kindness: no appreciation is planted.
                if (LevyTroops(npcHero).Men > 0)
                {
                    LevyTroopTake taken = WarLevyDesk.TakeTroops?.Invoke(npcHero) ?? new LevyTroopTake();
                    if (taken.Men <= 0) return ActionOutcome.Refused($"({npcHero.Name} has no room for more soldiers just now.)");

                    int stillOwed = WarLevyDesk.Owed(npcHero);
                    ModLog.Info($"[ACTION] give_troops: {taken.Men} soldiers (worth {taken.Value}) to {npcHero.Name} toward the levy.");

                    return new ActionOutcome($"{npcHero.Name} takes {taken.Men} soldiers from your ranks toward the levy, counted at {taken.Value} denars.{WarLevyDesk.AfterPayment(stillOwed)}", true);
                }

                if (!CourtActionResolver.TryGiveTroops(npcHero, profile, out int count))
                    return ActionOutcome.Refused($"({npcHero.Name} has no room or need for more soldiers just now.)");

                MobileParty npcParty = npcHero.PartyBelongedTo;
                MobileParty playerParty = MobileParty.MainParty;

                if (npcParty == null || playerParty == null) return null;

                int moved = CourtActionResolver.MoveLowestTierTroops(playerParty, npcParty, count);

                if (moved <= 0) return ActionOutcome.Refused($"({npcHero.Name} has no room or need for more soldiers just now.)");

                var today = (int) CampaignTime.Now.ToDays;
                profile.Events.Add(new NotableEvent(today, NotableEventType.Collaboration,
                    $"Accepted {moved} soldiers from the player when their own ranks ran thin."));
                CourtActionResolver.RecordCourtAction(profile, CourtActionResolver.GiveTroopsMarker, today); // M-B6

                AppreciationBehavior.Instance?.TryPlant(npcHero, Hero.MainHero, AppreciationSource.TroopsOrInfluenceSpentForThem,
                    $"The player reinforced {npcHero.Name}'s thinned ranks with {moved} soldiers.",
                    AppreciationBackfillRules.TroopsOrInfluenceSpentForThemMagnitude, knownToPlayer: true, today);

                ModLog.Info($"[ACTION] give_troops: {npcHero.Name} accepted {moved} troops from the player.");

                return new ActionOutcome($"{npcHero.Name} accepts {moved} soldiers from your ranks.", true);
            }
            catch (Exception ex)
            {
                ModLog.Error($"[ACTION] give_troops failed: {ex.GetType().Name}: {ex.Message}");

                return null;
            }
        }

        /// <summary>The soldiers a liege the player owes a levy would take toward it now; none when nothing is owed.</summary>
        private static LevyTroopTake LevyTroops(Hero npc)
        {
            try { return WarLevyDesk.Owed(npc) > 0 ? WarLevyDesk.PlanTroops?.Invoke(npc) ?? new LevyTroopTake() : new LevyTroopTake(); }
            catch { return new LevyTroopTake(); }
        }
    }
}
