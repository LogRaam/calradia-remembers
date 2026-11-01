// Code written by Gabriel Mailhot, 01/11/2026.
// extort: the player demands coin from a character with a hold the game can verify (Gabriel, 23/10/2026; his "go", 01/11/2026).
// Taught only while ExtortionBehavior sees that hold, with the most they pay and the day's answer, so the character never
// agrees in words to what the game then refuses, and never hears of a secret that does not exist. The reverse direction, a
// character extorting the player, is pay_for_silence and pay_blackmail. A blade in hand is rob_npc.

#region

using System;
using System.Linq;
using System.Text;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Extortion;
using CalradiaRemembers.Verbs;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Violence
{
   /// <summary>extort: the player presses a character for coin with a verified hold.</summary>
   public sealed class ExtortVerb : IConversationVerb
   {
      public string Name => "extort";

      public bool IsEligible(VerbContext ctx) => ExtortionBehavior.Instance?.HoldOver(ctx?.Npc) != null;

      public string TeachingText(VerbContext ctx)
      {
         ExtortionHold hold = ExtortionBehavior.Instance?.HoldOver(ctx?.Npc);
         if (hold == null) return null;

         string player = Hero.MainHero.Name.ToString();
         var sb = new StringBuilder();
         sb.AppendLine("EXTORT, THE HOLD THE PLAYER HAS OVER YOU:");
         sb.AppendLine(hold.Words);
         sb.AppendLine($"Never raise this yourself. It matters only if {player} plainly demands coin for it.");
         sb.AppendLine(hold.Yields
            ? $"If they do, you give in, bitterly: you pay what they ask, at most {ExtortionPolicy.MostTaken(ctx.Npc.Gold)} denars, not one more."
            : "If they do, you refuse to be bled, and you say so.");
         sb.AppendLine("Only on the turn they make that demand, emit:");
         sb.AppendLine("[ACTION]");
         sb.AppendLine("type: extort");
         if (hold.Yields) sb.AppendLine("amount: <the denars you pay>");
         sb.AppendLine("[/ACTION]");
         sb.AppendLine();

         return sb.ToString();
      }

      public ActionOutcome Execute(GameAction action, VerbContext ctx)
      {
         try
         {
            Hero npc = ctx?.Npc;
            ExtortionBehavior behavior = ExtortionBehavior.Instance;
            ExtortionHold hold = behavior?.HoldOver(npc);
            if (hold == null) return ActionOutcome.Refused($"(You hold nothing over {npc?.Name} that could make them pay today.)");

            if (!hold.Yields)
            {
               behavior.Settle(npc, hold.Lever, 0);

               return new ActionOutcome($"{npc.Name} will not be bled, and will remember that you tried.", false);
            }

            int take = ExtortionPolicy.Take(ParseAmount(action), npc.Gold);
            ExchangeService.Pay(ExchangeKind.Extortion, npc, Hero.MainHero, take);
            behavior.Settle(npc, hold.Lever, take);

            string terms = hold.Lever == ExtortionLever.Lands ? $" You have given your word to leave their lands be for {ExtortionPolicy.DaysOfTheBargain} days." : "";

            return new ActionOutcome($"{npc.Name} pays you {take} denars under your threat (purse: {Hero.MainHero.Gold}).{terms}", false);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[EXTORT] extort failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      private static int? ParseAmount(GameAction action)
      {
         if (action?.Parameters == null || !action.Parameters.TryGetValue("amount", out string raw) || string.IsNullOrEmpty(raw)) return null;

         string digits = new string(raw.Where(char.IsDigit).ToArray());

         return int.TryParse(digits, out int amount) ? amount : (int?) null;
      }
   }
}
