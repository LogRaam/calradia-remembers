// Code written by Gabriel Mailhot, 26/10/2026.

#region

using System;
using System.Text;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic.Marriage;
using CalradiaRemembers.Marriage;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Verbs
{
   /// <summary>
   ///   leave_house_to_wed: a ruler who heads her house accepts the player as a spouse on her one condition, that the
   ///   player leaves THEIR house to join hers (Gabriel, 26/10/2026: "Jamais elle laissera son trone"). Taught only while
   ///   <see cref="LeaveHouseForRulerPolicy" /> finds the road open. The player has the last word in a popup naming the
   ///   heir, and the hand-over runs once back on the map (<see cref="LeaveHouseForRulerBehavior" />).
   /// </summary>
   public sealed class LeaveHouseToWedVerb : IConversationVerb
   {
      public string Name => "leave_house_to_wed";

      public bool IsEligible(VerbContext ctx)
         => ctx?.Npc != null && LeaveHouseForRulerBehavior.WhyNot(ctx.Npc) == LeaveHouseBar.None;

      public string TeachingText(VerbContext ctx)
      {
         if (!IsEligible(ctx)) return null;

         Hero heir = LeaveHouseForRulerBehavior.BestHeir();
         string house = Hero.MainHero?.Clan?.Name?.ToString() ?? "their house";
         string role = Hero.MainHero?.IsFemale == true ? "wife" : "husband";

         var sb = new StringBuilder();
         sb.AppendLine("LEAVE_HOUSE_TO_WED, THE ONE ROAD TO YOUR HAND:");
         sb.AppendLine($"You will never give up your house or its rule, but you would take the player as your {role} if they");
         sb.AppendLine($"left theirs to stand in yours: {house} would pass to their heir, {heir?.Name}, and they would live on");
         sb.AppendLine("at your side, you remaining head of your house. When the player resolves in earnest to do this, and you");
         sb.AppendLine("accept them, and only then, emit:");
         sb.AppendLine("[ACTION]");
         sb.AppendLine("type: leave_house_to_wed");
         sb.AppendLine("[/ACTION]");
         sb.AppendLine("The player is then asked to confirm, for it cannot be undone. Never act as though it is already done.");
         sb.AppendLine();

         return sb.ToString();
      }

      public ActionOutcome Execute(GameAction action, VerbContext ctx)
      {
         try
         {
            Hero ruler = ctx?.Npc;
            if (ruler == null || Hero.MainHero == null) return null;

            LeaveHouseBar bar = LeaveHouseForRulerBehavior.WhyNot(ruler);
            if (bar != LeaveHouseBar.None) return ActionOutcome.Refused($"(Leaving your house for {ruler.Name} is not possible now: {bar}.)");

            Hero heir = LeaveHouseForRulerBehavior.BestHeir();
            Hero player = Hero.MainHero;
            string house = player.Clan?.Name?.ToString() ?? "your house";

            // Irreversible, and the model only proposed it: the player has the last word, as for a killing.
            InformationManager.ShowInquiry(new InquiryData(
               $"Leave {house} for {ruler.Name}?",
               $"{heir?.Name} becomes head of {house}, and you play on as {heir?.Name}. {player.Name}, now one of the world's lords, "
               + $"weds {ruler.Name} and joins her house; your romance with her stays with {player.Name}. This cannot be undone.",
               true, true, "Leave the house", "Not yet",
               () => Confirmed(ruler), () => Declined(ruler)), true);

            return new ActionOutcome($"The choice to leave {house} for {ruler.Name} waits on your word.", false);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] leave_house_to_wed failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      private static void Confirmed(Hero ruler)
      {
         LeaveHouseForRulerBehavior.Instance?.Request(ruler);
         InformationManager.DisplayMessage(new InformationMessage($"You will leave your house for {ruler.Name} once this conversation ends."));
      }

      private static void Declined(Hero ruler)
         => ModLog.Info($"[ACTION] leave_house_to_wed: the player declined to leave the house for {ruler?.Name}.");
   }
}
