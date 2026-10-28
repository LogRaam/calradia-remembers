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
         sb.AppendLine("gave up the rule of theirs, or its lands and gold, for you, you remaining head of your house. They may do it");
         if (roads.Contains(LeaveHouseRoad.ThroughTheHeir))
            sb.AppendLine($"- by handing {house} to their heir, {heir?.Name}, and living on at your side;");
         if (roads.Contains(LeaveHouseRoad.GiveHerAll))
            sb.AppendLine(LeaveHouseWording.GiveHerAllTeaching(WordingFor(ctx.Npc)));
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
            LeaveHouseWordingFacts words = WordingFor(ruler);

            // Irreversible, and the model only proposed it: the player has the last word, as for a killing, and chooses the
            // road when both are open (Gabriel, 26/10/2026).
            var choices = roads.Select(r => new InquiryElement(r, Title(r), null, true, Describe(r, ruler))).ToList();
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
               LeaveHouseWording.InquiryTitle(words),
               string.Join("\n\n", roads.Select(r => Describe(r, ruler))) + "\n\nThis cannot be undone.",
               choices, true, 1, 1, LeaveHouseWording.ConfirmButton, "Not yet",
               picked => Confirmed(ruler, picked?.FirstOrDefault()?.Identifier is LeaveHouseRoad r ? r : roads[0]),
               _ => Declined(ruler)), true);

            return new ActionOutcome($"The choice of how {house} gives way for {ruler.Name} waits on your word.", false);
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

         UnionFacts union = GiveHerAllService.UnionFor(ruler);
         int war = LeaveHouseForRulerPolicy.WarChance(union);
         string crown = union.BringsARealm
            ? $" You give up your crown: {Hero.MainHero.Clan?.Kingdom?.Name} is absorbed into {ruler.Clan?.Kingdom?.Name}."
            : "";
         string people = ruler.IsFemale ? "Her" : "His";
         string rivals = war >= 90 ? $" {people} people's rivals will almost surely answer with war."
            : war > 0 ? $" {people} people's rivals may answer with war (about {war} in 100)."
            : "";

         return $"You keep your character and your family. {LeaveHouseWording.GiveHerAllStanding(WordingFor(ruler))} {ruler.Name} receives your {union.FiefsBrought} town(s) and castle(s) and all your gold.{crown} "
                + $"You wed {(ruler.IsFemale ? "her" : "him")}, and {(ruler.IsFemale ? "she" : "he")} pays a season of your troops' wages.{rivals}";
      }

      private static void Confirmed(Hero ruler, LeaveHouseRoad road)
      {
         LeaveHouseForRulerBehavior.Instance?.Request(ruler, road);
         InformationManager.DisplayMessage(new InformationMessage(LeaveHouseWording.ChosenNotice(road, WordingFor(ruler))));
      }

      private static LeaveHouseWordingFacts WordingFor(Hero ruler)
         => new() {
            RulerName = ruler?.Name?.ToString() ?? "", RulerHouse = ruler?.Clan?.Name?.ToString() ?? (ruler?.IsFemale == true ? "her house" : "his house"), RulerIsFemale = ruler?.IsFemale == true,
            PlayerHouse = Hero.MainHero?.Clan?.Name?.ToString() ?? "your house"
         };

      private static void Declined(Hero ruler)
         => ModLog.Info($"[ACTION] leave_house_to_wed: the player declined to leave the house for {ruler?.Name}.");
   }
}
