// Code written by Gabriel Mailhot, 26/10/2026.

#region

using System;
using System.Linq;
using System.Text;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic.Marriage;
using CalradiaRemembers.Marriage;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Verbs
{
   /// <summary>
   ///   leave_house_to_wed: a ruler who heads her house accepts the player as a spouse on her one condition, that the
   ///   player leaves THEIR house to join hers (Gabriel, 26/10/2026: "Jamais elle laissera son trone"). Two roads: the heir
   ///   takes the house and the player plays on as the heir, or the player gives her their fiefs, gold and realm and keeps
   ///   their character and family (the road when there is no heir). Taught while either is open. The player chooses and
   ///   has the last word in a popup, and the hand-over runs once back on the map (<see cref="LeaveHouseForRulerBehavior" />).
   /// </summary>
   public sealed class LeaveHouseToWedVerb : IConversationVerb
   {
      public string Name => "leave_house_to_wed";

      public bool IsEligible(VerbContext ctx)
         => ctx?.Npc != null && LeaveHouseForRulerBehavior.OpenRoads(ctx.Npc).Length > 0;

      public string TeachingText(VerbContext ctx)
      {
         if (!IsEligible(ctx)) return null;

         LeaveHouseRoad[] roads = LeaveHouseForRulerBehavior.OpenRoads(ctx.Npc);
         Hero heir = LeaveHouseForRulerBehavior.BestHeir();
         string house = Hero.MainHero?.Clan?.Name?.ToString() ?? "their house";
         string role = Hero.MainHero?.IsFemale == true ? "wife" : "husband";

         var sb = new StringBuilder();
         sb.AppendLine("LEAVE_HOUSE_TO_WED, THE ONE ROAD TO YOUR HAND:");
         sb.AppendLine($"You will never give up your house or its rule, but you would take the player as your {role} if they");
         sb.AppendLine("gave up the rule of theirs for you, you remaining head of your house. They may do it");
         if (roads.Contains(LeaveHouseRoad.ThroughTheHeir))
            sb.AppendLine($"- by handing {house} to their heir, {heir?.Name}, and living on at your side;");
         if (roads.Contains(LeaveHouseRoad.GiveHerAll))
            sb.AppendLine("- or by bringing you their lands, their gold and any realm they rule, their kin staying with them in your service.");
         sb.AppendLine("When the player resolves in earnest to do this, and you accept them, and only then, emit:");
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

            LeaveHouseRoad[] roads = LeaveHouseForRulerBehavior.OpenRoads(ruler);
            if (roads.Length == 0)
               return ActionOutcome.Refused($"(Leaving your house for {ruler.Name} is not possible now: {LeaveHouseForRulerBehavior.WhyNot(ruler, LeaveHouseRoad.GiveHerAll)}.)");

            string house = Hero.MainHero.Clan?.Name?.ToString() ?? "your house";

            // Irreversible, and the model only proposed it: the player has the last word, as for a killing, and chooses the
            // road when both are open (Gabriel, 26/10/2026).
            var choices = roads.Select(r => new InquiryElement(r, Title(r), null, true, Describe(r, ruler))).ToList();
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
               $"Leave {house} for {ruler.Name}?",
               string.Join("\n\n", roads.Select(r => Describe(r, ruler))) + "\n\nThis cannot be undone.",
               choices, true, 1, 1, "Leave the house", "Not yet",
               picked => Confirmed(ruler, picked?.FirstOrDefault()?.Identifier is LeaveHouseRoad r ? r : roads[0]),
               _ => Declined(ruler)), true);

            return new ActionOutcome($"The choice to leave {house} for {ruler.Name} waits on your word.", false);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] leave_house_to_wed failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      private static string Title(LeaveHouseRoad road)
         => road == LeaveHouseRoad.ThroughTheHeir ? "Hand your house to your heir" : "Give her your lands and gold";

      private static string Describe(LeaveHouseRoad road, Hero ruler)
      {
         Hero player = Hero.MainHero;
         Hero heir = LeaveHouseForRulerBehavior.BestHeir();
         string her = ruler.IsFemale ? "her" : "his";
         if (road == LeaveHouseRoad.ThroughTheHeir)
            return $"{heir?.Name} becomes head of your house, and you play on as {heir?.Name}. {player.Name}, now one of the world's lords, "
                   + $"weds {ruler.Name} and joins {her} house; your romance with {(ruler.IsFemale ? "her" : "him")} stays with {player.Name}.";

         return $"You keep your character and your family. {ruler.Name} receives your towns, castles, gold and any realm you rule; your house "
                + $"serves {her} realm, you wed {(ruler.IsFemale ? "her" : "him")}, and {(ruler.IsFemale ? "she" : "he")} pays a season of your troops' wages. "
                + $"{her.Substring(0, 1).ToUpper() + her.Substring(1)} people's rivals may answer with war.";
      }

      private static void Confirmed(Hero ruler, LeaveHouseRoad road)
      {
         LeaveHouseForRulerBehavior.Instance?.Request(ruler, road);
         InformationManager.DisplayMessage(new InformationMessage($"You will leave your house for {ruler.Name} once this conversation is over and you are back at the map or a town's menu."));
      }

      private static void Declined(Hero ruler)
         => ModLog.Info($"[ACTION] leave_house_to_wed: the player declined to leave the house for {ruler?.Name}.");
   }
}
