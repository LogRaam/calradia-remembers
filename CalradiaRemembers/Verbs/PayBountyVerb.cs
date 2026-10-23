// Code written by Gabriel Mailhot, 23/10/2026.
// pay_bounty: the player buys off a realm's price on their head from one of its lords. froggyluv (Nexus, 22/10/2026):
// hunters again and again, "I've gone to Ransom brokers and see no option to settle this"; a bounty ended only by a rare
// amnesty or by capture. Gabriel (23/10/2026): "le seigneur en conversation pour payer". Taught only to a lord of a realm
// that holds a bounty on the player, with the exact price (OutlawPolicy.BountyPrice), so he never agrees to a sum the game
// would not take; the player confirms [Y/n], naming lord, realm and price, before any gold moves. The bounty is then
// lifted as an amnesty lifts it (ArrestBehavior.LiftBounty).

#region

using System;
using System.Text;
using CalradiaRemembers.Arrests;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Actions;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Verbs
{
   /// <summary>pay_bounty: the player pays a lord of a realm to lift its price on their head.</summary>
   public sealed class PayBountyVerb : IConversationVerb
   {
      public string Name => "pay_bounty";

      public bool IsEligible(VerbContext ctx) => Read(ctx?.Npc, out _) != null;

      public string TeachingText(VerbContext ctx)
      {
         Bounty bounty = Read(ctx?.Npc, out _);
         if (bounty == null) return null;

         string player = Hero.MainHero.Name.ToString();
         string realm = ctx.Npc.MapFaction.Name.ToString();
         string cause = bounty.Reason == BountyReason.Flight ? "for fleeing its law" : "for attacking its merchants in peace";
         var sb = new StringBuilder();
         sb.AppendLine("PAY_BOUNTY, THE PRICE ON THE PLAYER'S HEAD:");
         sb.AppendLine($"{realm} has put a price on {player}'s head {cause}; its bounty hunters ride after {player}.");
         sb.AppendLine($"You can have it lifted in the realm's name for exactly {OutlawPolicy.BountyPrice(bounty.Reason)} denars, paid to you. No other sum.");
         sb.AppendLine($"{player} is asked to confirm before any gold moves: never say it is paid or lifted before then.");
         sb.AppendLine("If the player plainly offers to pay it and you accept, emit:");
         sb.AppendLine("[ACTION]");
         sb.AppendLine("type: pay_bounty");
         sb.AppendLine("[/ACTION]");
         sb.AppendLine();

         return sb.ToString();
      }

      public ActionOutcome Execute(GameAction action, VerbContext ctx)
      {
         try
         {
            Hero lord = ctx?.Npc;
            Bounty bounty = Read(lord, out string refusal);
            if (bounty == null)
            {
               ModLog.Info($"[ARREST] pay_bounty refused: {refusal}");

               return ActionOutcome.Refused($"({refusal})");
            }

            int price = OutlawPolicy.BountyPrice(bounty.Reason);
            string realm = lord.MapFaction.Name.ToString();
            string why = ArrestBehavior.Instance.PayBounty(lord);
            if (why != null) return ActionOutcome.Refused($"({why})");

            ctx.Profile?.Events.Add(new NotableEvent((int) CampaignTime.Now.ToDays, NotableEventType.Agreement,
               $"{Hero.MainHero.Name} paid me {price} denars to have {realm}'s price on their head lifted."));

            return new ActionOutcome($"You pay {lord.Name} {price} denars. The price on your head in {realm} is lifted (purse: {Hero.MainHero.Gold}).", true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ARREST] pay_bounty failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   The question the player answers before paying, or null with the reason it would be refused (then the action runs
      ///   inline and the refusal is said). Shares <see cref="Read" /> with the executor, so what is asked is what is paid.
      /// </summary>
      internal static string Question(Hero lord, out string refusal)
      {
         Bounty bounty = Read(lord, out refusal);

         return bounty == null
            ? null
            : ActionConfirmationPolicy.BuildBountyQuestion(new BountyConfirmationFacts {
               LordName = lord.Name?.ToString(), RealmName = lord.MapFaction?.Name?.ToString(), Price = OutlawPolicy.BountyPrice(bounty.Reason)
            });
      }

      // The bounty this lord's realm holds on the player, or null with why he cannot take the payment.
      private static Bounty Read(Hero lord, out string refusal)
      {
         refusal = null;
         ArrestBehavior law = ArrestBehavior.Instance;
         if (law == null || lord == null || Hero.MainHero == null) return Fail("there is no bounty to pay", out refusal);
         if (!lord.IsLord || lord.Clan == Clan.PlayerClan || lord.IsPrisoner || !Mapping.HeroPresence.IsInTheWorld(lord))
            return Fail($"{lord.Name} cannot lift a realm's bounty", out refusal);

         Bounty bounty = law.BountyOf(lord.MapFaction);
         if (bounty == null) return Fail($"{lord.MapFaction?.Name?.ToString() ?? "His realm"} has no price on your head", out refusal);
         if (law.HoldsPlayer) return Fail("the law already holds you; your case is for the court now", out refusal);

         return bounty;
      }

      private static Bounty Fail(string why, out string refusal)
      {
         refusal = why;

         return null;
      }
   }
}
