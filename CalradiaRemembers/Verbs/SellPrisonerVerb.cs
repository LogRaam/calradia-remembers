// Code written by Gabriel Mailhot, 08/07/2026.
// Prisoner trade (ratified 2026-07-08), the SELL direction: the NPC sells a captive HIS CLAN holds to the
// player, for the player's gold. The LLM negotiates in-fiction; the bridge is law (price clamped by
// PrisonerTradePolicy around the game's ransom valuation, custody and the player's purse re-verified at
// execution). The other half of the bounty loop: a player who WANTS a prisoner can simply buy him.

#region

using System;
using System.Collections.Generic;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

#endregion

namespace CalradiaRemembers.Verbs
{
    /// <summary>
    ///   sell_prisoner: the NPC hands the player a hero captive their clan holds, for the player's gold.
    ///   Taught whenever the clan holds at least one; custody, price, and the player's purse are all
    ///   re-validated game-side at execution.
    /// </summary>
    public sealed class SellPrisonerVerb : IConversationVerb
    {
        public string Name => "sell_prisoner";

        public bool IsEligible(VerbContext ctx)
            => ctx?.Npc != null && CourtActionResolver.HeldPrisonersOf(ctx.Npc).Count > 0;

        public string TeachingText(VerbContext ctx)
        {
            if (ctx?.Npc == null) return null;

            List<Hero> held = CourtActionResolver.HeldPrisonersOf(ctx.Npc);
            if (held.Count == 0) return null;

            var sb = new StringBuilder();
            sb.AppendLine("SELL_PRISONER, SELLING A CAPTIVE YOU HOLD TO THE PLAYER:");
            sb.AppendLine("Your clan holds these captives, and the player might offer to BUY one from you");
            sb.AppendLine("(for ransom, for a bounty they hunt, or for purposes of their own):");
            foreach (Hero captive in held)
                sb.AppendLine($"- {captive.Name} (a fair price would be around {BuyPrisonerVerb.RansomAnchor(captive)} denars)");
            sb.AppendLine("Sell only if it serves you; a captive of value to your realm is not parted with");
            sb.AppendLine("lightly, and never below what they are worth. When you and the player have AGREED");
            sb.AppendLine("on the sale, and only then, emit:");
            sb.AppendLine("[ACTION]");
            sb.AppendLine("type: sell_prisoner");
            sb.AppendLine("target: the captive's name, exactly as listed above");
            sb.AppendLine("price: the agreed denars");
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
                Hero captive = CourtActionResolver.ResolveHeldPrisonerByName(targetName, npcHero);

                if (captive == null || !CourtActionResolver.CanRelease(captive, npcHero))
                    return ActionOutcome.Refused($"({npcHero.Name} holds no such captive to sell.)");

                action.Parameters.TryGetValue("price", out string priceStr);
                int.TryParse(priceStr?.Trim(), out int proposed);
                int price = PrisonerTradePolicy.ClampPrice(proposed, BuyPrisonerVerb.RansomAnchor(captive));

                if (Hero.MainHero.Gold < price)
                    return ActionOutcome.Refused($"(You cannot pay the {price} denars {npcHero.Name} asks for {captive.Name}.)");

                // The gold, then the man: the player pays, and the captive changes custody into the
                // player's own prison roster.
                GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, npcHero, price);
                CharacterObject captiveChar = captive.CharacterObject;
                PartyBase from = captive.PartyBelongedToAsPrisoner;
                if (from != null) TransferPrisonerAction.Apply(captiveChar, from, MobileParty.MainParty.Party);
                else TakePrisonerAction.Apply(MobileParty.MainParty.Party, captive);

                // A purchase is a custody handoff, not a battle capture: TransferPrisonerAction raises no
                // HeroPrisonerTaken, so capture quests would never stamp. We do NOT dispatch that event by
                // hand — its other listeners (stance fear, world chronicle, companion missions, nemesis)
                // are tuned for a real capture and would misfire on a sale. Stamp the quests directly
                // instead; harmless on the TakePrisoner path, where the native hook already stamped them
                // (Stamp only ever touches outstanding quests).
                NpcMemoryCampaignBehavior.Instance?.QuestTracker?.StampCaptureQuests(captive);

                var today = (int) CampaignTime.Now.ToDays;
                profile.Events.Add(new NotableEvent(today, NotableEventType.Collaboration,
                    $"Sold the captive {captive.Name} to the player for {price} denars."));

                ModLog.Info($"[ACTION] sell_prisoner: {npcHero.Name} sold {captive.Name} to the player for {price} denars.");

                return new ActionOutcome($"You pay {price} denars, and {captive.Name} is delivered into your custody.", true);
            }
            catch (Exception ex)
            {
                ModLog.Error($"[ACTION] sell_prisoner failed: {ex.GetType().Name}: {ex.Message}");

                return null;
            }
        }
    }
}
