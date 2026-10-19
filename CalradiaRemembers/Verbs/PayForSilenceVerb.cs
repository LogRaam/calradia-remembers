// Code written by Gabriel Mailhot, 05/09/2026.
// Mystery-lover pillar (design phase 3b-3): a lord who holds the player's secret-lover identity and owes them no
// loyalty can WEAPONIZE it, extracting gold for his silence. The held-secret prompt section (PromptBuilder
// .AppendHeldSecret, Leverage branch) teaches this verb inline, so its own TeachingText is null; this class only
// executes it: on a genuine, settled hush payment it moves the gold, stamps a cooldown so the lord backs off for a
// while (HushMoneyPolicy), and records a first-hand memory. Distinct from pay_blackmail, which is bound to the
// bastard-mother silence system; this is the secret-lover lever.

#region

using System;
using System.Linq;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic.Bastards;
using CalradiaRemembers.WorldEvents;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

#endregion

namespace CalradiaRemembers.Verbs
{
   /// <summary>pay_for_silence: the player pays a lord who holds their secret-lover identity to buy his silence.</summary>
   public sealed class PayForSilenceVerb : IConversationVerb
   {
      public string Name => "pay_for_silence";

      public bool IsEligible(VerbContext ctx)
         => ctx?.Npc != null && SecretLoverGossipBehavior.Instance != null && FindHeldSubject(ctx.Npc) != null;

      // Taught inline in the held-secret prompt section's Leverage branch, not via the registry.
      public string TeachingText(VerbContext ctx) => null;

      public ActionOutcome Execute(GameAction action, VerbContext ctx)
      {
         try
         {
            Hero npcHero = ctx?.Npc;
            SecretLoverGossipBehavior gossip = SecretLoverGossipBehavior.Instance;
            if (npcHero == null || gossip == null || Hero.MainHero == null) return null;

            string subjectId = FindHeldSubject(npcHero);
            if (subjectId == null)
               return ActionOutcome.Refused($"({npcHero.Name} holds nothing over you to buy silence for.)");

            int price = HushMoneyPolicy.ClampPayment(ParsePrice(action));

            if (Hero.MainHero.Gold < price)
               return ActionOutcome.Refused($"You cannot cover the {price} denars {npcHero.Name} wants for silence.");

            GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, npcHero, price);

            var today = (int) CampaignTime.Now.ToDays;
            gossip.RecordHushPaid(npcHero, subjectId, today);

            ctx.Profile?.Events.Add(new NotableEvent(today, NotableEventType.Other,
               $"Took {price} denars from the player to keep their secret quiet, for now."));

            ModLog.Info($"[ACTION] pay_for_silence: player paid {npcHero.Name} {price} to hush their secret.");

            return new ActionOutcome($"You pay {npcHero.Name} {price} denars for their silence (purse: {Hero.MainHero.Gold}).", false);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] pay_for_silence failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      // The first secret this NPC holds about the player, or null when they hold none.
      private static string FindHeldSubject(Hero npc)
      {
         SecretLoverGossipBehavior gossip = SecretLoverGossipBehavior.Instance;
         if (gossip == null || npc == null) return null;

         foreach (string subjectId in gossip.PlayerSecretLoverSubjectIds())
            if (gossip.Knows(npc.StringId, subjectId)) return subjectId;

         return null;
      }

      private static int ParsePrice(GameAction action)
      {
         if (action?.Parameters != null && action.Parameters.TryGetValue("price", out string raw) && !string.IsNullOrEmpty(raw))
         {
            string digits = new string(raw.Where(char.IsDigit).ToArray());
            if (int.TryParse(digits, out int p)) return p;
         }

         return HushMoneyPolicy.MinPayment;
      }
   }
}
