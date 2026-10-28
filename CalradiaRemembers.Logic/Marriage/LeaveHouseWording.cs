// Code written by Gabriel Mailhot, 28/10/2026.
// The words that ask the player to choose a road to a ruler's hand, and that teach her what each road gives. RaveN017Mod
// (Nexus, 28/10/2026) took the give-her-all road and wrote "I assumed this would mean joining House Pethros [...] I
// specifically chose the second option: to give her everything and join her." On that road the player stays head of
// their own house, sworn to her realm: the game always keeps the player at the head of their own clan. The window had
// said "Leave {house} for Rhagaea?" and its button "Leave the house", promising what that road does not do.

namespace CalradiaRemembers.Logic.Marriage
{
   /// <summary>Who the wording names.</summary>
   public sealed class LeaveHouseWordingFacts
   {
      /// <summary>The ruler the player would wed.</summary>
      public string RulerName { get; init; } = "";

      /// <summary>Her house, which the give-her-all road never lets the player join.</summary>
      public string RulerHouse { get; init; } = "";

      /// <summary>Whether the ruler is a woman (her/his).</summary>
      public bool RulerIsFemale { get; init; }

      /// <summary>The player's own house, which they keep heading on the give-her-all road.</summary>
      public string PlayerHouse { get; init; } = "";
   }

   /// <summary>The words of the choice between the two roads, and of the model's teaching.</summary>
   public static class LeaveHouseWording
   {
      /// <summary>The confirm button: it names no road, since one of them does not leave the house.</summary>
      public const string ConfirmButton = "Take this road";

      /// <summary>The window's title.</summary>
      public static string InquiryTitle(LeaveHouseWordingFacts f) => $"Wed {f.RulerName}: choose your road";

      /// <summary>What the give-her-all road keeps and what it never gives, said to the player.</summary>
      public static string GiveHerAllStanding(LeaveHouseWordingFacts f)
         => $"You remain head of {f.PlayerHouse}, now sworn to {Her(f)} realm; you do not join {f.RulerHouse}.";

      /// <summary>The give-her-all road as the ruler is taught it.</summary>
      public static string GiveHerAllTeaching(LeaveHouseWordingFacts f)
         => "- or by bringing you their lands, their gold and any realm they rule, and swearing their house to your realm. They "
            + $"stay head of {f.PlayerHouse}, their kin with them; they never join {f.RulerHouse}, so never tell them they will.";

      /// <summary>The note once the player has chosen, before the hand-over runs on the map.</summary>
      public static string ChosenNotice(LeaveHouseRoad road, LeaveHouseWordingFacts f)
         => road == LeaveHouseRoad.ThroughTheHeir
            ? $"You will hand {f.PlayerHouse} to your heir and wed {f.RulerName} once this conversation is over and you are back at the map or a town's menu."
            : $"You will give {f.RulerName} your lands and gold and wed {Him(f)} once this conversation is over and you are back at the map or a town's menu.";

      private static string Her(LeaveHouseWordingFacts f) => f.RulerIsFemale ? "her" : "his";

      private static string Him(LeaveHouseWordingFacts f) => f.RulerIsFemale ? "her" : "him";
   }
}
